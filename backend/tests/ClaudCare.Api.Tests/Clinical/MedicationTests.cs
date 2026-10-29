using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;
using static ClaudCare.Api.Tests.Clinical.ClinicalHelpers;

namespace ClaudCare.Api.Tests.Clinical;

[Collection(ApiCollection.Name)]
public class MedicationTests(ApiFactory root)
{
    /// <summary>مريض بوصفة: ميتفورمين مرتين يوميًا 30 يومًا، فيتامين د مرة 10 أيام، باراسيتامول عند الحاجة.</summary>
    private static async Task<(Clinic c, Clinic.Doc d, Clinic.Pat p, string dt)> SetupAsync(ApiFactory root, int vitDays = 10)
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id);
        var (appt, dt) = await c.InProgressAsync(d, p);
        var res = await c.VisitAsync(dt, Visit(appt, rx:
        [
            Rx(await c.DrugIdAsync("ميتفورمين 500 مجم"), timesPerDay: 2, days: 30),
            Rx(await c.DrugIdAsync("فيتامين د 50,000"), frequency: "مرة يوميًا", timesPerDay: 1, days: vitDays),
            Rx(await c.DrugIdAsync("باراسيتامول 500 مجم"), frequency: "عند الحاجة", timesPerDay: 0, days: 5),
        ]));
        Assert.Equal(201, res.Status);
        return (c, d, p, dt);
    }

    private static async Task<int> MedIdAsync(Clinic c, Clinic.Pat p, string namePart) =>
        (await c.Http.GetApiAsync("/api/me/medications", p.Token)).Body.EnumerateArray().First(m => m.GetProperty("name").GetString()!.Contains(namePart)).GetProperty("id").GetInt32();

    [Fact]
    public async Task Medication_list_shows_schedule_days_left_and_the_as_needed_drug_without_doses()
    {
        var (c, _, p, _) = await SetupAsync(root, vitDays: 4);
        var meds = (await c.Http.GetApiAsync("/api/me/medications", p.Token)).Body.EnumerateArray().ToList();
        Assert.Equal(3, meds.Count);

        var vit = meds.Single(m => m.GetProperty("name").GetString()!.Contains("فيتامين"));
        Assert.Equal(4, vit.GetProperty("daysLeft").GetInt32());
        Assert.True(vit.GetProperty("endingSoon").GetBoolean());

        var para = meds.Single(m => m.GetProperty("name").GetString()!.Contains("باراسيتامول"));
        Assert.Empty(para.GetProperty("times").EnumerateArray());

        var today = await c.Http.GetApiAsync("/api/me/medications/today", p.Token);
        Assert.Equal(3, today["total"].GetInt32());              // ميتفورمين×2 + فيتامين×1، والمسكن بلا جدول
        Assert.Equal(0, today["taken"].GetInt32());
        Assert.Equal(0m, today["adherence"].GetDecimal());
    }

    [Fact]
    public async Task Todays_doses_are_missed_upcoming_or_taken_and_adherence_follows()
    {
        var (c, _, p, _) = await SetupAsync(root);
        var met = await MedIdAsync(c, p, "ميتفورمين");

        // 09:15: جرعة 08:00 فائتة، و20:00 قادمة، وفيتامين 08:00 فائتة
        var before = await c.Http.GetApiAsync("/api/me/medications/today", p.Token);
        var states = before["doses"].EnumerateArray().Select(x => (x.GetProperty("slotIndex").GetInt32(), x.GetProperty("minutes").GetInt32(), x.GetProperty("state").GetString())).ToList();
        Assert.Contains((0, 480, "Missed"), states);
        Assert.Contains((1, 1200, "Upcoming"), states);

        var taken = await c.Http.PostAsync($"/api/me/medications/{met}/doses", new { slotIndex = 0, taken = true }, p.Token);
        Assert.Equal(200, taken.Status);
        Assert.Equal(1, taken["taken"].GetInt32());
        Assert.Equal(0.33m, taken["adherence"].GetDecimal());

        // التكرار آمن (idempotent)
        Assert.Equal(1, (await c.Http.PostAsync($"/api/me/medications/{met}/doses", new { slotIndex = 0, taken = true }, p.Token))["taken"].GetInt32());
        // التراجع
        Assert.Equal(0, (await c.Http.PostAsync($"/api/me/medications/{met}/doses", new { slotIndex = 0, taken = false }, p.Token))["taken"].GetInt32());
    }

    [Fact]
    public async Task Dose_logging_rules()
    {
        var (c, _, p, _) = await SetupAsync(root);
        var met = await MedIdAsync(c, p, "ميتفورمين");
        var other = await c.PatientAsync();

        Assert.Equal(400, (await c.Http.PostAsync($"/api/me/medications/{met}/doses", new { slotIndex = 5 }, p.Token)).Status);         // جرعة غير موجودة
        Assert.Equal(400, (await c.Http.PostAsync($"/api/me/medications/{met}/doses", new { taken = true }, p.Token)).Status);          // slotIndex مطلوب
        Assert.Equal("date_invalid", (await c.Http.PostAsync($"/api/me/medications/{met}/doses", new { slotIndex = 0, date = "2026-10-20" }, p.Token)).Code); // قبل أكثر من يوم
        Assert.Equal("date_invalid", (await c.Http.PostAsync($"/api/me/medications/{met}/doses", new { slotIndex = 0, date = "2026-10-28" }, p.Token)).Code); // مستقبل
        Assert.Equal(404, (await c.Http.PostAsync($"/api/me/medications/{met}/doses", new { slotIndex = 0 }, other.Token)).Status);     // دواء غيره
        // أمس خارج مدة العلاج (بدأت اليوم)
        Assert.Equal("date_invalid", (await c.Http.PostAsync($"/api/me/medications/{met}/doses", new { slotIndex = 0, date = "2026-10-26" }, p.Token)).Code);
    }

    [Fact]
    public async Task Reminder_toggle()
    {
        var (c, _, p, _) = await SetupAsync(root);
        var met = await MedIdAsync(c, p, "ميتفورمين");
        var off = await c.Http.SendJsonAsync(HttpMethod.Patch, $"/api/me/medications/{met}", new { reminderEnabled = false }, p.Token);
        Assert.Equal(200, off.Status);
        Assert.False(off["reminderEnabled"].GetBoolean());
        Assert.Equal(400, (await c.Http.SendJsonAsync(HttpMethod.Patch, $"/api/me/medications/{met}", new { }, p.Token)).Status);
    }

    [Fact]
    public async Task Refill_flow_request_then_doctor_approves_and_extends_the_course()
    {
        var (c, d, p, dt) = await SetupAsync(root, vitDays: 4);
        var vit = await MedIdAsync(c, p, "فيتامين");

        var req = await c.Http.PostAsync($"/api/me/medications/{vit}/refill", null, p.Token);
        Assert.Equal(200, req.Status);
        Assert.Equal("Requested", req.Str("refillStatus"));
        var again = await c.Http.PostAsync($"/api/me/medications/{vit}/refill", null, p.Token);
        Assert.Equal(409, again.Status);
        Assert.Equal("refill_pending", again.Code);

        var queue = await c.Http.GetApiAsync("/api/refill-requests", dt);
        Assert.Equal(1, queue.Body.GetArrayLength());
        Assert.Equal(p.Id, queue.Body[0].GetProperty("patient").GetProperty("id").GetInt32());
        Assert.Empty((await c.Http.GetApiAsync("/api/refill-requests", await c.DoctorTokenAsync(await c.DoctorAsync()))).Body.EnumerateArray()); // طبيب آخر لا يرى

        var approve = await c.Http.PostAsync($"/api/refill-requests/{vit}/approve", new { days = 20 }, dt);
        Assert.Equal(204, approve.Status);
        var after = (await c.Http.GetApiAsync("/api/me/medications", p.Token)).Body.EnumerateArray().First(m => m.GetProperty("id").GetInt32() == vit);
        Assert.Equal(24, after.GetProperty("daysLeft").GetInt32()); // 4 + 20
        Assert.False(after.GetProperty("endingSoon").GetBoolean());
        Assert.Equal("Approved", after.GetProperty("refillStatus").GetString());
        Assert.Contains("تمت الموافقة على إعادة الصرف", await c.NotificationTitlesAsync(p.Id));

        // لا طلب معلّق الآن
        Assert.Equal(409, (await c.Http.PostAsync($"/api/refill-requests/{vit}/approve", new { }, dt)).Status);
        // ويمكن طلب إعادة صرف جديدة
        Assert.Equal(200, (await c.Http.PostAsync($"/api/me/medications/{vit}/refill", null, p.Token)).Status);
        _ = d;
    }

    [Fact]
    public async Task Rejected_refill_notifies_with_the_reason_and_default_extension_equals_the_original_duration()
    {
        var (c, _, p, dt) = await SetupAsync(root, vitDays: 10);
        var vit = await MedIdAsync(c, p, "فيتامين");
        var met = await MedIdAsync(c, p, "ميتفورمين");

        await c.Http.PostAsync($"/api/me/medications/{vit}/refill", null, p.Token);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/refill-requests/{vit}/reject", new { reason = "" }, dt)).Status);
        Assert.Equal(204, (await c.Http.PostAsync($"/api/refill-requests/{vit}/reject", new { reason = "يلزم كشف جديد" }, dt)).Status);
        var rejected = (await c.Http.GetApiAsync("/api/me/medications", p.Token)).Body.EnumerateArray().First(m => m.GetProperty("id").GetInt32() == vit);
        Assert.Equal("Rejected", rejected.GetProperty("refillStatus").GetString());
        Assert.Contains("رُفض طلب إعادة الصرف", await c.NotificationTitlesAsync(p.Id));

        await c.Http.PostAsync($"/api/me/medications/{met}/refill", null, p.Token);
        await c.Http.PostAsync($"/api/refill-requests/{met}/approve", new { }, dt); // بلا days → مدة الوصفة الأصلية (30)
        var after = (await c.Http.GetApiAsync("/api/me/medications", p.Token)).Body.EnumerateArray().First(m => m.GetProperty("id").GetInt32() == met);
        Assert.Equal(60, after.GetProperty("daysLeft").GetInt32());
    }

    [Fact]
    public async Task Ended_courses_are_listed_separately_and_cannot_be_refilled()
    {
        var (c, _, p, _) = await SetupAsync(root);
        var vit = await MedIdAsync(c, p, "فيتامين");
        await c.WithDb(async db => { (await db.PatientMedications.FindAsync(vit))!.EndDate = new DateOnly(2026, 10, 20); await db.SaveChangesAsync(); return 0; });

        var active = (await c.Http.GetApiAsync("/api/me/medications?active=true", p.Token)).Body.EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();
        var ended = (await c.Http.GetApiAsync("/api/me/medications?active=false", p.Token)).Body.EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();
        Assert.DoesNotContain(vit, active);
        Assert.Equal([vit], ended);
        Assert.Equal("medication_ended", (await c.Http.PostAsync($"/api/me/medications/{vit}/refill", null, p.Token)).Code);
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/me/medications?active=false", p.Token)).Body[0].GetProperty("daysLeft").GetInt32());
    }
}
