using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ClaudCare.Api.Tests;

[Collection(ApiCollection.Name)]
public class ConstraintTests(ApiFactory factory)
{
    private ClaudCareDbContext NewDb()
    {
        _ = factory.CreateClient();
        return factory.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<ClaudCareDbContext>();
    }

    [Fact]
    public async Task Email_and_phone_must_be_unique_but_missing_values_are_allowed()
    {
        var db = NewDb();
        var email = $"{TestData.Uniq()}@t.test";
        db.Add(TestData.NewUser(UserRole.Reception, email: email));
        await db.SaveChangesAsync();

        // عدة مستخدمين بلا بريد ولا جوال: مسموح (NULL لا يتعارض)
        db.AddRange(TestData.NewUser(UserRole.Nurse), TestData.NewUser(UserRole.Nurse));
        await db.SaveChangesAsync();

        db.Add(TestData.NewUser(UserRole.Lab, email: email));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_doctor_slot_cannot_be_booked_twice_but_is_free_again_after_cancellation()
    {
        var db = NewDb();
        var (doctor, patient, branch) = await TestData.DoctorAndPatientAsync(db);
        var other = TestData.NewUser(UserRole.Patient, phone: "05" + Random.Shared.NextInt64(10_000_000, 99_999_999));
        db.Add(other);
        await db.SaveChangesAsync();
        var date = new DateOnly(2026, 11, 3);

        var first = TestData.NewAppointment(patient, doctor, branch.Id, date, 630);
        db.Add(first);
        await db.SaveChangesAsync();

        // حجز آخر لنفس الخانة → يرفضه الفهرس الفريد
        var clash = TestData.NewAppointment(other, doctor, branch.Id, date, 630);
        db.Add(clash);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.Entry(clash).State = EntityState.Detached;

        // خانة أخرى لنفس الطبيب مسموحة
        db.Add(TestData.NewAppointment(other, doctor, branch.Id, date, 660));
        await db.SaveChangesAsync();

        // بعد الإلغاء (المفتاح يصبح NULL) تُحجز الخانة من جديد
        first.Status = AppointmentStatus.Cancelled;
        first.ActiveSlotKey = null;
        await db.SaveChangesAsync();
        db.Add(TestData.NewAppointment(other, doctor, branch.Id, date, 630));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Clinical_and_financial_records_cannot_be_orphaned_by_deleting_the_user()
    {
        var db = NewDb();
        var (doctor, patient, branch) = await TestData.DoctorAndPatientAsync(db);
        db.Add(TestData.NewAppointment(patient, doctor, branch.Id, new DateOnly(2026, 11, 4), 540));
        await db.SaveChangesAsync();

        // حذف مباشر في القاعدة (دون تتبع EF) يجب أن تمنعه المفاتيح الأجنبية
        var ex = await Record.ExceptionAsync(() => db.Users.Where(u => u.Id == patient.Id).ExecuteDeleteAsync());
        Assert.IsType<MySqlConnector.MySqlException>(ex?.GetBaseException());
        Assert.Equal(MySqlConnector.MySqlErrorCode.RowIsReferenced2, ((MySqlConnector.MySqlException)ex!.GetBaseException()).ErrorCode);
        Assert.NotNull(await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == patient.Id));
    }

    [Fact]
    public async Task Deleting_an_invoice_removes_its_items_and_payments_but_not_the_patient()
    {
        var db = NewDb();
        var (_, patient, branch) = await TestData.DoctorAndPatientAsync(db);
        var inv = new Invoice { No = "I-" + TestData.Uniq(), PatientId = patient.Id, BranchId = branch.Id, Title = "كشف", IssueDate = new DateOnly(2026, 10, 12), DueDate = new DateOnly(2026, 10, 25), Subtotal = 400 };
        inv.Items.Add(new InvoiceItem { Description = "كشف استشاري", UnitPrice = 300 });
        inv.Payments.Add(new Payment { Amount = 80, Method = PaymentMethod.Card, PaidAt = DateTime.UtcNow });
        db.Add(inv);
        await db.SaveChangesAsync();

        db.Remove(inv);
        await db.SaveChangesAsync();
        Assert.Empty(await db.InvoiceItems.Where(i => i.InvoiceId == inv.Id).ToListAsync());
        Assert.Empty(await db.Payments.Where(i => i.InvoiceId == inv.Id).ToListAsync());
        Assert.NotNull(await db.Users.FindAsync(patient.Id));
    }

    [Fact]
    public async Task Prescription_items_are_deleted_with_the_prescription()
    {
        var db = NewDb();
        var (doctor, patient, _) = await TestData.DoctorAndPatientAsync(db);
        var rx = new Prescription { No = "RX-" + TestData.Uniq(), PatientId = patient.Id, DoctorId = doctor.Id };
        rx.Items.Add(new PrescriptionItem { DrugName = "ميتفورمين 500 مجم", Frequency = "مرتين يوميًا", TimesPerDay = 2, DurationDays = 30, Quantity = 60 });
        db.Add(rx);
        await db.SaveChangesAsync();
        db.Remove(rx);
        await db.SaveChangesAsync();
        Assert.Empty(await db.PrescriptionItems.Where(i => i.PrescriptionId == rx.Id).ToListAsync());
    }

    [Fact]
    public async Task Auditable_entities_get_utc_timestamps_from_the_clock()
    {
        _ = factory.CreateClient();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 9, 15, 0, TimeSpan.Zero));
        var opts = new DbContextOptionsBuilder<ClaudCareDbContext>().UseMySql(ApiFactory.ConnectionString, new MariaDbServerVersion(new Version(10, 4, 28))).Options;
        await using var db = new ClaudCareDbContext(opts, clock);

        var u = TestData.NewUser(UserRole.Admin, email: $"{TestData.Uniq()}@t.test");
        db.Add(u);
        await db.SaveChangesAsync();
        Assert.Equal(new DateTime(2026, 10, 27, 9, 15, 0), u.CreatedAt);
        Assert.Null(u.UpdatedAt);

        clock.Advance(TimeSpan.FromHours(2));
        u.FullName = "اسم جديد";
        await db.SaveChangesAsync();
        Assert.Equal(new DateTime(2026, 10, 27, 11, 15, 0), u.UpdatedAt);
        Assert.Equal(new DateTime(2026, 10, 27, 9, 15, 0), u.CreatedAt);
    }
}
