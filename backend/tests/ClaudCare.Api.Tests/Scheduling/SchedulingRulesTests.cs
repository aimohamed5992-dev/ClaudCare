using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Scheduling;

namespace ClaudCare.Api.Tests.Scheduling;

public class SlotCalculatorTests
{
    [Fact]
    public void Generates_a_grid_that_ends_before_closing_time()
    {
        var slots = SlotCalculator.Generate(480, 600, 30, new HashSet<int>(), false, 0, 0);
        Assert.Equal([480, 510, 540, 570], slots);
    }

    [Fact]
    public void Last_slot_must_fit_entirely_inside_the_shift()
    {
        Assert.Equal([480, 520], SlotCalculator.Generate(480, 580, 40, new HashSet<int>(), false, 0, 0)); // 560+40 > 580
    }

    [Fact]
    public void Removes_taken_and_past_slots_today_only()
    {
        var taken = new HashSet<int> { 510 };
        Assert.Equal([540, 570], SlotCalculator.Generate(480, 600, 30, taken, isToday: true, nowMinutes: 480, minLeadMinutes: 0));
        Assert.Equal([480, 540, 570], SlotCalculator.Generate(480, 600, 30, taken, isToday: false, nowMinutes: 9999, minLeadMinutes: 0));
    }

    [Fact]
    public void Minimum_lead_time_pushes_the_first_slot_later()
    {
        Assert.Equal([540, 570], SlotCalculator.Generate(480, 600, 30, new HashSet<int>(), true, 500, 15)); // 510 ≤ 515
    }

    [Theory]
    [InlineData(480, true)]
    [InlineData(510, true)]
    [InlineData(570, true)]
    [InlineData(600, false)]   // يجاوز النهاية
    [InlineData(500, false)]   // خارج الشبكة
    [InlineData(450, false)]   // قبل البداية
    public void OnGrid_checks_alignment_and_bounds(int minutes, bool expected) => Assert.Equal(expected, SlotCalculator.OnGrid(480, 600, 30, minutes));

    [Fact]
    public void Degenerate_slot_length_yields_nothing() => Assert.Empty(SlotCalculator.Generate(480, 600, 0, new HashSet<int>(), false, 0, 0));
}

public class PricingCalculatorTests
{
    private static PatientInsurance Policy(decimal copay = 0.2m, decimal limit = 1000, decimal used = 0, bool active = true, int expiresInDays = 100) =>
        new() { CopayRate = copay, AnnualLimit = limit, UsedAmount = used, IsActive = active, ExpiresAt = new DateOnly(2026, 10, 27).AddDays(expiresInDays) };

    private static readonly DateOnly Today = new(2026, 10, 27);

    [Fact]
    public void No_policy_means_the_patient_pays_everything()
    {
        var q = PricingCalculator.Quote(300, null, Today);
        Assert.Equal((300m, 0m, 300m, false), (q.Price, q.InsuranceCovered, q.Due, q.InsuranceApplied));
    }

    [Theory]
    [InlineData(300, 0.20, 240, 60)]
    [InlineData(250, 0.30, 175, 75)]
    [InlineData(99.99, 0.25, 74.99, 25.00)] // 74.9925 → 74.99
    public void Covers_one_minus_copay(decimal price, decimal copay, decimal covered, decimal due)
    {
        var q = PricingCalculator.Quote(price, Policy(copay), Today);
        Assert.Equal(covered, q.InsuranceCovered);
        Assert.Equal(due, q.Due);
        Assert.True(q.InsuranceApplied);
    }

    [Fact]
    public void Coverage_is_capped_by_the_remaining_annual_limit()
    {
        var q = PricingCalculator.Quote(300, Policy(limit: 1000, used: 900), Today);
        Assert.Equal(100, q.InsuranceCovered);
        Assert.Equal(200, q.Due);
        Assert.Equal(0, PricingCalculator.Quote(300, Policy(limit: 1000, used: 1000), Today).InsuranceCovered);
        Assert.Equal(0, PricingCalculator.Quote(300, Policy(limit: 1000, used: 5000), Today).InsuranceCovered); // لا قيمة سالبة
    }

    [Fact]
    public void Inactive_expired_or_opted_out_policies_cover_nothing()
    {
        Assert.Equal(0, PricingCalculator.Quote(300, Policy(active: false), Today).InsuranceCovered);
        Assert.Equal(0, PricingCalculator.Quote(300, Policy(expiresInDays: -1), Today).InsuranceCovered);
        Assert.Equal(240, PricingCalculator.Quote(300, Policy(expiresInDays: 0), Today).InsuranceCovered); // آخر يوم صلاحية ما زال صالحًا
        Assert.Equal(0, PricingCalculator.Quote(300, Policy(), Today, useInsurance: false).InsuranceCovered);
    }

