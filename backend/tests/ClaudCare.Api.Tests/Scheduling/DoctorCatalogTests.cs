using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Scheduling;

[Collection(ApiCollection.Name)]
public class DoctorCatalogTests(ApiFactory root)
{
    [Fact]
    public async Task Specialties_list_has_names_and_counts()
    {
        var c = await Clinic.CreateAsync(root);
        await c.DoctorAsync(specialty: "dentistry");
        var p = await c.PatientAsync();
        var res = await c.Http.GetApiAsync("/api/specialties", p.Token);

        Assert.Equal(200, res.Status);
        var all = res.Body.EnumerateArray().ToList();
        Assert.True(all.Count >= 15);
        var dentistry = all.Single(s => s.GetProperty("key").GetString() == "dentistry");
        Assert.Equal("الأسنان", dentistry.GetProperty("nameAr").GetString());
        Assert.True(dentistry.GetProperty("doctorCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Catalog_requires_authentication()
    {
        var c = await Clinic.CreateAsync(root);
        Assert.Equal(401, (await c.Http.GetApiAsync("/api/doctors")).Status);
        Assert.Equal(401, (await c.Http.GetApiAsync("/api/specialties")).Status);
    }

    [Fact]
    public async Task Search_by_name_specialty_and_text()
    {
        var c = await Clinic.CreateAsync(root);
        var tag = TestData.Uniq();
        var a = await c.DoctorAsync(name: $"د. أمل {tag}", specialty: "ophthalmology");
        await c.DoctorAsync(name: $"د. بشير {tag}", specialty: "ent");
        var p = await c.PatientAsync();

        var byName = await c.Http.GetApiAsync($"/api/doctors?q={Uri.EscapeDataString("أمل " + tag)}", p.Token);
        Assert.Equal(1, byName["total"].GetInt32());
        Assert.Equal(a.Id, byName["items"][0].GetProperty("id").GetInt32());

        var bySpecialty = await c.Http.GetApiAsync($"/api/doctors?q={tag}&specialty=ent", p.Token);
        Assert.Equal(1, bySpecialty["total"].GetInt32());
        Assert.Equal("ent", bySpecialty["items"][0].GetProperty("specialtyKey").GetString());

        var bySpecialtyName = await c.Http.GetApiAsync($"/api/doctors?q={tag}&specialty=none", p.Token);
        Assert.Equal(0, bySpecialtyName["total"].GetInt32());
    }

    [Fact]
    public async Task Sorting_by_price_rating_and_experience()
    {
        var c = await Clinic.CreateAsync(root);
        var tag = TestData.Uniq();
        var cheap = await c.DoctorAsync(price: 100, rating: 4.1m, years: 3, name: $"د. رخيص {tag}");
        var best = await c.DoctorAsync(price: 500, rating: 5.0m, years: 10, name: $"د. أفضل {tag}");
        var senior = await c.DoctorAsync(price: 300, rating: 4.4m, years: 30, name: $"د. خبير {tag}");
        var p = await c.PatientAsync();

        async Task<int[]> Order(string sort) =>
            (await c.Http.GetApiAsync($"/api/doctors?q={tag}&sort={sort}", p.Token))["items"].EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToArray();

        Assert.Equal([cheap.Id, senior.Id, best.Id], await Order("price"));
        Assert.Equal([best.Id, senior.Id, cheap.Id], await Order("rating"));
        Assert.Equal([senior.Id, best.Id, cheap.Id], await Order("experience"));
    }

    [Fact]
    public async Task Paging_is_clamped_and_reports_totals()
    {
        var c = await Clinic.CreateAsync(root);
        var tag = TestData.Uniq();
        for (var i = 0; i < 5; i++) await c.DoctorAsync(name: $"د. صفحة {tag} {i}");
        var p = await c.PatientAsync();

        var first = await c.Http.GetApiAsync($"/api/doctors?q={tag}&pageSize=2&page=1", p.Token);
        Assert.Equal(5, first["total"].GetInt32());
        Assert.Equal(2, first["items"].GetArrayLength());
        Assert.Equal(3, first["totalPages"].GetInt32());
        var last = await c.Http.GetApiAsync($"/api/doctors?q={tag}&pageSize=2&page=3", p.Token);
        Assert.Equal(1, last["items"].GetArrayLength());

        var huge = await c.Http.GetApiAsync($"/api/doctors?q={tag}&pageSize=1000", p.Token);
        Assert.Equal(50, huge["pageSize"].GetInt32());
        var zero = await c.Http.GetApiAsync($"/api/doctors?q={tag}&page=0", p.Token);
        Assert.Equal(1, zero["page"].GetInt32());
    }

    [Fact]
    public async Task Unlisted_and_inactive_doctors_are_hidden_everywhere()
    {
        var c = await Clinic.CreateAsync(root);
        var tag = TestData.Uniq();
        var hidden = await c.DoctorAsync(name: $"د. مخفي {tag}", listed: false);
        var off = await c.DoctorAsync(name: $"د. معطل {tag}", active: false);
        var p = await c.PatientAsync();

        Assert.Equal(0, (await c.Http.GetApiAsync($"/api/doctors?q={tag}", p.Token))["total"].GetInt32());
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/doctors/{hidden.Id}", p.Token)).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/doctors/{off.Id}/availability", p.Token)).Status);
    }

