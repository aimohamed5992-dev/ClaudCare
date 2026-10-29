using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Operations;

[Collection(ApiCollection.Name)]
public class AdmissionsTests(ApiFactory root)
{
    [Fact]
    public async Task Request_admit_claims_the_bed_and_a_second_patient_cannot_take_it()
    {
        var c = await Clinic.CreateAsync(root);
        var p1 = await c.PatientAsync(); var p2 = await c.PatientAsync();
        var adm = await c.TokenAsync(UserRole.Admissions);
        var bed = await c.BedAsync();

        var r1 = await c.Http.PostAsync("/api/admissions", new { patientId = p1.Id, type = "Emergency", reason = "كسر", requestedWard = "الباطنة" }, adm);
        var r2 = await c.Http.PostAsync("/api/admissions", new { patientId = p2.Id, type = "Emergency", reason = "التهاب", requestedWard = "الباطنة" }, adm);
        Assert.StartsWith("ADM-", r1.Str("no"));
        Assert.Equal("already_admitted", (await c.Http.PostAsync("/api/admissions", new { patientId = p1.Id, type = "Emergency", reason = "تكرار", requestedWard = "الباطنة" }, adm)).Code);

        var ok = await c.Http.PostAsync($"/api/admissions/{r1["id"].GetInt32()}/admit", new { bedId = bed }, adm);
        Assert.Equal("Admitted", ok.Str("status"));
        Assert.Equal(BedStatus.Occupied, await c.WithDb(async db => (await db.Beds.AsNoTracking().FirstAsync(b => b.Id == bed)).Status));
        Assert.Equal("bed_unavailable", (await c.Http.PostAsync($"/api/admissions/{r2["id"].GetInt32()}/admit", new { bedId = bed }, adm)).Code);
        Assert.Contains("تم تنويمك", await c.NotificationTitlesAsync(p1.Id));
    }

