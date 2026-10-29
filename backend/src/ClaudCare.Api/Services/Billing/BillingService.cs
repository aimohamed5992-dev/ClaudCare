using ClaudCare.Api.Auth;
using ClaudCare.Api.Configuration;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Services.Billing;

/// <summary>
/// الفواتير والمدفوعات. تُنشأ فاتورة مع كل حجز، وتُسوّى عند الإلغاء (استرداد أو رسوم متأخرة).
/// النطاق: المريض فواتيره، الاستقبال/مدير الفرع فواتير فرعهم، المالية/المستشفى/الإدارة الكل؛ خارج النطاق ⇒ 404.
/// </summary>
public sealed class BillingService(ClaudCareDbContext db, ICurrentUser me, IClinicClock clock, INotificationService notifications, IAuditService audit, TimeProvider time)
{
    private static readonly UserRole[] BranchDesk = [UserRole.Reception, UserRole.Branch];
    private static readonly UserRole[] AllBranches = [UserRole.Finance, UserRole.Hospital, UserRole.Admin];
    private static readonly PaymentMethod[] PatientMethods = [PaymentMethod.Card, PaymentMethod.ApplePay];
    private static readonly PaymentMethod[] StaffMethods = [PaymentMethod.Card, PaymentMethod.Cash, PaymentMethod.ApplePay, PaymentMethod.BankTransfer];

    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------ مع الحجز والإلغاء

