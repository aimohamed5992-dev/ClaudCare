using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Scheduling;

[Collection(ApiCollection.Name)]
public class AppointmentVisibilityTests(ApiFactory root)
{
    [Fact]
    public async Task Each_role_sees_only_its_own_scope()
    {
        var c = await Clinic.CreateAsync(root);
        var d1 = await c.DoctorAsync();
        var d2 = await c.DoctorAsync();
        var a = await c.PatientAsync();
        var b = await c.PatientAsync();
        var a1 = await c.BookOkAsync(a.Token, d1.Id, Clinic.Wed, Clinic.H10);
        var b1 = await c.BookOkAsync(b.Token, d2.Id, Clinic.Wed, Clinic.H10);

        // المريض
        var mine = await c.Http.GetApiAsync("/api/appointments", a.Token);
        Assert.Equal([a1], mine["items"].EnumerateArray().Select(x => x.GetProperty("id").GetInt32()));
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/appointments/{b1}", a.Token)).Status); // ليس له: يظهر غير موجود

        // الطبيب
        var d1Token = await c.DoctorTokenAsync(d1);
        Assert.Equal([a1], (await c.Http.GetApiAsync("/api/appointments", d1Token))["items"].EnumerateArray().Select(x => x.GetProperty("id").GetInt32()));
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/appointments/{b1}", d1Token)).Status);

        // الاستقبال في نفس الفرع يرى الاثنين، وفي فرع آخر لا يرى شيئًا
        var desk = await c.TokenAsync(UserRole.Reception);
        var deskIds = (await c.Http.GetApiAsync($"/api/appointments?doctorId={d1.Id}", desk))["items"].EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();
        Assert.Equal([a1], deskIds);
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/appointments/{b1}", desk)).Status);

        var otherBranch = await c.WithDb(async db => (await TestData.BranchAsync(db)).Id);
        var foreignDesk = await c.TokenAsync(UserRole.Reception, branchId: otherBranch);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/appointments/{a1}", foreignDesk)).Status);
        Assert.Equal(0, (await c.Http.GetApiAsync($"/api/appointments?doctorId={d1.Id}", foreignDesk))["total"].GetInt32());

        // الإدارة العليا ترى الكل، وأدوار لا شأن لها بالمواعيد ممنوعة
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/appointments/{a1}", await c.TokenAsync(UserRole.Admin))).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/appointments", await c.TokenAsync(UserRole.Pharmacy))).Status);
    }

    [Fact]
    public async Task Scopes_split_upcoming_past_and_cancelled_with_sensible_order_and_paging()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var later = await c.BookOkAsync(p.Token, d.Id, Clinic.Thu, Clinic.H10);
        var sooner = await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H09);
        var cancelled = await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H11);
        await c.Act(p.Token, cancelled, "cancel", new { reason = "تعارض" });
        var done = await c.BookOkAsync(p.Token, d.Id, Clinic.Sun, Clinic.H10);
        await c.WithDb(async db => { (await db.Appointments.FindAsync(done))!.Status = AppointmentStatus.Done; await db.SaveChangesAsync(); return 0; });

        async Task<int[]> Ids(string q)
        {
            var r = await c.Http.GetApiAsync($"/api/appointments?{q}", p.Token);
            Assert.True(r.Status == 200, $"{q} → {r.Status} {r.Body}");
            return r["items"].EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToArray();
        }

        Assert.Equal(new[] { sooner, later }, await Ids("scope=upcoming")); // تصاعدي حسب الوقت (الأحدث لاحقًا)، والمنتهي لا يظهر
        Assert.Equal([cancelled], await Ids("scope=cancelled"));
        Assert.Contains(done, await Ids("scope=past"));
        Assert.DoesNotContain(sooner, await Ids("scope=past"));

        var page = await c.Http.GetApiAsync("/api/appointments?scope=upcoming&pageSize=1&page=2", p.Token);
        Assert.Equal(1, page["items"].GetArrayLength());
        Assert.Equal(2, page["total"].GetInt32()); // القادمة: sooner و later فقط

        Assert.Equal([later], await Ids($"date={Clinic.Thu.Iso()}"));
        Assert.Equal([sooner], await Ids("status=Confirmed&from=2026-10-28&to=2026-10-28&scope=upcoming"));
    }

    [Fact]
    public async Task Doctor_agenda_counts_the_days_statuses()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(requiresConfirmation: true);
        var dt = await c.DoctorTokenAsync(d);
        var p1 = await c.PatientAsync();
        var p2 = await c.PatientAsync();
        var p3 = await c.PatientAsync();
        var pending = await c.BookOkAsync(p1.Token, d.Id, Clinic.Wed, Clinic.H09);
        var confirmed = await c.BookOkAsync(p2.Token, d.Id, Clinic.Wed, Clinic.H10);
        await c.Act(dt, confirmed, "confirm");
        var video = await c.BookOkAsync(p3.Token, d.Id, Clinic.Wed, Clinic.H11, "Video");
        await c.Act(dt, video, "confirm");
        var cancelled = await c.BookOkAsync(p1.Token, d.Id, Clinic.Wed, 12 * 60);
        await c.Act(p1.Token, cancelled, "cancel", new { reason = "ظرف" });

        var agenda = await c.Http.GetApiAsync("/api/doctors/me/agenda?date=2026-10-28", dt);
        Assert.Equal(200, agenda.Status);
        Assert.Equal(3, agenda["total"].GetInt32()); // دون الملغى
        Assert.Equal(1, agenda["pending"].GetInt32());
        Assert.Equal(2, agenda["confirmed"].GetInt32());
        Assert.Equal(1, agenda["cancelled"].GetInt32());
        Assert.Equal(1, agenda["video"].GetInt32());
        Assert.Equal(4, agenda["appointments"].GetArrayLength());
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/doctors/me/agenda", p1.Token)).Status);
        _ = pending;
    }
}

