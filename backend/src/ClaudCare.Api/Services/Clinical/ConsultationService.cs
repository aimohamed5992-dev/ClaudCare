using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Clinical;

/// <summary>
/// حفظ الكشف كاملًا في معاملة واحدة: الزيارة والعلامات والتشخيص، طلبات التحاليل والأشعة، الوصفة (مع فحص الحساسية)،
/// أدوية المريض وجداول جرعاتها، وإكمال الموعد.
/// </summary>
public sealed class ConsultationService(
    ClaudCareDbContext db, PatientAccessService access, ICurrentUser me, IClinicClock clock, INotificationService notifications, IAuditService audit, TimeProvider time)
{
    public async Task<VisitResultDto> CreateAsync(CreateVisitRequest req, CancellationToken ct)
    {
        var (appt, patientId) = await ResolveContextAsync(req, ct);
        ValidateVitals(req.Vitals);

        var labs = (req.LabTests ?? []).Select(k => ClinicalCatalog.Lab(k) ?? throw ApiException.Validation(nameof(req.LabTests), $"فحص غير معروف: {k}")).DistinctBy(l => l.Key).ToList();
        var imaging = (req.ImagingStudies ?? []).Select(k => ClinicalCatalog.Imaging(k) ?? throw ApiException.Validation(nameof(req.ImagingStudies), $"دراسة غير معروفة: {k}")).DistinctBy(i => i.Key).ToList();

        var warnings = new List<string>();
        var lines = await PrepareDrugsAsync(patientId, req.Prescription ?? [], warnings, ct);

        var branchId = appt?.BranchId ?? (await db.DoctorProfiles.AsNoTracking().FirstAsync(d => d.UserId == me.Id, ct)).BranchId;
        var today = clock.Today;
        var now = time.GetUtcNow().UtcDateTime;

        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var visit = new Visit
            {
                AppointmentId = appt?.Id, PatientId = patientId, DoctorId = me.Id, BranchId = branchId, VisitDate = today,
                Complaint = req.Complaint.Trim(), SystolicBp = req.Vitals?.SystolicBp, DiastolicBp = req.Vitals?.DiastolicBp, HeartRate = req.Vitals?.HeartRate,
                TemperatureC = req.Vitals?.TemperatureC, Spo2 = req.Vitals?.Spo2, Diagnosis = req.Diagnosis.Trim(), Notes = req.Notes?.Trim(), FollowUp = req.FollowUp?.Trim(),
            };
            db.Visits.Add(visit);

            var labOrders = labs.Select(l => new LabOrder { No = "TMP-" + Guid.NewGuid().ToString("N")[..12], PatientId = patientId, DoctorId = me.Id, BranchId = branchId, TestName = l.Name, Category = l.Category, OrderedAt = now }).ToList();
            var imgOrders = imaging.Select(i => new ImagingOrder { No = "TMP-" + Guid.NewGuid().ToString("N")[..12], PatientId = patientId, DoctorId = me.Id, BranchId = branchId, StudyName = i.Name, Modality = i.Modality, OrderedAt = now }).ToList();
            db.LabOrders.AddRange(labOrders);
            db.ImagingOrders.AddRange(imgOrders);

            Prescription? rx = null;
            if (lines.Count > 0)
            {
                rx = new Prescription { No = "TMP-" + Guid.NewGuid().ToString("N")[..12], PatientId = patientId, DoctorId = me.Id, BranchId = branchId, Visit = visit, Status = PrescriptionStatus.Pending };
                foreach (var l in lines) rx.Items.Add(l.Item);
                db.Prescriptions.Add(rx);
            }
            await db.SaveChangesAsync(ct); // للحصول على المعرّفات

            foreach (var l in labOrders) { l.No = $"LAB-{2000 + l.Id}"; l.VisitId = visit.Id; }
            foreach (var i in imgOrders) i.No = $"IMG-{1000 + i.Id}";
            if (rx is not null)
            {
                rx.No = $"RX-{3000 + rx.Id}";
                foreach (var l in lines)
                    db.PatientMedications.Add(new PatientMedication
                    {
                        PatientId = patientId, PrescriptionItemId = l.Item.Id, TimesCsv = MedicationSchedule.ToCsv(MedicationSchedule.Times(l.Item.TimesPerDay)),
                        StartDate = today, EndDate = today.AddDays(l.Item.DurationDays - 1),
                    });
            }

            if (appt is not null)
            {
                appt.Status = AppointmentStatus.Done;
                appt.CompletedAt = now;
            }

            notifications.Add(patientId, NotificationType.Appointment, "تم حفظ كشفك", rx is null ? req.Diagnosis.Trim() : $"{req.Diagnosis.Trim()} · الوصفة {rx.No}", nameof(Visit), visit.Id);
            foreach (var l in labOrders) notifications.Add(patientId, NotificationType.Result, "طُلب لك فحص", l.TestName, nameof(LabOrder), l.Id);
            foreach (var i in imgOrders) notifications.Add(patientId, NotificationType.Result, "طُلبت لك دراسة أشعة", i.StudyName, nameof(ImagingOrder), i.Id);
            audit.Add("visit.create", me.Id, nameof(Visit), visit.Id, $"patient={patientId} rx={rx?.No} labs={labOrders.Count} imaging={imgOrders.Count}", me.Ip);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new VisitResultDto(visit.Id, appt?.Id, rx?.No,
                labOrders.Select(l => new OrderRefDto(l.Id, l.No, l.TestName)).ToList(), imgOrders.Select(i => new OrderRefDto(i.Id, i.No, i.StudyName)).ToList(), warnings);
        });
        return result;
    }

    private async Task<(Appointment? appt, int patientId)> ResolveContextAsync(CreateVisitRequest req, CancellationToken ct)
    {
        if (req.AppointmentId is { } aid)
        {
            var appt = await db.Appointments.FirstOrDefaultAsync(a => a.Id == aid && a.DoctorId == me.Id, ct) ?? throw ApiException.NotFound("appointment_not_found", "الموعد غير موجود.");
            if (req.PatientId is { } p && p != appt.PatientId) throw ApiException.Validation(nameof(req.PatientId), "المريض لا يطابق الموعد");
            if (appt.Status != AppointmentStatus.InProgress) throw ApiException.Conflict("invalid_state", "ابدأ الكشف أولًا قبل حفظه.");
            if (await db.Visits.AnyAsync(v => v.AppointmentId == aid, ct)) throw ApiException.Conflict("visit_exists", "سُجّل كشف لهذا الموعد سابقًا.");
            return (appt, appt.PatientId);
        }
        var patientId = req.PatientId ?? throw ApiException.Validation(nameof(req.PatientId), "حدّد المريض أو الموعد");
        await access.RequireAsync(patientId, AccessLevel.Clinical, ct);
        return (null, patientId);
    }

    private static void ValidateVitals(VitalsInput? v)
    {
        if (v is null) return;
        if ((v.SystolicBp is null) != (v.DiastolicBp is null)) throw ApiException.Validation(nameof(v.DiastolicBp), "أدخل الضغط الانقباضي والانبساطي معًا");
        if (v.SystolicBp is { } s && v.DiastolicBp is { } d && s <= d) throw ApiException.Validation(nameof(v.SystolicBp), "الانقباضي يجب أن يكون أعلى من الانبساطي");
    }

    private sealed record Line(PrescriptionItem Item);

    private async Task<List<Line>> PrepareDrugsAsync(int patientId, List<PrescriptionItemInput> items, List<string> warnings, CancellationToken ct)
    {
        if (items.Count == 0) return [];

        var allergies = (await db.PatientAllergies.AsNoTracking().Where(a => a.PatientId == patientId).Select(a => a.Name).ToListAsync(ct)).Select(Norm).ToHashSet();
        var today = clock.Today;
        var activeNames = (await db.PatientMedications.AsNoTracking().Where(m => m.PatientId == patientId && m.EndDate >= today).Select(m => m.PrescriptionItem.DrugName).ToListAsync(ct)).Select(Norm).ToHashSet();
        var ids = items.Where(i => i.DrugId != null).Select(i => i.DrugId!.Value).Distinct().ToList();
        var drugs = await db.Drugs.AsNoTracking().Where(d => ids.Contains(d.Id) && d.IsActive).ToDictionaryAsync(d => d.Id, ct);

        var conflicts = new List<object>();
        var lines = new List<Line>();
        var seen = new HashSet<string>();
        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            Drug? drug = null;
            if (it.DrugId is { } did && !drugs.TryGetValue(did, out drug)) throw ApiException.Validation($"Prescription[{i}]", "دواء غير موجود");
            var name = (drug?.Name ?? it.DrugName)?.Trim();
            if (string.IsNullOrEmpty(name)) throw ApiException.Validation($"Prescription[{i}]", "اسم الدواء مطلوب");
            if (!seen.Add(Norm(name))) throw ApiException.Validation($"Prescription[{i}]", $"الدواء مكرر في الوصفة: {name}");

            if (drug is not null && allergies.Contains(Norm(drug.Group)))
            {
                if (!it.AllergyOverride) conflicts.Add(new { index = i, drug = name, allergy = drug.Group });
                else warnings.Add($"وُصف {name} رغم حساسية المريض من {drug.Group} بإقرار الطبيب.");
            }
            else if (drug is null) warnings.Add($"{name}: دواء خارج الدليل فلم يُفحص تعارضه مع الحساسية.");

            if (activeNames.Contains(Norm(name))) warnings.Add($"{name} موجود ضمن أدوية المريض الحالية.");

            var qty = it.TimesPerDay == 0 ? 1 : it.TimesPerDay * it.DurationDays!.Value;
            lines.Add(new Line(new PrescriptionItem
            {
                DrugId = drug?.Id, DrugName = name, Dose = it.Dose?.Trim(), Frequency = it.Frequency.Trim(), TimesPerDay = it.TimesPerDay, DurationDays = it.DurationDays!.Value,
                Quantity = qty, Instructions = it.Instructions?.Trim(), AllergyOverride = it.AllergyOverride, UnitPrice = drug?.UnitPrice ?? 0,
            }));
        }

        if (conflicts.Count > 0)
            throw new ApiException(StatusCodes.Status409Conflict, "allergy_conflict", "يوجد دواء يتعارض مع حساسية المريض. راجعه أو أقرّ بتحمّل المسؤولية.", new Dictionary<string, object?> { ["conflicts"] = conflicts });
        return lines;
    }

    private static string Norm(string s) => s.Trim().ToLowerInvariant();

    /// <summary>فحص دفعة أدوية قبل الكتابة (لشاشة الوصفة): تعارض الحساسية والتكرار مع الأدوية الحالية.</summary>
    public async Task<IReadOnlyList<DrugCheckItemDto>> CheckAsync(DrugCheckRequest req, CancellationToken ct)
    {
        await access.RequireAsync(req.PatientId!.Value, AccessLevel.Clinical, ct);
        var allergies = (await db.PatientAllergies.AsNoTracking().Where(a => a.PatientId == req.PatientId).Select(a => a.Name).ToListAsync(ct)).Select(Norm).ToHashSet();
        var today = clock.Today;
        var active = (await db.PatientMedications.AsNoTracking().Where(m => m.PatientId == req.PatientId && m.EndDate >= today).Select(m => m.PrescriptionItem.DrugName).ToListAsync(ct)).Select(Norm).ToHashSet();
        var drugs = await db.Drugs.AsNoTracking().Where(d => req.DrugIds.Contains(d.Id)).ToListAsync(ct);
        return req.DrugIds.Distinct().Where(id => drugs.Any(d => d.Id == id)).Select(id =>
        {
            var d = drugs.First(x => x.Id == id);
            return new DrugCheckItemDto(d.Id, d.Name, allergies.Contains(Norm(d.Group)) ? d.Group : null, active.Contains(Norm(d.Name)));
        }).ToList();
    }
}
