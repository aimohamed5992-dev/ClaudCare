using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Clinical;

public static class ClinicalHelpers
{
    /// <summary>ينشئ ملف المريض الصحي (إن لم يوجد) مع حساسيات/أمراض مزمنة اختيارية.</summary>
    public static Task EnsureProfileAsync(this Clinic c, int patientId, string[]? allergies = null, string[]? conditions = null, string? blood = null, int? birthYear = null) =>
        c.WithDb(async db =>
        {
            var p = await db.PatientProfiles.Include(x => x.Allergies).Include(x => x.Conditions).FirstOrDefaultAsync(x => x.UserId == patientId);
            if (p is null)
            {
                p = new PatientProfile { UserId = patientId, MedicalRecordNo = "MRN-T" + TestData.Uniq() };
                db.PatientProfiles.Add(p);
            }
            foreach (var a in allergies ?? []) p.Allergies.Add(new PatientAllergy { Name = a });
            foreach (var cnd in conditions ?? []) p.Conditions.Add(new PatientCondition { Name = cnd });
            if (blood is not null) p.BloodType = blood;
            if (birthYear is not null) (await db.Users.FirstAsync(u => u.Id == patientId)).BirthYear = birthYear;
            await db.SaveChangesAsync();
            return 0;
        });

    public static Task<int> DrugIdAsync(this Clinic c, string name) => c.WithDb(async db => (await db.Drugs.FirstAsync(d => d.Name == name)).Id);

    /// <summary>موعد اليوم 09:30 يمرّ بالوصول والبدء حتى «قيد الكشف». يعيد (الموعد، توكن الطبيب).</summary>
    public static async Task<(int appointmentId, string doctorToken)> InProgressAsync(this Clinic c, Clinic.Doc doctor, Clinic.Pat patient, int minutes = Clinic.H0930, string? deskToken = null)
    {
        var id = await c.BookOkAsync(patient.Token, doctor.Id, Clinic.Today, minutes);
        deskToken ??= await c.TokenAsync(UserRole.Reception);
        var arrive = await c.Act(deskToken, id, "arrive");
        Assert.Equal(200, arrive.Status);
        var dt = await c.DoctorTokenAsync(doctor);
        var start = await c.Act(dt, id, "start");
        Assert.Equal(200, start.Status);
        return (id, dt);
    }

    public static Task<ApiResponse> VisitAsync(this Clinic c, string doctorToken, object body) => c.Http.PostAsync("/api/visits", body, doctorToken);

    public static object Visit(int? appointmentId, int? patientId = null, string complaint = "ألم في الصدر", string diagnosis = "ارتفاع ضغط الدم", object? vitals = null,
        string[]? labs = null, string[]? imaging = null, object[]? rx = null, string? followUp = "بعد أسبوعين", string? notes = null) =>
        new { appointmentId, patientId, complaint, diagnosis, vitals, labTests = labs, imagingStudies = imaging, prescription = rx, followUp, notes };

    public static object Rx(int? drugId, string? name = null, string frequency = "مرتين يوميًا", int timesPerDay = 2, int days = 7, string? dose = "قرص واحد", bool allergyOverride = false, string? instructions = "بعد الأكل") =>
        new { drugId, drugName = name, dose, frequency, timesPerDay, durationDays = days, instructions, allergyOverride };

    public static Task<T> Db<T>(this Clinic c, Func<ClaudCareDbContext, Task<T>> work) => c.WithDb(work);
}
