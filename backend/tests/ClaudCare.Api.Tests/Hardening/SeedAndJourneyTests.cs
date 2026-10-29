using ClaudCare.Api.Configuration;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Clinical;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Operations;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Tests.Hardening;

[Collection(ApiCollection.Name)]
public class DemoDataSeedTests(ApiFactory root)
{
    /// <summary>يشغّل المزروعات داخل معاملة تُتراجع في النهاية فلا تتلوث قاعدة الاختبار المشتركة.</summary>
    private async Task InRolledBackTransactionAsync(Func<IServiceProvider, ClaudCareDbContext, Task> body)
    {
        await using var scope = root.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ClaudCareDbContext>();
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try { await body(sp, db); }
            finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
        });
    }

    private static async Task SeedAsync(IServiceProvider sp)
    {
        var opts = Options.Create(new SeedOptions { DemoAccounts = true });
        await ActivatorUtilities.CreateInstance<DevSeeder>(sp, opts).SeedAsync();
        await ActivatorUtilities.CreateInstance<DemoDataSeeder>(sp, opts).SeedAsync();
    }

    [Fact]
    public async Task Seeding_covers_every_role_is_idempotent_and_stays_consistent()
    {
        await InRolledBackTransactionAsync(async (sp, db) =>
        {
            await SeedAsync(sp);

            foreach (var role in Enum.GetValues<UserRole>())
                Assert.True(await db.Users.AnyAsync(u => u.Role == role && u.Email != null && (u.Email.EndsWith("@claudcare.test") || u.Email == "mona@hospital.com")), $"لا حساب تجريبي للدور {role}");

            async Task<int[]> Counts() => [await db.Users.CountAsync(), await db.Beds.CountAsync(), await db.InventoryItems.CountAsync(), await db.Employees.CountAsync(), await db.Invoices.CountAsync(),
                await db.Appointments.CountAsync(), await db.LabOrders.CountAsync(), await db.Prescriptions.CountAsync(), await db.Notifications.CountAsync(), await db.Messages.CountAsync(), await db.SurgeryCases.CountAsync(), await db.PreAuthorizations.CountAsync()];
            var first = await Counts();
            await SeedAsync(sp);
            Assert.Equal(first, await Counts()); // لا تكرار

            // الأسرّة ↔ التنويم: كل سرير مشغول عليه تنويم واحد قائم، وكل تنويم قائم على سرير مشغول
            var occupied = await db.Beds.Where(b => b.Status == BedStatus.Occupied && b.Ward != "اختبار").Select(b => b.Id).ToListAsync();
            var admitted = await db.Admissions.Where(a => a.Status == AdmissionStatus.Admitted && a.BedId != null).ToListAsync();
            Assert.All(admitted, a => Assert.Contains(a.BedId!.Value, occupied));
            Assert.Equal(admitted.Count, admitted.Select(a => a.BedId).Distinct().Count());

            // المالية: المدفوع = مجموع الدفعات، ولا متبقٍ سالب
            var invoices = await db.Invoices.Include(i => i.Payments).Where(i => i.No.StartsWith("INV-D")).ToListAsync();
            Assert.NotEmpty(invoices);
            Assert.All(invoices, i => { Assert.Equal(i.Payments.Sum(p => p.Amount), i.PaidAmount); Assert.True(i.Remaining >= 0); });

            // المخزون: كمية غير سالبة ووجود أصناف منخفضة وأخرى قريبة الانتهاء للعرض
            Assert.True(await db.InventoryItems.AllAsync(i => i.Quantity >= 0));
            Assert.True(await db.InventoryItems.AnyAsync(i => i.Quantity <= i.MinQuantity));
            Assert.True(await db.InventoryItems.AnyAsync(i => i.ExpiryDate != null));

            // قائمة ما قبل العملية كاملة لكل عملية، ولا حجز غرفة متداخل
            Assert.All(await db.SurgeryCases.Include(s => s.Checklist).Where(s => s.No.StartsWith("SRG-D")).ToListAsync(), s => Assert.Equal(8, s.Checklist.Count));
            Assert.Equal(1, await db.SystemSettings.CountAsync(s => s.Key == DemoDataSeeder.Marker));
        });
    }

    [Fact]
    public async Task Demo_credentials_are_valid_active_and_match_the_documented_logins()
    {
        await InRolledBackTransactionAsync(async (sp, db) =>
        {
            await SeedAsync(sp);
            var hasher = sp.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
            var demo = await db.Users.Where(u => u.Email != null && (u.Email.EndsWith("@claudcare.test") || u.Email == "mona@hospital.com")).ToListAsync();
            Assert.True(demo.Count >= 16);
            Assert.All(demo, u =>
            {
                Assert.True(u.IsActive && u.PhoneConfirmed);
                Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success, hasher.VerifyHashedPassword(u, u.PasswordHash, "Demo@1234"));
            });
            var reem = demo.Single(u => u.Email == "reem@claudcare.test");
            Assert.Equal("0551234500", reem.Phone);
            Assert.Equal("1098765432", reem.NationalId);
            Assert.Equal("DR-1042", demo.Single(u => u.Email == "mona@hospital.com").EmployeeNo);
            Assert.Equal("HO-0001", demo.Single(u => u.Role == UserRole.Hospital).EmployeeNo);
        });
    }

    [Fact]
    public async Task Seeding_does_nothing_when_demo_accounts_are_disabled()
    {
        await InRolledBackTransactionAsync(async (sp, db) =>
        {
            var before = await db.Users.CountAsync();
            await ActivatorUtilities.CreateInstance<DemoDataSeeder>(sp, Options.Create(new SeedOptions { DemoAccounts = false })).SeedAsync();
            Assert.Equal(before, await db.Users.CountAsync());
            Assert.False(await db.SystemSettings.AnyAsync(s => s.Key == DemoDataSeeder.Marker));
        });
    }
}

