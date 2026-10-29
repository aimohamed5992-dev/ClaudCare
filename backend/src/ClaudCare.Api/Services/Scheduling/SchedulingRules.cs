using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Services.Scheduling;

/// <summary>توليد الخانات المتاحة (منطق خالص قابل للاختبار).</summary>
public static class SlotCalculator
{
    /// <summary>
    /// خانات يوم واحد لطبيب: ضمن ساعات الدوام وعلى شبكة <paramref name="slotMinutes"/>، تنتهي قبل نهاية الدوام،
    /// لا تتقاطع مع المحجوزة، وبعد الآن + المهلة إن كان اليوم هو اليوم.
    /// </summary>
    public static List<int> Generate(int startMinutes, int endMinutes, int slotMinutes, IReadOnlySet<int> taken, bool isToday, int nowMinutes, int minLeadMinutes)
    {
        var slots = new List<int>();
        if (slotMinutes <= 0) return slots;
        for (var m = startMinutes; m + slotMinutes <= endMinutes; m += slotMinutes)
        {
            if (taken.Contains(m)) continue;
            if (isToday && m <= nowMinutes + minLeadMinutes) continue;
            slots.Add(m);
        }
        return slots;
    }

    /// <summary>هل الوقت على شبكة الخانات داخل ساعات الدوام؟</summary>
    public static bool OnGrid(int startMinutes, int endMinutes, int slotMinutes, int minutes) =>
        slotMinutes > 0 && minutes >= startMinutes && minutes + slotMinutes <= endMinutes && (minutes - startMinutes) % slotMinutes == 0;
}

public readonly record struct PriceQuote(decimal Price, decimal InsuranceCovered, decimal Due, bool InsuranceApplied)
{
    public static PriceQuote Of(decimal price, decimal covered) => new(price, covered, price - covered, covered > 0);
}

public static class PricingCalculator
{
    /// <summary>
    /// يحسب حصة التأمين: (1 − نسبة التحمّل) × السعر، مقيّدة بالمتبقي من الحد السنوي؛ لا شيء إن كانت الوثيقة منتهية أو غير فعّالة.
    /// </summary>
    public static PriceQuote Quote(decimal price, PatientInsurance? policy, DateOnly today, bool useInsurance = true)
    {
        if (!useInsurance || policy is null || !policy.IsActive || policy.ExpiresAt < today) return PriceQuote.Of(price, 0);
        var remainingLimit = Math.Max(0, policy.AnnualLimit - policy.UsedAmount);
        var covered = Math.Min(Math.Round(price * (1 - policy.CopayRate), 2, MidpointRounding.AwayFromZero), remainingLimit);
        return PriceQuote.Of(price, covered);
    }

    /// <summary>رسوم الإلغاء المتأخر (نسبة من المبلغ الذي يدفعه المريض).</summary>
    public static decimal LateCancellationFee(decimal due, int percent) => Math.Round(due * percent / 100m, 2, MidpointRounding.AwayFromZero);
}

public enum AppointmentAction { Confirm, Arrive, Start, Complete, Cancel, Reschedule, NoShow, Review }

/// <summary>آلة حالات الموعد: من يستطيع فعل ماذا ومتى. مصدر وحيد للقواعد يستخدمه الـ API ويُعرَض للواجهات كـ allowedActions.</summary>
public static class AppointmentWorkflow
{
    private static readonly UserRole[] FrontDesk = [UserRole.Reception, UserRole.Branch, UserRole.Hospital, UserRole.Admin];

    public static IReadOnlyList<AppointmentAction> Allowed(UserRole role, bool isOwnPatient, bool isOwnDoctor, AppointmentStatus status, VisitType type, bool reviewed)
    {
        var list = new List<AppointmentAction>();
        var desk = FrontDesk.Contains(role);
        var doctor = role == UserRole.Doctor && isOwnDoctor;
        var patient = role == UserRole.Patient && isOwnPatient;

        if (status == AppointmentStatus.Pending && (doctor || desk)) list.Add(AppointmentAction.Confirm);
        if (status is AppointmentStatus.Pending or AppointmentStatus.Confirmed && type != VisitType.Video && (desk || role == UserRole.Nurse)) list.Add(AppointmentAction.Arrive);
        if (doctor && ((status == AppointmentStatus.Arrived) || (status == AppointmentStatus.Confirmed && type == VisitType.Video))) list.Add(AppointmentAction.Start);
        if (doctor && status == AppointmentStatus.InProgress) list.Add(AppointmentAction.Complete);
        if (status is AppointmentStatus.Pending or AppointmentStatus.Confirmed && (patient || doctor || desk)) list.Add(AppointmentAction.Cancel);
        // المريض يعيد الجدولة قبل وصوله فقط؛ الطبيب والاستقبال حتى بعد الوصول
        if ((status is AppointmentStatus.Pending or AppointmentStatus.Confirmed && patient) || (status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.Arrived && (doctor || desk))) list.Add(AppointmentAction.Reschedule);
        if (status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.Arrived && (doctor || desk)) list.Add(AppointmentAction.NoShow);
        if (status == AppointmentStatus.Done && patient && !reviewed) list.Add(AppointmentAction.Review);
        return list;
    }

    /// <summary>هل يملك هذا الدور (بهذه العلاقة) هذا الإجراء في أي حالة؟ للتمييز بين 403 (ممنوع دائمًا) و409 (حالة غير مناسبة الآن).</summary>
    public static bool RoleMayEverDo(UserRole role, bool isOwnPatient, bool isOwnDoctor, AppointmentAction action) =>
        Enum.GetValues<AppointmentStatus>().Any(s => Enum.GetValues<VisitType>().Any(t => Allowed(role, isOwnPatient, isOwnDoctor, s, t, reviewed: false).Contains(action)));
}
