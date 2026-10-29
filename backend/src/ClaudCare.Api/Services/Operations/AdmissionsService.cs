using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Operations;

/// <summary>
/// الأسرّة والتنويم والنقل والخروج. السرير يُحجز بتحديث ذري (Available ⇒ Occupied) فلا يُخصَّص لمريضين.
/// الخروج يتطلب إكمال الخطوات الأربع وسداد فواتير التنويم فعليًا.
/// </summary>
public sealed class AdmissionsService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, INotificationService notifications, IAuditService audit, TimeProvider time)
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
    private static readonly UserRole[] Requesters = [UserRole.Admissions, UserRole.Doctor, UserRole.Emergency];

    // ------------------------------------------------------------ الأسرّة

    public async Task<BedBoardDto> BedsAsync(string? ward, BedStatus? status, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions, UserRole.Nurse, UserRole.Emergency, UserRole.Branch, UserRole.Doctor);
        var b = scope.BranchFilter;
        var all = db.Beds.AsNoTracking().Where(x => b == null || x.BranchId == b);
        var counts = await all.GroupBy(x => x.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        int C(BedStatus s) => counts.FirstOrDefault(c => c.Key == s)?.N ?? 0;

        var q = all;
        if (!string.IsNullOrWhiteSpace(ward)) q = q.Where(x => x.Ward == ward);
        if (status is { } st) q = q.Where(x => x.Status == st);
        var beds = await q.OrderBy(x => x.Ward).ThenBy(x => x.Code).ToListAsync(ct);
        var ids = beds.Select(x => x.Id).ToList();
        var occupants = await db.Admissions.AsNoTracking().Where(a => a.BedId != null && ids.Contains(a.BedId.Value) && a.Status == AdmissionStatus.Admitted)
            .Select(a => new { a.BedId, a.Patient.FullName }).ToListAsync(ct);
        return new BedBoardDto(counts.Sum(c => c.N), C(BedStatus.Available), C(BedStatus.Occupied), C(BedStatus.Cleaning), C(BedStatus.Reserved),
            beds.Select(x => new BedDto(x.Id, x.Ward, x.Code, x.Status, x.ReservedFor, occupants.FirstOrDefault(o => o.BedId == x.Id)?.FullName)).ToList());
    }

    /// <summary>تغيير حالة السرير يدويًا (نظافة/متاح/محجوز). «مشغول» لا يُضبط إلا بالتنويم.</summary>
    public async Task<BedDto> SetBedStatusAsync(int id, BedStatus status, string? reservedFor, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions, UserRole.Nurse);
        if (status == BedStatus.Occupied) throw ApiException.Validation(nameof(status), "الحالة «مشغول» تُضبط بالتنويم فقط");
        var bed = await db.Beds.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (bed is null || !scope.InBranch(bed.BranchId)) throw ApiException.NotFound("bed_not_found", "السرير غير موجود.");
        if (bed.Status == BedStatus.Occupied) throw ApiException.Conflict("bed_occupied", "السرير مشغول بمريض.");
        if (status == BedStatus.Reserved && string.IsNullOrWhiteSpace(reservedFor)) throw ApiException.Validation(nameof(reservedFor), "اذكر لمن الحجز");
        bed.Status = status;
        bed.ReservedFor = status == BedStatus.Reserved ? reservedFor!.Trim() : null;
        await db.SaveChangesAsync(ct);
        return new BedDto(bed.Id, bed.Ward, bed.Code, bed.Status, bed.ReservedFor, null);
    }

    // ------------------------------------------------------------ التنويم

    public async Task<AdmissionDto> RequestAsync(RequestAdmissionRequest r, CancellationToken ct)
    {
        scope.Require(Requesters);
        if (!await db.Users.AnyAsync(u => u.Id == r.PatientId && u.Role == UserRole.Patient, ct)) throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");
        if (await db.Admissions.AnyAsync(a => a.PatientId == r.PatientId && a.Status != AdmissionStatus.Discharged, ct)) throw ApiException.Conflict("already_admitted", "للمريض طلب تنويم أو إقامة قائمة.");
        var doctorId = r.DoctorId ?? (me.Role == UserRole.Doctor ? me.Id : null);
        if (doctorId is { } d && !await db.Users.AnyAsync(u => u.Id == d && u.Role == UserRole.Doctor, ct)) throw ApiException.NotFound("doctor_not_found", "الطبيب غير موجود.");

        var a = new Admission { No = "TMP" + Guid.NewGuid().ToString("N")[..16], PatientId = r.PatientId!.Value, BranchId = scope.BranchForNew(), DoctorId = doctorId, Type = r.Type!.Value,
            Reason = r.Reason.Trim(), RequestedWard = r.RequestedWard.Trim(), RequestedFrom = r.RequestedFrom?.Trim(), RequestedAt = UtcNow };
        db.Admissions.Add(a);
        await db.SaveChangesAsync(ct);
        a.No = $"ADM-{a.Id:D5}";
        audit.Add("admission.request", me.Id, nameof(Admission), a.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return await GetAsync(a.Id, ct);
    }

    public async Task<PagedResult<AdmissionDto>> ListAsync(AdmissionStatus? status, string? ward, int? page, int? pageSize, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions, UserRole.Nurse, UserRole.Emergency, UserRole.Branch, UserRole.Doctor);
        var (p, size) = Paging.Clamp(page, pageSize);
        var b = scope.BranchFilter;
        var q = db.Admissions.AsNoTracking().Where(a => b == null || a.BranchId == b);
        if (me.Role == UserRole.Doctor) q = q.Where(a => a.DoctorId == me.Id);
        if (status is { } s) q = q.Where(a => a.Status == s);
        if (!string.IsNullOrWhiteSpace(ward)) q = q.Where(a => a.RequestedWard == ward);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(a => a.Patient).Include(a => a.Bed).Include(a => a.Doctor).OrderByDescending(a => a.RequestedAt).ThenByDescending(a => a.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<AdmissionDto>(rows.Select(ToDto).ToList(), p, size, total);
    }

    public async Task<AdmissionDto> GetAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions, UserRole.Nurse, UserRole.Emergency, UserRole.Branch, UserRole.Doctor);
        var a = await db.Admissions.AsNoTracking().Include(x => x.Patient).Include(x => x.Bed).Include(x => x.Doctor).FirstOrDefaultAsync(x => x.Id == id, ct);
        var visible = a is not null && scope.InBranch(a.BranchId) && (me.Role != UserRole.Doctor || a.DoctorId == me.Id);
        return visible ? ToDto(a!) : throw ApiException.NotFound("admission_not_found", "التنويم غير موجود.");
    }

    public async Task<AdmissionDto> SetChecksAsync(int id, AdmissionChecksRequest r, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions);
        var a = await FindAsync(id, ct);
        if (a.Status == AdmissionStatus.Discharged) throw ApiException.Conflict("invalid_state", "انتهى التنويم.");
        if (r.GuardianConsent is { } g) a.GuardianConsent = g;
        if (r.InsuranceApprovalVerified is { } v) a.InsuranceApprovalVerified = v;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdmissionDto> AdmitAsync(int id, int bedId, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions);
        var a = await FindAsync(id, ct);
        if (a.Status != AdmissionStatus.Pending) throw ApiException.Conflict("invalid_state", $"لا يمكن التنويم والطلب في حالة «{a.Status}».");

        var year = DateTime.UtcNow.Year;
        var birth = await db.Users.Where(u => u.Id == a.PatientId).Select(u => u.BirthYear).FirstAsync(ct);
        if (birth is { } by && year - by < 18 && !a.GuardianConsent) throw ApiException.Conflict("guardian_consent_required", "تنويم القاصر يتطلب موافقة ولي الأمر.");
        if (a.Type == AdmissionType.Planned && !a.InsuranceApprovalVerified) throw ApiException.Conflict("insurance_approval_required", "التنويم المخطط يتطلب التحقق من موافقة التأمين.");

        await ClaimBedAsync(bedId, a.BranchId, ct);
        a.BedId = bedId;
        a.Status = AdmissionStatus.Admitted;
        a.AdmittedAt = UtcNow;
        notifications.Add(a.PatientId, NotificationType.System, "تم تنويمك", a.RequestedWard, nameof(Admission), a.Id);
        if (a.DoctorId is { } d) notifications.Add(d, NotificationType.System, "تم تنويم مريضك", a.No, nameof(Admission), a.Id);
        audit.Add("admission.admit", me.Id, nameof(Admission), a.Id, $"bed={bedId}", me.Ip);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdmissionDto> DischargeAsync(int id, DischargeRequest r, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions, UserRole.Doctor);
        var a = await FindAsync(id, ct);
        if (a.Status != AdmissionStatus.Admitted) throw ApiException.Conflict("invalid_state", "المريض غير منوَّم حاليًا.");
        var missing = new List<string>();
        if (!r.DischargeOrderConfirmed) missing.Add("تأكيد أمر الخروج");
        if (!r.MedsReconciled) missing.Add("مطابقة الأدوية");
        if (!r.BillSettled) missing.Add("تسوية الفاتورة");
        if (!r.InstructionsGiven) missing.Add("تعليمات الخروج");
        if (missing.Count > 0) throw new ApiException(StatusCodes.Status409Conflict, "discharge_incomplete", "أكمل خطوات الخروج: " + string.Join("، ", missing), new Dictionary<string, object?> { ["missing"] = missing });
        var unpaid = await db.Invoices.Where(i => i.AdmissionId == a.Id && i.Status != InvoiceStatus.Cancelled).ToListAsync(ct);
        if (unpaid.Any(i => i.Remaining > 0)) throw ApiException.Conflict("bill_unsettled", "توجد فواتير تنويم غير مسددة.");

        a.DischargeOrderConfirmed = a.MedsReconciled = a.BillSettled = a.InstructionsGiven = true;
        a.DischargeSummary = r.Summary.Trim();
        a.Status = AdmissionStatus.Discharged;
        a.DischargedAt = UtcNow;
        if (a.BedId is { } bed) await db.Beds.Where(x => x.Id == bed).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BedStatus.Cleaning), ct);
        notifications.Add(a.PatientId, NotificationType.System, "تم خروجك من المستشفى", "نتمنى لك الشفاء العاجل", nameof(Admission), a.Id);
        audit.Add("admission.discharge", me.Id, nameof(Admission), a.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    // ------------------------------------------------------------ النقل

    public async Task<TransferDto> RequestTransferAsync(int admissionId, TransferRequest r, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions, UserRole.Doctor, UserRole.Nurse);
        var a = await FindAsync(admissionId, ct);
        if (a.Status != AdmissionStatus.Admitted) throw ApiException.Conflict("invalid_state", "المريض غير منوَّم حاليًا.");
        if (await db.BedTransfers.AnyAsync(t => t.AdmissionId == a.Id && t.Status != TransferStatus.Done, ct)) throw ApiException.Conflict("transfer_pending", "يوجد طلب نقل قائم.");
        var from = a.BedId is { } bid ? await db.Beds.Where(x => x.Id == bid).Select(x => x.Ward).FirstAsync(ct) : a.RequestedWard;
        if (from == r.ToWard.Trim()) throw ApiException.Validation(nameof(r.ToWard), "القسم الحالي هو نفسه");
        var t = new BedTransfer { No = "TMP" + Guid.NewGuid().ToString("N")[..16], AdmissionId = a.Id, FromWard = from, ToWard = r.ToWard.Trim(), Reason = r.Reason.Trim() };
        db.BedTransfers.Add(t);
        await db.SaveChangesAsync(ct);
        t.No = $"TRF-{t.Id:D5}";
        await db.SaveChangesAsync(ct);
        return await TransferAsync(t.Id, ct);
    }

    public async Task<IReadOnlyList<TransferDto>> TransfersAsync(TransferStatus? status, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions, UserRole.Nurse, UserRole.Doctor, UserRole.Branch);
        var b = scope.BranchFilter;
        var q = db.BedTransfers.AsNoTracking().Where(t => b == null || t.Admission.BranchId == b);
        if (status is { } s) q = q.Where(t => t.Status == s);
        return await q.OrderByDescending(t => t.Id).Take(100).Select(t => new TransferDto(t.Id, t.No, t.AdmissionId, t.Admission.Patient.FullName, t.FromWard, t.ToWard, t.Reason, t.Status, t.CreatedAt, t.CompletedAt)).ToListAsync(ct);
    }

    public async Task<TransferDto> ApproveTransferAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions);
        var t = await FindTransferAsync(id, ct);
        if (t.Status != TransferStatus.WaitingBed) throw ApiException.Conflict("invalid_state", "الطلب ليس بانتظار الموافقة.");
        t.Status = TransferStatus.Approved;
        await db.SaveChangesAsync(ct);
        return await TransferAsync(id, ct);
    }

    public async Task<TransferDto> CompleteTransferAsync(int id, int bedId, CancellationToken ct)
    {
        scope.Require(UserRole.Admissions);
        var t = await FindTransferAsync(id, ct);
        if (t.Status != TransferStatus.Approved) throw ApiException.Conflict("invalid_state", "وافق على الطلب أولًا.");
        var a = await db.Admissions.FirstAsync(x => x.Id == t.AdmissionId, ct);
        var target = await db.Beds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bedId && x.BranchId == a.BranchId, ct) ?? throw ApiException.NotFound("bed_not_found", "السرير غير موجود.");
        if (!target.Ward.Equals(t.ToWard, StringComparison.OrdinalIgnoreCase)) throw ApiException.Validation(nameof(bedId), "السرير ليس في القسم المطلوب");

        await ClaimBedAsync(bedId, a.BranchId, ct);
        if (a.BedId is { } old) await db.Beds.Where(x => x.Id == old).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BedStatus.Cleaning), ct);
        a.BedId = bedId;
        t.ToBedId = bedId;
        t.Status = TransferStatus.Done;
        t.CompletedAt = UtcNow;
        notifications.Add(a.PatientId, NotificationType.System, "تم نقلك إلى سرير جديد", t.ToWard, nameof(Admission), a.Id);
        await db.SaveChangesAsync(ct);
        return await TransferAsync(id, ct);
    }

    // ------------------------------------------------------------ مساعدات

    /// <summary>حجز السرير بتحديث شرطي ذري؛ فشله يعني أنه ليس متاحًا (أو أُخذ للتو).</summary>
    private async Task ClaimBedAsync(int bedId, int branchId, CancellationToken ct)
    {
        if (!await db.Beds.AnyAsync(b => b.Id == bedId && b.BranchId == branchId, ct)) throw ApiException.NotFound("bed_not_found", "السرير غير موجود.");
        var rows = await db.Beds.Where(b => b.Id == bedId && b.BranchId == branchId && b.Status == BedStatus.Available)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BedStatus.Occupied).SetProperty(b => b.ReservedFor, (string?)null), ct);
        if (rows == 0) throw ApiException.Conflict("bed_unavailable", "السرير غير متاح.");
    }

    private async Task<Admission> FindAsync(int id, CancellationToken ct)
    {
        var a = await db.Admissions.FirstOrDefaultAsync(x => x.Id == id, ct);
        var visible = a is not null && scope.InBranch(a.BranchId) && (me.Role != UserRole.Doctor || a.DoctorId == me.Id);
        return visible ? a! : throw ApiException.NotFound("admission_not_found", "التنويم غير موجود.");
    }

    private async Task<BedTransfer> FindTransferAsync(int id, CancellationToken ct)
    {
        var t = await db.BedTransfers.Include(x => x.Admission).FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is not null && scope.InBranch(t.Admission.BranchId) ? t : throw ApiException.NotFound("transfer_not_found", "طلب النقل غير موجود.");
    }

    private async Task<TransferDto> TransferAsync(int id, CancellationToken ct) =>
        await db.BedTransfers.AsNoTracking().Where(t => t.Id == id).Select(t => new TransferDto(t.Id, t.No, t.AdmissionId, t.Admission.Patient.FullName, t.FromWard, t.ToWard, t.Reason, t.Status, t.CreatedAt, t.CompletedAt)).FirstAsync(ct);

    private static AdmissionDto ToDto(Admission a) => new(a.Id, a.No, new PersonRefDto(a.Patient.Id, a.Patient.FullName, a.Patient.AvatarKey), a.Type, a.Status, a.Reason, a.RequestedWard, a.Bed?.Code, a.Bed?.Ward, a.Doctor?.FullName,
        a.RequestedAt, a.AdmittedAt, a.DischargedAt, a.GuardianConsent, a.InsuranceApprovalVerified, a.DischargeOrderConfirmed, a.MedsReconciled, a.BillSettled, a.InstructionsGiven, a.DischargeSummary);
}
