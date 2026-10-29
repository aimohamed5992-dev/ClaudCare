using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Clinical;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Operations;

[Collection(ApiCollection.Name)]
public class PharmacyDispenseTests(ApiFactory root)
{
    [Fact]
    public async Task Queue_shows_stock_and_dispensing_deducts_it_records_a_sale_and_notifies()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync(rxQuantityDays: 30);
        await c.StockAsync(k.DrugId, quantity: 200);
        var ph = await c.TokenAsync(UserRole.Pharmacy);

        var q = await c.Http.GetApiAsync("/api/pharmacy/prescriptions", ph);
        var rx = q["items"].EnumerateArray().First(x => x.GetProperty("id").GetInt32() == k.RxId);
        var qty = rx.GetProperty("items")[0].GetProperty("quantity").GetInt32();
        Assert.Equal(200, rx.GetProperty("items")[0].GetProperty("inStock").GetInt32());

        Assert.Equal("Preparing", (await c.Http.PostAsync($"/api/pharmacy/prescriptions/{k.RxId}/prepare", null, ph)).Str("status"));
        var sale = await c.Http.PostAsync($"/api/pharmacy/prescriptions/{k.RxId}/dispense", new { method = "Cash" }, ph);
        Assert.Equal(200, sale.Status);
        Assert.StartsWith("SAL-", sale.Str("no"));
        Assert.Equal(200 - qty, await c.QuantityAsync(k.DrugId));
        Assert.Contains("تم صرف وصفتك", await c.NotificationTitlesAsync(k.Patient.Id));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/pharmacy/prescriptions/{k.RxId}/dispense", new { method = "Cash" }, ph)).Status); // لا صرف مرتين
        Assert.Equal(200 - qty, await c.QuantityAsync(k.DrugId));
    }

    [Fact]
    public async Task Insufficient_stock_blocks_dispensing_and_changes_nothing()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        await c.StockAsync(k.DrugId, quantity: 1);
        var ph = await c.TokenAsync(UserRole.Pharmacy);
        var r = await c.Http.PostAsync($"/api/pharmacy/prescriptions/{k.RxId}/dispense", new { method = "Card" }, ph);
        Assert.Equal("insufficient_stock", r.Code);
        Assert.Equal(1, await c.QuantityAsync(k.DrugId));
        Assert.Equal(PrescriptionStatus.Pending, await c.WithDb(async db => (await db.Prescriptions.AsNoTracking().FirstAsync(x => x.Id == k.RxId)).Status));
    }

    [Fact]
    public async Task Concurrent_dispenses_of_the_same_prescription_succeed_once()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        await c.StockAsync(k.DrugId, quantity: 500);
        var ph = await c.TokenAsync(UserRole.Pharmacy);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => c.Http.PostAsync($"/api/pharmacy/prescriptions/{k.RxId}/dispense", new { method = "Cash" }, ph)));
        Assert.Single(results, r => r.Status == 200);
        Assert.Equal(1, await c.WithDb(db => db.PharmacySales.CountAsync(s => s.PrescriptionId == k.RxId)));
    }

    [Fact]
    public async Task Rejecting_notifies_patient_and_doctor_and_scope_is_per_branch()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        var foreign = await c.TokenAsync(UserRole.Pharmacy, branchId: await c.OtherBranchAsync());
        Assert.Equal(404, (await c.Http.PostAsync($"/api/pharmacy/prescriptions/{k.RxId}/reject", new { reason = "غير متوفر" }, foreign)).Status);

        var ph = await c.TokenAsync(UserRole.Pharmacy);
        Assert.Equal("Rejected", (await c.Http.PostAsync($"/api/pharmacy/prescriptions/{k.RxId}/reject", new { reason = "تعارض دوائي" }, ph)).Str("status"));
        Assert.Contains("تعذّر صرف الوصفة", await c.NotificationTitlesAsync(k.Patient.Id));
        Assert.Contains("رفضت الصيدلية وصفة", await c.NotificationTitlesAsync(k.Doctor.Id));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/pharmacy/prescriptions/{k.RxId}/prepare", null, ph)).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/pharmacy/prescriptions", await c.TokenAsync(UserRole.Nurse))).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/pharmacy/prescriptions", k.Patient.Token)).Status);
    }
}

