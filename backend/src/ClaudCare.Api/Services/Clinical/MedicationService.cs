using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Clinical;

/// <summary>أدوية المريض: القائمة، جرعات اليوم والالتزام، التذكير، وطلب/اعتماد إعادة الصرف.</summary>
public sealed class MedicationService(ClaudCareDbContext db, ICurrentUser me, IClinicClock clock, INotificationService notifications, IAuditService audit, TimeProvider time)
{
    public const int EndingSoonDays = 5;

    private IQueryable<PatientMedication> Mine() =>
        db.PatientMedications.Where(m => m.PatientId == me.Id).Include(m => m.PrescriptionItem).ThenInclude(i => i.Prescription).ThenInclude(r => r.Doctor);

    public async Task<IReadOnlyList<MedicationDto>> ListAsync(bool? active, CancellationToken ct)
    {
        var today = clock.Today;
        var q = Mine().AsNoTracking();
        if (active == true) q = q.Where(m => m.EndDate >= today);
        if (active == false) q = q.Where(m => m.EndDate < today);
        return (await q.OrderByDescending(m => m.EndDate).ThenBy(m => m.Id).ToListAsync(ct)).Select(m => ToDto(m, today)).ToList();
    }

    public async Task<TodayDosesDto> TodayAsync(CancellationToken ct) => await DosesForAsync(clock.Today, ct);

    private async Task<TodayDosesDto> DosesForAsync(DateOnly date, CancellationToken ct)
    {
        var meds = await Mine().AsNoTracking().Where(m => m.StartDate <= date && m.EndDate >= date).ToListAsync(ct);
        var ids = meds.Select(m => m.Id).ToList();
        var taken = (await db.DoseLogs.AsNoTracking().Where(l => ids.Contains(l.PatientMedicationId) && l.Date == date).Select(l => new { l.PatientMedicationId, l.SlotIndex }).ToListAsync(ct))
            .Select(l => (l.PatientMedicationId, l.SlotIndex)).ToHashSet();

        var doses = new List<DoseDto>();
        foreach (var m in meds)
        {
            var times = MedicationSchedule.FromCsv(m.TimesCsv);
            for (var i = 0; i < times.Length; i++)
            {
                var state = taken.Contains((m.Id, i)) ? Contracts.DoseState.Taken : (date < clock.Today || times[i] < clock.NowMinutes) ? Contracts.DoseState.Missed : Contracts.DoseState.Upcoming;
                doses.Add(new DoseDto(m.Id, m.PrescriptionItem.DrugName, m.PrescriptionItem.Dose, m.PrescriptionItem.Instructions, i, times[i], state));
            }
        }
        doses = doses.OrderBy(d => d.Minutes).ThenBy(d => d.MedicationId).ToList();
        var takenCount = doses.Count(d => d.State == Contracts.DoseState.Taken);
        return new TodayDosesDto(date, doses, takenCount, doses.Count, doses.Count == 0 ? 1m : Math.Round((decimal)takenCount / doses.Count, 2));
    }

    public async Task<TodayDosesDto> LogDoseAsync(int medicationId, int slotIndex, bool taken, DateOnly? date, CancellationToken ct)
    {
        var d = date ?? clock.Today;
        if (d > clock.Today || d < clock.Today.AddDays(-1)) throw ApiException.BadRequest("date_invalid", "يمكن تسجيل جرعات اليوم أو أمس فقط.");
        var med = await Mine().FirstOrDefaultAsync(m => m.Id == medicationId, ct) ?? throw ApiException.NotFound("medication_not_found", "الدواء غير موجود.");
        var times = MedicationSchedule.FromCsv(med.TimesCsv);
        if (slotIndex < 0 || slotIndex >= times.Length) throw ApiException.Validation("SlotIndex", "رقم الجرعة غير صحيح");
        if (d < med.StartDate || d > med.EndDate) throw ApiException.BadRequest("date_invalid", "التاريخ خارج مدة العلاج.");

        var log = await db.DoseLogs.FirstOrDefaultAsync(l => l.PatientMedicationId == med.Id && l.Date == d && l.SlotIndex == slotIndex, ct);
        if (taken && log is null) db.DoseLogs.Add(new DoseLog { PatientMedicationId = med.Id, Date = d, SlotIndex = slotIndex, TakenAt = time.GetUtcNow().UtcDateTime });
        else if (!taken && log is not null) db.DoseLogs.Remove(log);
        await db.SaveChangesAsync(ct);
        return await DosesForAsync(d, ct);
    }

    public async Task<MedicationDto> SetReminderAsync(int medicationId, bool enabled, CancellationToken ct)
    {
        var med = await Mine().FirstOrDefaultAsync(m => m.Id == medicationId, ct) ?? throw ApiException.NotFound("medication_not_found", "الدواء غير موجود.");
        med.ReminderEnabled = enabled;
        await db.SaveChangesAsync(ct);
        return ToDto(med, clock.Today);
    }