[Collection(ApiCollection.Name)]
public class AppointmentCancelRescheduleTests(ApiFactory root)
{
    [Fact]
    public async Task Early_cancellation_is_free_and_frees_the_slot()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var a = await c.PatientAsync();
        var b = await c.PatientAsync();
        var id = await c.BookOkAsync(a.Token, d.Id, Clinic.Thu, Clinic.H10);

        var res = await c.Act(a.Token, id, "cancel", new { reason = "حجزت بالخطأ" });
        Assert.Equal(200, res.Status);
        Assert.False(res["lateCancellation"].GetBoolean());
        Assert.Equal(0, res["cancellationFee"].GetDecimal());
        Assert.Equal("Cancelled", res["appointment"].GetProperty("status").GetString());
        Assert.Equal("حجزت بالخطأ", res["appointment"].GetProperty("cancelReason").GetString());

        // الخانة متاحة لغيره
        Assert.Equal(201, (await c.BookAsync(b.Token, d.Id, Clinic.Thu, Clinic.H10)).Status);
        Assert.Contains("أُلغي موعد", await c.NotificationTitlesAsync(d.Id));
    }

    [Fact]
    public async Task Cancelling_within_24_hours_reports_a_fee_of_half_the_patient_share()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(price: 300);
        var p = await c.PatientAsync();
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Today, Clinic.H0930); // بعد 15 دقيقة

        var res = await c.Act(p.Token, id, "cancel", new { reason = "ظرف طارئ" });
        Assert.True(res["lateCancellation"].GetBoolean());
        Assert.Equal(150, res["cancellationFee"].GetDecimal());
    }

    [Fact]
    public async Task Patient_cannot_cancel_after_the_start_time_but_staff_can()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var desk = await c.TokenAsync(UserRole.Reception);
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Today, Clinic.H0930);
        c.Clock.Advance(TimeSpan.FromMinutes(20)); // 09:35

        var res = await c.Act(p.Token, id, "cancel", new { reason = "متأخر" });
        Assert.Equal(409, res.Status);
        Assert.Equal("appointment_started", res.Code);

        var fresh = await c.TokenAsync(UserRole.Reception);
        Assert.Equal(200, (await c.Act(fresh, id, "cancel", new { reason = "لم يحضر المريض" })).Status);
        _ = desk;
    }

    [Fact]
    public async Task Cancel_rules_reason_state_ownership()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var a = await c.PatientAsync();
        var b = await c.PatientAsync();
        var id = await c.BookOkAsync(a.Token, d.Id, Clinic.Thu, Clinic.H10);

        Assert.Equal(400, (await c.Act(a.Token, id, "cancel", new { reason = "" })).Status);
        Assert.True((await c.Act(a.Token, id, "cancel")).Status >= 400); // بلا جسم
        Assert.Equal(404, (await c.Act(b.Token, id, "cancel", new { reason = "ليس موعدي" })).Status);

        Assert.Equal(200, (await c.Act(a.Token, id, "cancel", new { reason = "ظرف" })).Status);
        var again = await c.Act(a.Token, id, "cancel", new { reason = "ظرف" });
        Assert.Equal(409, again.Status);
        Assert.Equal("invalid_state", again.Code);
    }

    [Fact]
    public async Task Doctor_cancellation_notifies_the_patient_and_reception_notifies_both()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p1 = await c.PatientAsync();
        var p2 = await c.PatientAsync();
        var byDoctor = await c.BookOkAsync(p1.Token, d.Id, Clinic.Thu, Clinic.H10);
        var byDesk = await c.BookOkAsync(p2.Token, d.Id, Clinic.Thu, Clinic.H11);

        Assert.Equal(200, (await c.Act(await c.DoctorTokenAsync(d), byDoctor, "cancel", new { reason = "إجازة طارئة" })).Status);
        Assert.Contains("أُلغي موعد", await c.NotificationTitlesAsync(p1.Id));

        Assert.Equal(200, (await c.Act(await c.TokenAsync(UserRole.Reception), byDesk, "cancel", new { reason = "طلب المريض" })).Status);
        Assert.Contains("أُلغي موعد", await c.NotificationTitlesAsync(p2.Id));
        Assert.Single((await c.NotificationTitlesAsync(d.Id)).Where(t => t == "أُلغي موعد")); // الطبيب أُبلغ بإلغاء الاستقبال فقط (إلغاؤه بنفسه لا يُبلّغه)
    }

    [Fact]
    public async Task Patient_reschedules_to_a_free_slot_and_the_old_slot_is_released()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var a = await c.PatientAsync();
        var b = await c.PatientAsync();
        var id = await c.BookOkAsync(a.Token, d.Id, Clinic.Wed, Clinic.H10);

        var res = await c.Act(a.Token, id, "reschedule", new { date = "2026-10-29", startMinutes = Clinic.H11 });
        Assert.Equal(200, res.Status);
        Assert.Equal("2026-10-29", res.Str("date"));
        Assert.Equal(Clinic.H11, res["startMinutes"].GetInt32());
        Assert.Contains("تغيّر موعدك", (await c.NotificationTitlesAsync(d.Id)));

        Assert.Equal(201, (await c.BookAsync(b.Token, d.Id, Clinic.Wed, Clinic.H10)).Status); // الخانة القديمة حُرّرت
        Assert.Equal(409, (await c.BookAsync(b.Token, d.Id, Clinic.Thu, Clinic.H11)).Status); // الجديدة مشغولة
    }

    [Fact]
    public async Task Reschedule_validations()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var a = await c.PatientAsync();
        var b = await c.PatientAsync();
        var id = await c.BookOkAsync(a.Token, d.Id, Clinic.Wed, Clinic.H10);
        await c.BookOkAsync(b.Token, d.Id, Clinic.Wed, Clinic.H11);
        var other = await c.DoctorAsync();
        await c.BookOkAsync(a.Token, other.Id, Clinic.Thu, Clinic.H09);

        Assert.Equal("same_slot", (await c.Act(a.Token, id, "reschedule", new { date = "2026-10-28", startMinutes = Clinic.H10 })).Code);
        Assert.Equal("slot_taken", (await c.Act(a.Token, id, "reschedule", new { date = "2026-10-28", startMinutes = Clinic.H11 })).Code);
        Assert.Equal("slot_invalid", (await c.Act(a.Token, id, "reschedule", new { date = "2026-10-30", startMinutes = Clinic.H10 })).Code);
        Assert.Equal("patient_busy", (await c.Act(a.Token, id, "reschedule", new { date = "2026-10-29", startMinutes = Clinic.H09 })).Code);
        Assert.Equal(404, (await c.Act(b.Token, id, "reschedule", new { date = "2026-10-29", startMinutes = Clinic.H10 })).Status);
        Assert.Equal(400, (await c.Act(a.Token, id, "reschedule", new { })).Status);
    }

    [Fact]
    public async Task Doctor_rescheduling_a_pending_request_confirms_it()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(requiresConfirmation: true);
        var dt = await c.DoctorTokenAsync(d);
        var p = await c.PatientAsync();
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);

        var res = await c.Act(dt, id, "reschedule", new { date = "2026-10-29", startMinutes = Clinic.H10 });
        Assert.Equal(200, res.Status);
        Assert.Equal("Confirmed", res.Str("status"));
        Assert.Contains("تغيّر موعدك", await c.NotificationTitlesAsync(p.Id));
    }
}

