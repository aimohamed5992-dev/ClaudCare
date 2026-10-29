using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;
using static ClaudCare.Api.Tests.Clinical.ClinicalHelpers;

namespace ClaudCare.Api.Tests.Clinical;

[Collection(ApiCollection.Name)]
public class PatientRecordTests(ApiFactory root)
{
    private static async Task<Clinic.Pat> RelatedPatientAsync(Clinic c, Clinic.Doc d, string name, int? birthYear = null, string[]? allergies = null, string[]? conditions = null, int slot = Clinic.H10)
    {
        var p = await c.PatientAsync();
        await c.WithDb(async db => { (await db.Users.FirstAsync(u => u.Id == p.Id)).FullName = name; await db.SaveChangesAsync(); return 0; });
        await c.EnsureProfileAsync(p.Id, allergies, conditions, birthYear: birthYear);
        await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, slot);
        return p;
    }

    [Fact]
    public async Task Doctor_lists_only_their_patients_with_filters_search_and_paging()
    {
        var c = await Clinic.CreateAsync(root);
        var tag = TestData.Uniq();
        var d = await c.DoctorAsync();
        var dt = await c.DoctorTokenAsync(d);
        var senior = await RelatedPatientAsync(c, d, $"عمّ {tag}", birthYear: 1950, conditions: ["قصور في القلب"], slot: Clinic.H09);
        var kid = await RelatedPatientAsync(c, d, $"طفل {tag}", birthYear: 2019, allergies: ["الغبار"], slot: Clinic.H10);
        var adult = await RelatedPatientAsync(c, d, $"بالغ {tag}", birthYear: 1990, slot: Clinic.H11);
        var stranger = await c.PatientAsync();
        await c.WithDb(async db => { (await db.Users.FirstAsync(u => u.Id == stranger.Id)).FullName = $"غريب {tag}"; await db.SaveChangesAsync(); return 0; });

        async Task<List<int>> Ids(string q) => (await c.Http.GetApiAsync($"/api/patients?q={tag}{q}", dt))["items"].EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();

        var all = await Ids("");
        Assert.Equal(3, all.Count);
        Assert.DoesNotContain(stranger.Id, all);
        Assert.Equal([senior.Id], await Ids("&filter=seniors"));
        Assert.Equal([kid.Id], await Ids("&filter=kids"));
        Assert.Equal([senior.Id], await Ids("&filter=chronic"));
        Assert.Equal([kid.Id], await Ids("&filter=allergy"));

        // البحث بالمرض (سريري) وبالجوال ورقم الملف
        Assert.Equal([senior.Id], (await c.Http.GetApiAsync($"/api/patients?q={Uri.EscapeDataString("قصور في القلب")}", dt))["items"].EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).Where(i => i == senior.Id).ToList());
        Assert.Equal([adult.Id], (await c.Http.GetApiAsync($"/api/patients?q={adult.Phone}", dt))["items"].EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList());

        var one = (await c.Http.GetApiAsync($"/api/patients?q={tag}&filter=chronic", dt))["items"][0];
        Assert.Equal("قصور في القلب", one.GetProperty("mainCondition").GetString());
        Assert.False(one.GetProperty("hasAllergies").GetBoolean());
        Assert.InRange(one.GetProperty("age").GetInt32(), 70, 80);

        var page = await c.Http.GetApiAsync($"/api/patients?q={tag}&pageSize=2&page=2", dt);
        Assert.Equal(1, page["items"].GetArrayLength());
        Assert.Equal(3, page["total"].GetInt32());
    }

    [Fact]
    public async Task Cancelled_appointments_do_not_create_a_care_relationship()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var id = await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);
        var dt = await c.DoctorTokenAsync(d);
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/record", dt)).Status);
        await c.Act(p.Token, id, "cancel", new { reason = "ظرف" });
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/record", dt)).Status);
    }

    [Fact]
    public async Task Desk_staff_search_everyone_but_see_no_clinical_details()
    {
        var c = await Clinic.CreateAsync(root);
        var tag = TestData.Uniq();
        var d = await c.DoctorAsync();
        var p = await RelatedPatientAsync(c, d, $"مريض {tag}", conditions: ["سكري"], allergies: ["البنسلين"]);
        var desk = await c.TokenAsync(UserRole.Reception);

        var res = await c.Http.GetApiAsync($"/api/patients?q={tag}", desk);
        Assert.Equal(1, res["total"].GetInt32());
        var item = res["items"][0];
        Assert.True(item.GetProperty("mainCondition").ValueKind is System.Text.Json.JsonValueKind.Null);
        Assert.False(item.GetProperty("hasAllergies").GetBoolean());
        // التصفية السريرية تُتجاهل، والبحث بالمرض لا يطابق للاستقبال
        Assert.Equal(1, (await c.Http.GetApiAsync($"/api/patients?q={tag}&filter=chronic", desk))["total"].GetInt32());
        Assert.Equal(0, (await c.Http.GetApiAsync($"/api/patients?q={Uri.EscapeDataString("سكري")}&pageSize=50", desk))["items"].EnumerateArray().Count(x => x.GetProperty("id").GetInt32() == p.Id));
        // الملف السريري ممنوع (يظهر غير موجود)
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/record", desk)).Status);
        // المختبر والصيدلية والمرضى لا يبحثون
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/patients", await c.TokenAsync(UserRole.Lab))).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/patients", p.Token)).Status);
    }

    [Fact]
    public async Task Record_overview_for_the_doctor_nurse_and_the_patient_but_nobody_else()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var other = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var stranger = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id, allergies: ["البنسلين"], conditions: ["سكري النوع الثاني", "ارتفاع الكوليسترول"], blood: "A+", birthYear: 1970);
        var met = await c.DrugIdAsync("ميتفورمين 500 مجم");
        var (appt, dt) = await c.InProgressAsync(d, p);
        await c.VisitAsync(dt, Visit(appt, vitals: new { systolicBp = 135, diastolicBp = 85, heartRate = 78 }, rx: [Rx(met, days: 30)]));

        var rec = await c.Http.GetApiAsync($"/api/patients/{p.Id}/record", dt);
        Assert.Equal(200, rec.Status);
        Assert.Equal("A+", rec["summary"].GetProperty("bloodType").GetString());
        Assert.Equal("سكري النوع الثاني", rec["summary"].GetProperty("mainCondition").GetString());
        Assert.Equal(["البنسلين"], rec["allergies"].EnumerateArray().Select(x => x.GetProperty("name").GetString()!));
        Assert.Equal(2, rec["conditions"].GetArrayLength());
        Assert.Equal(135, rec["latestVitals"].GetProperty("systolicBp").GetInt32());
        Assert.Equal(1, rec["activeMedications"].GetArrayLength());
        Assert.Equal(1, rec["visitsCount"].GetInt32());
        Assert.Equal(1, rec["prescriptionsCount"].GetInt32());

        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/record", await c.TokenAsync(UserRole.Nurse))).Status);
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/record", p.Token)).Status); // نفسه
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/record", stranger.Token)).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/record", await c.DoctorTokenAsync(other))).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/record", await c.TokenAsync(UserRole.Lab))).Status); // المختبر لا يدخل من هنا (يظهر غير موجود)
        Assert.Equal(404, (await c.Http.GetApiAsync("/api/patients/99999999/record", dt)).Status);
    }

    [Fact]
    public async Task Clinical_profile_updates_replace_lists_and_validate()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id, allergies: ["الغبار"]);
        await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);
        var dt = await c.DoctorTokenAsync(d);

        var bad = await c.Http.SendJsonAsync(HttpMethod.Put, $"/api/patients/{p.Id}/clinical-profile", new { bloodType = "Z+" }, dt);
        Assert.Equal(400, bad.Status);

        var ok = await c.Http.SendJsonAsync(HttpMethod.Put, $"/api/patients/{p.Id}/clinical-profile", new
        {
            bloodType = "O+", heightCm = 165, weightKg = 61.5,
            allergies = new[] { new { name = "البنسلين", reaction = "طفح جلدي" }, new { name = "البنسلين", reaction = "مكرر" } },
            conditions = new[] { new { name = "مقدمات السكري", sinceYear = 2023 } },
        }, dt);
        Assert.Equal(200, ok.Status);
        Assert.Equal("O+", ok.Str("bloodType"));
        Assert.Equal(["البنسلين"], ok["allergies"].EnumerateArray().Select(x => x.GetProperty("name").GetString()!)); // الغبار حُذف والمكرر أُسقط
        Assert.Equal(61.5m, ok["weightKg"].GetDecimal());

        // التحديث الجزئي (بدون قوائم) لا يمسّ القوائم
        var partial = await c.Http.SendJsonAsync(HttpMethod.Put, $"/api/patients/{p.Id}/clinical-profile", new { heightCm = 170 }, dt);
        Assert.Equal(1, partial["allergies"].GetArrayLength());
        Assert.Equal(1, partial["conditions"].GetArrayLength());

        Assert.Equal(403, (await c.Http.SendJsonAsync(HttpMethod.Put, $"/api/patients/{p.Id}/clinical-profile", new { heightCm = 170 }, p.Token)).Status);
        Assert.Equal(403, (await c.Http.SendJsonAsync(HttpMethod.Put, $"/api/patients/{p.Id}/clinical-profile", new { heightCm = 170 }, await c.TokenAsync(UserRole.Admissions))).Status);
        Assert.Equal(200, (await c.Http.SendJsonAsync(HttpMethod.Put, $"/api/patients/{p.Id}/clinical-profile", new { heightCm = 171 }, await c.TokenAsync(UserRole.Nurse))).Status);

        // وبعد الحفظ يمنع النظام وصف البنسلين
        var amox = await c.DrugIdAsync("أموكسيسيلين 500 مجم");
        var (appt, dt2) = await c.InProgressAsync(d, p, Clinic.H11);
        Assert.Equal("allergy_conflict", (await c.VisitAsync(dt2, Visit(appt, rx: [Rx(amox)]))).Code);
    }

    [Fact]
    public async Task Lab_and_imaging_results_hide_unfinished_work_and_flag_abnormal_values()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id);
        var (appt, dt) = await c.InProgressAsync(d, p);
        var v = await c.VisitAsync(dt, Visit(appt, labs: ["cbc", "vitamin-d"], imaging: ["chest-xray", "ecg"]));
        var cbc = v["labOrders"][0].GetProperty("id").GetInt32();
        var vit = v["labOrders"][1].GetProperty("id").GetInt32();
        var chest = v["imagingOrders"][0].GetProperty("id").GetInt32();
        var ecg = v["imagingOrders"][1].GetProperty("id").GetInt32();

        await c.WithDb(async db =>
        {
            var l = await db.LabOrders.Include(x => x.Items).FirstAsync(x => x.Id == vit);
            l.Status = LabOrderStatus.ResultReady;
            l.ResultAt = DateTime.UtcNow;
            l.Items.Add(new LabResultItem { Name = "فيتامين د", Value = 14, Unit = "نانوجرام/مل", RefMin = 30, RefMax = 100, Flag = ResultFlag.Critical });
            l.Items.Add(new LabResultItem { Name = "كالسيوم", Value = 9.5m, Unit = "مجم/دل", RefMin = 8.5m, RefMax = 10.5m, Flag = ResultFlag.Normal });
            var rep = await db.ImagingOrders.FirstAsync(x => x.Id == chest);
            rep.Status = ImagingOrderStatus.Reported;
            rep.Report = new ImagingReport { Findings = "الحقول الرئوية صافية.", Impression = "أشعة صدر طبيعية.", Status = ReportStatus.Final, ReportedAt = DateTime.UtcNow };
            var draft = await db.ImagingOrders.FirstAsync(x => x.Id == ecg);
            draft.Report = new ImagingReport { Findings = "مسودة", Impression = "مسودة", Status = ReportStatus.Draft };
            await db.SaveChangesAsync();
            return 0;
        });

        var labs = await c.Http.GetApiAsync($"/api/patients/{p.Id}/lab-orders", dt);
        var byId = labs["items"].EnumerateArray().ToDictionary(x => x.GetProperty("id").GetInt32());
        Assert.False(byId[cbc].GetProperty("ready").GetBoolean());
        Assert.Empty(byId[cbc].GetProperty("items").EnumerateArray());
        Assert.True(byId[vit].GetProperty("ready").GetBoolean());
        Assert.Equal(1, byId[vit].GetProperty("abnormalCount").GetInt32());
        Assert.Equal("Critical", byId[vit].GetProperty("items")[0].GetProperty("flag").GetString());

        // تفاصيل طلب + منع الوصول لطلب مريض آخر
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/me/lab-results/{vit}", p.Token)).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/me/lab-results/{vit}", (await c.PatientAsync()).Token)).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/lab-orders/99999999", dt)).Status);

        var imaging = (await c.Http.GetApiAsync("/api/me/imaging", p.Token))["items"].EnumerateArray().ToDictionary(x => x.GetProperty("id").GetInt32());
        Assert.Equal("أشعة صدر طبيعية.", imaging[chest].GetProperty("report").GetProperty("impression").GetString());
        Assert.True(imaging[ecg].GetProperty("report").ValueKind is System.Text.Json.JsonValueKind.Null); // المسودة لا تُعرض
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/me/imaging/{chest}", p.Token)).Status);
    }

    [Fact]
    public async Task Health_summary_for_the_patient_and_visits_pagination()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id, allergies: ["اللاتكس"], conditions: ["ربو"], blood: "B+");
        await c.BookOkAsync(p.Token, d.Id, Clinic.Wed, Clinic.H10);
        var dt = await c.DoctorTokenAsync(d);
        for (var i = 0; i < 3; i++) Assert.Equal(201, (await c.VisitAsync(dt, Visit(null, p.Id, diagnosis: $"تشخيص {i}"))).Status);

        var sum = await c.Http.GetApiAsync("/api/me/health-summary", p.Token);
        Assert.Equal("B+", sum.Str("bloodType"));
        Assert.Equal("اللاتكس", sum["allergies"][0].GetProperty("name").GetString());

        var page = await c.Http.GetApiAsync("/api/me/visits?pageSize=2&page=1", p.Token);
        Assert.Equal(3, page["total"].GetInt32());
        Assert.Equal(2, page["items"].GetArrayLength());
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/me/visits", dt)).Status); // مسارات /me للمريض فقط
    }
}
