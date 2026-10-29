using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Operations;

[Collection(ApiCollection.Name)]
public class LabWorkflowTests(ApiFactory root)
{
    [Fact]
    public async Task Full_lab_flow_flags_results_and_notifies_patient_and_doctor()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        var lab = await c.TokenAsync(UserRole.Lab);

        Assert.Equal("Ordered", (await c.Http.GetApiAsync($"/api/lab-orders/{k.LabId}", lab)).Str("status"));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/start", null, lab)).Status); // لم تُسحب العينة
        Assert.Equal("SampleCollected", (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/collect", null, lab)).Str("status"));
        Assert.Equal("InProgress", (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/start", null, lab)).Str("status"));

        // 9.0% = مرتفع جدًا (حرج) فوق 1.6× الحد الأعلى 5.6؛ الوحدة والمدى من الكتالوج
        var res = await c.Http.PutAsync($"/api/lab-orders/{k.LabId}/results", new { items = new[] { new { name = "السكر التراكمي", value = 9.5m } } }, lab);
        Assert.Equal(200, res.Status);
        Assert.Equal("ResultReady", res.Str("status"));
        var item = res["items"][0];
        Assert.Equal("Critical", item.GetProperty("flag").GetString());
        Assert.Equal("%", item.GetProperty("unit").GetString());
        Assert.Equal(5.6m, item.GetProperty("refMax").GetDecimal());

        Assert.Contains("نتيجة تحليل جاهزة", await c.NotificationTitlesAsync(k.Patient.Id));
        Assert.Contains("نتيجة حرجة", await c.NotificationTitlesAsync(k.Doctor.Id));
        Assert.Equal(409, (await c.Http.PutAsync($"/api/lab-orders/{k.LabId}/results", new { items = new[] { new { name = "x", value = 1 } } }, lab)).Status); // لا تكرار
        Assert.Equal(409, (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/cancel", new { reason = "خطأ" }, lab)).Status);
    }

    [Fact]
    public async Task Nurse_collects_samples_but_cannot_enter_results_and_other_roles_are_blocked()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        var nurse = await c.TokenAsync(UserRole.Nurse);
        Assert.Equal(200, (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/collect", null, nurse)).Status);
        Assert.Equal(403, (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/start", null, nurse)).Status);
        Assert.Equal(403, (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/collect", null, await c.TokenAsync(UserRole.Pharmacy))).Status);
        Assert.Equal(403, (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/collect", null, k.Patient.Token)).Status);
    }

    [Fact]
    public async Task Other_branch_lab_sees_404_and_validation_rejects_bad_results()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        var foreign = await c.TokenAsync(UserRole.Lab, branchId: await c.OtherBranchAsync());
        Assert.Equal(404, (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/collect", null, foreign)).Status);

        var lab = await c.TokenAsync(UserRole.Lab);
        await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/collect", null, lab);
        await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/start", null, lab);
        Assert.Equal(400, (await c.Http.PutAsync($"/api/lab-orders/{k.LabId}/results", new { items = Array.Empty<object>() }, lab)).Status);
        Assert.Equal(400, (await c.Http.PutAsync($"/api/lab-orders/{k.LabId}/results", new { items = new[] { new { name = "أ", value = 1 }, new { name = "أ", value = 2 } } }, lab)).Status); // مكرر
        Assert.Equal(400, (await c.Http.PutAsync($"/api/lab-orders/{k.LabId}/results", new { items = new[] { new { name = "أ", value = 1, refMin = 10, refMax = 5 } } }, lab)).Status);
    }

    [Fact]
    public async Task Doctor_can_cancel_only_their_own_order_before_results()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        var other = await c.DoctorAsync();
        Assert.Equal(404, (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/cancel", new { reason = "لا حاجة" }, await c.DoctorTokenAsync(other))).Status);
        Assert.Equal("Cancelled", (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/cancel", new { reason = "لا حاجة" }, k.DoctorToken)).Str("status"));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/lab-orders/{k.LabId}/collect", null, await c.TokenAsync(UserRole.Lab))).Status);
    }
}

[Collection(ApiCollection.Name)]
public class RadiologyWorkflowTests(ApiFactory root)
{
    private static async Task<int> DeviceAsync(Clinic c, string modality = "X-Ray") => await c.WithDb(async db =>
    {
        var d = new ImagingDevice { BranchId = c.BranchId, Name = "جهاز " + Guid.NewGuid().ToString("N")[..6], Modality = modality };
        db.ImagingDevices.Add(d); await db.SaveChangesAsync(); return d.Id;
    });

    [Fact]
    public async Task Schedule_start_draft_then_final_report_locks_and_notifies()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        var rad = await c.TokenAsync(UserRole.Radiology);
        var dev = await DeviceAsync(c);
        var at = c.Clock.GetUtcNow().UtcDateTime.AddHours(5);

        Assert.Equal(1, (await c.Http.GetApiAsync("/api/imaging-devices", rad)).Body.GetArrayLength() > 0 ? 1 : 0);
        var sched = await c.Http.PostAsync($"/api/imaging-orders/{k.ImagingId}/schedule", new { scheduledAt = at, deviceId = dev }, rad);
        Assert.Equal("Scheduled", sched.Str("status"));
        Assert.Contains("تم تحديد موعد الأشعة", await c.NotificationTitlesAsync(k.Patient.Id));

        Assert.Equal(409, (await c.Http.PutAsync($"/api/imaging-orders/{k.ImagingId}/report", new { findings = "طبيعي", impression = "لا شيء" }, rad)).Status); // قبل البدء
        Assert.Equal("InProgress", (await c.Http.PostAsync($"/api/imaging-orders/{k.ImagingId}/start", null, rad)).Str("status"));

        var draft = await c.Http.PutAsync($"/api/imaging-orders/{k.ImagingId}/report", new { findings = "رئتان صافيتان", impression = "طبيعي" }, rad);
        Assert.Equal("InProgress", draft.Str("status"));
        Assert.Equal("Draft", draft["report"].GetProperty("status").GetString());

        var final = await c.Http.PutAsync($"/api/imaging-orders/{k.ImagingId}/report", new { findings = "رئتان صافيتان بلا ارتشاح", impression = "فحص طبيعي", final = true }, rad);
        Assert.Equal("Reported", final.Str("status"));
        Assert.Equal("Final", final["report"].GetProperty("status").GetString());
        Assert.Contains("تقرير الأشعة جاهز", await c.NotificationTitlesAsync(k.Patient.Id));
        Assert.Contains("تقرير أشعة جاهز", await c.NotificationTitlesAsync(k.Doctor.Id));
        Assert.Equal(409, (await c.Http.PutAsync($"/api/imaging-orders/{k.ImagingId}/report", new { findings = "تعديل", impression = "تعديل" }, rad)).Status);
    }

    [Fact]
    public async Task Device_must_match_modality_be_free_and_in_the_branch()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        var rad = await c.TokenAsync(UserRole.Radiology);
        var mri = await DeviceAsync(c, "MRI");
        var xray = await DeviceAsync(c);
        var at = c.Clock.GetUtcNow().UtcDateTime.AddHours(5);

        Assert.Equal(400, (await c.Http.PostAsync($"/api/imaging-orders/{k.ImagingId}/schedule", new { scheduledAt = at, deviceId = mri }, rad)).Status);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/imaging-orders/{k.ImagingId}/schedule", new { scheduledAt = c.Clock.GetUtcNow().UtcDateTime.AddDays(-1) }, rad)).Status);
        Assert.Equal(200, (await c.Http.PostAsync($"/api/imaging-orders/{k.ImagingId}/schedule", new { scheduledAt = at, deviceId = xray }, rad)).Status);

        var k2 = await c.ConsultAsync();
        Assert.Equal("device_busy", (await c.Http.PostAsync($"/api/imaging-orders/{k2.ImagingId}/schedule", new { scheduledAt = at.AddMinutes(10), deviceId = xray }, rad)).Code);
        Assert.Equal(200, (await c.Http.PostAsync($"/api/imaging-orders/{k2.ImagingId}/schedule", new { scheduledAt = at.AddMinutes(45), deviceId = xray }, rad)).Status);
    }

    [Fact]
    public async Task Scope_and_roles_are_enforced()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        var foreign = await c.TokenAsync(UserRole.Radiology, branchId: await c.OtherBranchAsync());
        Assert.Equal(404, (await c.Http.PostAsync($"/api/imaging-orders/{k.ImagingId}/start", null, foreign)).Status);
        Assert.Equal(403, (await c.Http.PostAsync($"/api/imaging-orders/{k.ImagingId}/start", null, await c.TokenAsync(UserRole.Lab))).Status);
        Assert.Equal("Cancelled", (await c.Http.PostAsync($"/api/imaging-orders/{k.ImagingId}/cancel", new { reason = "تكرار" }, k.DoctorToken)).Str("status"));
    }
}