[Collection(ApiCollection.Name)]
public class AppointmentWorkflowTests(ApiFactory root)
{
    [Fact]
    public async Task Clinic_visit_flows_arrive_start_complete_with_the_right_actors()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var dt = await c.DoctorTokenAsync(d);
        var p = await c.PatientAsync();
        var desk = await c.TokenAsync(UserRole.Reception);
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Today, Clinic.H0930);

        // الطبيب لا يبدأ قبل وصول المريض، والمريض لا يسجّل وصوله
        Assert.Equal("invalid_state", (await c.Act(dt, id, "start")).Code);
        Assert.Equal(403, (await c.Act(p.Token, id, "arrive")).Status);

        var arrived = await c.Act(desk, id, "arrive");
        Assert.Equal("Arrived", arrived.Str("status"));
        Assert.Contains("وصل المريض", await c.NotificationTitlesAsync(d.Id));

        Assert.Equal(403, (await c.Act(desk, id, "start")).Status); // الاستقبال لا يبدأ الكشف
        var started = await c.Act(dt, id, "start");
        Assert.Equal("InProgress", started.Str("status"));
        Assert.Contains("Complete", started["allowedActions"].EnumerateArray().Select(x => x.GetString()));

        var done = await c.Act(dt, id, "complete");
        Assert.Equal("Done", done.Str("status"));
        Assert.Contains("انتهى كشفك", await c.NotificationTitlesAsync(p.Id));
        Assert.Contains("Review", (await c.Http.GetApiAsync($"/api/appointments/{id}", p.Token))["allowedActions"].EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Arrival_is_only_on_the_day_of_the_appointment()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var desk = await c.TokenAsync(UserRole.Reception);
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);
        var res = await c.Act(desk, id, "arrive");
        Assert.Equal(409, res.Status);
        Assert.Equal("not_today", res.Code);
    }

    [Fact]
    public async Task Video_visit_starts_directly_from_confirmed_and_cannot_be_checked_in()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var dt = await c.DoctorTokenAsync(d);
        var p = await c.PatientAsync();
        var desk = await c.TokenAsync(UserRole.Reception);
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Today, Clinic.H0930, "Video");

        Assert.Equal("invalid_state", (await c.Act(desk, id, "arrive")).Code);
        Assert.Equal("InProgress", (await c.Act(dt, id, "start")).Str("status"));
    }

    [Fact]
    public async Task Another_doctor_cannot_touch_the_appointment()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var intruder = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Today, Clinic.H0930, "Video");
        Assert.Equal(404, (await c.Act(await c.DoctorTokenAsync(intruder), id, "start")).Status);
    }

    [Fact]
    public async Task No_show_only_after_the_time_and_frees_the_slot()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Today, Clinic.H0930);

        var dt = await c.DoctorTokenAsync(d);
        Assert.Equal("appointment_not_started", (await c.Act(dt, id, "no-show")).Code);

        c.Clock.Advance(TimeSpan.FromMinutes(20));
        dt = await c.DoctorTokenAsync(d);
        Assert.Equal("NoShow", (await c.Act(dt, id, "no-show")).Str("status"));
        var key = await c.WithDb(async db => (await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == id)).ActiveSlotKey);
        Assert.Null(key);
    }

    // ---------------------------------------------------------------- التقييم

    private static async Task<(Clinic c, Clinic.Doc d, Clinic.Pat p, int id)> DoneVisitAsync(ApiFactory root, decimal rating = 4.0m, int reviews = 10)
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(rating: rating, reviews: reviews);
        var p = await c.PatientAsync();
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);
        await c.WithDb(async db => { (await db.Appointments.FindAsync(id))!.Status = AppointmentStatus.Done; await db.SaveChangesAsync(); return 0; });
        return (c, d, p, id);
    }

    [Fact]
    public async Task Review_after_a_completed_visit_updates_the_doctor_average_once()
    {
        var (c, d, p, id) = await DoneVisitAsync(root, rating: 4.00m, reviews: 10);

        Assert.Equal(400, (await c.Act(p.Token, id, "review", new { rating = 6 })).Status);
        Assert.Equal(400, (await c.Act(p.Token, id, "review", new { rating = 0 })).Status);

        var ok = await c.Act(p.Token, id, "review", new { rating = 5, comment = "ممتاز" });
        Assert.Equal(201, ok.Status);
        Assert.Equal(5, ok["rating"].GetInt32());

        var doctor = await c.Http.GetApiAsync($"/api/doctors/{d.Id}", p.Token);
        Assert.Equal(11, doctor["summary"].GetProperty("reviewsCount").GetInt32());
        Assert.Equal(4.09m, doctor["summary"].GetProperty("rating").GetDecimal()); // (4.00×10 + 5) / 11
        Assert.Equal("ممتاز", doctor["latestReviews"][0].GetProperty("comment").GetString());
        Assert.Matches(@"^\S+ \S\.$", doctor["latestReviews"][0].GetProperty("patientName").GetString()!); // اسم مختصر

        var again = await c.Act(p.Token, id, "review", new { rating = 1 });
        Assert.Equal(409, again.Status);
        Assert.Equal("already_reviewed", again.Code);
        Assert.True((await c.Http.GetApiAsync($"/api/appointments/{id}", p.Token))["reviewed"].GetBoolean());
    }

    [Fact]
    public async Task Review_needs_a_completed_visit_and_its_owner()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var other = await c.PatientAsync();
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);

        Assert.Equal("invalid_state", (await c.Act(p.Token, id, "review", new { rating = 5 })).Code); // لم يُكمَل بعد
        Assert.Equal(404, (await c.Act(other.Token, id, "review", new { rating = 5 })).Status);
        Assert.Equal(403, (await c.Act(await c.DoctorTokenAsync(d), id, "review", new { rating = 5 })).Status);
    }
}
