using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudCare.Api.Tests;

[Collection(ApiCollection.Name)]
public class SchemaTests(ApiFactory factory)
{
    private async Task<(IServiceScope scope, ClaudCareDbContext db)> Open()
    {
        _ = factory.CreateClient(); // يضمن تشغيل التطبيق (إنشاء القاعدة وتطبيق الـ migrations)
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaudCareDbContext>();
        return await Task.FromResult((scope, db));
    }

    [Fact]
    public async Task Startup_applies_all_migrations_and_the_model_matches_the_database()
    {
        var (scope, db) = await Open();
        using var _ = scope;
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        // لا تغييرات في النموذج غير مضمَّنة في migration (يمنع نسيان إضافة migration)
        Assert.False(db.Database.HasPendingModelChanges(), "النموذج تغيّر دون إضافة migration جديد.");
    }

    [Fact]
    public async Task Every_entity_has_a_table()
    {
        var (scope, db) = await Open();
        using var _ = scope;
        var expected = db.Model.GetEntityTypes().Select(t => t.GetTableName()!).Distinct().ToList();
        Assert.True(expected.Count >= 60, $"عدد الجداول المتوقع كبير: {expected.Count}");

        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()";
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) actual.Add(r.GetString(0));

        var missing = expected.Where(t => !actual.Contains(t)).ToList();
        Assert.True(missing.Count == 0, "جداول ناقصة: " + string.Join(", ", missing));
    }

    [Fact]
    public async Task Specialties_are_seeded_with_arabic_names()
    {
        var (scope, db) = await Open();
        using var _ = scope;
        var all = await db.Specialties.AsNoTracking().ToListAsync();
        Assert.True(all.Count >= 15);
        Assert.Equal("القلب", all.Single(s => s.Key == "cardiology").NameAr);
        Assert.Equal(all.Count, all.Select(s => s.Key).Distinct().Count());
    }

    [Fact]
    public async Task Enums_are_stored_as_readable_strings_and_arabic_text_round_trips()
    {
        var (scope, db) = await Open();
        using var _ = scope;
        var b = await TestData.BranchAsync(db);
        var u = TestData.NewUser(UserRole.Pharmacy, email: $"{TestData.Uniq()}@t.test", branchId: b.Id);
        u.FullName = "ص. لمى الشهري – صيدلانية";
        u.Gender = Gender.Female;
        db.Add(u);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var back = await db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id);
        Assert.Equal("ص. لمى الشهري – صيدلانية", back.FullName);
        Assert.Equal(UserRole.Pharmacy, back.Role);

        var raw = await db.Database.SqlQuery<string>($"SELECT Role AS Value FROM Users WHERE Id = {u.Id}").SingleAsync();
        Assert.Equal("Pharmacy", raw);
    }

    [Fact]
    public async Task Decimals_dates_and_text_columns_keep_precision()
    {
        var (scope, db) = await Open();
        using var _ = scope;
        var (doctor, patient, branch) = await TestData.DoctorAndPatientAsync(db);
        var order = new LabOrder { No = "L-" + TestData.Uniq(), PatientId = patient.Id, DoctorId = doctor.Id, TestName = "فيتامين د", OrderedAt = new DateTime(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc) };
        order.Items.Add(new LabResultItem { Name = "فيتامين د", Value = 14.125m, Unit = "نانوجرام/مل", RefMin = 30, RefMax = 100, Flag = ResultFlag.Critical });
        var visit = new Visit { PatientId = patient.Id, DoctorId = doctor.Id, BranchId = branch.Id, VisitDate = new DateOnly(2026, 10, 12), TemperatureC = 37.8m, Notes = new string('ب', 5000), Diagnosis = "x", Complaint = "y" };
        db.AddRange(order, visit);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        Assert.Equal(14.125m, (await db.LabResultItems.AsNoTracking().SingleAsync(i => i.LabOrderId == order.Id)).Value);
        var v = await db.Visits.AsNoTracking().SingleAsync(x => x.Id == visit.Id);
        Assert.Equal(37.8m, v.TemperatureC);
        Assert.Equal(new DateOnly(2026, 10, 12), v.VisitDate);
        Assert.Equal(5000, v.Notes!.Length);
    }

    [Fact]
    public void Money_helpers_compute_remaining_net_and_cash_difference()
    {
        var inv = new Invoice { Subtotal = 400, Discount = 20, InsuranceCovered = 280, PaidAmount = 50 };
        Assert.Equal(380, inv.Total);
        Assert.Equal(50, inv.Remaining);
        Assert.Equal(0, new Invoice { Subtotal = 100, InsuranceCovered = 100 }.Remaining);
        Assert.Equal(0, new Invoice { Subtotal = 100, PaidAmount = 150 }.Remaining); // لا قيمة سالبة

        Assert.Equal(29_800, new PayrollRecord { Base = 28_000, Extras = 3_200, Deductions = 1_400 }.Net);

        var close = new DailyClose { CashExpected = 18_450, CashCounted = 18_150, CardExpected = 42_300, CardCounted = 42_300 };
        Assert.Equal(-300, close.Difference);
    }
}