    /// <summary>يضيف فاتورة الموعد (ودفعته الإلكترونية إن وُجدت) للسياق دون حفظ، ويستهلك من حد التأمين. يُكمَل الربط بعد الحفظ بـ <see cref="LinkAsync"/>.</summary>
    public async Task<Invoice> StageForAppointmentAsync(Appointment appt, string doctorName, PriceQuote quote, PaymentMethod? method, bool paid, CancellationToken ct)
    {
        var invoice = new Invoice
        {
            No = "TMP" + Guid.NewGuid().ToString("N")[..16],
            PatientId = appt.PatientId, BranchId = appt.BranchId, Title = $"كشف — {doctorName}",
            IssueDate = clock.Today, DueDate = appt.Date,
            Subtotal = quote.Price, InsuranceCovered = quote.InsuranceCovered,
            Items = [new InvoiceItem { Description = $"كشف — {doctorName}", Quantity = 1, UnitPrice = quote.Price }],
        };
        if (paid && quote.Due > 0 && method is { } m)
        {
            invoice.Payments.Add(new Payment { Amount = quote.Due, Method = m, PaidAt = UtcNow, ReceivedByUserId = null, Reference = "SIM-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant() });
            invoice.PaidAmount = quote.Due;
        }
        invoice.Recalculate();
        db.Invoices.Add(invoice);

        if (quote.InsuranceCovered > 0)
        {
            var policy = await ActivePolicyAsync(appt.PatientId, ct);
            if (policy is not null) policy.UsedAmount += quote.InsuranceCovered;
        }
        return invoice;
    }

    /// <summary>بعد حفظ الموعد والفاتورة: يرقّم الفاتورة ويربطها بالموعد.</summary>
    public async Task LinkAsync(Invoice invoice, Appointment appt, CancellationToken ct)
    {
        invoice.No = $"INV-{invoice.Id:D6}";
        invoice.AppointmentId = appt.Id;
        appt.InvoiceId = invoice.Id;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>تسوية فاتورة الموعد الملغى. lateFee &gt; 0 ⇒ تتحول الفاتورة إلى رسوم إلغاء متأخر؛ وإلا تُلغى ويُسترد المدفوع.</summary>
    public async Task SettleCancellationAsync(Appointment appt, decimal lateFee, CancellationToken ct)
    {
        if (appt.InvoiceId is not { } invoiceId) return;
        var inv = await db.Invoices.Include(i => i.Items).Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (inv is null || inv.Status == InvoiceStatus.Cancelled) return;

        if (inv.InsuranceCovered > 0)
        {
            var policy = await ActivePolicyAsync(inv.PatientId, ct);
            if (policy is not null) policy.UsedAmount = Math.Max(0, policy.UsedAmount - inv.InsuranceCovered);
        }

        var lastMethod = inv.Payments.OrderByDescending(p => p.Id).Select(p => (PaymentMethod?)p.Method).FirstOrDefault() ?? PaymentMethod.Card;
        var paid = inv.PaidAmount;

        if (lateFee <= 0)
        {
            if (paid > 0) AddRefund(inv, paid, lastMethod);
            inv.PaidAmount = 0;
            inv.Status = InvoiceStatus.Cancelled;
            if (paid > 0) appt.Paid = false;
            if (paid > 0) notifications.Add(inv.PatientId, NotificationType.Invoice, "تم استرداد المبلغ", $"{paid:0.##} ر.س · {inv.No}", nameof(Invoice), inv.Id);
            return;
        }

        db.InvoiceItems.RemoveRange(inv.Items);
        inv.Items = [new InvoiceItem { Description = "رسوم إلغاء متأخر", Quantity = 1, UnitPrice = lateFee }];
        inv.Title = "رسوم إلغاء متأخر";
        inv.Subtotal = lateFee;
        inv.InsuranceCovered = 0;
        inv.Discount = 0;
        inv.DueDate = clock.Today;
        if (paid > lateFee) AddRefund(inv, paid - lateFee, lastMethod);
        inv.PaidAmount = Math.Min(paid, lateFee);
        inv.Recalculate();
        appt.Paid = false;
        notifications.Add(inv.PatientId, NotificationType.Invoice, "رسوم إلغاء متأخر", $"{lateFee:0.##} ر.س · {inv.No}", nameof(Invoice), inv.Id);
    }

    private void AddRefund(Invoice inv, decimal amount, PaymentMethod method) =>
        inv.Payments.Add(new Payment { Amount = -amount, Method = method, PaidAt = UtcNow, ReceivedByUserId = me.IsAuthenticated ? me.Id : null, Reference = "REFUND" });

    private Task<PatientInsurance?> ActivePolicyAsync(int patientId, CancellationToken ct) =>
        db.PatientInsurances.Where(p => p.PatientId == patientId && p.IsActive).OrderByDescending(p => p.ExpiresAt).FirstOrDefaultAsync(ct);

    // ------------------------------------------------------------ القراءة

    public async Task<InvoiceListDto> ListAsync(InvoiceStatus? status, int? patientId, int? appointmentId, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = Scoped();
        if (patientId is { } pid && me.Role != UserRole.Patient) q = q.Where(i => i.PatientId == pid);
        if (appointmentId is { } aid) q = q.Where(i => i.AppointmentId == aid);

        var outstanding = await q.Where(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid).SumAsync(i => i.Subtotal - i.Discount - i.InsuranceCovered - i.PaidAmount, ct);
        var unpaid = await q.CountAsync(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid, ct);

        if (status is { } st) q = q.Where(i => i.Status == st);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(i => i.Items).Include(i => i.Payments).OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new InvoiceListDto(new PagedResult<InvoiceDto>(await ToDtosAsync(rows, ct), p, size, total), outstanding, unpaid);
    }

    public async Task<InvoiceDto> GetAsync(int id, CancellationToken ct)
    {
        var inv = await Scoped().Include(i => i.Items).Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id, ct)
                  ?? throw ApiException.NotFound("invoice_not_found", "الفاتورة غير موجودة.");
        return (await ToDtosAsync([inv], ct))[0];
    }

    // ------------------------------------------------------------ الدفع

    public async Task<InvoiceDto> PayAsync(int id, PayInvoiceRequest req, CancellationToken ct)
    {
        var isPatient = me.Role == UserRole.Patient;
        if (!isPatient && !BranchDesk.Contains(me.Role) && !AllBranches.Contains(me.Role))
            throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية تحصيل الفواتير.");
        var method = req.Method!.Value;
        if (!(isPatient ? PatientMethods : StaffMethods).Contains(method)) throw ApiException.Validation(nameof(req.Method), "طريقة الدفع غير مدعومة");
        if (!await Scoped().AnyAsync(i => i.Id == id, ct)) throw ApiException.NotFound("invoice_not_found", "الفاتورة غير موجودة.");

        var strategy = db.Database.CreateExecutionStrategy();
        var invoiceId = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // قفل الصف لمنع دفعتين متزامنتين من تجاوز المتبقي
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Invoices WHERE Id = {id} FOR UPDATE", ct);
            var inv = await db.Invoices.Include(i => i.Items).Include(i => i.Payments).FirstAsync(i => i.Id == id, ct);

            if (inv.Status == InvoiceStatus.Cancelled) throw ApiException.Conflict("invalid_state", "الفاتورة ملغاة.");
            if (inv.Remaining <= 0) throw ApiException.Conflict("invalid_state", "لا يوجد مبلغ مستحق على هذه الفاتورة.");

            var amount = isPatient ? inv.Remaining : req.Amount ?? inv.Remaining;
            if (amount > inv.Remaining) throw ApiException.Validation(nameof(req.Amount), $"المبلغ يتجاوز المتبقي ({inv.Remaining:0.##})");
            decimal change = 0;
            if (req.Tendered is { } tendered)
            {
                if (method != PaymentMethod.Cash) throw ApiException.Validation(nameof(req.Tendered), "المبلغ المستلَم للدفع النقدي فقط");
                if (tendered < amount) throw ApiException.Validation(nameof(req.Tendered), "المبلغ المستلَم أقل من المطلوب");
                change = tendered - amount;
            }

            inv.Payments.Add(new Payment { Amount = amount, Method = method, PaidAt = UtcNow, ReceivedByUserId = me.Id, Reference = req.Reference?.Trim() ?? "SIM-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), ChangeGiven = change });
            inv.PaidAmount += amount;
            inv.Recalculate();

            if (inv.AppointmentId is { } aid && inv.Remaining <= 0)
            {
                var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == aid, ct);
                if (appt is { Status: not AppointmentStatus.Cancelled }) { appt.Paid = true; appt.PaymentMethod = method; }
            }
            if (!isPatient) notifications.Add(inv.PatientId, NotificationType.Payment, "تم استلام دفعتك", $"{amount:0.##} ر.س · {inv.No}", nameof(Invoice), inv.Id);
            audit.Add("invoice.pay", me.Id, nameof(Invoice), inv.Id, $"{method} {amount}", me.Ip);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return inv.Id;
        });
        db.ChangeTracker.Clear();
        return await GetAsync(invoiceId, ct);
    }

    // ------------------------------------------------------------ التأمين (المريض)

    public async Task<PatientInsuranceDto> MyInsuranceAsync(CancellationToken ct)
    {
        var p = await db.PatientInsurances.AsNoTracking().Include(x => x.Company).Where(x => x.PatientId == me.Id && x.IsActive).OrderByDescending(x => x.ExpiresAt).FirstOrDefaultAsync(ct)
                ?? throw ApiException.NotFound("insurance_not_found", "لا توجد وثيقة تأمين.");
        var expired = p.ExpiresAt < clock.Today;
        return new PatientInsuranceDto(p.Id, p.Company.Name, p.Tier, p.PolicyNo, p.ExpiresAt, p.CopayRate, p.AnnualLimit, p.UsedAmount, Math.Max(0, p.AnnualLimit - p.UsedAmount), p.IsActive, expired);
    }

    // ------------------------------------------------------------ مساعدات

    private IQueryable<Invoice> Scoped()
    {
        var q = db.Invoices.AsNoTracking();
        if (me.Role == UserRole.Patient) return q.Where(i => i.PatientId == me.Id);
        if (BranchDesk.Contains(me.Role)) return me.BranchId is { } b ? q.Where(i => i.BranchId == b) : q.Where(_ => false);
        if (AllBranches.Contains(me.Role)) return q;
        throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية عرض الفواتير.");
    }

    private async Task<List<InvoiceDto>> ToDtosAsync(List<Invoice> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.PatientId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        return rows.Select(i =>
        {
            var u = users[i.PatientId];
            return new InvoiceDto(i.Id, i.No, i.Title, new PersonRefDto(u.Id, u.FullName, u.AvatarKey), i.BranchId, i.AppointmentId, i.IssueDate, i.DueDate, i.Status,
                i.Subtotal, i.InsuranceCovered, i.Discount, i.PaidAmount, i.Remaining, i.Notes,
                i.Items.Select(x => new InvoiceItemDto(x.Description, x.Quantity, x.UnitPrice, x.Quantity * x.UnitPrice)).ToList(),
                i.Payments.OrderBy(p => p.Id).Select(p => new PaymentDto(p.Id, p.Amount, p.Method, p.PaidAt, p.Reference, p.ChangeGiven)).ToList());
        }).ToList();
    }
}