[Collection(ApiCollection.Name)]
public class PharmacyStockTests(ApiFactory root)
{
    [Fact]
    public async Task Inventory_add_update_adjust_low_filter_and_floor_at_zero()
    {
        var c = await Clinic.CreateAsync(root);
        var drug = await c.DrugIdAsync("ميتفورمين 500 مجم");
        var ph = await c.TokenAsync(UserRole.Pharmacy);
        var other = await c.TokenAsync(UserRole.Pharmacy, branchId: await c.OtherBranchAsync());

        var add = await c.Http.PostAsync("/api/pharmacy/inventory", new { drugId = drug, quantity = 8, minQuantity = 10, unitCost = 5, unitPrice = 9 }, ph);
        Assert.Equal(201, add.Status);
        Assert.True(add["low"].GetBoolean());
        Assert.Equal("inventory_exists", (await c.Http.PostAsync("/api/pharmacy/inventory", new { drugId = drug, quantity = 1, minQuantity = 1, unitCost = 1, unitPrice = 1 }, ph)).Code);
        var id = add["id"].GetInt32();

        Assert.Contains((await c.Http.GetApiAsync("/api/pharmacy/inventory?lowOnly=true&pageSize=50", ph))["items"].EnumerateArray(), x => x.GetProperty("id").GetInt32() == id);
        Assert.Equal(404, (await c.Http.PostAsync($"/api/pharmacy/inventory/{id}/adjust", new { delta = 5, reason = "جرد" }, other)).Status);

        var up = await c.Http.PostAsync($"/api/pharmacy/inventory/{id}/adjust", new { delta = 20, reason = "جرد" }, ph);
        Assert.Equal(28, up["quantity"].GetInt32());
        Assert.False(up["low"].GetBoolean());
        Assert.Equal("insufficient_stock", (await c.Http.PostAsync($"/api/pharmacy/inventory/{id}/adjust", new { delta = -100, reason = "تالف" }, ph)).Code);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/pharmacy/inventory/{id}/adjust", new { delta = 0, reason = "لا شيء" }, ph)).Status);
        Assert.Equal(15m, (await c.Http.PutAsync($"/api/pharmacy/inventory/{id}", new { unitPrice = 15 }, ph))["unitPrice"].GetDecimal());
    }

    [Fact]
    public async Task Direct_sale_prices_from_inventory_deducts_and_rejects_oversell()
    {
        var c = await Clinic.CreateAsync(root);
        var drug = await c.DrugIdAsync("ميتفورمين 500 مجم");
        await c.StockAsync(drug, quantity: 10, price: 12);
        var ph = await c.TokenAsync(UserRole.Pharmacy);

        var sale = await c.Http.PostAsync("/api/pharmacy/sales", new { lines = new[] { new { drugId = drug, quantity = 2 }, new { drugId = drug, quantity = 1 } }, method = "Cash", customerName = "عميل" }, ph);
        Assert.Equal(201, sale.Status);
        Assert.Equal(36m, sale["total"].GetDecimal()); // البنود المكررة تُدمج: 3 × 12
        Assert.Equal(7, await c.QuantityAsync(drug));

        Assert.Equal("insufficient_stock", (await c.Http.PostAsync("/api/pharmacy/sales", new { lines = new[] { new { drugId = drug, quantity = 8 } }, method = "Cash" }, ph)).Code);
        Assert.Equal(7, await c.QuantityAsync(drug));
        Assert.Equal(400, (await c.Http.PostAsync("/api/pharmacy/sales", new { lines = Array.Empty<object>(), method = "Cash" }, ph)).Status);
        Assert.Equal(400, (await c.Http.PostAsync("/api/pharmacy/sales", new { lines = new[] { new { drugId = drug, quantity = 1 } }, method = "PayAtClinic" }, ph)).Status);
        Assert.Equal(1, (await c.Http.GetApiAsync($"/api/pharmacy/sales/{sale["id"].GetInt32()}", ph))["lines"].GetArrayLength());
    }

    [Fact]
    public async Task Purchase_order_goes_draft_sent_partial_then_received_and_grows_the_stock()
    {
        var c = await Clinic.CreateAsync(root);
        var drug = await c.DrugIdAsync("فيتامين د 50,000");
        var ph = await c.TokenAsync(UserRole.Pharmacy);

        var po = await c.Http.PostAsync("/api/pharmacy/purchase-orders", new { supplier = "مورد الاختبار", lines = new[] { new { drugId = drug, quantity = 100, unitCost = 4 } } }, ph);
        Assert.Equal(201, po.Status);
        Assert.StartsWith("PO-", po.Str("no"));
        Assert.Equal(400m, po["total"].GetDecimal());
        var id = po["id"].GetInt32();
        var line = po["lines"][0].GetProperty("id").GetInt32();

        Assert.Equal(409, (await c.Http.PostAsync($"/api/pharmacy/purchase-orders/{id}/receive", new { lines = new[] { new { lineId = line, quantity = 10 } } }, ph)).Status); // لم يُرسل
        Assert.Equal("Sent", (await c.Http.PostAsync($"/api/pharmacy/purchase-orders/{id}/send", null, ph)).Str("status"));
        Assert.Equal(400, (await c.Http.PostAsync($"/api/pharmacy/purchase-orders/{id}/receive", new { lines = new[] { new { lineId = line, quantity = 101 } } }, ph)).Status);

        Assert.Equal("PartiallyReceived", (await c.Http.PostAsync($"/api/pharmacy/purchase-orders/{id}/receive", new { lines = new[] { new { lineId = line, quantity = 40 } } }, ph)).Str("status"));
        Assert.Equal(40, await c.QuantityAsync(drug)); // الصنف أُنشئ بالاستلام
        Assert.Equal("Received", (await c.Http.PostAsync($"/api/pharmacy/purchase-orders/{id}/receive", new { lines = new[] { new { lineId = line, quantity = 60 } } }, ph)).Str("status"));
        Assert.Equal(100, await c.QuantityAsync(drug));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/pharmacy/purchase-orders/{id}/receive", new { lines = new[] { new { lineId = line, quantity = 1 } } }, ph)).Status);
    }
}
