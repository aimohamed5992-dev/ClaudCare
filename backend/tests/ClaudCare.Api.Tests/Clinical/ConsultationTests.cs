using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;
using static ClaudCare.Api.Tests.Clinical.ClinicalHelpers;

namespace ClaudCare.Api.Tests.Clinical;

[Collection(ApiCollection.Name)]
public class ConsultationTests(ApiFactory root)
{
    [Fact]
    public async Task A_full_consultation_creates_the_visit_orders_prescription_and_patient_medications_and_completes_the_appointment()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id);
        var (appt, dt) = await c.InProgressAsync(d, p);
        var metformin = await c.DrugIdAsync("ميتفورمين 500 مجم");
        var vitD = await c.DrugIdAsync("فيتامين د 50,000");

        var res = await c.VisitAsync(dt, Visit(appt, vitals: new { systolicBp = 135, diastolicBp = 85, heartRate = 78, temperatureC = 36.8, spo2 = 98 },
            labs: ["hba1c", "kidney"], imaging: ["chest-xray"],
            rx: [Rx(metformin, timesPerDay: 2, days: 30), Rx(vitD, frequency: "مرة يوميًا", timesPerDay: 1, days: 14, instructions: "بعد الغداء")]));

        Assert.Equal(201, res.Status);
        Assert.Equal(appt, res["appointmentId"].GetInt32());
        Assert.Matches(@"^RX-\d+$", res.Str("prescriptionNo"));
        Assert.Equal(2, res["labOrders"].GetArrayLength());
        Assert.Equal(1, res["imagingOrders"].GetArrayLength());
        Assert.Matches(@"^LAB-\d+$", res["labOrders"][0].GetProperty("no").GetString()!);
        Assert.Matches(@"^IMG-\d+$", res["imagingOrders"][0].GetProperty("no").GetString()!);

        // الموعد اكتمل
        Assert.Equal(AppointmentStatus.Done, await c.StatusAsync(appt));

        // المريض يرى كل شيء
        var visits = await c.Http.GetApiAsync("/api/me/visits", p.Token);
        Assert.Equal(1, visits["total"].GetInt32());
        var v = visits["items"][0];
        Assert.Equal("ارتفاع ضغط الدم", v.GetProperty("diagnosis").GetString());
        Assert.Equal(135, v.GetProperty("vitals").GetProperty("systolicBp").GetInt32());
        Assert.Equal(2, v.GetProperty("medications").GetArrayLength());

        var rxList = await c.Http.GetApiAsync("/api/me/prescriptions", p.Token);
        var items = rxList["items"][0].GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(60, items[0].GetProperty("quantity").GetInt32()); // 2 × 30

        var labs = await c.Http.GetApiAsync("/api/me/lab-results", p.Token);
        Assert.Equal(2, labs["total"].GetInt32());
        Assert.False(labs["items"][0].GetProperty("ready").GetBoolean()); // لم تُنفَّذ بعد
        Assert.Empty(labs["items"][0].GetProperty("items").EnumerateArray());
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/me/imaging", p.Token))["total"].GetInt32());

        // أدوية المريض وجداول جرعاتها
        var meds = (await c.Http.GetApiAsync("/api/me/medications?active=true", p.Token)).Body.EnumerateArray().ToList();
        Assert.Equal(2, meds.Count);
        var met = meds.Single(m => m.GetProperty("name").GetString()!.Contains("ميتفورمين"));
        Assert.Equal([480, 1200], met.GetProperty("times").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal(30, met.GetProperty("daysLeft").GetInt32());
        Assert.False(met.GetProperty("endingSoon").GetBoolean());

        var titles = await c.NotificationTitlesAsync(p.Id);
        Assert.Contains("تم حفظ كشفك", titles);
        Assert.Equal(2, titles.Count(t => t == "طُلب لك فحص"));
        Assert.Contains("طُلبت لك دراسة أشعة", titles);
    }

    [Fact]
    public async Task Orders_are_visible_to_the_ordering_doctor_and_the_patient_with_scoping()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var other = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id);
        var (appt, dt) = await c.InProgressAsync(d, p);
        await c.VisitAsync(dt, Visit(appt, labs: ["cbc"], imaging: ["ecg"]));

        var mine = await c.Http.GetApiAsync("/api/lab-orders", dt);
        Assert.Equal(1, mine["total"].GetInt32());
        Assert.Equal(p.Id, mine["items"][0].GetProperty("patient").GetProperty("id").GetInt32());
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/imaging-orders", dt))["total"].GetInt32());
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/lab-orders", await c.DoctorTokenAsync(other)))["total"].GetInt32());
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/lab-orders", p.Token))["total"].GetInt32());

        var id = mine["items"][0].GetProperty("id").GetInt32();
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/lab-orders/{id}", dt)).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/lab-orders/{id}", await c.DoctorTokenAsync(other))).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/lab-orders", await c.TokenAsync(UserRole.Pharmacy))).Status);
        // طلبات المختبر في فرعه
        Assert.Equal(1, (await c.Http.GetApiAsync($"/api/lab-orders?patientId={p.Id}", await c.TokenAsync(UserRole.Lab)))["total"].GetInt32());
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/lab-orders?status=ResultReady", dt))["total"].GetInt32());
    }

    [Fact]
    public async Task Input_validation()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id);
        var (appt, dt) = await c.InProgressAsync(d, p);
        var drug = await c.DrugIdAsync("باراسيتامول 500 مجم");

        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, complaint: "أ"))).Status);
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, diagnosis: ""))).Status);
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, vitals: new { systolicBp = 400, diastolicBp = 80 }))).Status);
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, vitals: new { systolicBp = 120 }))).Status);          // أحدهما فقط
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, vitals: new { systolicBp = 80, diastolicBp = 90 }))).Status); // انقباضي < انبساطي
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, vitals: new { temperatureC = 50 }))).Status);
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, labs: ["nope"]))).Status);
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, imaging: ["nope"]))).Status);
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, rx: [Rx(drug), Rx(drug)]))).Status);           // مكرر
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, rx: [Rx(99999999)]))).Status);                 // دواء غير موجود
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, rx: [Rx(null, null)]))).Status);               // لا معرّف ولا اسم
        Assert.Equal(400, (await c.VisitAsync(dt, Visit(appt, rx: [Rx(drug, days: 0)]))).Status);            // مدة غير صالحة
        // لم يُحفظ شيء من المحاولات الفاشلة
        Assert.Equal(AppointmentStatus.InProgress, await c.StatusAsync(appt));
        Assert.Equal(0, await c.Db(db => db.Visits.CountAsync(v => v.PatientId == p.Id)));
        Assert.Equal(201, (await c.VisitAsync(dt, Visit(appt))).Status);
    }

    [Fact]
    public async Task Allergy_conflict_blocks_the_whole_visit_unless_the_doctor_overrides()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id, allergies: ["البنسلين"]);
        var (appt, dt) = await c.InProgressAsync(d, p);
        var amox = await c.DrugIdAsync("أموكسيسيلين 500 مجم");
        var safe = await c.DrugIdAsync("باراسيتامول 500 مجم");

        var blocked = await c.VisitAsync(dt, Visit(appt, labs: ["cbc"], rx: [Rx(safe), Rx(amox)]));
        Assert.Equal(409, blocked.Status);
        Assert.Equal("allergy_conflict", blocked.Code);
        var conflict = blocked["conflicts"][0];
        Assert.Equal(1, conflict.GetProperty("index").GetInt32());
        Assert.Equal("البنسلين", conflict.GetProperty("allergy").GetString());
        // لا شيء حُفظ: لا زيارة ولا طلبات ولا وصفة، والموعد ما زال قيد الكشف
        Assert.Equal(0, await c.Db(db => db.Visits.CountAsync(v => v.PatientId == p.Id)));
        Assert.Equal(0, await c.Db(db => db.LabOrders.CountAsync(v => v.PatientId == p.Id)));
        Assert.Equal(0, await c.Db(db => db.Prescriptions.CountAsync(v => v.PatientId == p.Id)));
        Assert.Equal(AppointmentStatus.InProgress, await c.StatusAsync(appt));

        var overridden = await c.VisitAsync(dt, Visit(appt, rx: [Rx(amox, allergyOverride: true)]));
        Assert.Equal(201, overridden.Status);
        Assert.Contains(overridden["warnings"].EnumerateArray().Select(x => x.GetString()), w => w!.Contains("رغم حساسية"));
        Assert.True(await c.Db(db => db.PrescriptionItems.AnyAsync(i => i.Prescription.PatientId == p.Id && i.AllergyOverride)));
    }

    [Fact]
    public async Task Free_text_drugs_are_allowed_with_a_warning_and_active_duplicates_are_flagged()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id);
        var drug = await c.DrugIdAsync("أملوديبين 5 مجم");

        var (a1, dt) = await c.InProgressAsync(d, p, Clinic.H0930);
        var first = await c.VisitAsync(dt, Visit(a1, rx: [Rx(drug, timesPerDay: 1, days: 30), Rx(null, "كريم مرطّب خاص")]));
        Assert.Equal(201, first.Status);
        Assert.Contains(first["warnings"].EnumerateArray().Select(x => x.GetString()), w => w!.Contains("خارج الدليل"));

        // كشف ثانٍ بنفس الدواء → تنبيه تكرار (غير مانع)
        var (a2, _) = await c.InProgressAsync(d, p, Clinic.H0930 + 60);
        var second = await c.VisitAsync(dt, Visit(a2, rx: [Rx(drug, timesPerDay: 1, days: 30)]));
        Assert.Equal(201, second.Status);
        Assert.Contains(second["warnings"].EnumerateArray().Select(x => x.GetString()), w => w!.Contains("ضمن أدوية المريض الحالية"));
    }

    [Fact]
    public async Task The_appointment_context_rules()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var other = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var p2 = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id);

        // موعد مؤكد لم يبدأ
        var confirmed = await c.BookOkAsync(p.Token, d.Id, Clinic.Today, Clinic.H0930);
        var dt = await c.DoctorTokenAsync(d);
        var notStarted = await c.VisitAsync(dt, Visit(confirmed));
        Assert.Equal(409, notStarted.Status);
        Assert.Equal("invalid_state", notStarted.Code);

        // طبيب آخر لا يرى الموعد
        Assert.Equal(404, (await c.VisitAsync(await c.DoctorTokenAsync(other), Visit(confirmed))).Status);

        var (appt, dt2) = await c.InProgressAsync(d, p, Clinic.H0930 + 30);
        Assert.Equal(400, (await c.VisitAsync(dt2, Visit(appt, patientId: p2.Id))).Status); // مريض لا يطابق الموعد
        Assert.Equal(201, (await c.VisitAsync(dt2, Visit(appt))).Status);
        // الموعد صار Done فلا كشف ثانٍ عليه
        Assert.Equal(409, (await c.VisitAsync(dt2, Visit(appt))).Status);
        Assert.Equal(404, (await c.VisitAsync(dt2, Visit(99999999))).Status);
    }

    [Fact]
    public async Task A_walk_in_visit_needs_a_patient_in_the_doctors_care()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var inCare = await c.PatientAsync();
        var stranger = await c.PatientAsync();
        await c.BookOkAsync(inCare.Token, d.Id, Clinic.Wed, Clinic.H10); // علاقة رعاية بموعد قادم
        var dt = await c.DoctorTokenAsync(d);

        Assert.Equal(400, (await c.VisitAsync(dt, Visit(null))).Status);                           // لا مريض ولا موعد
        Assert.Equal(404, (await c.VisitAsync(dt, Visit(null, stranger.Id))).Status);              // ليس مريضه
        var ok = await c.VisitAsync(dt, Visit(null, inCare.Id, diagnosis: "متابعة"));
        Assert.Equal(201, ok.Status);
        Assert.True(ok["appointmentId"].ValueKind is System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Only_doctors_write_visits()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        Assert.Equal(403, (await c.VisitAsync(p.Token, Visit(null, p.Id))).Status);
        Assert.Equal(403, (await c.VisitAsync(await c.TokenAsync(UserRole.Nurse), Visit(null, p.Id))).Status);
        Assert.Equal(401, (await c.Http.PostAsync("/api/visits", Visit(null, p.Id))).Status);
    }

    [Fact]
    public async Task Drug_check_reports_allergies_and_active_duplicates()
    {
        var c = await Clinic.CreateAsync(root);
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id, allergies: ["البنسلين"]);
        var amox = await c.DrugIdAsync("أموكسيسيلين 500 مجم");
        var met = await c.DrugIdAsync("ميتفورمين 500 مجم");
        var para = await c.DrugIdAsync("باراسيتامول 500 مجم");
        var (appt, dt) = await c.InProgressAsync(d, p);
        await c.VisitAsync(dt, Visit(appt, rx: [Rx(met, days: 30)]));

        var (a2, _) = await c.InProgressAsync(d, p, Clinic.H11);
        var res = await c.Http.PostAsync("/api/prescriptions/check", new { patientId = p.Id, drugIds = new[] { amox, met, para, 99999999 } }, dt);
        Assert.Equal(200, res.Status);
        var rows = res.Body.EnumerateArray().ToDictionary(x => x.GetProperty("drugId").GetInt32());
        Assert.Equal(3, rows.Count); // غير الموجود يُتجاهل
        Assert.Equal("البنسلين", rows[amox].GetProperty("allergyConflict").GetString());
        Assert.True(rows[met].GetProperty("duplicateActive").GetBoolean());
        Assert.False(rows[para].GetProperty("duplicateActive").GetBoolean());
        Assert.True(rows[para].GetProperty("allergyConflict").ValueKind is System.Text.Json.JsonValueKind.Null);
        Assert.Equal(404, (await c.Http.PostAsync("/api/prescriptions/check", new { patientId = (await c.PatientAsync()).Id, drugIds = new[] { amox } }, dt)).Status);
        _ = a2;
    }
}