    [Fact]
    public async Task Concurrent_admits_to_one_bed_succeed_for_exactly_one_patient()
    {
        var c = await Clinic.CreateAsync(root);
        var adm = await c.TokenAsync(UserRole.Admissions);
        var bed = await c.BedAsync();
        var ids = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            var p = await c.PatientAsync();
            ids.Add((await c.Http.PostAsync("/api/admissions", new { patientId = p.Id, type = "Emergency", reason = "حالة", requestedWard = "الباطنة" }, adm))["id"].GetInt32());
        }
        var results = await Task.WhenAll(ids.Select(id => c.Http.PostAsync($"/api/admissions/{id}/admit", new { bedId = bed }, adm)));
        Assert.Single(results, r => r.Status == 200);
        Assert.Equal(1, await c.WithDb(db => db.Admissions.CountAsync(a => a.BedId == bed)));
    }

    [Fact]
    public async Task Minors_need_guardian_consent_and_planned_stays_need_insurance_verification()
    {
        var c = await Clinic.CreateAsync(root);
        var minor = await c.PatientAsync((db, id) => db.Users.First(u => u.Id == id).BirthYear = 2018);
        var adult = await c.PatientAsync();
        var adm = await c.TokenAsync(UserRole.Admissions);

        var a = (await c.Http.PostAsync("/api/admissions", new { patientId = minor.Id, type = "Emergency", reason = "حمى", requestedWard = "الأطفال" }, adm))["id"].GetInt32();
        var bed = await c.BedAsync("الأطفال");
        Assert.Equal("guardian_consent_required", (await c.Http.PostAsync($"/api/admissions/{a}/admit", new { bedId = bed }, adm)).Code);
        await c.Http.PutAsync($"/api/admissions/{a}/checks", new { guardianConsent = true }, adm);
        Assert.Equal(200, (await c.Http.PostAsync($"/api/admissions/{a}/admit", new { bedId = bed }, adm)).Status);

        var planned = (await c.Http.PostAsync("/api/admissions", new { patientId = adult.Id, type = "Planned", reason = "عملية", requestedWard = "الجراحة" }, adm))["id"].GetInt32();
        var bed2 = await c.BedAsync("الجراحة");
        Assert.Equal("insurance_approval_required", (await c.Http.PostAsync($"/api/admissions/{planned}/admit", new { bedId = bed2 }, adm)).Code);
        await c.Http.PutAsync($"/api/admissions/{planned}/checks", new { insuranceApprovalVerified = true }, adm);
        Assert.Equal(200, (await c.Http.PostAsync($"/api/admissions/{planned}/admit", new { bedId = bed2 }, adm)).Status);
    }

    [Fact]
    public async Task Discharge_requires_all_steps_and_a_settled_bill_then_frees_the_bed_for_cleaning()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        var (id, bed, adm) = await c.AdmittedAsync(p.Id);
        var inv = await c.WithDb(async db =>
        {
            var i = new Invoice { No = "T" + Guid.NewGuid().ToString("N")[..12], PatientId = p.Id, BranchId = c.BranchId, AdmissionId = id, Title = "إقامة", IssueDate = Clinic.Today, DueDate = Clinic.Today, Subtotal = 500 };
            db.Invoices.Add(i); await db.SaveChangesAsync(); return i.Id;
        });

        var partial = await c.Http.PostAsync($"/api/admissions/{id}/discharge", new { dischargeOrderConfirmed = true, medsReconciled = false, billSettled = true, instructionsGiven = false, summary = "تحسن" }, adm);
        Assert.Equal("discharge_incomplete", partial.Code);
        Assert.Equal(2, partial["missing"].GetArrayLength());

        var all = new { dischargeOrderConfirmed = true, medsReconciled = true, billSettled = true, instructionsGiven = true, summary = "تحسن وخرج" };
        Assert.Equal("bill_unsettled", (await c.Http.PostAsync($"/api/admissions/{id}/discharge", all, adm)).Code); // الفاتورة الفعلية غير مسددة
        Assert.Equal(200, (await c.Http.PostAsync($"/api/invoices/{inv}/pay", new { method = "Cash" }, await c.TokenAsync(UserRole.Reception))).Status);

        Assert.Equal("Discharged", (await c.Http.PostAsync($"/api/admissions/{id}/discharge", all, adm)).Str("status"));
        Assert.Equal(BedStatus.Cleaning, await c.WithDb(async db => (await db.Beds.AsNoTracking().FirstAsync(b => b.Id == bed)).Status));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/admissions/{id}/discharge", all, adm)).Status);
    }

    [Fact]
    public async Task Transfer_request_approve_complete_moves_the_patient_and_cleans_the_old_bed()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        var (id, oldBed, adm) = await c.AdmittedAsync(p.Id, "الباطنة");
        var icu = await c.BedAsync("العناية");

        var t = await c.Http.PostAsync($"/api/admissions/{id}/transfers", new { toWard = "العناية", reason = "تدهور" }, adm);
        Assert.Equal(201, t.Status);
        Assert.Equal("transfer_pending", (await c.Http.PostAsync($"/api/admissions/{id}/transfers", new { toWard = "العناية", reason = "تكرار" }, adm)).Code);
        var tid = t["id"].GetInt32();
        Assert.Equal(409, (await c.Http.PostAsync($"/api/bed-transfers/{tid}/complete", new { bedId = icu }, adm)).Status); // لم يُوافَق
        Assert.Equal("Approved", (await c.Http.PostAsync($"/api/bed-transfers/{tid}/approve", null, adm)).Str("status"));
        Assert.Equal(400, (await c.Http.PostAsync($"/api/bed-transfers/{tid}/complete", new { bedId = await c.BedAsync("الباطنة") }, adm)).Status); // قسم خطأ
        Assert.Equal("Done", (await c.Http.PostAsync($"/api/bed-transfers/{tid}/complete", new { bedId = icu }, adm)).Str("status"));

        Assert.Equal(BedStatus.Cleaning, await c.WithDb(async db => (await db.Beds.AsNoTracking().FirstAsync(b => b.Id == oldBed)).Status));
        Assert.Equal(icu, await c.WithDb(async db => (await db.Admissions.AsNoTracking().FirstAsync(a => a.Id == id)).BedId));
    }

    [Fact]
    public async Task Bed_board_counts_and_manual_status_rules_and_scope()
    {
        var c = await Clinic.CreateAsync(root);
        var adm = await c.TokenAsync(UserRole.Admissions);
        var free = await c.BedAsync(); await c.BedAsync(status: BedStatus.Occupied);
        var board = await c.Http.GetApiAsync("/api/beds", adm);
        Assert.Equal(2, board["total"].GetInt32());
        Assert.Equal(1, board["available"].GetInt32());
        Assert.Equal(400, (await c.Http.PostAsync($"/api/beds/{free}/status", new { status = "Occupied" }, adm)).Status);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/beds/{free}/status", new { status = "Reserved" }, adm)).Status);
        Assert.Equal("Reserved", (await c.Http.PostAsync($"/api/beds/{free}/status", new { status = "Reserved", reservedFor = "مريض مخطط" }, adm)).Str("status"));
        var foreign = await c.TokenAsync(UserRole.Admissions, branchId: await c.OtherBranchAsync());
        Assert.Equal(404, (await c.Http.PostAsync($"/api/beds/{free}/status", new { status = "Cleaning" }, foreign)).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/beds", await c.TokenAsync(UserRole.Pharmacy))).Status);
    }
}

