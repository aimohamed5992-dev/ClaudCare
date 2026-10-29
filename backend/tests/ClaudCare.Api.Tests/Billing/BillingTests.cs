using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Billing;

[Collection(ApiCollection.Name)]
public class InvoiceCreationTests(ApiFactory root)
{
    [Fact]
    public async Task Booking_with_card_and_insurance_creates_a_paid_numbered_invoice_and_consumes_the_limit()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 300);
        var p = await c.PatientAsync();
        var policyId = await c.InsureAsync(p.Id, copay: 0.2m);

        var appt = await c.BookOkAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H10);
        var list = await c.Http.GetApiAsync("/api/invoices", p.Token);
        Assert.Equal(1, list["page"].J("total").GetInt32());
        var inv = list["page"].J("items")[0];

        Assert.StartsWith("INV-", inv.GetProperty("no").GetString());
        Assert.Equal(appt, inv.GetProperty("appointmentId").GetInt32());
        Assert.Equal("Paid", inv.GetProperty("status").GetString());
        Assert.Equal(300m, inv.GetProperty("subtotal").GetDecimal());
        Assert.Equal(240m, inv.GetProperty("insuranceCovered").GetDecimal());
        Assert.Equal(60m, inv.GetProperty("paid").GetDecimal());
        Assert.Equal(0m, inv.GetProperty("remaining").GetDecimal());
        Assert.Equal(1, inv.GetProperty("payments").GetArrayLength());
        Assert.Equal(0m, list["outstandingTotal"].GetDecimal());

        Assert.Equal(240m, await c.WithDb(async db => (await db.PatientInsurances.AsNoTracking().FirstAsync(x => x.Id == policyId)).UsedAmount));
        Assert.Equal(inv.GetProperty("id").GetInt32(), await c.WithDb(async db => (await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appt)).InvoiceId));
    }

    [Fact]
    public async Task Pay_at_clinic_leaves_the_invoice_unpaid_and_fully_covered_visits_are_marked_covered()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 300);
        var cash = await c.PatientAsync();
        var covered = await c.PatientAsync();
        await c.InsureAsync(covered.Id, copay: 0m);

        Assert.Equal(201, (await c.BookAsync(cash.Token, doc.Id, Clinic.Wed, Clinic.H10, payment: "PayAtClinic")).Status);
        var a = (await c.Http.GetApiAsync("/api/invoices", cash.Token));
        Assert.Equal("Unpaid", a["page"].J("items")[0].GetProperty("status").GetString());
        Assert.Equal(300m, a["outstandingTotal"].GetDecimal());
        Assert.Equal(1, a["unpaidCount"].GetInt32());

        Assert.Equal(201, (await c.BookAsync(covered.Token, doc.Id, Clinic.Wed, Clinic.H11, payment: null)).Status);
        var b = (await c.Http.GetApiAsync("/api/invoices", covered.Token))["page"].J("items")[0];
        Assert.Equal("Covered", b.GetProperty("status").GetString());
        Assert.Equal(0, b.GetProperty("payments").GetArrayLength());
    }
}