    [Fact]
    public async Task Detail_includes_profile_hours_and_branch()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(price: 350, video: 250, years: 15, name: "د. تفصيل");
        var p = await c.PatientAsync();
        var res = await c.Http.GetApiAsync($"/api/doctors/{d.Id}", p.Token);

        Assert.Equal(200, res.Status);
        Assert.Equal("د. تفصيل", res["summary"].GetProperty("fullName").GetString());
        Assert.Equal(350, res["summary"].GetProperty("consultationPrice").GetDecimal());
        Assert.Equal(250, res["summary"].GetProperty("videoPrice").GetDecimal());
        Assert.Equal(5, res["workingHours"].GetArrayLength());
        Assert.Equal(30, res["slotMinutes"].GetInt32());
        Assert.False(string.IsNullOrEmpty(res.Str("branchName")));
        Assert.Equal(404, (await c.Http.GetApiAsync("/api/doctors/99999999", p.Token)).Status);
    }

    [Fact]
    public async Task Next_slot_is_today_when_open_otherwise_the_next_working_day()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();

        // 09:15 → أقرب خانة اليوم 09:30
        var open = await c.Http.GetApiAsync($"/api/doctors/{d.Id}", p.Token);
        Assert.Equal("2026-10-27", open["summary"].GetProperty("nextSlot").GetProperty("date").GetString());
        Assert.Equal(Clinic.H0930, open["summary"].GetProperty("nextSlot").GetProperty("startMinutes").GetInt32());

        // 17:00 (بعد الدوام) → الأربعاء 08:00
        c.SetLocal(17);
        p = await c.PatientAsync(); // الرمز السابق انتهت صلاحيته بتقديم الساعة
        var closed = await c.Http.GetApiAsync($"/api/doctors/{d.Id}", p.Token);
        Assert.Equal("2026-10-28", closed["summary"].GetProperty("nextSlot").GetProperty("date").GetString());
        Assert.Equal(480, closed["summary"].GetProperty("nextSlot").GetProperty("startMinutes").GetInt32());

        // الخميس 16:30 → الأحد (تخطي الجمعة والسبت)
        c.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 29, 13, 30, 0, TimeSpan.Zero));
        p = await c.PatientAsync();
        var weekend = await c.Http.GetApiAsync($"/api/doctors/{d.Id}", p.Token);
        Assert.Equal("2026-11-01", weekend["summary"].GetProperty("nextSlot").GetProperty("date").GetString());
    }

    [Fact]
    public async Task Doctor_without_working_hours_has_no_next_slot()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(days: []);
        var p = await c.PatientAsync();
        var res = await c.Http.GetApiAsync($"/api/doctors/{d.Id}", p.Token);
        Assert.True(res["summary"].GetProperty("nextSlot").ValueKind is System.Text.Json.JsonValueKind.Null);
    }

    // ---------------------------------------------------------------- الخانات المتاحة

    [Fact]
    public async Task Availability_returns_only_working_days_on_the_slot_grid_and_skips_the_past()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(start: 9 * 60, end: 11 * 60, slot: 30);
        var p = await c.PatientAsync();
        var res = await c.Http.GetApiAsync($"/api/doctors/{d.Id}/availability?days=7", p.Token);

        Assert.Equal(200, res.Status);
        var days = res["days"].EnumerateArray().ToList();
        // اليوم 09:15: المتبقي 09:30, 10:00, 10:30 (11:00 تنتهي بعد الدوام)
        Assert.Equal("2026-10-27", days[0].GetProperty("date").GetString());
        Assert.Equal([570, 600, 630], days[0].GetProperty("slots").EnumerateArray().Select(x => x.GetInt32()));
        // لا جمعة ولا سبت
        var dates = days.Select(x => x.GetProperty("date").GetString()).ToList();
        Assert.DoesNotContain("2026-10-30", dates);
        Assert.DoesNotContain("2026-10-31", dates);
        Assert.Contains("2026-11-01", dates);
        Assert.Equal([540, 570, 600, 630], days[1].GetProperty("slots").EnumerateArray().Select(x => x.GetInt32())); // الأربعاء كامل
    }

    [Fact]
    public async Task Booked_slots_disappear_and_cancelled_ones_come_back()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);

        async Task<List<int>> Slots() => (await c.Http.GetApiAsync($"/api/doctors/{d.Id}/availability?from=2026-10-28&days=1", p.Token))["days"][0].GetProperty("slots").EnumerateArray().Select(x => x.GetInt32()).ToList();

        Assert.DoesNotContain(Clinic.H10, await Slots());
        await c.Act(p.Token, id, "cancel", new { reason = "اختبار" });
        Assert.Contains(Clinic.H10, await Slots());
    }

    [Fact]
    public async Task Availability_respects_video_support_range_limits_and_leave()
    {
        var c = await Clinic.CreateAsync(root);
        var noVideo = await c.DoctorAsync(video: null);
        var p = await c.PatientAsync();

        var video = await c.Http.GetApiAsync($"/api/doctors/{noVideo.Id}/availability?type=Video", p.Token);
        Assert.Equal(400, video.Status);
        Assert.Equal("video_unavailable", video.Code);

        // from في الماضي يبدأ من اليوم؛ days تُقيَّد بحد أقصى
        var past = await c.Http.GetApiAsync($"/api/doctors/{noVideo.Id}/availability?from=2020-01-01&days=3", p.Token);
        Assert.Equal("2026-10-27", past["days"][0].GetProperty("date").GetString());
        var many = await c.Http.GetApiAsync($"/api/doctors/{noVideo.Id}/availability?days=9999", p.Token);
        Assert.True(many["days"].GetArrayLength() <= 60);

        // إجازة معتمدة تُسقط اليوم
        await c.WithDb(async db =>
        {
            var emp = new Employee { UserId = noVideo.Id, BranchId = c.BranchId, FullName = "طبيب", JobTitle = "طبيب", HireDate = new DateOnly(2020, 1, 1) };
            emp.Leaves(db, Clinic.Wed, Clinic.Wed);
            await db.SaveChangesAsync();
            return 0;
        });
        var withLeave = await c.Http.GetApiAsync($"/api/doctors/{noVideo.Id}/availability?from=2026-10-28&days=2", p.Token);
        var dates = withLeave["days"].EnumerateArray().Select(x => x.GetProperty("date").GetString()).ToList();
        Assert.DoesNotContain("2026-10-28", dates);
        Assert.Contains("2026-10-29", dates);
    }

    // ---------------------------------------------------------------- المفضلة

    [Fact]
    public async Task Favorites_add_list_filter_and_remove_are_idempotent()
    {
        var c = await Clinic.CreateAsync(root);
        var tag = TestData.Uniq();
        var d1 = await c.DoctorAsync(name: $"د. مفضل {tag} 1");
        var d2 = await c.DoctorAsync(name: $"د. مفضل {tag} 2");
        var p = await c.PatientAsync();

        Assert.Equal(204, (await c.Http.SendJsonAsync(HttpMethod.Put, $"/api/me/favorites/doctors/{d1.Id}", null, p.Token)).Status);
        Assert.Equal(204, (await c.Http.SendJsonAsync(HttpMethod.Put, $"/api/me/favorites/doctors/{d1.Id}", null, p.Token)).Status); // مكرر

        var list = await c.Http.GetApiAsync("/api/me/favorites/doctors", p.Token);
        Assert.Single(list.Body.EnumerateArray());
        Assert.True(list.Body[0].GetProperty("isFavorite").GetBoolean());

        var search = await c.Http.GetApiAsync($"/api/doctors?q={tag}", p.Token);
        var flags = search["items"].EnumerateArray().ToDictionary(x => x.GetProperty("id").GetInt32(), x => x.GetProperty("isFavorite").GetBoolean());
        Assert.True(flags[d1.Id]);
        Assert.False(flags[d2.Id]);

        var only = await c.Http.GetApiAsync($"/api/doctors?q={tag}&favoritesOnly=true", p.Token);
        Assert.Equal(1, only["total"].GetInt32());

        Assert.Equal(204, (await c.Http.SendJsonAsync(HttpMethod.Delete, $"/api/me/favorites/doctors/{d1.Id}", null, p.Token)).Status);
        Assert.Equal(204, (await c.Http.SendJsonAsync(HttpMethod.Delete, $"/api/me/favorites/doctors/{d1.Id}", null, p.Token)).Status);
        Assert.Empty((await c.Http.GetApiAsync("/api/me/favorites/doctors", p.Token)).Body.EnumerateArray());
    }

    [Fact]
    public async Task Favorites_are_private_and_patient_only()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var a = await c.PatientAsync();
        var b = await c.PatientAsync();
        await c.Http.SendJsonAsync(HttpMethod.Put, $"/api/me/favorites/doctors/{d.Id}", null, a.Token);

        Assert.Empty((await c.Http.GetApiAsync("/api/me/favorites/doctors", b.Token)).Body.EnumerateArray());
        var staff = await c.TokenAsync(UserRole.Reception);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/me/favorites/doctors", staff)).Status);
        Assert.Equal(404, (await c.Http.SendJsonAsync(HttpMethod.Put, "/api/me/favorites/doctors/99999999", null, a.Token)).Status);
    }

    // ---------------------------------------------------------------- دوام الطبيب

    [Fact]
    public async Task Doctor_reads_and_updates_working_hours_which_changes_availability()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync(start: 480, end: 960);
        var dt = await c.DoctorTokenAsync(d);
        var p = await c.PatientAsync();

        Assert.Equal(5, (await c.Http.GetApiAsync("/api/doctors/me/working-hours", dt)).Body.GetArrayLength());

        // الأربعاء عطلة، والخميس 10:00–12:00، والجمعة جديدة
        var update = await c.Http.SendJsonAsync(HttpMethod.Put, "/api/doctors/me/working-hours", new
        {
            days = new object[]
            {
                new { dayOfWeek = "Wednesday", isActive = false, startMinutes = 480, endMinutes = 960 },
                new { dayOfWeek = "Thursday", isActive = true, startMinutes = 600, endMinutes = 720 },
                new { dayOfWeek = "Friday", isActive = true, startMinutes = 600, endMinutes = 720 },
            },
        }, dt);
        Assert.Equal(200, update.Status);

        var avail = await c.Http.GetApiAsync($"/api/doctors/{d.Id}/availability?from=2026-10-28&days=3", p.Token);
        var days = avail["days"].EnumerateArray().ToDictionary(x => x.GetProperty("date").GetString()!, x => x.GetProperty("slots").EnumerateArray().Select(s => s.GetInt32()).ToList());
        Assert.False(days.ContainsKey("2026-10-28"));
        Assert.Equal([600, 630, 660, 690], days["2026-10-29"]);
        Assert.Equal([600, 630, 660, 690], days["2026-10-30"]);
    }

    [Fact]
    public async Task Working_hours_validation_and_future_appointments_warning()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var dt = await c.DoctorTokenAsync(d);
        var p = await c.PatientAsync();
        await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H09);

        var tooShort = await c.Http.SendJsonAsync(HttpMethod.Put, "/api/doctors/me/working-hours", new { days = new[] { new { dayOfWeek = "Monday", isActive = true, startMinutes = 600, endMinutes = 630 } } }, dt);
        Assert.Equal(400, tooShort.Status);
        var dup = await c.Http.SendJsonAsync(HttpMethod.Put, "/api/doctors/me/working-hours", new { days = new[] { new { dayOfWeek = "Monday", isActive = true, startMinutes = 480, endMinutes = 960 }, new { dayOfWeek = "Monday", isActive = true, startMinutes = 480, endMinutes = 960 } } }, dt);
        Assert.Equal(400, dup.Status);

        // تعطيل الأربعاء يبقي الموعد لكن يُنبَّه الطبيب
        var ok = await c.Http.SendJsonAsync(HttpMethod.Put, "/api/doctors/me/working-hours", new { days = new[] { new { dayOfWeek = "Wednesday", isActive = false, startMinutes = 480, endMinutes = 960 } } }, dt);
        Assert.Equal(200, ok.Status);
        Assert.Equal(1, ok["futureAppointmentsOutsideHours"].GetInt32());

        Assert.Equal(403, (await c.Http.GetApiAsync("/api/doctors/me/working-hours", p.Token)).Status);
    }
}

internal static class LeaveExtensions
{
    public static void Leaves(this Employee e, ClaudCare.Api.Data.ClaudCareDbContext db, DateOnly from, DateOnly to)
    {
        e.LeaveBalanceDays = 21;
        db.Employees.Add(e);
        db.LeaveRequests.Add(new LeaveRequest { Employee = e, Type = LeaveType.Annual, From = from, To = to, Days = to.DayNumber - from.DayNumber + 1, Status = LeaveStatus.Approved });
    }
}