[Collection(ApiCollection.Name)]
public class NursingTests(ApiFactory root)
{
    [Fact]
    public async Task Vitals_are_validated_alerted_and_listed_newest_first()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        var nurse = await c.TokenAsync(UserRole.Nurse);

        Assert.Equal(400, (await c.Http.PostAsync("/api/vitals", new { patientId = p.Id }, nurse)).Status);
        Assert.Equal(400, (await c.Http.PostAsync("/api/vitals", new { patientId = p.Id, systolicBp = 100, diastolicBp = 120 }, nurse)).Status);
        var ok = await c.Http.PostAsync("/api/vitals", new { patientId = p.Id, systolicBp = 120, diastolicBp = 80, heartRate = 72, spo2 = 98 }, nurse);
        Assert.Equal(201, ok.Status);
        Assert.Equal(0, ok["alerts"].GetArrayLength());
        c.Clock.Advance(TimeSpan.FromMinutes(5));
        var bad = await c.Http.PostAsync("/api/vitals", new { patientId = p.Id, spo2 = 85, heartRate = 145 }, nurse);
        Assert.Contains("انخفاض تشبع الأكسجين", bad["alerts"].EnumerateArray().Select(x => x.GetString()));

        var list = await c.Http.GetApiAsync($"/api/patients/{p.Id}/vitals", nurse);
        Assert.Equal(85, list.Body[0].GetProperty("spo2").GetInt32());
        Assert.Equal(2, list.Body.GetArrayLength());
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/vitals", p.Token)).Status); // المريض يرى نفسه
        Assert.Equal(403, (await c.Http.PostAsync("/api/vitals", new { patientId = p.Id, spo2 = 98 }, p.Token)).Status);
        Assert.Equal(403, (await c.Http.PostAsync("/api/vitals", new { patientId = p.Id, spo2 = 98 }, await c.TokenAsync(UserRole.Pharmacy))).Status);
    }

    [Fact]
    public async Task Notes_are_for_staff_only_and_medication_administration_follows_the_state_rules()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        var (adm, _, _) = await c.AdmittedAsync(p.Id);
        var nurse = await c.TokenAsync(UserRole.Nurse);

        Assert.Equal(201, (await c.Http.PostAsync($"/api/patients/{p.Id}/nursing-notes", new { note = "المريض مستقر", admissionId = adm }, nurse)).Status);
        Assert.Equal(1, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/nursing-notes", nurse)).Body.GetArrayLength());
        Assert.Equal(403, (await c.Http.GetApiAsync($"/api/patients/{p.Id}/nursing-notes", p.Token)).Status);

        var due = c.Clock.GetUtcNow().UtcDateTime.AddHours(1);
        var dose = await c.Http.PostAsync($"/api/admissions/{adm}/medications", new { drugName = "باراسيتامول", dose = "500 مجم", dueAt = due }, nurse);
        Assert.Equal(201, dose.Status);
        var id = dose["id"].GetInt32();
        Assert.Equal(400, (await c.Http.PostAsync($"/api/medication-administrations/{id}/skip", new { }, nurse)).Status); // السبب إلزامي
        Assert.Equal("Delayed", (await c.Http.PostAsync($"/api/medication-administrations/{id}/delay", new { notes = "المريض نائم" }, nurse)).Str("status"));
        var given = await c.Http.PostAsync($"/api/medication-administrations/{id}/give", new { }, nurse);
        Assert.Equal("Given", given.Str("status"));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/medication-administrations/{id}/give", new { }, nurse)).Status);
        Assert.Equal(403, (await c.Http.PostAsync($"/api/medication-administrations/{id}/give", new { }, await c.TokenAsync(UserRole.Pharmacy))).Status);
        Assert.Equal(404, (await c.Http.PostAsync($"/api/medication-administrations/{id}/give", new { }, await c.TokenAsync(UserRole.Nurse, branchId: await c.OtherBranchAsync()))).Status);
        Assert.Equal(1, (await c.Http.GetApiAsync($"/api/admissions/{adm}/medications", nurse)).Body.GetArrayLength());
    }
}

