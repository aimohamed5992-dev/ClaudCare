using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Operations;

/// <summary>
/// الصيدلية: قائمة الوصفات (تجهيز/صرف/رفض) مع خصم المخزون، المخزون، البيع المباشر، أوامر الشراء.
/// الصرف يخصم المخزون ويسجل عملية بيع داخل معاملة بقفل صفوف المخزون، فلا يُباع ما ليس موجودًا حتى مع التزامن.
/// </summary>
public sealed class PharmacyService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, INotificationService notifications, IAuditService audit, TimeProvider time)
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
    private static readonly PaymentMethod[] SaleMethods = [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.ApplePay, PaymentMethod.Insurance];

    // ------------------------------------------------------------ الوصفات

    public async Task<PagedResult<RxQueueDto>> QueueAsync(PrescriptionStatus? status, int? page, int? pageSize, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var (p, size) = Paging.Clamp(page, pageSize);
        var b = scope.BranchFilter;
        var q = db.Prescriptions.AsNoTracking().Where(r => b == null || r.BranchId == b);
        if (status is { } s) q = q.Where(r => r.Status == s); else q = q.Where(r => r.Status == PrescriptionStatus.Pending || r.Status == PrescriptionStatus.Preparing);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(r => r.Items).Include(r => r.Patient).Include(r => r.Doctor)
            .OrderByDescending(r => r.Priority).ThenBy(r => r.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<RxQueueDto>(await ToQueueDtosAsync(rows, ct), p, size, total);
    }

    public async Task<RxQueueDto> PrepareAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var rx = await FindRxAsync(id, ct);
        if (rx.Status != PrescriptionStatus.Pending) throw ApiException.Conflict("invalid_state", $"لا يمكن التجهيز والوصفة في حالة «{rx.Status}».");
        rx.Status = PrescriptionStatus.Preparing;
        await db.SaveChangesAsync(ct);
        return (await ToQueueDtosAsync([rx], ct))[0];
    }

    public async Task<RxQueueDto> RejectAsync(int id, string reason, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var rx = await FindRxAsync(id, ct);
        if (rx.Status is not (PrescriptionStatus.Pending or PrescriptionStatus.Preparing)) throw ApiException.Conflict("invalid_state", $"لا يمكن الرفض والوصفة في حالة «{rx.Status}».");
        rx.Status = PrescriptionStatus.Rejected;
        rx.RejectionReason = reason.Trim();
        notifications.Add(rx.PatientId, NotificationType.Medication, "تعذّر صرف الوصفة", reason.Trim(), nameof(Prescription), rx.Id);
        notifications.Add(rx.DoctorId, NotificationType.Alert, "رفضت الصيدلية وصفة", $"{rx.No} · {reason.Trim()}", nameof(Prescription), rx.Id);
        await db.SaveChangesAsync(ct);
        return (await ToQueueDtosAsync([rx], ct))[0];
    }

    public async Task<SaleDto> DispenseAsync(int id, PaymentMethod method, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        if (!SaleMethods.Contains(method)) throw ApiException.Validation(nameof(method), "طريقة الدفع غير مدعومة");
        var branch = scope.BranchForNew();
        var strategy = db.Database.CreateExecutionStrategy();
        var saleId = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Prescriptions WHERE Id = {id} FOR UPDATE", ct);
            var rx = await FindRxAsync(id, ct);
            if (rx.Status is not (PrescriptionStatus.Pending or PrescriptionStatus.Preparing)) throw ApiException.Conflict("invalid_state", $"لا يمكن الصرف والوصفة في حالة «{rx.Status}».");

            var lines = new List<PharmacySaleLine>();
            foreach (var item in rx.Items.Where(i => !i.Dispensed))
            {
                var name = item.SubstituteDrugName ?? item.DrugName;
                if (item.DrugId is { } drugId)
                {
                    var inv = await LockInventoryAsync(branch, drugId, ct);
                    if (inv is null || inv.Quantity < item.Quantity)
                        throw new ApiException(StatusCodes.Status409Conflict, "insufficient_stock", $"المخزون غير كافٍ للصنف «{name}».");
                    inv.Quantity -= item.Quantity;
                }
                item.Dispensed = true;
                lines.Add(new PharmacySaleLine { DrugId = item.DrugId, ItemName = name, Quantity = item.Quantity, UnitPrice = item.UnitPrice });
            }
            rx.Status = PrescriptionStatus.Dispensed;
            rx.DispensedAt = UtcNow;
            rx.DispensedByUserId = me.Id;
            var sale = new PharmacySale { No = "TMP" + Guid.NewGuid().ToString("N")[..16], BranchId = branch, PatientId = rx.PatientId, PrescriptionId = rx.Id, Method = method,
                Total = lines.Sum(l => l.Quantity * l.UnitPrice), SoldByUserId = me.Id, Lines = lines };
            db.PharmacySales.Add(sale);
            await db.SaveChangesAsync(ct);
            sale.No = $"SAL-{sale.Id:D6}";
            notifications.Add(rx.PatientId, NotificationType.Medication, "تم صرف وصفتك", $"{rx.No} · {sale.Total:0.##} ر.س", nameof(Prescription), rx.Id);
            audit.Add("pharmacy.dispense", me.Id, nameof(Prescription), rx.Id, $"{sale.No} {sale.Total}", me.Ip);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return sale.Id;
        });
        db.ChangeTracker.Clear();
        return await GetSaleAsync(saleId, ct);
    }

    // ------------------------------------------------------------ المخزون

    public async Task<PagedResult<InventoryDto>> InventoryAsync(bool lowOnly, string? search, int? page, int? pageSize, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var (p, size) = Paging.Clamp(page, pageSize);
        var b = scope.BranchFilter;
        var q = db.InventoryItems.AsNoTracking().Where(i => b == null || i.BranchId == b);
        if (lowOnly) q = q.Where(i => i.Quantity <= i.MinQuantity);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(i => i.Drug.Name.Contains(search.Trim()));
        var total = await q.CountAsync(ct);
        var rows = await q.Include(i => i.Drug).OrderBy(i => i.Drug.Name).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<InventoryDto>(rows.Select(ToDto).ToList(), p, size, total);
    }

    public async Task<InventoryDto> AddInventoryAsync(AddInventoryRequest req, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var branch = scope.BranchForNew();
        if (!await db.Drugs.AnyAsync(d => d.Id == req.DrugId && d.IsActive, ct)) throw ApiException.NotFound("drug_not_found", "الدواء غير موجود.");
        if (await db.InventoryItems.AnyAsync(i => i.BranchId == branch && i.DrugId == req.DrugId, ct)) throw ApiException.Conflict("inventory_exists", "الصنف موجود في المخزون؛ عدّل كميته.");
        var item = new InventoryItem { BranchId = branch, DrugId = req.DrugId!.Value, Quantity = req.Quantity, MinQuantity = req.MinQuantity, UnitCost = req.UnitCost, UnitPrice = req.UnitPrice, ExpiryDate = req.ExpiryDate, Supplier = req.Supplier?.Trim() };
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(await db.InventoryItems.AsNoTracking().Include(i => i.Drug).FirstAsync(i => i.Id == item.Id, ct));
    }

    public async Task<InventoryDto> UpdateInventoryAsync(int id, UpdateInventoryRequest req, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var i = await FindInventoryAsync(id, ct);
        if (req.MinQuantity is { } m) i.MinQuantity = m;
        if (req.UnitPrice is { } up) i.UnitPrice = up;
        if (req.UnitCost is { } uc) i.UnitCost = uc;
        if (req.ExpiryDate is { } ex) i.ExpiryDate = ex;
        if (req.Supplier is not null) i.Supplier = req.Supplier.Trim();
        await db.SaveChangesAsync(ct);
        return ToDto(i);
    }

    /// <summary>تسوية الكمية بالزيادة أو النقص (جرد/تالف) بسبب إلزامي؛ لا تنزل تحت الصفر.</summary>
    public async Task<InventoryDto> AdjustAsync(int id, int delta, string reason, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        if (delta == 0) throw ApiException.Validation(nameof(delta), "أدخل كمية غير صفرية");
        var i = await FindInventoryAsync(id, ct);
        // تحديث ذري يمنع السباق مع الصرف
        var rows = await db.InventoryItems.Where(x => x.Id == id && x.Quantity + delta >= 0).ExecuteUpdateAsync(s => s.SetProperty(x => x.Quantity, x => x.Quantity + delta), ct);
        if (rows == 0) throw ApiException.Conflict("insufficient_stock", "الكمية ستصبح أقل من صفر.");
        audit.Add("pharmacy.adjust", me.Id, nameof(InventoryItem), id, $"{delta:+0;-0} {reason.Trim()}", me.Ip);
        await db.SaveChangesAsync(ct);
        db.Entry(i).State = EntityState.Detached;
        return ToDto(await db.InventoryItems.AsNoTracking().Include(x => x.Drug).FirstAsync(x => x.Id == id, ct));
    }

    // ------------------------------------------------------------ البيع المباشر

    public async Task<SaleDto> CreateSaleAsync(CreateSaleRequest req, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var method = req.Method!.Value;
        if (!SaleMethods.Contains(method)) throw ApiException.Validation(nameof(req.Method), "طريقة الدفع غير مدعومة");
        var branch = scope.BranchForNew();
        if (req.PatientId is { } pid && !await db.Users.AnyAsync(u => u.Id == pid && u.Role == UserRole.Patient, ct)) throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");

        var strategy = db.Database.CreateExecutionStrategy();
        var saleId = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var lines = new List<PharmacySaleLine>();
            foreach (var l in req.Lines.GroupBy(x => x.DrugId!.Value).Select(g => (DrugId: g.Key, Qty: g.Sum(x => x.Quantity))).OrderBy(x => x.DrugId))
            {
                var inv = await LockInventoryAsync(branch, l.DrugId, ct);
                var drug = await db.Drugs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == l.DrugId, ct) ?? throw ApiException.NotFound("drug_not_found", "الدواء غير موجود.");
                if (inv is null || inv.Quantity < l.Qty) throw new ApiException(StatusCodes.Status409Conflict, "insufficient_stock", $"المخزون غير كافٍ للصنف «{drug.Name}».");
                inv.Quantity -= l.Qty;
                lines.Add(new PharmacySaleLine { DrugId = l.DrugId, ItemName = drug.Name, Quantity = l.Qty, UnitPrice = inv.UnitPrice });
            }
            var sale = new PharmacySale { No = "TMP" + Guid.NewGuid().ToString("N")[..16], BranchId = branch, PatientId = req.PatientId, CustomerName = req.CustomerName?.Trim(), Method = method,
                Total = lines.Sum(l => l.Quantity * l.UnitPrice), SoldByUserId = me.Id, Lines = lines };
            db.PharmacySales.Add(sale);
            await db.SaveChangesAsync(ct);
            sale.No = $"SAL-{sale.Id:D6}";
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return sale.Id;
        });
        db.ChangeTracker.Clear();
        return await GetSaleAsync(saleId, ct);
    }

    public async Task<PagedResult<SaleDto>> SalesAsync(DateOnly? date, int? page, int? pageSize, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var (p, size) = Paging.Clamp(page, pageSize);
        var b = scope.BranchFilter;
        var q = db.PharmacySales.AsNoTracking().Where(s => b == null || s.BranchId == b);
        if (date is { } d) { var from = d.ToDateTime(TimeOnly.MinValue); q = q.Where(s => s.CreatedAt >= from && s.CreatedAt < from.AddDays(1)); }
        var total = await q.CountAsync(ct);
        var rows = await q.Include(s => s.Lines).OrderByDescending(s => s.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<SaleDto>(rows.Select(ToDto).ToList(), p, size, total);
    }

    public async Task<SaleDto> GetSaleAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var b = scope.BranchFilter;
        var s = await db.PharmacySales.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id && (b == null || x.BranchId == b), ct) ?? throw ApiException.NotFound("sale_not_found", "عملية البيع غير موجودة.");
        return ToDto(s);
    }

    // ------------------------------------------------------------ أوامر الشراء

    public async Task<PurchaseOrderDto> CreatePoAsync(CreatePurchaseOrderRequest req, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var branch = scope.BranchForNew();
        var drugIds = req.Lines.Select(l => l.DrugId!.Value).Distinct().ToList();
        if (await db.Drugs.CountAsync(d => drugIds.Contains(d.Id) && d.IsActive, ct) != drugIds.Count) throw ApiException.NotFound("drug_not_found", "أحد الأدوية غير موجود.");
        var po = new PurchaseOrder { No = "TMP" + Guid.NewGuid().ToString("N")[..16], BranchId = branch, Supplier = req.Supplier.Trim(), OrderDate = DateOnly.FromDateTime(UtcNow), CreatedByUserId = me.Id,
            Lines = req.Lines.GroupBy(l => l.DrugId!.Value).Select(g => new PurchaseOrderLine { DrugId = g.Key, Quantity = g.Sum(x => x.Quantity), UnitCost = g.Last().UnitCost }).ToList() };
        po.Total = po.Lines.Sum(l => l.Quantity * l.UnitCost);
        db.PurchaseOrders.Add(po);
        await db.SaveChangesAsync(ct);
        po.No = $"PO-{po.Id:D5}";
        await db.SaveChangesAsync(ct);
        return await GetPoAsync(po.Id, ct);
    }

    public async Task<PagedResult<PurchaseOrderDto>> PosAsync(PurchaseOrderStatus? status, int? page, int? pageSize, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var (p, size) = Paging.Clamp(page, pageSize);
        var b = scope.BranchFilter;
        var q = db.PurchaseOrders.AsNoTracking().Where(o => b == null || o.BranchId == b);
        if (status is { } s) q = q.Where(o => o.Status == s);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(o => o.Lines).ThenInclude(l => l.Drug).OrderByDescending(o => o.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<PurchaseOrderDto>(rows.Select(ToDto).ToList(), p, size, total);
    }

    public async Task<PurchaseOrderDto> GetPoAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var b = scope.BranchFilter;
        var o = await db.PurchaseOrders.AsNoTracking().Include(x => x.Lines).ThenInclude(l => l.Drug).FirstOrDefaultAsync(x => x.Id == id && (b == null || x.BranchId == b), ct)
                ?? throw ApiException.NotFound("purchase_order_not_found", "أمر الشراء غير موجود.");
        return ToDto(o);
    }

    public async Task<PurchaseOrderDto> SendPoAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var o = await FindPoAsync(id, ct);
        if (o.Status != PurchaseOrderStatus.Draft) throw ApiException.Conflict("invalid_state", "أُرسل أمر الشراء سابقًا.");
        o.Status = PurchaseOrderStatus.Sent;
        await db.SaveChangesAsync(ct);
        return await GetPoAsync(id, ct);
    }

    /// <summary>استلام كامل أو جزئي: يزيد المخزون (وينشئ الصنف إن لم يكن موجودًا) ويحدّث حالة الأمر.</summary>
    public async Task<PurchaseOrderDto> ReceiveAsync(int id, List<ReceiveLineInput> inputs, CancellationToken ct)
    {
        scope.Require(UserRole.Pharmacy);
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM PurchaseOrders WHERE Id = {id} FOR UPDATE", ct);
            var o = await FindPoAsync(id, ct);
            if (o.Status is PurchaseOrderStatus.Draft) throw ApiException.Conflict("invalid_state", "أرسل أمر الشراء قبل الاستلام.");
            if (o.Status is PurchaseOrderStatus.Received) throw ApiException.Conflict("invalid_state", "استُلم أمر الشراء بالكامل.");
            foreach (var r in inputs)
            {
                var line = o.Lines.FirstOrDefault(l => l.Id == r.LineId) ?? throw ApiException.Validation(nameof(inputs), "بند غير تابع لأمر الشراء");
                if (line.ReceivedQuantity + r.Quantity > line.Quantity) throw ApiException.Validation(nameof(inputs), "الكمية المستلمة تتجاوز المطلوبة");
                line.ReceivedQuantity += r.Quantity;
                var inv = await LockInventoryAsync(o.BranchId, line.DrugId, ct);
                if (inv is null)
                {
                    var price = await db.Drugs.Where(d => d.Id == line.DrugId).Select(d => d.UnitPrice).FirstAsync(ct);
                    db.InventoryItems.Add(new InventoryItem { BranchId = o.BranchId, DrugId = line.DrugId, Quantity = r.Quantity, MinQuantity = 10, UnitCost = line.UnitCost, UnitPrice = price, Supplier = o.Supplier });
                }
                else { inv.Quantity += r.Quantity; inv.UnitCost = line.UnitCost; inv.Supplier = o.Supplier; }
            }
            o.Status = o.Lines.All(l => l.ReceivedQuantity >= l.Quantity) ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
            audit.Add("pharmacy.receive", me.Id, nameof(PurchaseOrder), o.Id, o.Status.ToString(), me.Ip);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        db.ChangeTracker.Clear();
        return await GetPoAsync(id, ct);
    }

    // ------------------------------------------------------------ مساعدات

    /// <summary>صف المخزون بقفل كتابة داخل المعاملة الحالية.</summary>
    private async Task<InventoryItem?> LockInventoryAsync(int branch, int drugId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM InventoryItems WHERE BranchId = {branch} AND DrugId = {drugId} FOR UPDATE", ct);
        return await db.InventoryItems.FirstOrDefaultAsync(i => i.BranchId == branch && i.DrugId == drugId, ct);
    }

    private async Task<Prescription> FindRxAsync(int id, CancellationToken ct)
    {
        var rx = await db.Prescriptions.Include(r => r.Items).Include(r => r.Patient).Include(r => r.Doctor).FirstOrDefaultAsync(r => r.Id == id, ct);
        return rx is not null && scope.InBranch(rx.BranchId) ? rx : throw ApiException.NotFound("prescription_not_found", "الوصفة غير موجودة.");
    }

    private async Task<InventoryItem> FindInventoryAsync(int id, CancellationToken ct)
    {
        var i = await db.InventoryItems.Include(x => x.Drug).FirstOrDefaultAsync(x => x.Id == id, ct);
        return i is not null && scope.InBranch(i.BranchId) ? i : throw ApiException.NotFound("inventory_not_found", "الصنف غير موجود في المخزون.");
    }

    private async Task<PurchaseOrder> FindPoAsync(int id, CancellationToken ct)
    {
        var o = await db.PurchaseOrders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is not null && scope.InBranch(o.BranchId) ? o : throw ApiException.NotFound("purchase_order_not_found", "أمر الشراء غير موجود.");
    }

    private async Task<List<RxQueueDto>> ToQueueDtosAsync(List<Prescription> rows, CancellationToken ct)
    {
        var drugIds = rows.SelectMany(r => r.Items.Where(i => i.DrugId != null).Select(i => i.DrugId!.Value)).Distinct().ToList();
        var branches = rows.Select(r => r.BranchId).Distinct().ToList();
        var stock = await db.InventoryItems.AsNoTracking().Where(i => drugIds.Contains(i.DrugId) && branches.Contains(i.BranchId)).Select(i => new { i.BranchId, i.DrugId, i.Quantity }).ToListAsync(ct);
        return rows.Select(r => new RxQueueDto(r.Id, r.No, r.Status, r.Priority, r.CreatedAt, new PersonRefDto(r.Patient.Id, r.Patient.FullName, r.Patient.AvatarKey), r.Doctor.FullName,
            r.Items.Sum(i => i.Quantity * i.UnitPrice),
            r.Items.OrderBy(i => i.Id).Select(i => new RxItemQueueDto(i.Id, i.SubstituteDrugName ?? i.DrugName, i.Dose, i.Frequency, i.Quantity, i.Dispensed, i.DrugId,
                i.DrugId is { } d ? stock.FirstOrDefault(s => s.DrugId == d && s.BranchId == r.BranchId)?.Quantity ?? 0 : null, i.AllergyOverride)).ToList(), r.RejectionReason)).ToList();
    }

    private InventoryDto ToDto(InventoryItem i)
    {
        var soon = i.ExpiryDate is { } e && e <= DateOnly.FromDateTime(UtcNow).AddDays(90);
        return new InventoryDto(i.Id, i.DrugId, i.Drug.Name, i.Drug.Form, i.Quantity, i.MinQuantity, i.Quantity <= i.MinQuantity, i.ExpiryDate, soon, i.Supplier, i.UnitCost, i.UnitPrice);
    }

    private static SaleDto ToDto(PharmacySale s) => new(s.Id, s.No, s.CreatedAt, s.Method, s.Total, s.Status, s.CustomerName, s.PrescriptionId,
        s.Lines.OrderBy(l => l.Id).Select(l => new SaleLineDto(l.ItemName, l.Quantity, l.UnitPrice, l.Quantity * l.UnitPrice)).ToList());

    private static PurchaseOrderDto ToDto(PurchaseOrder o) => new(o.Id, o.No, o.Supplier, o.Status, o.OrderDate, o.Total,
        o.Lines.OrderBy(l => l.Id).Select(l => new PoLineDto(l.Id, l.DrugId, l.Drug?.Name ?? "", l.Quantity, l.ReceivedQuantity, l.UnitCost)).ToList());
}
