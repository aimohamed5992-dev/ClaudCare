using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Operations;

/// <summary>
/// الطوارئ: الفرز (1 حرج – 5 غير عاجل) مرتبًا بالأخطر ثم الأقدم. إن دلّت العلامات الحيوية على خطر يرفع الخادم المستوى إلى 2 على الأكثر
/// (لا يخفضه أبدًا). الإدخال للتنويم ينشئ طلب تنويم طارئ.
/// </summary>
public sealed class EmergencyService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, IAuditService audit, TimeProvider time)
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
    private static readonly UserRole[] Staff = [UserRole.Emergency, UserRole.Nurse];

    public static bool LooksCritical(int? sbp, int? hr, int? spo2, int? rr, string? redFlags) =>
        spo2 is < 90 || sbp is < 90 || hr is > 150 or < 40 || rr is > 35 or < 6 || !string.IsNullOrWhiteSpace(redFlags);

    public async Task<EmergencyCaseDto> CreateAsync(CreateEmergencyCaseRequest r, CancellationToken ct)
    {
        scope.Require(Staff);
        if (r.PatientId is { } pid && !await db.Users.AnyAsync(u => u.Id == pid && u.Role == UserRole.Patient, ct)) throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");
        var level = r.TriageLevel!.Value;
        var escalated = level > 2 && LooksCritical(r.SystolicBp, r.HeartRate, r.Spo2, r.RespiratoryRate, r.RedFlags);
        if (escalated) level = 2;
        var c = new EmergencyCase { BranchId = scope.BranchForNew(), PatientId = r.PatientId, PatientName = r.PatientName.Trim(), Age = r.Age, Complaint = r.Complaint.Trim(), TriageLevel = level, Zone = r.Zone.Trim(),
            ArrivedAt = UtcNow, Consciousness = r.Consciousness?.Trim(), SystolicBp = r.SystolicBp, HeartRate = r.HeartRate, Spo2 = r.Spo2, RespiratoryRate = r.RespiratoryRate, TemperatureC = r.TemperatureC,
            PainScore = r.PainScore, RedFlags = r.RedFlags?.Trim(), TriagedByUserId = me.Id };
        db.EmergencyCases.Add(c);
        audit.Add("emergency.triage", me.Id, nameof(EmergencyCase), details: $"level={level}", ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(c, escalated);
    }

    public async Task<EmergencyBoardDto> BoardAsync(bool activeOnly, EmergencyStatus? status, CancellationToken ct)
    {
        scope.Require(Staff.Append(UserRole.Branch).Append(UserRole.Doctor).ToArray());
        var b = scope.BranchFilter;
        var q = db.EmergencyCases.AsNoTracking().Where(c => b == null || c.BranchId == b);
        if (status is { } s) q = q.Where(c => c.Status == s);
        else if (activeOnly) q = q.Where(c => c.Status == EmergencyStatus.Waiting || c.Status == EmergencyStatus.InExam);
        var rows = await q.OrderBy(c => c.TriageLevel).ThenBy(c => c.ArrivedAt).Take(200).ToListAsync(ct);
        return new EmergencyBoardDto(rows.Count(c => c.Status == EmergencyStatus.Waiting), rows.Count(c => c.Status == EmergencyStatus.InExam),
            rows.Where(c => c.Status is EmergencyStatus.Waiting or EmergencyStatus.InExam).GroupBy(c => c.TriageLevel).ToDictionary(g => g.Key, g => g.Count()), rows.Select(c => ToDto(c)).ToList());
    }

    public async Task<EmergencyCaseDto> RetriageAsync(int id, int level, string? redFlags, CancellationToken ct)
    {
        scope.Require(Staff);
        var c = await FindAsync(id, ct);
        if (c.Status is EmergencyStatus.Discharged or EmergencyStatus.Admitted) throw ApiException.Conflict("invalid_state", "انتهت الحالة.");
        c.TriageLevel = level;
        if (redFlags is not null) c.RedFlags = redFlags.Trim();
        c.TriagedByUserId = me.Id;
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<EmergencyCaseDto> StartExamAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Emergency);
        var c = await FindAsync(id, ct);
        if (c.Status != EmergencyStatus.Waiting) throw ApiException.Conflict("invalid_state", $"لا يمكن بدء الفحص والحالة «{c.Status}».");
        c.Status = EmergencyStatus.InExam;
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    public async Task<EmergencyCaseDto> DischargeAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Emergency);
        var c = await FindAsync(id, ct);
        if (c.Status is not (EmergencyStatus.Waiting or EmergencyStatus.InExam)) throw ApiException.Conflict("invalid_state", $"لا يمكن الإخراج والحالة «{c.Status}».");
        c.Status = EmergencyStatus.Discharged;
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    /// <summary>إدخال للتنويم: ينشئ طلب تنويم طارئ (يشترط مريضًا مسجلًا) ويربطه بالحالة.</summary>
    public async Task<EmergencyCaseDto> AdmitAsync(int id, string ward, string? reason, CancellationToken ct)
    {
        scope.Require(UserRole.Emergency);
        var c = await FindAsync(id, ct);
        if (c.Status != EmergencyStatus.InExam) throw ApiException.Conflict("invalid_state", "ابدأ الفحص قبل الإدخال.");
        if (c.PatientId is not { } pid) throw ApiException.Conflict("patient_required", "اربط الحالة بمريض مسجّل قبل الإدخال.");
        if (await db.Admissions.AnyAsync(a => a.PatientId == pid && a.Status != AdmissionStatus.Discharged, ct)) throw ApiException.Conflict("already_admitted", "للمريض طلب تنويم أو إقامة قائمة.");
        var a = new Admission { No = "TMP" + Guid.NewGuid().ToString("N")[..16], PatientId = pid, BranchId = c.BranchId, Type = AdmissionType.Emergency, Reason = (reason ?? c.Complaint).Trim(), RequestedWard = ward.Trim(), RequestedFrom = "الطوارئ", RequestedAt = UtcNow };
        db.Admissions.Add(a);
        await db.SaveChangesAsync(ct);
        a.No = $"ADM-{a.Id:D5}";
        c.AdmissionId = a.Id;
        c.Status = EmergencyStatus.Admitted;
        audit.Add("emergency.admit", me.Id, nameof(EmergencyCase), c.Id, a.No, me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    // ------------------------------------------------------------ الإسعاف

    public async Task<AmbulanceDto> CreateAmbulanceAsync(CreateAmbulanceRequest r, CancellationToken ct)
    {
        scope.Require(UserRole.Emergency);
        var a = new Ambulance { BranchId = scope.BranchForNew(), UnitNo = r.UnitNo.Trim(), PatientDescription = r.PatientDescription.Trim(), Condition = r.Condition.Trim(), TriageLevel = r.TriageLevel!.Value, EtaMinutes = r.EtaMinutes };
        db.Ambulances.Add(a);
        await db.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task<IReadOnlyList<AmbulanceDto>> AmbulancesAsync(CancellationToken ct)
    {
        scope.Require(Staff.Append(UserRole.Branch).ToArray());
        var b = scope.BranchFilter;
        return (await db.Ambulances.AsNoTracking().Where(a => (b == null || a.BranchId == b) && a.Status == AmbulanceStatus.EnRoute).OrderBy(a => a.EtaMinutes).ThenBy(a => a.Id).ToListAsync(ct)).Select(ToDto).ToList();
    }

    /// <summary>وصول الإسعاف: يُغلق الطلب وتُنشأ حالة طوارئ بانتظار الفحص بمستوى فرز الإسعاف.</summary>
    public async Task<EmergencyCaseDto> ArriveAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Emergency);
        var a = await db.Ambulances.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null || !scope.InBranch(a.BranchId)) throw ApiException.NotFound("ambulance_not_found", "الإسعاف غير موجود.");
        if (a.Status == AmbulanceStatus.Arrived) throw ApiException.Conflict("invalid_state", "وصل الإسعاف سابقًا.");
        a.Status = AmbulanceStatus.Arrived;
        var c = new EmergencyCase { BranchId = a.BranchId, PatientName = a.PatientDescription, Complaint = a.Condition, TriageLevel = a.TriageLevel, Zone = "الإسعاف", ArrivedAt = UtcNow, TriagedByUserId = me.Id };
        db.EmergencyCases.Add(c);
        await db.SaveChangesAsync(ct);
        return ToDto(c);
    }

    private async Task<EmergencyCase> FindAsync(int id, CancellationToken ct)
    {
        var c = await db.EmergencyCases.FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is not null && scope.InBranch(c.BranchId) ? c : throw ApiException.NotFound("case_not_found", "الحالة غير موجودة.");
    }

    private EmergencyCaseDto ToDto(EmergencyCase c, bool escalated = false) => new(c.Id, c.PatientName, c.PatientId, c.Age, c.Complaint, c.TriageLevel, escalated, c.Zone, c.Status, c.ArrivedAt,
        (int)Math.Max(0, (UtcNow - c.ArrivedAt).TotalMinutes), c.Consciousness, c.SystolicBp, c.HeartRate, c.Spo2, c.RespiratoryRate, c.TemperatureC, c.PainScore, c.RedFlags, c.AdmissionId);

    private static AmbulanceDto ToDto(Ambulance a) => new(a.Id, a.UnitNo, a.PatientDescription, a.Condition, a.TriageLevel, a.EtaMinutes, a.Status);
}
