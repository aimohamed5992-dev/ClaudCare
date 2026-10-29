using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Clinical;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Operations;

/// <summary>العلامات الحيوية وملاحظات التمريض وسجل إعطاء الأدوية (MAR). الاطلاع حسب صلاحية الملف السريري، والكتابة للتمريض والطوارئ والطبيب.</summary>
public sealed class NursingService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, PatientAccessService access, IAuditService audit, TimeProvider time)
{
    private static readonly UserRole[] Writers = [UserRole.Nurse, UserRole.Emergency, UserRole.Doctor];
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<NurseVitalsDto> RecordVitalsAsync(RecordVitalsRequest r, CancellationToken ct)
    {
        scope.Require(Writers);
        await access.RequireAsync(r.PatientId!.Value, AccessLevel.Clinical, ct);
        if (r.SystolicBp is null && r.DiastolicBp is null && r.HeartRate is null && r.TemperatureC is null && r.Spo2 is null && r.RespiratoryRate is null)
            throw ApiException.Validation(nameof(r.SystolicBp), "سجّل قياسًا واحدًا على الأقل");
        if (r.SystolicBp is { } s && r.DiastolicBp is { } d && d >= s) throw ApiException.Validation(nameof(r.DiastolicBp), "الانبساطي يجب أن يقل عن الانقباضي");
        if (r.AdmissionId is { } a && !await db.Admissions.AnyAsync(x => x.Id == a && x.PatientId == r.PatientId, ct)) throw ApiException.NotFound("admission_not_found", "التنويم غير موجود.");

        var v = new VitalSignRecord { PatientId = r.PatientId!.Value, AdmissionId = r.AdmissionId, RecordedByUserId = me.Id, RecordedAt = UtcNow, SystolicBp = r.SystolicBp, DiastolicBp = r.DiastolicBp,
            HeartRate = r.HeartRate, TemperatureC = r.TemperatureC, Spo2 = r.Spo2, RespiratoryRate = r.RespiratoryRate, Notes = r.Notes?.Trim() };
        db.VitalSignRecords.Add(v);
        await db.SaveChangesAsync(ct);
        return ToDto(v);
    }

    public async Task<IReadOnlyList<NurseVitalsDto>> VitalsAsync(int patientId, int? take, CancellationToken ct)
    {
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var rows = await db.VitalSignRecords.AsNoTracking().Where(v => v.PatientId == patientId).OrderByDescending(v => v.RecordedAt).ThenByDescending(v => v.Id).Take(Math.Clamp(take ?? 30, 1, 200)).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    /// <summary>تنبيهات القيم الحرجة (للعرض فقط؛ التفسير السريري للطاقم).</summary>
    public static IReadOnlyList<string> AlertsFor(int? sbp, int? hr, decimal? temp, int? spo2, int? rr)
    {
        var a = new List<string>();
        if (spo2 is < 92) a.Add("انخفاض تشبع الأكسجين");
        if (hr is > 130 or < 40) a.Add("معدل نبض غير طبيعي");
        if (sbp is > 180 or < 90) a.Add("ضغط الدم خارج المدى الآمن");
        if (temp is >= 39.5m or < 35m) a.Add("درجة حرارة غير طبيعية");
        if (rr is > 30 or < 8) a.Add("معدل تنفس غير طبيعي");
        return a;
    }

    private static NurseVitalsDto ToDto(VitalSignRecord v) => new(v.Id, v.RecordedAt, v.SystolicBp, v.DiastolicBp, v.HeartRate, v.TemperatureC, v.Spo2, v.RespiratoryRate, v.Notes, AlertsFor(v.SystolicBp, v.HeartRate, v.TemperatureC, v.Spo2, v.RespiratoryRate));

    // ------------------------------------------------------------ الملاحظات

    public async Task<NursingNoteDto> AddNoteAsync(int patientId, string note, int? admissionId, CancellationToken ct)
    {
        scope.Require(UserRole.Nurse, UserRole.Emergency);
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        if (admissionId is { } a && !await db.Admissions.AnyAsync(x => x.Id == a && x.PatientId == patientId, ct)) throw ApiException.NotFound("admission_not_found", "التنويم غير موجود.");
        var n = new NursingNote { PatientId = patientId, AdmissionId = admissionId, NurseId = me.Id, Note = note.Trim(), CreatedAt = UtcNow };
        db.NursingNotes.Add(n);
        await db.SaveChangesAsync(ct);
        return new NursingNoteDto(n.Id, n.CreatedAt, (await db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id, ct)).FullName, n.Note, n.AdmissionId);
    }

