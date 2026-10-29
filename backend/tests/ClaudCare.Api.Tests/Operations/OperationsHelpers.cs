using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Clinical;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Operations;

public sealed record Consulted(Clinic.Doc Doctor, Clinic.Pat Patient, string DoctorToken, int LabId, int ImagingId, int RxId, int DrugId);

public static class OperationsHelpers
{
    /// <summary>كشف كامل ينتج طلب مختبر (HbA1c) وطلب أشعة (صدر) ووصفة بدواء واحد.</summary>
    public static async Task<Consulted> ConsultAsync(this Clinic c, int rxQuantityDays = 30)
    {
        var d = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.EnsureProfileAsync(p.Id);
        var (appt, dt) = await c.InProgressAsync(d, p);
        var drug = await c.DrugIdAsync("ميتفورمين 500 مجم");
        var res = await c.VisitAsync(dt, ClinicalHelpers.Visit(appt, labs: ["hba1c"], imaging: ["chest-xray"], rx: [ClinicalHelpers.Rx(drug, timesPerDay: 2, days: rxQuantityDays)]));
        Assert.Equal(201, res.Status);
        var rx = await c.WithDb(async db => (await db.Prescriptions.AsNoTracking().OrderByDescending(r => r.Id).FirstAsync(r => r.PatientId == p.Id)).Id);
        return new Consulted(d, p, dt, res["labOrders"][0].GetProperty("id").GetInt32(), res["imagingOrders"][0].GetProperty("id").GetInt32(), rx, drug);
    }

    public static Task StockAsync(this Clinic c, int drugId, int quantity, int min = 5, decimal price = 12, int? branchId = null) =>
        c.WithDb(async db =>
        {
            var b = branchId ?? c.BranchId;
            var i = await db.InventoryItems.FirstOrDefaultAsync(x => x.BranchId == b && x.DrugId == drugId);
            if (i is null) db.InventoryItems.Add(new InventoryItem { BranchId = b, DrugId = drugId, Quantity = quantity, MinQuantity = min, UnitCost = price / 2, UnitPrice = price });
            else i.Quantity = quantity;
            await db.SaveChangesAsync();
            return 0;
        });

    public static Task<int> QuantityAsync(this Clinic c, int drugId, int? branchId = null) =>
        c.WithDb(async db => (await db.InventoryItems.AsNoTracking().FirstAsync(x => x.BranchId == (branchId ?? c.BranchId) && x.DrugId == drugId)).Quantity);

    public static async Task<int> OtherBranchAsync(this Clinic c) => await c.WithDb(async db => (await TestData.BranchAsync(db)).Id);
}

public static class WardHelpers
{
    public static Task<int> BedAsync(this Clinic c, string ward = "الباطنة", BedStatus status = BedStatus.Available, int? branchId = null) =>
        c.WithDb(async db =>
        {
            var b = new Bed { BranchId = branchId ?? c.BranchId, Ward = ward, Code = "B-" + Guid.NewGuid().ToString("N")[..6], Status = status };
            db.Beds.Add(b); await db.SaveChangesAsync(); return b.Id;
        });

    public static Task<int> RoomAsync(this Clinic c, OperatingRoomState state = OperatingRoomState.Available) =>
        c.WithDb(async db =>
        {
            var r = new OperatingRoom { BranchId = c.BranchId, Code = "OR-" + Guid.NewGuid().ToString("N")[..5], State = state };
            db.OperatingRooms.Add(r); await db.SaveChangesAsync(); return r.Id;
        });

    /// <summary>طلب تنويم + قبول في سرير؛ يعيد (admissionId, bedId, token موظف التنويم).</summary>
    public static async Task<(int admission, int bed, string adm)> AdmittedAsync(this Clinic c, int patientId, string ward = "الباطنة")
    {
        var adm = await c.TokenAsync(UserRole.Admissions);
        var req = await c.Http.PostAsync("/api/admissions", new { patientId, type = "Emergency", reason = "ألم صدر", requestedWard = ward }, adm);
        Assert.Equal(201, req.Status);
        var bed = await c.BedAsync(ward);
        var admit = await c.Http.PostAsync($"/api/admissions/{req["id"].GetInt32()}/admit", new { bedId = bed }, adm);
        Assert.Equal(200, admit.Status);
        return (req["id"].GetInt32(), bed, adm);
    }
}