[Collection(ApiCollection.Name)]
public class EmergencyTests(ApiFactory root)
{
    private static object Case(string name = "مريض", int level = 4, int? spo2 = null, int? patientId = null, string? redFlags = null) =>
        new { patientName = name, patientId, age = 40, complaint = "ألم", triageLevel = level, zone = "الأحمر", spo2, redFlags };

    [Fact]
    public async Task Board_orders_by_severity_then_arrival_and_critical_vitals_escalate_the_level()
    {
        var c = await Clinic.CreateAsync(root);
        var er = await c.TokenAsync(UserRole.Emergency);
        var low = await c.Http.PostAsync("/api/emergency/cases", Case("خفيف", level: 5), er);
        c.Clock.Advance(TimeSpan.FromMinutes(3));
        var high = await c.Http.PostAsync("/api/emergency/cases", Case("حرج", level: 1), er);
        c.Clock.Advance(TimeSpan.FromMinutes(3));
        var esc = await c.Http.PostAsync("/api/emergency/cases", Case("تشبع منخفض", level: 4, spo2: 82), er);
        Assert.True(esc["escalated"].GetBoolean());
        Assert.Equal(2, esc["triageLevel"].GetInt32());
        Assert.False((await c.Http.PostAsync("/api/emergency/cases", Case("هادئ", level: 3, spo2: 98), er))["escalated"].GetBoolean());

        var board = await c.Http.GetApiAsync("/api/emergency/cases", er);
        var names = board["cases"].EnumerateArray().Select(x => x.GetProperty("patientName").GetString()).ToList();
        Assert.True(names.IndexOf("حرج") < names.IndexOf("تشبع منخفض"));
        Assert.True(names.IndexOf("تشبع منخفض") < names.IndexOf("هادئ"));
        Assert.True(names.IndexOf("هادئ") < names.IndexOf("خفيف"));
        Assert.Equal(4, board["waiting"].GetInt32());
        Assert.True(board["cases"].EnumerateArray().First(x => x.GetProperty("patientName").GetString() == "خفيف").GetProperty("waitingMinutes").GetInt32() >= 6);
        Assert.Equal(400, (await c.Http.PostAsync("/api/emergency/cases", Case(level: 9), er)).Status);
    }