    [Fact]
    public void Late_cancellation_fee_is_a_percentage_of_the_patient_share()
    {
        Assert.Equal(150, PricingCalculator.LateCancellationFee(300, 50));
        Assert.Equal(0, PricingCalculator.LateCancellationFee(0, 50));
        Assert.Equal(33.00m, PricingCalculator.LateCancellationFee(99.99m, 33)); // 32.9967 → 33.00
    }
}

public class AppointmentWorkflowRulesTests
{
    private static IReadOnlyList<AppointmentAction> A(UserRole role, AppointmentStatus s, VisitType t = VisitType.Clinic, bool ownPatient = false, bool ownDoctor = false, bool reviewed = false) =>
        AppointmentWorkflow.Allowed(role, ownPatient, ownDoctor, s, t, reviewed);

    [Fact]
    public void Patient_can_only_cancel_reschedule_and_review_their_own()
    {
        Assert.Equal([AppointmentAction.Cancel, AppointmentAction.Reschedule], A(UserRole.Patient, AppointmentStatus.Confirmed, ownPatient: true));
        Assert.Empty(A(UserRole.Patient, AppointmentStatus.Confirmed, ownPatient: false));
        Assert.Equal([AppointmentAction.Review], A(UserRole.Patient, AppointmentStatus.Done, ownPatient: true));
        Assert.Empty(A(UserRole.Patient, AppointmentStatus.Done, ownPatient: true, reviewed: true));
        Assert.Empty(A(UserRole.Patient, AppointmentStatus.Arrived, ownPatient: true).Where(x => x == AppointmentAction.Reschedule)); // لا إعادة جدولة بعد الوصول
        Assert.Empty(A(UserRole.Patient, AppointmentStatus.Cancelled, ownPatient: true));
    }

    [Fact]
    public void Doctor_actions_follow_the_clinical_flow()
    {
        Assert.Contains(AppointmentAction.Confirm, A(UserRole.Doctor, AppointmentStatus.Pending, ownDoctor: true));
        Assert.DoesNotContain(AppointmentAction.Start, A(UserRole.Doctor, AppointmentStatus.Confirmed, ownDoctor: true));
        Assert.Contains(AppointmentAction.Start, A(UserRole.Doctor, AppointmentStatus.Confirmed, VisitType.Video, ownDoctor: true));
        Assert.Contains(AppointmentAction.Start, A(UserRole.Doctor, AppointmentStatus.Arrived, ownDoctor: true));
        Assert.Contains(AppointmentAction.Complete, A(UserRole.Doctor, AppointmentStatus.InProgress, ownDoctor: true));
        Assert.Empty(A(UserRole.Doctor, AppointmentStatus.Confirmed, ownDoctor: false));
        Assert.Empty(A(UserRole.Doctor, AppointmentStatus.Done, ownDoctor: true));
    }

    [Fact]
    public void Front_desk_checks_in_clinic_visits_only()
    {
        Assert.Contains(AppointmentAction.Arrive, A(UserRole.Reception, AppointmentStatus.Confirmed));
        Assert.DoesNotContain(AppointmentAction.Arrive, A(UserRole.Reception, AppointmentStatus.Confirmed, VisitType.Video));
        Assert.DoesNotContain(AppointmentAction.Start, A(UserRole.Reception, AppointmentStatus.Arrived));
        Assert.Empty(A(UserRole.Pharmacy, AppointmentStatus.Confirmed));
        Assert.Empty(A(UserRole.Lab, AppointmentStatus.Pending));
    }

    [Fact]
    public void Role_may_ever_distinguishes_forbidden_from_wrong_state()
    {
        Assert.False(AppointmentWorkflow.RoleMayEverDo(UserRole.Patient, true, false, AppointmentAction.Confirm));
        Assert.True(AppointmentWorkflow.RoleMayEverDo(UserRole.Patient, true, false, AppointmentAction.Cancel));
        Assert.False(AppointmentWorkflow.RoleMayEverDo(UserRole.Reception, false, false, AppointmentAction.Start));
        Assert.True(AppointmentWorkflow.RoleMayEverDo(UserRole.Doctor, false, true, AppointmentAction.Complete));
        Assert.False(AppointmentWorkflow.RoleMayEverDo(UserRole.Doctor, false, false, AppointmentAction.Complete)); // ليس طبيبها
    }
}
