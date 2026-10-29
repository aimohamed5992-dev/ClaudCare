using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Operations;

/// <summary>
/// التأمين: بحث الوثائق، الموافقات المسبقة (طلب ← مستندات ← قرار)، والمطالبات من الفواتير (مسودة ← مقدَّمة ← مسددة/جزئية/مرفوضة).
/// الطبيب يرى طلباته فقط؛ الباقي لموظف التأمين والإدارة.
/// </summary>
public sealed class InsuranceService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, IClinicClock clock, INotificationService notifications, IAuditService audit, TimeProvider time)
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<InsuranceCompanyDto>> CompaniesAsync(CancellationToken ct)
    {
        scope.Require(UserRole.Insurance, UserRole.Reception, UserRole.Admissions, UserRole.Finance, UserRole.Doctor);
        return await db.InsuranceCompanies.AsNoTracking().OrderBy(c => c.Name).Select(c => new InsuranceCompanyDto(c.Id, c.Name, c.Status, c.ContractEndsAt, c.DiscountPct)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PolicyLookupDto>> PoliciesAsync(int patientId, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance, UserRole.Reception, UserRole.Admissions, UserRole.Finance);
        var today = clock.Today;
        return (await db.PatientInsurances.AsNoTracking().Include(p => p.Company).Where(p => p.PatientId == patientId).OrderByDescending(p => p.ExpiresAt).ToListAsync(ct))
            .Select(p => new PolicyLookupDto(p.Id, p.Company.Name, p.Tier, p.PolicyNo, p.ExpiresAt, p.CopayRate, p.AnnualLimit, p.UsedAmount, Math.Max(0, p.AnnualLimit - p.UsedAmount), p.IsActive, p.ExpiresAt < today)).ToList();
    }

    // ------------------------------------------------------------ الموافقات المسبقة

    public async Task<PreAuthDto> RequestPreAuthAsync(PreAuthInput r, CancellationToken ct)
    {
        scope.Require(UserRole.Doctor, UserRole.Insurance, UserRole.Reception, UserRole.Admissions);
        if (!await db.Users.AnyAsync(u => u.Id == r.PatientId && u.Role == UserRole.Patient, ct)) throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");
        var today = clock.Today;
        var policy = await db.PatientInsurances.Include(p => p.Company).Where(p => p.PatientId == r.PatientId && p.IsActive && p.ExpiresAt >= today).OrderByDescending(p => p.ExpiresAt).FirstOrDefaultAsync(ct)
                     ?? throw ApiException.Conflict("no_active_policy", "لا توجد وثيقة تأمين سارية للمريض.");
        var pa = new PreAuthorization { No = "TMP" + Guid.NewGuid().ToString("N")[..16], PatientId = r.PatientId!.Value, CompanyId = policy.CompanyId, PolicyId = policy.Id, DoctorId = me.Role == UserRole.Doctor ? me.Id : null,
            ProcedureName = r.ProcedureName.Trim(), Diagnosis = r.Diagnosis?.Trim(), Amount = r.Amount!.Value, Priority = r.Priority, SubmittedAt = UtcNow,
            Documents = (r.Documents ?? []).Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).Distinct().Take(20).Select(d => new PreAuthDocument { Name = d }).ToList() };
        db.PreAuthorizations.Add(pa);
        await db.SaveChangesAsync(ct);
        pa.No = $"PA-{pa.Id:D5}";
        audit.Add("insurance.preauth_request", me.Id, nameof(PreAuthorization), pa.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return await PreAuthAsync(pa.Id, ct);
    }

    public async Task<PagedResult<PreAuthDto>> PreAuthsAsync(ApprovalStatus? status, int? page, int? pageSize, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance, UserRole.Doctor, UserRole.Finance);
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = db.PreAuthorizations.AsNoTracking().AsQueryable();
        if (me.Role == UserRole.Doctor) q = q.Where(x => x.DoctorId == me.Id);
        if (status is { } s) q = q.Where(x => x.Status == s);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(x => x.Patient).Include(x => x.Company).Include(x => x.Policy).Include(x => x.Doctor).Include(x => x.Documents)
            .OrderByDescending(x => x.Priority).ThenBy(x => x.SubmittedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<PreAuthDto>(rows.Select(ToDto).ToList(), p, size, total);
    }

    public async Task<PreAuthDto> PreAuthAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance, UserRole.Doctor, UserRole.Finance, UserRole.Reception, UserRole.Admissions);
        var x = await db.PreAuthorizations.AsNoTracking().Include(a => a.Patient).Include(a => a.Company).Include(a => a.Policy).Include(a => a.Doctor).Include(a => a.Documents).FirstOrDefaultAsync(a => a.Id == id, ct);
        var visible = x is not null && (me.Role == UserRole.Doctor ? x.DoctorId == me.Id : me.Role is not (UserRole.Reception or UserRole.Admissions) || x.DoctorId == null);
        return visible ? ToDto(x!) : throw ApiException.NotFound("preauth_not_found", "طلب الموافقة غير موجود.");
    }

    public async Task<PreAuthDto> ReceiveDocumentAsync(int id, int docId, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance);
        var doc = await db.PreAuthDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.PreAuthorizationId == id, ct) ?? throw ApiException.NotFound("document_not_found", "المستند غير موجود.");
        doc.Received = true;
        await db.SaveChangesAsync(ct);
        return await PreAuthAsync(id, ct);
    }

        public Task<PreAuthDto> DecideAsync(int id, ApprovalStatus decision, decimal? approved, string? rejectReason, CancellationToken ct) => RowLock.RunAsync(db, "PreAuthorizations", id, () => DecideCoreAsync(id, decision, approved, rejectReason, ct), ct);

    private async Task<PreAuthDto> DecideCoreAsync(int id, ApprovalStatus decision, decimal? approved, string? rejectReason, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance);
        if (decision == ApprovalStatus.Pending) throw ApiException.Validation(nameof(decision), "قرار غير صالح");
        var pa = await db.PreAuthorizations.Include(x => x.Documents).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("preauth_not_found", "طلب الموافقة غير موجود.");
        if (pa.Status is not (ApprovalStatus.Pending or ApprovalStatus.MoreInfoNeeded)) throw ApiException.Conflict("invalid_state", "سبق البتّ في الطلب.");
        if (decision == ApprovalStatus.Rejected && string.IsNullOrWhiteSpace(rejectReason)) throw ApiException.Validation(nameof(rejectReason), "اذكر سبب الرفض");
        if (decision == ApprovalStatus.Approved)
        {
            var missing = pa.Documents.Where(d => !d.Received).Select(d => d.Name).ToList();
            if (missing.Count > 0) throw new ApiException(StatusCodes.Status409Conflict, "documents_missing", "مستندات لم تُستلم: " + string.Join("، ", missing), new Dictionary<string, object?> { ["missing"] = missing });
            var amount = approved ?? pa.Amount;
            if (amount <= 0 || amount > pa.Amount) throw ApiException.Validation(nameof(approved), "المبلغ المعتمد يتجاوز المطلوب");
            pa.ApprovedAmount = amount;
        }
        pa.Status = decision;
        pa.RejectReason = decision == ApprovalStatus.Rejected ? rejectReason!.Trim() : null;
        pa.DecidedAt = UtcNow; pa.DecidedByUserId = me.Id;
        var title = decision switch { ApprovalStatus.Approved => "اعتُمدت الموافقة المسبقة", ApprovalStatus.Rejected => "رُفضت الموافقة المسبقة", _ => "الموافقة المسبقة تحتاج معلومات" };
        notifications.Add(pa.PatientId, NotificationType.Alert, title, pa.ProcedureName, nameof(PreAuthorization), pa.Id);
        if (pa.DoctorId is { } d) notifications.Add(d, NotificationType.Alert, title, $"{pa.No} · {pa.ProcedureName}", nameof(PreAuthorization), pa.Id);
        audit.Add("insurance.preauth_decide", me.Id, nameof(PreAuthorization), pa.Id, decision.ToString(), me.Ip);
        await db.SaveChangesAsync(ct);
        return await PreAuthAsync(id, ct);
    }

    private static PreAuthDto ToDto(PreAuthorization x) => new(x.Id, x.No, new PersonRefDto(x.Patient.Id, x.Patient.FullName, x.Patient.AvatarKey), x.Company.Name, x.Policy?.PolicyNo, x.Doctor?.FullName, x.ProcedureName, x.Diagnosis, x.Amount, x.Priority, x.Status,
        x.SubmittedAt, x.DecidedAt, x.ApprovedAmount, x.RejectReason, x.Documents.OrderBy(d => d.Id).Select(d => new PreAuthDocDto(d.Id, d.Name, d.Received)).ToList());

    // ------------------------------------------------------------ المطالبات

    public async Task<ClaimDto> CreateClaimAsync(int invoiceId, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance, UserRole.Finance);
        var inv = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId, ct) ?? throw ApiException.NotFound("invoice_not_found", "الفاتورة غير موجودة.");
        if (inv.Status == InvoiceStatus.Cancelled || inv.InsuranceCovered <= 0) throw ApiException.Conflict("not_claimable", "لا حصة تأمين في هذه الفاتورة.");
        if (await db.InsuranceClaims.AnyAsync(c => c.InvoiceId == invoiceId && c.Status != ClaimStatus.Rejected, ct)) throw ApiException.Conflict("claim_exists", "أُنشئت مطالبة لهذه الفاتورة.");
        var policy = await db.PatientInsurances.AsNoTracking().Where(p => p.PatientId == inv.PatientId).OrderByDescending(p => p.ExpiresAt).FirstOrDefaultAsync(ct) ?? throw ApiException.Conflict("no_active_policy", "لا توجد وثيقة للمريض.");
        var c = new InsuranceClaim { No = "TMP" + Guid.NewGuid().ToString("N")[..16], PatientId = inv.PatientId, CompanyId = policy.CompanyId, InvoiceId = inv.Id, Amount = inv.InsuranceCovered };
        db.InsuranceClaims.Add(c);
        await db.SaveChangesAsync(ct);
        c.No = $"CLM-{c.Id:D5}";
        await db.SaveChangesAsync(ct);
        return await ClaimAsync(c.Id, ct);
    }

    public async Task<PagedResult<ClaimDto>> ClaimsAsync(ClaimStatus? status, int? page, int? pageSize, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance, UserRole.Finance);
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = db.InsuranceClaims.AsNoTracking().AsQueryable();
        if (status is { } s) q = q.Where(c => c.Status == s);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(c => c.Patient).Include(c => c.Company).OrderByDescending(c => c.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<ClaimDto>(rows.Select(ToDto).ToList(), p, size, total);
    }

    public async Task<ClaimDto> ClaimAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance, UserRole.Finance);
        var c = await db.InsuranceClaims.AsNoTracking().Include(x => x.Patient).Include(x => x.Company).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("claim_not_found", "المطالبة غير موجودة.");
        return ToDto(c);
    }

    public async Task<ClaimDto> SubmitClaimAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance);
        var c = await LoadClaimAsync(id, ct);
        if (c.Status != ClaimStatus.Draft) throw ApiException.Conflict("invalid_state", "قُدّمت المطالبة سابقًا.");
        c.Status = ClaimStatus.Submitted; c.SubmittedAt = UtcNow;
        await db.SaveChangesAsync(ct);
        return await ClaimAsync(id, ct);
    }

        public Task<ClaimDto> SettleClaimAsync(int id, decimal paid, CancellationToken ct) => RowLock.RunAsync(db, "InsuranceClaims", id, () => SettleClaimCoreAsync(id, paid, ct), ct);

    private async Task<ClaimDto> SettleClaimCoreAsync(int id, decimal paid, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance, UserRole.Finance);
        var c = await LoadClaimAsync(id, ct);
        if (c.Status is not (ClaimStatus.Submitted or ClaimStatus.PartiallyPaid)) throw ApiException.Conflict("invalid_state", "تُسوّى المطالبات المقدّمة فقط.");
        if (paid <= 0 || c.PaidAmount + paid > c.Amount) throw ApiException.Validation(nameof(paid), "المبلغ يتجاوز المتبقي من المطالبة");
        c.PaidAmount += paid;
        c.Status = c.PaidAmount >= c.Amount ? ClaimStatus.Paid : ClaimStatus.PartiallyPaid;
        if (c.Status == ClaimStatus.Paid) c.PaidAt = UtcNow;
        audit.Add("insurance.claim_settle", me.Id, nameof(InsuranceClaim), c.Id, $"{paid}", me.Ip);
        await db.SaveChangesAsync(ct);
        return await ClaimAsync(id, ct);
    }

    public async Task<ClaimDto> RejectClaimAsync(int id, string reason, CancellationToken ct)
    {
        scope.Require(UserRole.Insurance);
        var c = await LoadClaimAsync(id, ct);
        if (c.Status != ClaimStatus.Submitted) throw ApiException.Conflict("invalid_state", "يُرفض المقدَّم فقط.");
        c.Status = ClaimStatus.Rejected; c.RejectReason = reason.Trim();
        await db.SaveChangesAsync(ct);
        return await ClaimAsync(id, ct);
    }

    private async Task<InsuranceClaim> LoadClaimAsync(int id, CancellationToken ct) =>
        await db.InsuranceClaims.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("claim_not_found", "المطالبة غير موجودة.");

    private static ClaimDto ToDto(InsuranceClaim c) => new(c.Id, c.No, new PersonRefDto(c.Patient.Id, c.Patient.FullName, c.Patient.AvatarKey), c.Company.Name, c.InvoiceId, c.Amount, c.PaidAmount, c.Status, c.SubmittedAt, c.PaidAt, c.RejectReason);
}