    [Fact]
    public async Task Exam_discharge_and_admit_transitions_create_a_linked_emergency_admission()
    {
        var c = await Clinic.CreateAsync(root);
        var er = await c.TokenAsync(UserRole.Emergency);
        var p = await c.PatientAsync();
        var linked = (await c.Http.PostAsync("/api/emergency/cases", Case("مسجل", level: 2, patientId: p.Id), er))["id"].GetInt32();
        var anon = (await c.Http.PostAsync("/api/emergency/cases", Case("مجهول", level: 2), er))["id"].GetInt32();

        Assert.Equal(409, (await c.Http.PostAsync($"/api/emergency/cases/{linked}/admit", new { ward = "العناية" }, er)).Status); // لم يبدأ الفحص
        await c.Http.PostAsync($"/api/emergency/cases/{linked}/start-exam", null, er);
        await c.Http.PostAsync($"/api/emergency/cases/{anon}/start-exam", null, er);
        Assert.Equal("patient_required", (await c.Http.PostAsync($"/api/emergency/cases/{anon}/admit", new { ward = "العناية" }, er)).Code);

        var admitted = await c.Http.PostAsync($"/api/emergency/cases/{linked}/admit", new { ward = "العناية" }, er);
        Assert.Equal("Admitted", admitted.Str("status"));
        var adm = await c.WithDb(async db => await db.Admissions.AsNoTracking().FirstAsync(a => a.PatientId == p.Id));
        Assert.Equal(AdmissionType.Emergency, adm.Type);
        Assert.Equal(adm.Id, admitted["admissionId"].GetInt32());
        Assert.Equal("Discharged", (await c.Http.PostAsync($"/api/emergency/cases/{anon}/discharge", null, er)).Str("status"));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/emergency/cases/{anon}/discharge", null, er)).Status);
    }

    [Fact]
    public async Task Ambulance_arrival_creates_a_waiting_case_and_scope_is_enforced()
    {
        var c = await Clinic.CreateAsync(root);
        var er = await c.TokenAsync(UserRole.Emergency);
        var amb = (await c.Http.PostAsync("/api/emergency/ambulances", new { unitNo = "A-7", patientDescription = "رجل 60 سنة", condition = "جلطة محتملة", triageLevel = 1, etaMinutes = 6 }, er))["id"].GetInt32();
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/emergency/ambulances", er)).Body.EnumerateArray().Count(x => x.GetProperty("id").GetInt32() == amb));
        var foreign = await c.TokenAsync(UserRole.Emergency, branchId: await c.OtherBranchAsync());
        Assert.Equal(404, (await c.Http.PostAsync($"/api/emergency/ambulances/{amb}/arrive", null, foreign)).Status);

        var arrived = await c.Http.PostAsync($"/api/emergency/ambulances/{amb}/arrive", null, er);
        Assert.Equal(1, arrived["triageLevel"].GetInt32());
        Assert.Equal("Waiting", arrived.Str("status"));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/emergency/ambulances/{amb}/arrive", null, er)).Status);
        Assert.DoesNotContain((await c.Http.GetApiAsync("/api/emergency/ambulances", er)).Body.EnumerateArray(), x => x.GetProperty("id").GetInt32() == amb);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/emergency/cases", await c.TokenAsync(UserRole.Pharmacy))).Status);
    }
}

