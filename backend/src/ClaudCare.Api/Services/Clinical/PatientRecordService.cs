using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Clinical;

/// <summary>قراءة الملف الطبي: قائمة المرضى، الملف، الزيارات، التحاليل، الأشعة، الوصفات، والملف الصحي.</summary>
public sealed class PatientRecordService(ClaudCareDbContext db, PatientAccessService access, ICurrentUser me, IClinicClock clock)
{
    public const int KidsUnderAge = 14, SeniorsFromAge = 65;

    // ------------------------------------------------------------ قائمة المرضى

    public async Task<PagedResult<PatientSummaryDto>> SearchAsync(string? q, string? filter, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Clamp(page, pageSize);
        var clinical = access.CanSeeClinicalInGeneral;
        var year = clock.Today.Year;
        var query = db.Users.AsNoTracking().Where(u => u.Role == UserRole.Patient && u.IsActive);

        switch (me.Role)
        {
            case UserRole.Doctor:
                var id = me.Id;
                query = query.Where(u => db.Appointments.Any(a => a.DoctorId == id && a.PatientId == u.Id && a.Status != AppointmentStatus.Cancelled) || db.Visits.Any(v => v.DoctorId == id && v.PatientId == u.Id));
                break;
            case UserRole.Reception or UserRole.Nurse or UserRole.Branch or UserRole.Emergency or UserRole.Admissions or UserRole.Surgery or UserRole.Hospital or UserRole.Admin:
                break;
            default:
                throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية عرض المرضى.");
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = clinical
                ? query.Where(u => u.FullName.Contains(s) || u.Phone!.Contains(s) || u.NationalId!.Contains(s) || db.PatientProfiles.Any(pp => pp.UserId == u.Id && (pp.MedicalRecordNo.Contains(s) || pp.Conditions.Any(c => c.Name.Contains(s)))))
                : query.Where(u => u.FullName.Contains(s) || u.Phone!.Contains(s) || u.NationalId!.Contains(s) || db.PatientProfiles.Any(pp => pp.UserId == u.Id && pp.MedicalRecordNo.Contains(s)));
        }

        switch (filter?.ToLowerInvariant())
        {
            case "chronic" when clinical: query = query.Where(u => db.PatientConditions.Any(c => c.PatientId == u.Id)); break;
            case "allergy" when clinical: query = query.Where(u => db.PatientAllergies.Any(a => a.PatientId == u.Id)); break;
            case "kids": query = query.Where(u => u.BirthYear != null && year - u.BirthYear < KidsUnderAge); break;
            case "seniors": query = query.Where(u => u.BirthYear != null && year - u.BirthYear >= SeniorsFromAge); break;
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(u => u.FullName).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<PatientSummaryDto>(await SummariesAsync(rows, clinical, ct), p, size, total);
    }

    private async Task<List<PatientSummaryDto>> SummariesAsync(List<User> users, bool clinical, CancellationToken ct)
    {
        var ids = users.Select(u => u.Id).ToList();
        var profiles = await db.PatientProfiles.AsNoTracking().Where(x => ids.Contains(x.UserId)).Include(x => x.Conditions).Include(x => x.Allergies).ToDictionaryAsync(x => x.UserId, ct);
        var lastVisits = (await db.Visits.AsNoTracking().Where(v => ids.Contains(v.PatientId)).GroupBy(v => v.PatientId).Select(g => new { g.Key, D = g.Max(v => v.VisitDate) }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.D);
        var year = clock.Today.Year;
        return users.Select(u =>
        {
            profiles.TryGetValue(u.Id, out var pr);
            return new PatientSummaryDto(u.Id, u.FullName, u.BirthYear is { } by ? year - by : null, u.Gender, u.AvatarKey, pr?.MedicalRecordNo ?? "", pr?.BloodType, u.Phone,
                clinical ? pr?.Conditions.OrderBy(c => c.Id).FirstOrDefault()?.Name : null, clinical && (pr?.Allergies.Count ?? 0) > 0, clinical ? lastVisits.GetValueOrDefault(u.Id) is { } d && d != default ? d : null : null);
        }).ToList();
    }

    // ------------------------------------------------------------ الملف

    public async Task<PatientRecordDto> RecordAsync(int patientId, CancellationToken ct)
    {
        var u = await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var profile = await db.PatientProfiles.AsNoTracking().Include(p => p.Allergies).Include(p => p.Conditions).FirstOrDefaultAsync(p => p.UserId == patientId, ct);
        var summary = (await SummariesAsync([u], true, ct))[0];

        var lastVisit = await db.Visits.AsNoTracking().Where(v => v.PatientId == patientId && (v.SystolicBp != null || v.HeartRate != null || v.TemperatureC != null || v.Spo2 != null))
            .OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.Id).FirstOrDefaultAsync(ct);
        var vitals = lastVisit is null ? null : new VitalsDto(lastVisit.SystolicBp, lastVisit.DiastolicBp, lastVisit.HeartRate, lastVisit.TemperatureC, lastVisit.Spo2, lastVisit.VisitDate);

        var today = clock.Today;
        var activeMeds = (await db.PatientMedications.AsNoTracking().Where(m => m.PatientId == patientId && m.EndDate >= today && m.StartDate <= today)
                .Include(m => m.PrescriptionItem).OrderBy(m => m.PrescriptionItem.DrugName).ToListAsync(ct))
            .Select(m => new ActiveMedicationDto(m.PrescriptionItem.DrugName, m.PrescriptionItem.Dose, m.PrescriptionItem.Frequency, m.EndDate)).ToList();

        return new PatientRecordDto(summary, profile?.HeightCm, profile?.WeightKg,
            (profile?.Allergies ?? []).Select(a => new AllergyDto(a.Name, a.Reaction)).ToList(),
            (profile?.Conditions ?? []).OrderBy(c => c.Id).Select(c => new ConditionDto(c.Name, c.SinceYear)).ToList(),
            vitals, activeMeds,
            await db.Visits.CountAsync(v => v.PatientId == patientId, ct),
            await db.LabOrders.CountAsync(l => l.PatientId == patientId && l.Status == LabOrderStatus.ResultReady, ct),
            await db.Prescriptions.CountAsync(r => r.PatientId == patientId, ct));
    }

    public async Task<HealthSummaryDto> HealthSummaryAsync(int patientId, CancellationToken ct)
    {
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var p = await db.PatientProfiles.AsNoTracking().Include(x => x.Allergies).Include(x => x.Conditions).FirstOrDefaultAsync(x => x.UserId == patientId, ct);
        return new HealthSummaryDto(p?.BloodType, p?.HeightCm, p?.WeightKg, (p?.Allergies ?? []).Select(a => new AllergyDto(a.Name, a.Reaction)).ToList(),
            (p?.Conditions ?? []).OrderBy(c => c.Id).Select(c => new ConditionDto(c.Name, c.SinceYear)).ToList());
    }

    public async Task<HealthSummaryDto> UpdateClinicalProfileAsync(int patientId, UpdateClinicalProfileRequest req, CancellationToken ct)
    {
        if (!PatientAccessService.CanWriteClinical(me.Role)) throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية تعديل الملف الطبي.");
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var p = await db.PatientProfiles.Include(x => x.Allergies).Include(x => x.Conditions).FirstOrDefaultAsync(x => x.UserId == patientId, ct)
                ?? throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");

        if (req.BloodType is { } bt)
        {
            if (!new[] { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" }.Contains(bt)) throw ApiException.Validation(nameof(req.BloodType), "فصيلة الدم غير صحيحة");
            p.BloodType = bt;
        }
        if (req.HeightCm is not null) p.HeightCm = req.HeightCm;
        if (req.WeightKg is not null) p.WeightKg = req.WeightKg;
        if (req.Allergies is { } al)
        {
            p.Allergies.Clear();
            foreach (var a in al.DistinctBy(x => x.Name.Trim())) p.Allergies.Add(new PatientAllergy { Name = a.Name.Trim(), Reaction = a.Reaction?.Trim() });
        }
        if (req.Conditions is { } cs)
        {
            p.Conditions.Clear();
            foreach (var c in cs.DistinctBy(x => x.Name.Trim())) p.Conditions.Add(new PatientCondition { Name = c.Name.Trim(), SinceYear = c.SinceYear });
        }
        await db.SaveChangesAsync(ct);
        return await HealthSummaryAsync(patientId, ct);
    }

    // ------------------------------------------------------------ القوائم

    public async Task<PagedResult<VisitDto>> VisitsAsync(int patientId, int? page, int? pageSize, CancellationToken ct)
    {
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = db.Visits.AsNoTracking().Where(v => v.PatientId == patientId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);

        var docIds = rows.Select(v => v.DoctorId).Distinct().ToList();
        var docs = await db.DoctorProfiles.AsNoTracking().Include(d => d.User).Include(d => d.Specialty).Where(d => docIds.Contains(d.UserId)).ToDictionaryAsync(d => d.UserId, ct);
        var visitIds = rows.Select(v => v.Id).ToList();
        var rx = await db.Prescriptions.AsNoTracking().Include(r => r.Items).Where(r => r.VisitId != null && visitIds.Contains(r.VisitId.Value)).ToListAsync(ct);

        var items = rows.Select(v =>
        {
            var d = docs.GetValueOrDefault(v.DoctorId);
            var pres = rx.FirstOrDefault(r => r.VisitId == v.Id);
            return new VisitDto(v.Id, v.VisitDate, d?.User.FullName ?? "", d?.Specialty.NameAr, v.Complaint, v.Diagnosis, v.Notes, v.FollowUp,
                new VitalsDto(v.SystolicBp, v.DiastolicBp, v.HeartRate, v.TemperatureC, v.Spo2, v.VisitDate), pres?.No,
                pres?.Items.Select(i => $"{i.DrugName} – {i.Frequency}").ToList() ?? []);
        }).ToList();
        return new PagedResult<VisitDto>(items, p, size, total);
    }

    public async Task<PagedResult<PrescriptionDto>> PrescriptionsAsync(int patientId, int? page, int? pageSize, CancellationToken ct)
    {
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = db.Prescriptions.AsNoTracking().Where(r => r.PatientId == patientId);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(r => r.Items).Include(r => r.Doctor).OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<PrescriptionDto>(rows.Select(r => new PrescriptionDto(r.Id, r.No, r.CreatedAt, r.Doctor.FullName, r.Status,
            r.Items.OrderBy(i => i.Id).Select(i => new PrescriptionItemDto(i.Id, i.DrugName, i.Dose, i.Frequency, i.TimesPerDay, i.DurationDays, i.Quantity, i.Instructions, i.Dispensed)).ToList())).ToList(), p, size, total);
    }

    public async Task<PagedResult<LabOrderDto>> LabOrdersAsync(int patientId, int? page, int? pageSize, CancellationToken ct)
    {
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = db.LabOrders.AsNoTracking().Where(l => l.PatientId == patientId && l.Status != LabOrderStatus.Cancelled);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(l => l.Items).Include(l => l.Doctor).OrderByDescending(l => l.OrderedAt).ThenByDescending(l => l.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<LabOrderDto>(rows.Select(l => ToDto(l, null)).ToList(), p, size, total);
    }

    public async Task<LabOrderDto> LabOrderAsync(int patientId, int orderId, CancellationToken ct)
    {
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var l = await db.LabOrders.AsNoTracking().Include(x => x.Items).Include(x => x.Doctor).FirstOrDefaultAsync(x => x.Id == orderId && x.PatientId == patientId, ct)
                ?? throw ApiException.NotFound("order_not_found", "الطلب غير موجود.");
        return ToDto(l, null);
    }

    public async Task<PagedResult<ImagingOrderDto>> ImagingOrdersAsync(int patientId, int? page, int? pageSize, CancellationToken ct)
    {
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = db.ImagingOrders.AsNoTracking().Where(i => i.PatientId == patientId && i.Status != ImagingOrderStatus.Cancelled);
        var total = await q.CountAsync(ct);
        var rows = await q.Include(i => i.Report).Include(i => i.Doctor).OrderByDescending(i => i.OrderedAt).ThenByDescending(i => i.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<ImagingOrderDto>(rows.Select(i => ToDto(i, null)).ToList(), p, size, total);
    }

    public async Task<ImagingOrderDto> ImagingOrderAsync(int patientId, int orderId, CancellationToken ct)
    {
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        var i = await db.ImagingOrders.AsNoTracking().Include(x => x.Report).Include(x => x.Doctor).FirstOrDefaultAsync(x => x.Id == orderId && x.PatientId == patientId, ct)
                ?? throw ApiException.NotFound("order_not_found", "الطلب غير موجود.");
        return ToDto(i, null);
    }

    // ------------------------------------------------------------ تحويل

    public static LabOrderDto ToDto(LabOrder l, PersonRefDto? patient)
    {
        var ready = l.Status == LabOrderStatus.ResultReady;
        var items = ready ? l.Items.OrderBy(i => i.Id).Select(i => new LabResultItemDto(i.Name, i.Value, i.Unit, i.RefMin, i.RefMax, i.Flag, i.Remark)).ToList() : [];
        return new LabOrderDto(l.Id, l.No, l.TestName, l.Category, l.Priority, l.Status, l.OrderedAt, l.ResultAt, l.Doctor?.FullName, patient, ready, items.Count(i => i.Flag != ResultFlag.Normal), items);
    }

    public static ImagingOrderDto ToDto(ImagingOrder i, PersonRefDto? patient, bool includeDrafts = false)
    {
        // تقرير غير نهائي لا يراه المريض ولا الطبيب الطالب كنتيجة؛ يراه طاقم الأشعة والإدارة فقط
        var report = i.Report is { } r && (includeDrafts || r.Status == ReportStatus.Final) ? new ImagingReportDto(r.Findings, r.Impression, r.Status, r.ReportedAt, r.ImageKey) : null;
        return new ImagingOrderDto(i.Id, i.No, i.StudyName, i.Modality, i.Priority, i.Status, i.OrderedAt, i.Doctor?.FullName, patient, report);
    }
}