[Collection(ApiCollection.Name)]
public class InvoicePaymentTests(ApiFactory root)
{
    private static async Task<(Clinic c, Clinic.Pat p, int invoiceId)> UnpaidAsync(ApiFactory root, decimal price = 300)
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: price);
        var p = await c.PatientAsync();
        await c.BookAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H10, payment: "PayAtClinic");
        var id = (await c.Http.GetApiAsync("/api/invoices", p.Token))["page"].J("items")[0].GetProperty("id").GetInt32();
        return (c, p, id);
    }

    [Fact]
    public async Task Patient_pays_the_full_remaining_by_card_and_the_appointment_becomes_paid()
    {
        var (c, p, id) = await UnpaidAsync(root);
        var r = await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "ApplePay" }, p.Token);
        Assert.Equal(200, r.Status);
        Assert.Equal("Paid", r.Str("status"));
        Assert.Equal(300m, r["paid"].GetDecimal());

        var paid = await c.WithDb(async db => (await db.Appointments.AsNoTracking().FirstAsync(a => a.InvoiceId == id)).Paid);
        Assert.True(paid);
        Assert.Equal("invalid_state", (await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Card" }, p.Token)).Code);
    }

    [Fact]
    public async Task Patient_cannot_pay_with_cash_or_pay_someone_elses_invoice()
    {
        var (c, p, id) = await UnpaidAsync(root);
        var other = await c.PatientAsync();
        Assert.Equal(400, (await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Cash" }, p.Token)).Status);
        Assert.Equal(404, (await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Card" }, other.Token)).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/invoices/{id}", other.Token)).Status);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/invoices/{id}/pay", new { }, p.Token)).Status); // الطريقة إلزامية
    }

    [Fact]
    public async Task Reception_takes_partial_cash_with_change_then_settles_the_rest_and_over_payment_is_rejected()
    {
        var (c, p, id) = await UnpaidAsync(root);
        var desk = await c.TokenAsync(UserRole.Reception);

        var first = await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Cash", amount = 100, tendered = 150 }, desk);
        Assert.Equal(200, first.Status);
        Assert.Equal("PartiallyPaid", first.Str("status"));
        Assert.Equal(200m, first["remaining"].GetDecimal());
        Assert.Equal(50m, first["payments"][0].GetProperty("changeGiven").GetDecimal());

        Assert.Equal(400, (await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Cash", amount = 250 }, desk)).Status);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Card", amount = 50, tendered = 60 }, desk)).Status);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Cash", amount = 50, tendered = 40 }, desk)).Status);

        var last = await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Card" }, desk);
        Assert.Equal("Paid", last.Str("status"));
        Assert.Contains("تم استلام دفعتك", await c.NotificationTitlesAsync(p.Id));
    }

    [Fact]
    public async Task Invoice_visibility_follows_role_and_branch()
    {
        var (c, p, id) = await UnpaidAsync(root);
        var branch2 = await c.WithDb(async db => (await TestData.BranchAsync(db)).Id);
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/invoices/{id}", await c.TokenAsync(UserRole.Reception))).Status);
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/invoices/{id}", await c.TokenAsync(UserRole.Reception, branchId: branch2))).Status);
        Assert.Equal(404, (await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Cash" }, await c.TokenAsync(UserRole.Reception, branchId: branch2))).Status);
        Assert.Equal(200, (await c.Http.GetApiAsync($"/api/invoices/{id}", await c.TokenAsync(UserRole.Finance))).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/invoices", await c.TokenAsync(UserRole.Pharmacy))).Status);
        Assert.Equal(403, (await c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Cash" }, await c.TokenAsync(UserRole.Lab))).Status);
        Assert.Equal(401, (await c.Http.GetApiAsync("/api/invoices")).Status);
    }

    [Fact]
    public async Task Two_simultaneous_payments_cannot_both_succeed()
    {
        var (c, p, id) = await UnpaidAsync(root);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => c.Http.PostAsync($"/api/invoices/{id}/pay", new { method = "Card" }, p.Token)));
        Assert.Single(results, r => r.Status == 200);
        Assert.All(results.Where(r => r.Status != 200), r => Assert.Equal(409, r.Status));
        Assert.Equal(1, await c.WithDb(db => db.Payments.CountAsync(x => x.InvoiceId == id)));
        Assert.Equal(300m, await c.WithDb(async db => (await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == id)).PaidAmount));
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var (c, p, id) = await UnpaidAsync(root);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/invoices?status=Unpaid", p.Token))["page"].J("total").GetInt32());
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/invoices?status=Paid", p.Token))["page"].J("total").GetInt32());
    }
}

[Collection(ApiCollection.Name)]
public class InvoiceCancellationTests(ApiFactory root)
{
    [Fact]
    public async Task Free_cancellation_cancels_the_invoice_refunds_the_card_and_restores_the_insurance_limit()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 300);
        var p = await c.PatientAsync();
        var policyId = await c.InsureAsync(p.Id, copay: 0.2m);
        var appt = await c.BookOkAsync(p.Token, doc.Id, Clinic.Thu, Clinic.H10); // بعد أكثر من 24 ساعة

        var cancel = await c.Act(p.Token, appt, "cancel", new { reason = "ظرف طارئ" });
        Assert.Equal(200, cancel.Status);
        Assert.False(cancel["lateCancellation"].GetBoolean());

        var inv = (await c.Http.GetApiAsync("/api/invoices", p.Token))["page"].J("items")[0];
        Assert.Equal("Cancelled", inv.GetProperty("status").GetString());
        Assert.Equal(0m, inv.GetProperty("paid").GetDecimal());
        Assert.Equal(-60m, inv.GetProperty("payments")[1].GetProperty("amount").GetDecimal());
        Assert.Equal(0m, await c.WithDb(async db => (await db.PatientInsurances.AsNoTracking().FirstAsync(x => x.Id == policyId)).UsedAmount));
        Assert.False(await c.WithDb(async db => (await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appt)).Paid));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/invoices/{inv.GetProperty("id").GetInt32()}/pay", new { method = "Card" }, p.Token)).Status);
    }

    [Fact]
    public async Task Late_cancellation_keeps_only_the_fee_and_refunds_the_difference()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 300);
        var p = await c.PatientAsync();
        var appt = await c.BookOkAsync(p.Token, doc.Id, Clinic.Today, Clinic.H11); // بعد ساعة وخمس وأربعين دقيقة

        var cancel = await c.Act(p.Token, appt, "cancel", new { reason = "ظرف طارئ" });
        Assert.True(cancel["lateCancellation"].GetBoolean());
        Assert.Equal(150m, cancel["cancellationFee"].GetDecimal());

        var inv = (await c.Http.GetApiAsync("/api/invoices", p.Token))["page"].J("items")[0];
        Assert.Equal("Paid", inv.GetProperty("status").GetString());
        Assert.Equal(150m, inv.GetProperty("subtotal").GetDecimal());
        Assert.Equal(150m, inv.GetProperty("paid").GetDecimal());
        Assert.Equal("رسوم إلغاء متأخر", inv.GetProperty("items")[0].GetProperty("description").GetString());
        Assert.Equal(-150m, inv.GetProperty("payments")[1].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Late_cancellation_of_an_unpaid_visit_leaves_the_fee_to_collect()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 300);
        var p = await c.PatientAsync();
        var appt = await c.BookAsync(p.Token, doc.Id, Clinic.Today, Clinic.H11, payment: "PayAtClinic");
        await c.Act(p.Token, appt["id"].GetInt32(), "cancel", new { reason = "ظرف طارئ" });

        var list = await c.Http.GetApiAsync("/api/invoices", p.Token);
        var inv = list["page"].J("items")[0];
        Assert.Equal("Unpaid", inv.GetProperty("status").GetString());
        Assert.Equal(150m, inv.GetProperty("remaining").GetDecimal());
        Assert.Contains("رسوم إلغاء متأخر", await c.NotificationTitlesAsync(p.Id));
        Assert.Equal("Paid", (await c.Http.PostAsync($"/api/invoices/{inv.GetProperty("id").GetInt32()}/pay", new { method = "Card" }, p.Token)).Str("status"));
    }

    [Fact]
    public async Task Cancelling_by_the_doctor_is_always_free_even_when_close_to_the_visit()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 300);
        var p = await c.PatientAsync();
        var appt = await c.BookOkAsync(p.Token, doc.Id, Clinic.Today, Clinic.H11);
        var r = await c.Act(await c.DoctorTokenAsync(doc), appt, "cancel", new { reason = "طارئ لدى الطبيب" });
        Assert.Equal(200, r.Status);
        Assert.Equal("Cancelled", (await c.Http.GetApiAsync("/api/invoices", p.Token))["page"].J("items")[0].GetProperty("status").GetString());
    }
}

[Collection(ApiCollection.Name)]
public class MyInsuranceTests(ApiFactory root)
{
    [Fact]
    public async Task Returns_the_active_policy_with_remaining_limit_or_404()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        Assert.Equal("insurance_not_found", (await c.Http.GetApiAsync("/api/me/insurance", p.Token)).Code);

        await c.InsureAsync(p.Id, copay: 0.2m, limit: 100_000, used: 62_000);
        var r = await c.Http.GetApiAsync("/api/me/insurance", p.Token);
        Assert.Equal(38_000m, r["remaining"].GetDecimal());
        Assert.Equal(0.2m, r["copayRate"].GetDecimal());
        Assert.False(r["expired"].GetBoolean());
    }
}