[Collection(ApiCollection.Name)]
public class ReceptionTests(ApiFactory root)
{
    [Fact]
    public async Task Tickets_are_numbered_per_branch_and_day_even_when_issued_concurrently()
    {
        var c = await Clinic.CreateAsync(root);
        var desk = await c.TokenAsync(UserRole.Reception);
        var other = await c.TokenAsync(UserRole.Reception, branchId: await c.OtherBranchAsync());

        var issued = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => c.Http.PostAsync("/api/reception/queue", new { patientName = $"زائر {i}" }, desk)));
        Assert.All(issued, r => Assert.Equal(201, r.Status));
        Assert.Equal(Enumerable.Range(1, 6), issued.Select(r => r["ticketNo"].GetInt32()).Order());
        Assert.Equal(1, (await c.Http.PostAsync("/api/reception/queue", new { patientName = "فرع آخر" }, other))["ticketNo"].GetInt32());
        Assert.Equal(400, (await c.Http.PostAsync("/api/reception/queue", new { }, desk)).Status);
    }

    [Fact]
    public async Task Ticket_transitions_and_doctor_visibility()
    {
        var c = await Clinic.CreateAsync(root);
        var desk = await c.TokenAsync(UserRole.Reception);
        var doc = await c.DoctorAsync();
        var mine = (await c.Http.PostAsync("/api/reception/queue", new { patientName = "أ", doctorId = doc.Id }, desk))["id"].GetInt32();
        await c.Http.PostAsync("/api/reception/queue", new { patientName = "ب" }, desk);

        var dt = await c.DoctorTokenAsync(doc);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/reception/queue", dt)).Body.GetArrayLength());
        Assert.Equal(409, (await c.Http.PostAsync($"/api/reception/queue/{mine}/done", null, desk)).Status); // لم يُنادَ
        Assert.Equal("Called", (await c.Http.PostAsync($"/api/reception/queue/{mine}/call", null, dt)).Str("status"));
        Assert.Equal("Done", (await c.Http.PostAsync($"/api/reception/queue/{mine}/done", null, dt)).Str("status"));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/reception/queue/{mine}/cancel", null, desk)).Status);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/reception/queue?status=Waiting", desk)).Body.GetArrayLength());
        Assert.Equal(404, (await c.Http.PostAsync($"/api/reception/queue/{mine}/call", null, await c.TokenAsync(UserRole.Reception, branchId: await c.OtherBranchAsync()))).Status);
        Assert.Equal(403, (await c.Http.PostAsync("/api/reception/queue", new { patientName = "x" }, await c.TokenAsync(UserRole.Pharmacy))).Status);
    }

    [Fact]
    public async Task Register_walk_in_patient_normalizes_phone_blocks_duplicates_and_cannot_login_with_a_guess()
    {
        var c = await Clinic.CreateAsync(root);
        var desk = await c.TokenAsync(UserRole.Reception);
        var phone = "05" + Random.Shared.NextInt64(10_000_000, 99_999_999);

        var ok = await c.Http.PostAsync("/api/reception/patients", new { fullName = "زائر جديد", phone = "+966" + phone[1..], birthYear = 1990, gender = "Male" }, desk);
        Assert.Equal(201, ok.Status);
        Assert.Equal("phone_taken", (await c.Http.PostAsync("/api/reception/patients", new { fullName = "مكرر", phone }, desk)).Code);
        Assert.Equal(400, (await c.Http.PostAsync("/api/reception/patients", new { fullName = "خطأ", phone = "123" }, desk)).Status);
        Assert.NotNull(await c.WithDb(db => db.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == ok["id"].GetInt32())));
        Assert.NotEqual(200, (await c.Http.PostAsync("/api/auth/login", new { identifier = phone, password = "Test@12345" })).Status);
        Assert.Equal(403, (await c.Http.PostAsync("/api/reception/patients", new { fullName = "x y", phone }, await c.TokenAsync(UserRole.Nurse))).Status);
    }

    [Fact]
    public async Task Today_dashboard_counts_appointments_and_queue()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(); var p = await c.PatientAsync();
        await c.BookOkAsync(p.Token, doc.Id, Clinic.Today, Clinic.H11);
        var desk = await c.TokenAsync(UserRole.Reception);
        await c.Http.PostAsync("/api/reception/queue", new { patientName = "أ" }, desk);
        var t = await c.Http.GetApiAsync("/api/reception/today", desk);
        Assert.Equal(1, t["appointments"].GetInt32());
        Assert.Equal(1, t["confirmed"].GetInt32());
        Assert.Equal(1, t["queueWaiting"].GetInt32());
    }
}

[Collection(ApiCollection.Name)]
public class SurgeryTests(ApiFactory root)
{
    private static async Task<(int id, string tok, int room, Clinic.Doc surgeon, Clinic.Pat patient)> ScheduleAsync(Clinic c, DateOnly? date = null, int start = 600, string? alert = null)
    {
        var tok = await c.TokenAsync(UserRole.Surgery);
        var room = await c.RoomAsync(); var surgeon = await c.DoctorAsync(); var patient = await c.PatientAsync();
        var r = await c.Http.PostAsync("/api/surgery/cases", new { patientId = patient.Id, procedureName = "استئصال الزائدة", roomId = room, date = (date ?? Clinic.Today).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            startMinutes = start, durationMinutes = 90, surgeonId = surgeon.Id, preOpAlert = alert }, tok);
        Assert.Equal(201, r.Status);
        return (r["id"].GetInt32(), tok, room, surgeon, patient);
    }