    public async Task<MedicationDto> RequestRefillAsync(int medicationId, CancellationToken ct)
    {
        var med = await Mine().FirstOrDefaultAsync(m => m.Id == medicationId, ct) ?? throw ApiException.NotFound("medication_not_found", "الدواء غير موجود.");
        var today = clock.Today;
        if (med.EndDate < today) throw ApiException.Conflict("medication_ended", "انتهى هذا العلاج؛ احجز موعدًا مع طبيبك.");
        if (med.RefillStatus == RefillStatus.Requested) throw ApiException.Conflict("refill_pending", "طلب إعادة الصرف قيد المراجعة.");

        med.RefillStatus = RefillStatus.Requested;
        med.RefillRequestedAt = time.GetUtcNow().UtcDateTime;
        notifications.Add(med.PrescriptionItem.Prescription.DoctorId, NotificationType.Medication, "طلب إعادة صرف", $"{me.Id}: {med.PrescriptionItem.DrugName}", nameof(PatientMedication), med.Id);
        audit.Add("medication.refill_request", me.Id, nameof(PatientMedication), med.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(med, today);
    }

    // ------------------------------------------------------------ الطبيب: طلبات إعادة الصرف

    public async Task<IReadOnlyList<RefillRequestDto>> RefillQueueAsync(CancellationToken ct)
    {
        var rows = await db.PatientMedications.AsNoTracking().Where(m => m.RefillStatus == RefillStatus.Requested && m.PrescriptionItem.Prescription.DoctorId == me.Id)
            .Include(m => m.PrescriptionItem).Include(m => m.Patient).OrderBy(m => m.RefillRequestedAt).ToListAsync(ct);
        return rows.Select(m => new RefillRequestDto(m.Id, m.PrescriptionItem.DrugName, new PersonRefDto(m.Patient.Id, m.Patient.FullName, m.Patient.AvatarKey), m.RefillRequestedAt, m.EndDate)).ToList();
    }

    public async Task ApproveRefillAsync(int medicationId, int? days, CancellationToken ct)
    {
        var med = await RequestedForMeAsync(medicationId, ct);
        var extra = days ?? med.PrescriptionItem.DurationDays;
        med.EndDate = (med.EndDate < clock.Today ? clock.Today : med.EndDate).AddDays(extra);
        med.RefillStatus = RefillStatus.Approved;
        notifications.Add(med.PatientId, NotificationType.Medication, "تمت الموافقة على إعادة الصرف", $"{med.PrescriptionItem.DrugName} · {extra} يومًا إضافية", nameof(PatientMedication), med.Id);
        audit.Add("medication.refill_approve", me.Id, nameof(PatientMedication), med.Id, $"+{extra}d", me.Ip);
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectRefillAsync(int medicationId, string reason, CancellationToken ct)
    {
        var med = await RequestedForMeAsync(medicationId, ct);
        med.RefillStatus = RefillStatus.Rejected;
        notifications.Add(med.PatientId, NotificationType.Medication, "رُفض طلب إعادة الصرف", $"{med.PrescriptionItem.DrugName}: {reason.Trim()}", nameof(PatientMedication), med.Id);
        audit.Add("medication.refill_reject", me.Id, nameof(PatientMedication), med.Id, reason.Trim(), me.Ip);
        await db.SaveChangesAsync(ct);
    }

    private async Task<PatientMedication> RequestedForMeAsync(int id, CancellationToken ct)
    {
        var med = await db.PatientMedications.Include(m => m.PrescriptionItem).ThenInclude(i => i.Prescription)
            .FirstOrDefaultAsync(m => m.Id == id && m.PrescriptionItem.Prescription.DoctorId == me.Id, ct) ?? throw ApiException.NotFound("refill_not_found", "الطلب غير موجود.");
        if (med.RefillStatus != RefillStatus.Requested) throw ApiException.Conflict("invalid_state", "لا يوجد طلب معلّق لهذا الدواء.");
        return med;
    }

    private static MedicationDto ToDto(PatientMedication m, DateOnly today)
    {
        var item = m.PrescriptionItem;
        var left = Math.Max(0, m.EndDate.DayNumber - today.DayNumber + 1);
        var times = MedicationSchedule.FromCsv(m.TimesCsv);
        return new MedicationDto(m.Id, item.DrugName, item.Dose, item.Frequency, item.TimesPerDay, times, m.StartDate, m.EndDate, left, left is > 0 and <= EndingSoonDays, m.EndDate >= today,
            item.Prescription.Doctor.FullName, item.Instructions, m.ReminderEnabled, m.RefillStatus);
    }
}
