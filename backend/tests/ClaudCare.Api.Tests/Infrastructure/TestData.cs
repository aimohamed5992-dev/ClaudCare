using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Tests.Infrastructure;

/// <summary>منشئات بيانات اختبار بقيم فريدة (القاعدة مشتركة بين الاختبارات).</summary>
public static class TestData
{
    public static string Uniq() => Guid.NewGuid().ToString("N")[..10];

    public static async Task<Branch> BranchAsync(ClaudCareDbContext db)
    {
        var h = new Hospital { Name = $"مستشفى {Uniq()}" };
        var b = new Branch { Hospital = h, Name = "الفرع الرئيسي", City = "الرياض" };
        db.Add(b);
        await db.SaveChangesAsync();
        return b;
    }

    public static User NewUser(UserRole role, string? email = null, string? phone = null, int? branchId = null) => new()
    {
        FullName = $"مستخدم {Uniq()}",
        Email = email,
        Phone = phone,
        PasswordHash = "x",
        Role = role,
        BranchId = branchId,
    };

    public static async Task<(User doctor, User patient, Branch branch)> DoctorAndPatientAsync(ClaudCareDbContext db)
    {
        var branch = await BranchAsync(db);
        var doctor = NewUser(UserRole.Doctor, email: $"{Uniq()}@t.test", branchId: branch.Id);
        var patient = NewUser(UserRole.Patient, phone: "05" + Random.Shared.NextInt64(10_000_000, 99_999_999), branchId: branch.Id);
        db.AddRange(doctor, patient);
        await db.SaveChangesAsync();
        return (doctor, patient, branch);
    }

    public static Appointment NewAppointment(User patient, User doctor, int branchId, DateOnly date, int minutes, AppointmentStatus status = AppointmentStatus.Confirmed) => new()
    {
        PatientId = patient.Id,
        DoctorId = doctor.Id,
        BranchId = branchId,
        Date = date,
        StartMinutes = minutes,
        Status = status,
        Type = VisitType.Clinic,
        Reason = "كشف",
        Price = 300,
        ActiveSlotKey = Appointment.SlotKey(doctor.Id, date, minutes),
    };
}