    [Fact]
    public async Task Full_flow_checklist_approval_start_and_complete_with_room_state()
    {
        var c = await Clinic.CreateAsync(root);
        var (id, tok, room, surgeon, patient) = await ScheduleAsync(c);

        var s = await c.Http.GetApiAsync($"/api/surgery/cases/{id}", tok);
        Assert.StartsWith("SRG-", s.Str("no"));
        Assert.Equal(8, s["checklist"].GetArrayLength());
        Assert.Equal("preop_not_ready", (await c.Http.PostAsync($"/api/surgery/cases/{id}/start", null, tok)).Code);
        var blocked = await c.Http.PostAsync($"/api/surgery/cases/{id}/preop/approve", null, tok);
        Assert.Equal("checklist_incomplete", blocked.Code);
        Assert.Equal(8, blocked["missing"].GetArrayLength());

        foreach (var k in s["checklist"].EnumerateArray().Select(x => x.GetProperty("key").GetString()))
            Assert.Equal(200, (await c.Http.PutAsync($"/api/surgery/cases/{id}/checklist/{k}", new { done = true }, tok)).Status);
        Assert.Equal(404, (await c.Http.PutAsync($"/api/surgery/cases/{id}/checklist/nope", new { done = true }, tok)).Status);
        Assert.True((await c.Http.PostAsync($"/api/surgery/cases/{id}/preop/approve", null, tok))["preOpReady"].GetBoolean());

        Assert.Equal("InProgress", (await c.Http.PostAsync($"/api/surgery/cases/{id}/start", null, tok)).Str("status"));
        Assert.Equal(OperatingRoomState.InSurgery, await c.WithDb(async db => (await db.OperatingRooms.AsNoTracking().FirstAsync(r => r.Id == room)).State));
        Assert.Equal(409, (await c.Http.PutAsync($"/api/surgery/cases/{id}/checklist/consent", new { done = false }, tok)).Status); // مقفلة
        Assert.Equal("Done", (await c.Http.PostAsync($"/api/surgery/cases/{id}/complete", null, tok)).Str("status"));
        Assert.Equal(OperatingRoomState.Cleaning, await c.WithDb(async db => (await db.OperatingRooms.AsNoTracking().FirstAsync(r => r.Id == room)).State));
        Assert.Contains("انتهت عمليتك", await c.NotificationTitlesAsync(patient.Id));
    }

    [Fact]
    public async Task Preop_alert_must_be_acknowledged_and_unticking_revokes_approval()
    {
        var c = await Clinic.CreateAsync(root);
        var (id, tok, _, _, _) = await ScheduleAsync(c, alert: "حساسية من اللاتكس");
        foreach (var k in SurgeryKeys) await c.Http.PutAsync($"/api/surgery/cases/{id}/checklist/{k}", new { done = true }, tok);

        Assert.Equal("alert_not_acknowledged", (await c.Http.PostAsync($"/api/surgery/cases/{id}/preop/approve", new { acknowledgeAlert = false }, tok)).Code);
        Assert.True((await c.Http.PostAsync($"/api/surgery/cases/{id}/preop/approve", new { acknowledgeAlert = true }, tok))["preOpReady"].GetBoolean());
        var revoked = await c.Http.PutAsync($"/api/surgery/cases/{id}/checklist/fasting", new { done = false }, tok);
        Assert.False(revoked["preOpReady"].GetBoolean());
    }

    private static readonly string[] SurgeryKeys = ["consent", "identity", "site", "fasting", "labs", "imaging", "allergies", "anesthesia"];