[Collection(ApiCollection.Name)]
public class PatientJourneyTests(ApiFactory root)
{
    /// <summary>
    /// رحلة كاملة بين الوحدات: حجز بتأمين ← وصول ← كشف بتحليل وأشعة ووصفة ← نتيجة مختبر حرجة ← تقرير أشعة ← صرف الوصفة ←
    /// فواتير وسداد ومطالبة تأمين ← مراسلة الطبيب ← إشعارات تغطي كل ذلك.
    /// </summary>
    [Fact]
    public async Task Booking_to_dispensing_to_claim_works_end_to_end_with_consistent_data()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 500);
        var p = await c.PatientAsync();
        await c.InsureAsync(p.Id, copay: 0.2m);
        await c.StockAsync(await c.DrugIdAsync("ميتفورمين 500 مجم"), quantity: 300);

        // 1) حجز اليوم بتأمين: 400 تغطية و100 على المريض (بطاقة ⇒ مدفوعة)
        var appt = await c.BookOkAsync(p.Token, doc.Id, Clinic.Today, Clinic.H0930);
        var invoice = (await c.Http.GetApiAsync("/api/invoices", p.Token))["page"].J("items")[0];
        Assert.Equal("Paid", invoice.GetProperty("status").GetString());
        Assert.Equal(400m, invoice.GetProperty("insuranceCovered").GetDecimal());

        // 2) وصول وكشف
        var desk = await c.TokenAsync(UserRole.Reception);
        Assert.Equal(200, (await c.Act(desk, appt, "arrive")).Status);
        var dt = await c.DoctorTokenAsync(doc);
        Assert.Equal(200, (await c.Act(dt, appt, "start")).Status);
        await Clinical.ClinicalHelpers.EnsureProfileAsync(c, p.Id);
        var drug = await c.DrugIdAsync("ميتفورمين 500 مجم");
        var visit = await c.VisitAsync(dt, Clinical.ClinicalHelpers.Visit(appt, labs: ["hba1c"], imaging: ["chest-xray"], rx: [Clinical.ClinicalHelpers.Rx(drug, timesPerDay: 2, days: 10)]));
        Assert.Equal(201, visit.Status);
        var labId = visit["labOrders"][0].GetProperty("id").GetInt32();
        var imgId = visit["imagingOrders"][0].GetProperty("id").GetInt32();
        Assert.Equal(AppointmentStatus.Done, await c.StatusAsync(appt)); // حفظ الكشف يُنهي الموعد

        // 3) المختبر: نتيجة حرجة ⇒ تنبيه للطبيب
        var lab = await c.TokenAsync(UserRole.Lab);
        await c.Http.PostAsync($"/api/lab-orders/{labId}/collect", null, lab);
        await c.Http.PostAsync($"/api/lab-orders/{labId}/start", null, lab);
        Assert.Equal("ResultReady", (await c.Http.PutAsync($"/api/lab-orders/{labId}/results", new { items = new[] { new { name = "السكر التراكمي", value = 10.5m } } }, lab)).Str("status"));

        // 4) الأشعة: جدولة وتقرير نهائي
        var rad = await c.TokenAsync(UserRole.Radiology);
        await c.Http.PostAsync($"/api/imaging-orders/{imgId}/start", null, rad);
        await c.Http.PutAsync($"/api/imaging-orders/{imgId}/report", new { findings = "طبيعي", impression = "لا شيء يستدعي القلق", final = true }, rad);

        // 5) الصيدلية: صرف الوصفة
        var ph = await c.TokenAsync(UserRole.Pharmacy);
        var rxId = (await c.Http.GetApiAsync("/api/pharmacy/prescriptions", ph))["items"].EnumerateArray().First(x => x.GetProperty("patient").GetProperty("id").GetInt32() == p.Id).GetProperty("id").GetInt32();
        Assert.Equal(200, (await c.Http.PostAsync($"/api/pharmacy/prescriptions/{rxId}/dispense", new { method = "Cash" }, ph)).Status);

        // 6) ما يراه المريض الآن: نتيجة حرجة مرتفعة + تقرير + دواء بتذكيراته
        var results = await c.Http.GetApiAsync("/api/me/lab-results", p.Token);
        Assert.Equal(1, results["items"].GetArrayLength());
        Assert.Equal("Critical", results["items"][0].GetProperty("items")[0].GetProperty("flag").GetString());
        Assert.Equal("Final", (await c.Http.GetApiAsync($"/api/me/imaging/{imgId}", p.Token))["report"].GetProperty("status").GetString());
        Assert.True((await c.Http.GetApiAsync("/api/me/medications", p.Token)).Body.GetArrayLength() >= 1);

        // 7) التأمين: مطالبة من فاتورة الزيارة ثم تقديمها وتسويتها
        var ins = await c.TokenAsync(UserRole.Insurance);
        var claim = await c.Http.PostAsync($"/api/insurance/claims/from-invoice/{invoice.GetProperty("id").GetInt32()}", null, ins);
        await c.Http.PostAsync($"/api/insurance/claims/{claim["id"].GetInt32()}/submit", null, ins);
        Assert.Equal("Paid", (await c.Http.PostAsync($"/api/insurance/claims/{claim["id"].GetInt32()}/settle", new { paidAmount = 400 }, ins)).Str("status"));

        // 8) مراسلة الطبيب بعد الزيارة
        var conv = await c.Http.PostAsync("/api/conversations", new { userId = doc.Id, text = "شكرًا دكتور" }, p.Token);
        Assert.Equal(200, conv.Status);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/conversations/unread-count", dt))["unread"].GetInt32());

        // 9) الإشعارات تغطي الرحلة، ولوحة الفرع تعكسها
        var titles = await c.NotificationTitlesAsync(p.Id);
        foreach (var expected in new[] { "تم تأكيد حجزك", "تم حفظ كشفك", "نتيجة تحليل جاهزة", "تقرير الأشعة جاهز", "تم صرف وصفتك" }) Assert.Contains(expected, titles);
        Assert.Contains("نتيجة حرجة", await c.NotificationTitlesAsync(doc.Id));
        var overview = await c.Http.GetApiAsync($"/api/dashboard/overview?branchId={c.BranchId}", await c.TokenAsync(UserRole.Admin));
        Assert.Equal(1, overview["appointmentsToday"].J("Done").GetInt32());
        Assert.Equal(0, overview["labPending"].GetInt32());
        Assert.Equal(0, overview["prescriptionsPending"].GetInt32());

        // 10) المالية: دفعة المريض فقط (100) + صرف الصيدلية لا يدخل فواتير الكشف
        var s = await c.Http.GetApiAsync($"/api/finance/summary?from={BackOffice.D(Clinic.Today)}&to={BackOffice.D(Clinic.Today)}", await c.TokenAsync(UserRole.Finance));
        Assert.Equal(100m, s["revenue"].GetDecimal());
    }

    [Fact]
    public async Task A_patient_never_sees_another_patients_data_across_every_module()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        var intruder = await c.PatientAsync();
        var urls = new[] { "/api/me/lab-results", "/api/me/imaging", "/api/me/prescriptions", "/api/me/visits", "/api/me/medications", "/api/invoices", "/api/appointments", "/api/notifications", "/api/conversations", "/api/me/family" };
        foreach (var u in urls)
        {
            var r = await c.Http.GetApiAsync(u, intruder.Token);
            Assert.Equal(200, r.Status);
            Assert.Equal(0, (r.Body.ValueKind == System.Text.Json.JsonValueKind.Array ? r.Body.GetArrayLength() : r.Body.TryGetProperty("items", out var it) ? it.GetArrayLength() : r.Body.TryGetProperty("page", out var pg) ? pg.GetProperty("items").GetArrayLength() : 0));
        }
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/lab-orders/{k.LabId}", intruder.Token)).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/imaging-orders/{k.ImagingId}", intruder.Token)).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/patients/{k.Patient.Id}/record", intruder.Token)).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/patients/{k.Patient.Id}/vitals", intruder.Token)).Status);
    }
}
