using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Scheduling;

[Collection(ApiCollection.Name)]
public class BookingTests(ApiFactory root)
{
    [Fact]
    public async Task Patient_books_a_clinic_visit_and_it_is_confirmed_and_paid()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(price: 300);
        var p = await c.PatientAsync();

        var res = await c.BookAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10, reason: "ألم في الصدر");
        Assert.Equal(201, res.Status);
        Assert.NotNull(res.Raw.Headers.Location);
        Assert.Equal("Confirmed", res.Str("status"));
        Assert.Equal("Clinic", res.Str("type"));
        Assert.Equal(300, res["price"].GetDecimal());
        Assert.Equal(0, res["insuranceCovered"].GetDecimal());
        Assert.Equal(300, res["due"].GetDecimal());
        Assert.True(res["paid"].GetBoolean());
        Assert.Equal("Card", res.Str("paymentMethod"));
        Assert.Equal("2026-10-28", res.Str("date"));
        Assert.Equal(Clinic.H10, res["startMinutes"].GetInt32());
        Assert.Equal(30, res["durationMinutes"].GetInt32());
        Assert.Equal(d.Id, res["doctor"].GetProperty("id").GetInt32());
        Assert.Equal(p.Id, res["patient"].GetProperty("id").GetInt32());
        Assert.Equal("القلب", res.Str("specialtyName"));
        var actions = res["allowedActions"].EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("Cancel", actions);
        Assert.Contains("Reschedule", actions);
        Assert.DoesNotContain("Confirm", actions);
    }

    [Fact]
    public async Task Booking_notifies_both_sides()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);

        Assert.Contains("حجز جديد", await c.NotificationTitlesAsync(d.Id));
        Assert.Contains("تم تأكيد حجزك", await c.NotificationTitlesAsync(p.Id));
    }

    [Fact]
    public async Task Doctor_who_requires_confirmation_gets_pending_bookings_then_confirms()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(requiresConfirmation: true);
        var dt = await c.DoctorTokenAsync(d);
        var p = await c.PatientAsync();

        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);
        Assert.Equal(AppointmentStatus.Pending, await c.StatusAsync(id));
        Assert.Contains("حجزك بانتظار تأكيد الطبيب", await c.NotificationTitlesAsync(p.Id));

        // المريض لا يؤكد موعده
        Assert.Equal(403, (await c.Act(p.Token, id, "confirm")).Status);
        var ok = await c.Act(dt, id, "confirm");
        Assert.Equal(200, ok.Status);
        Assert.Equal("Confirmed", ok.Str("status"));
        Assert.Contains("تم تأكيد موعدك", await c.NotificationTitlesAsync(p.Id));
        // لا يمكن تأكيده مرتين
        var again = await c.Act(dt, id, "confirm");
        Assert.Equal(409, again.Status);
        Assert.Equal("invalid_state", again.Code);
    }

    [Theory]
    [InlineData(0.20, 100_000, 0, 300, 240, 60)]
    [InlineData(0.20, 100, 0, 300, 100, 200)]       // سقف الحد السنوي المتبقي
    [InlineData(0.20, 100_000, 99_900, 300, 100, 200)] // المتبقي من الحد 100 فقط
    [InlineData(0.00, 100_000, 0, 300, 300, 0)]     // مغطى بالكامل
    public async Task Insurance_covers_the_share_after_copay_up_to_the_remaining_limit(double copay, double limit, double used, double price, double covered, double due)
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(price: (decimal)price);
        var p = await c.PatientAsync();
        await c.InsureAsync(p.Id, (decimal)copay, (decimal)limit, (decimal)used);

        var res = await c.BookAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10, payment: due == 0 ? null : "Card");
        Assert.Equal(201, res.Status);
        Assert.Equal((decimal)covered, res["insuranceCovered"].GetDecimal());
        Assert.Equal((decimal)due, res["due"].GetDecimal());
        if (due == 0)
        {
            Assert.Equal("Insurance", res.Str("paymentMethod"));
            Assert.True(res["paid"].GetBoolean());
        }
    }

    [Fact]
    public async Task Expired_inactive_or_opted_out_insurance_is_not_applied()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(price: 300);

        var expired = await c.PatientAsync();
        await c.InsureAsync(expired.Id, expires: new DateOnly(2026, 10, 1));
        Assert.Equal(0, (await c.BookAsync(expired.Token, d.Id, Clinic.Wed, Clinic.H09))["insuranceCovered"].GetDecimal());

        var inactive = await c.PatientAsync();
        await c.InsureAsync(inactive.Id, active: false);
        Assert.Equal(0, (await c.BookAsync(inactive.Token, d.Id, Clinic.Wed, Clinic.H10))["insuranceCovered"].GetDecimal());

        var optedOut = await c.PatientAsync();
        await c.InsureAsync(optedOut.Id);
        Assert.Equal(0, (await c.BookAsync(optedOut.Token, d.Id, Clinic.Wed, Clinic.H11, insurance: false))["insuranceCovered"].GetDecimal());
    }

    [Fact]
    public async Task Video_visits_use_the_video_price_and_need_doctor_support()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(price: 300, video: 200);
        var noVideo = await c.DoctorAsync(video: null);
        var p = await c.PatientAsync();

        var ok = await c.BookAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10, type: "Video");
        Assert.Equal(201, ok.Status);
        Assert.Equal(200, ok["price"].GetDecimal());

        var bad = await c.BookAsync(p.Token, noVideo.Id, Clinic.Wed, Clinic.H10, type: "Video");
        Assert.Equal(400, bad.Status);
        Assert.Equal("video_unavailable", bad.Code);
    }

    [Fact]
    public async Task Payment_method_rules()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();

        var missing = await c.BookAsync(p.Token, d.Id, Clinic.Wed, Clinic.H09, payment: null);
        Assert.Equal(400, missing.Status);
        Assert.True(missing["errors"].TryGetProperty("PaymentMethod", out _));

        var cash = await c.BookAsync(p.Token, d.Id, Clinic.Wed, Clinic.H09, payment: "Cash"); // المريض لا يدفع نقدًا عبر التطبيق
        Assert.Equal(400, cash.Status);

        var clinic = await c.BookAsync(p.Token, d.Id, Clinic.Wed, Clinic.H09, payment: "PayAtClinic");
        Assert.Equal(201, clinic.Status);
        Assert.False(clinic["paid"].GetBoolean());
        Assert.Equal("PayAtClinic", clinic.Str("paymentMethod"));
    }

    [Theory]
    [InlineData("2026-10-26", 600, "slot_invalid")]   // أمس
    [InlineData("2026-10-30", 600, "slot_invalid")]   // جمعة (عطلة)
    [InlineData("2026-10-28", 485, "slot_invalid")]   // خارج الشبكة
    [InlineData("2026-10-28", 450, "slot_invalid")]   // قبل الدوام
    [InlineData("2026-10-28", 960, "slot_invalid")]   // بعد الدوام
    [InlineData("2026-10-28", 945, "slot_invalid")]   // ليس على الشبكة
    [InlineData("2027-02-01", 600, "slot_invalid")]   // أبعد من 60 يومًا
    [InlineData("2026-10-27", 540, "slot_invalid")]   // اليوم لكن 09:00 مضى
    [InlineData("2026-10-27", 555, "slot_invalid")]   // 09:15 ليست على الشبكة
    public async Task Invalid_slots_are_rejected(string date, int minutes, string code)
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var res = await c.BookAsync(p.Token, d.Id, DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), minutes);
        Assert.Equal(400, res.Status);
        Assert.Equal(code, res.Code);
    }

    [Fact]
    public async Task A_taken_slot_is_a_conflict_and_a_leave_day_is_refused()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var a = await c.PatientAsync();
        var b = await c.PatientAsync();
        await c.BookOkAsync(a.Token, d.Id, Clinic.Wed, Clinic.H10);

        var clash = await c.BookAsync(b.Token, d.Id, Clinic.Wed, Clinic.H10);
        Assert.Equal(409, clash.Status);
        Assert.Equal("slot_taken", clash.Code);

        var onLeave = await c.DoctorAsync();
        await c.WithDb(async db =>
        {
            var emp = new Employee { UserId = onLeave.Id, BranchId = c.BranchId, FullName = "ط", JobTitle = "طبيب", HireDate = new DateOnly(2020, 1, 1) };
            emp.Leaves(db, Clinic.Thu, Clinic.Thu);
            await db.SaveChangesAsync();
            return 0;
        });
        var res = await c.BookAsync(b.Token, onLeave.Id, Clinic.Thu, Clinic.H10);
        Assert.Equal("doctor_on_leave", res.Code);
    }

    [Fact]
    public async Task Concurrent_bookings_of_the_same_slot_yield_exactly_one_winner()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var patients = new List<Clinic.Pat>();
        for (var i = 0; i < 8; i++) patients.Add(await c.PatientAsync());

        var results = await Task.WhenAll(patients.Select(p => Task.Run(() => c.BookAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10))));

        Assert.Equal(1, results.Count(r => r.Status == 201));
        Assert.Equal(7, results.Count(r => r.Status == 409 && r.Code == "slot_taken"));
        var rows = await c.WithDb(db => db.Appointments.CountAsync(a => a.DoctorId == d.Id && a.Date == Clinic.Wed && a.StartMinutes == Clinic.H10));
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task A_patient_cannot_be_in_two_places_at_once_but_family_members_can()
    {
        var c = await Clinic.CreateAsync(root);
        var d1 = await c.DoctorAsync();
        var d2 = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var kidId = await c.WithDb(async db =>
        {
            var k = new FamilyMember { PatientId = p.Id, Name = "زياد", Relation = "ابني", BirthYear = 2019 };
            db.Add(k);
            await db.SaveChangesAsync();
            return k.Id;
        });
        await c.BookOkAsync(p.Token, d1.Id, Clinic.Wed, Clinic.H10);

        var busy = await c.BookAsync(p.Token, d2.Id, Clinic.Wed, Clinic.H10);
        Assert.Equal(409, busy.Status);
        Assert.Equal("patient_busy", busy.Code);

        var forKid = await c.BookAsync(p.Token, d2.Id, Clinic.Wed, Clinic.H10, familyMemberId: kidId);
        Assert.Equal(201, forKid.Status);
        Assert.Equal("زياد", forKid.Str("familyMemberName"));
    }

    [Fact]
    public async Task Family_member_of_someone_else_is_not_found()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var owner = await c.PatientAsync();
        var other = await c.PatientAsync();
        var kidId = await c.WithDb(async db =>
        {
            var k = new FamilyMember { PatientId = owner.Id, Name = "طفل", Relation = "ابني", BirthYear = 2019 };
            db.Add(k);
            await db.SaveChangesAsync();
            return k.Id;
        });
        var res = await c.BookAsync(other.Token, d.Id, Clinic.Wed, Clinic.H10, familyMemberId: kidId);
        Assert.Equal(404, res.Status);
        Assert.Equal("family_member_not_found", res.Code);
    }

    [Fact]
    public async Task Reception_books_on_behalf_of_a_patient_but_others_cannot()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var other = await c.PatientAsync();
        var desk = await c.TokenAsync(UserRole.Reception);

        var missing = await c.BookAsync(desk, d.Id, Clinic.Wed, Clinic.H10);
        Assert.Equal(400, missing.Status);

        var ok = await c.BookAsync(desk, d.Id, Clinic.Wed, Clinic.H10, patientId: p.Id, payment: "Cash");
        Assert.Equal(201, ok.Status);
        Assert.Equal(p.Id, ok["patient"].GetProperty("id").GetInt32());
        Assert.Equal("Cash", ok.Str("paymentMethod"));

        // المريض لا يحجز باسم غيره، والطبيب والمختبر لا يحجزون
        Assert.Equal(403, (await c.BookAsync(p.Token, d.Id, Clinic.Wed, Clinic.H11, patientId: other.Id)).Status);
        Assert.Equal(403, (await c.BookAsync(await c.DoctorTokenAsync(d), d.Id, Clinic.Wed, Clinic.H11, patientId: p.Id)).Status);
        Assert.Equal(403, (await c.BookAsync(await c.TokenAsync(UserRole.Lab), d.Id, Clinic.Wed, Clinic.H11, patientId: p.Id)).Status);
    }

    [Fact]
    public async Task Missing_required_fields_are_validation_errors()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        var res = await c.Http.PostAsync("/api/appointments", new { reason = "x" }, p.Token);
        Assert.Equal(400, res.Status);
        Assert.Equal("validation_failed", res.Code);
        Assert.True(res["errors"].TryGetProperty("DoctorId", out _));
        Assert.True(res["errors"].TryGetProperty("Date", out _));
        Assert.Equal(404, (await c.BookAsync(p.Token, 99999999, Clinic.Wed, Clinic.H10)).Status);
        Assert.Equal(401, (await c.Http.PostAsync("/api/appointments", new { })).Status);
    }
}