    [Fact]
    public async Task Scheduling_rejects_room_surgeon_overlaps_past_dates_and_maintenance()
    {
        var c = await Clinic.CreateAsync(root);
        var (_, tok, room, surgeon, _) = await ScheduleAsync(c, Clinic.Wed, start: 600);
        string D(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var p2 = await c.PatientAsync(); var surgeon2 = await c.DoctorAsync(); var room2 = await c.RoomAsync();
        object Body(int r, int s, int start, DateOnly d) => new { patientId = p2.Id, procedureName = "عملية", roomId = r, date = D(d), startMinutes = start, durationMinutes = 60, surgeonId = s };

        Assert.Equal("room_busy", (await c.Http.PostAsync("/api/surgery/cases", Body(room, surgeon2.Id, 650, Clinic.Wed), tok)).Code);
        Assert.Equal("surgeon_busy", (await c.Http.PostAsync("/api/surgery/cases", Body(room2, surgeon.Id, 650, Clinic.Wed), tok)).Code);
        Assert.Equal(201, (await c.Http.PostAsync("/api/surgery/cases", Body(room, surgeon2.Id, 700, Clinic.Wed), tok)).Status); // بعد انتهاء الأولى (600+90=690)
        Assert.Equal(400, (await c.Http.PostAsync("/api/surgery/cases", Body(room2, surgeon2.Id, 600, new DateOnly(2026, 10, 1)), tok)).Status);
        var maint = await c.RoomAsync(OperatingRoomState.Maintenance);
        Assert.Equal("room_unavailable", (await c.Http.PostAsync("/api/surgery/cases", Body(maint, surgeon2.Id, 600, Clinic.Thu), tok)).Code);
        Assert.Equal(403, (await c.Http.PostAsync("/api/surgery/cases", Body(room2, surgeon2.Id, 600, Clinic.Thu), await c.TokenAsync(UserRole.Nurse))).Status);
    }

    [Fact]
    public async Task Start_needs_today_and_a_free_room_and_cancel_only_while_scheduled()
    {
        var c = await Clinic.CreateAsync(root);
        var (future, tok, _, _, _) = await ScheduleAsync(c, Clinic.Wed);
        foreach (var k in SurgeryKeys) await c.Http.PutAsync($"/api/surgery/cases/{future}/checklist/{k}", new { done = true }, tok);
        await c.Http.PostAsync($"/api/surgery/cases/{future}/preop/approve", null, tok);
        Assert.Equal("not_today", (await c.Http.PostAsync($"/api/surgery/cases/{future}/start", null, tok)).Code);

        var (today, _, room, _, _) = await ScheduleAsync(c);
        foreach (var k in SurgeryKeys) await c.Http.PutAsync($"/api/surgery/cases/{today}/checklist/{k}", new { done = true }, tok);
        await c.Http.PostAsync($"/api/surgery/cases/{today}/preop/approve", null, tok);
        await c.Http.PostAsync($"/api/surgery/rooms/{room}/state", new { state = "Cleaning" }, tok);
        Assert.Equal("room_unavailable", (await c.Http.PostAsync($"/api/surgery/cases/{today}/start", null, tok)).Code);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/surgery/rooms/{room}/state", new { state = "InSurgery" }, tok)).Status);

        Assert.Equal("Cancelled", (await c.Http.PostAsync($"/api/surgery/cases/{future}/cancel", null, tok)).Str("status"));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/surgery/cases/{future}/cancel", null, tok)).Status);
    }

    [Fact]
    public async Task Surgeon_sees_only_own_cases_and_other_branches_get_404()
    {
        var c = await Clinic.CreateAsync(root);
        var (id, tok, _, surgeon, _) = await ScheduleAsync(c);
        await ScheduleAsync(c);
        var st = await c.DoctorTokenAsync(surgeon);
        Assert.Equal([id], (await c.Http.GetApiAsync("/api/surgery/cases", st)).Body.EnumerateArray().Select(x => x.GetProperty("id").GetInt32()));
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/surgery/cases/{id}", await c.DoctorTokenAsync(await c.DoctorAsync()))).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/surgery/cases/{id}", await c.TokenAsync(UserRole.Surgery, branchId: await c.OtherBranchAsync()))).Status);
        Assert.Equal(403, (await c.Http.PostAsync($"/api/surgery/cases/{id}/start", null, st)).Status);
    }
}