    public async Task<IReadOnlyList<NursingNoteDto>> NotesAsync(int patientId, CancellationToken ct)
    {
        if (me.Role == UserRole.Patient) throw ApiException.Forbidden("action_not_allowed", "ملاحظات التمريض للطاقم فقط.");
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        return await db.NursingNotes.AsNoTracking().Where(n => n.PatientId == patientId).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(100)
            .Select(n => new NursingNoteDto(n.Id, n.CreatedAt, n.Nurse.FullName, n.Note, n.AdmissionId)).ToListAsync(ct);
    }

    // ------------------------------------------------------------ سجل إعطاء الأدوية

    public async Task<AdminDoseDto> ScheduleDoseAsync(int admissionId, ScheduleDoseRequest r, CancellationToken ct)
    {
        scope.Require(UserRole.Nurse, UserRole.Doctor);
        var adm = await AdmissionAsync(admissionId, ct);
        if (adm.Status != AdmissionStatus.Admitted) throw ApiException.Conflict("invalid_state", "المريض غير منوَّم حاليًا.");
        var m = new MedicationAdministration { AdmissionId = admissionId, DrugName = r.DrugName.Trim(), Dose = r.Dose?.Trim(), DueAt = r.DueAt!.Value, PrescriptionItemId = r.PrescriptionItemId };
        db.MedicationAdministrations.Add(m);
        await db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    public async Task<IReadOnlyList<AdminDoseDto>> DosesAsync(int admissionId, DateOnly? date, CancellationToken ct)
    {
        if (me.Role == UserRole.Patient) throw ApiException.Forbidden("action_not_allowed", "السجل للطاقم فقط.");
        await AdmissionAsync(admissionId, ct);
        var q = db.MedicationAdministrations.AsNoTracking().Where(m => m.AdmissionId == admissionId);
        if (date is { } d) { var from = d.ToDateTime(TimeOnly.MinValue); q = q.Where(m => m.DueAt >= from && m.DueAt < from.AddDays(1)); }
        return (await q.OrderBy(m => m.DueAt).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<AdminDoseDto> ActAsync(int doseId, DoseStatus target, string? notes, CancellationToken ct)
    {
        scope.Require(UserRole.Nurse);
        var m = await db.MedicationAdministrations.FirstOrDefaultAsync(x => x.Id == doseId, ct) ?? throw ApiException.NotFound("dose_not_found", "الجرعة غير موجودة.");
        await AdmissionAsync(m.AdmissionId, ct); // نطاق الفرع
        if (m.Status is DoseStatus.Given or DoseStatus.Skipped) throw ApiException.Conflict("invalid_state", "سُجّلت هذه الجرعة سابقًا.");
        if (target is DoseStatus.Skipped or DoseStatus.Delayed && string.IsNullOrWhiteSpace(notes)) throw ApiException.Validation(nameof(notes), "اذكر السبب");
        m.Status = target;
        m.Notes = notes?.Trim() ?? m.Notes;
        if (target == DoseStatus.Given) { m.GivenAt = UtcNow; m.GivenByUserId = me.Id; }
        audit.Add($"mar.{target.ToString().ToLowerInvariant()}", me.Id, nameof(MedicationAdministration), m.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    private async Task<Admission> AdmissionAsync(int id, CancellationToken ct)
    {
        var a = await db.Admissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is not null && scope.InBranch(a.BranchId) ? a : throw ApiException.NotFound("admission_not_found", "التنويم غير موجود.");
    }

    private AdminDoseDto ToDto(MedicationAdministration m) => new(m.Id, m.AdmissionId, m.DrugName, m.Dose, m.DueAt, m.GivenAt, m.Status, m.Notes, m.Status is DoseStatus.Due or DoseStatus.Delayed && m.DueAt < UtcNow.AddMinutes(-30));
}
