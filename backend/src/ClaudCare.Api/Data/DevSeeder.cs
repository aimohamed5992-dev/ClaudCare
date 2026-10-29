using ClaudCare.Api.Configuration;
using ClaudCare.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Data;

/// <summary>
/// حسابات تجريبية لكل دور (Development فقط) بكلمة مرور واحدة. تستخدمها تطبيقات الويب والموبايل أثناء التطوير.
/// تُوسَّع بالبيانات السريرية الكاملة في الخطوة 8.
/// </summary>
public sealed class DevSeeder(ClaudCareDbContext db, IPasswordHasher<User> hasher, IOptions<SeedOptions> options, ILogger<DevSeeder> logger)
{
    private sealed record Demo(UserRole Role, string Name, string Email, string? Phone = null, string? NationalId = null, string? EmployeeNo = null, Gender? Gender = null, int? BirthYear = null);

    private static readonly Demo[] Accounts =
    [
        new(UserRole.Admin, "أحمد المدير", "admin@claudcare.test", "0500000001", EmployeeNo: "AD-0001", Gender: Domain.Gender.Male),
        new(UserRole.Hospital, "مدير المستشفى", "hospital@claudcare.test", "0500000002", EmployeeNo: "HO-0001"),
        new(UserRole.Branch, "مدير الفرع", "branch@claudcare.test", "0500000003", EmployeeNo: "BR-0001"),
        new(UserRole.Lab, "فني المختبر", "lab@claudcare.test", "0500000004", EmployeeNo: "LB-0001"),
        new(UserRole.Radiology, "د. عمر الخطيب", "radiology@claudcare.test", "0500000005", EmployeeNo: "RD-0001"),
        new(UserRole.Pharmacy, "ص. لمى الشهري", "pharmacy@claudcare.test", "0500000006", EmployeeNo: "PH-0001", Gender: Domain.Gender.Female),
        new(UserRole.Doctor, "د. منى القاسم", "mona@hospital.com", "0551234567", EmployeeNo: "DR-1042", Gender: Domain.Gender.Female),
        new(UserRole.Nurse, "نادين يوسف", "nurse@claudcare.test", "0500000008", EmployeeNo: "NR-0001", Gender: Domain.Gender.Female),
        new(UserRole.Reception, "هبة ناصر", "reception@claudcare.test", "0500000009", EmployeeNo: "RC-0001", Gender: Domain.Gender.Female),
        new(UserRole.Insurance, "عبدالعزيز الحربي", "insurance@claudcare.test", "0500000010", EmployeeNo: "IN-0001"),
        new(UserRole.Finance, "دينا الفارس", "finance@claudcare.test", "0500000011", EmployeeNo: "FN-0001", Gender: Domain.Gender.Female),
        new(UserRole.Hr, "ريما العبدالله", "hr@claudcare.test", "0500000012", EmployeeNo: "HR-0001", Gender: Domain.Gender.Female),
        new(UserRole.Emergency, "خالد المطيري", "emergency@claudcare.test", "0500000013", EmployeeNo: "ER-0001"),
        new(UserRole.Admissions, "ليلى الغامدي", "admissions@claudcare.test", "0500000014", EmployeeNo: "AM-0001", Gender: Domain.Gender.Female),
        new(UserRole.Surgery, "د. سلطان الدوسري", "surgery@claudcare.test", "0500000015", EmployeeNo: "SG-0001"),
        new(UserRole.Patient, "ريم العلي", "reem@claudcare.test", "0551234500", NationalId: "1098765432", Gender: Domain.Gender.Female, BirthYear: 1998),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!options.Value.DemoAccounts) return;

        var branch = await db.Branches.OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
        if (branch is null)
        {
            branch = new Branch { Hospital = new Hospital { Name = "مستشفى ClaudCare", LicenseNo = "MOH-100234", Phone = "0112345678", Email = "info@claudcare.test" }, Name = "الفرع الرئيسي", City = "الرياض", Address = "طريق الملك فهد" };
            db.Branches.Add(branch);
            await db.SaveChangesAsync(ct);
        }

        var created = 0;
        foreach (var a in Accounts)
        {
            if (await db.Users.AnyAsync(u => u.Email == a.Email, ct)) continue;
            var u = new User
            {
                FullName = a.Name, Email = a.Email, Phone = a.Phone, NationalId = a.NationalId, EmployeeNo = a.EmployeeNo, Role = a.Role,
                Gender = a.Gender, BirthYear = a.BirthYear, BranchId = a.Role == UserRole.Admin ? null : branch.Id, PhoneConfirmed = true,
            };
            u.PasswordHash = hasher.HashPassword(u, options.Value.DemoPassword);
            db.Users.Add(u);
            await db.SaveChangesAsync(ct);

            if (a.Role == UserRole.Patient)
                db.PatientProfiles.Add(new PatientProfile { UserId = u.Id, MedicalRecordNo = $"MRN-{u.Id:D6}", BloodType = "O+", HeightCm = 165, WeightKg = 61 });
            if (a.Role == UserRole.Doctor)
                db.DoctorProfiles.Add(new DoctorProfile
                {
                    UserId = u.Id, SpecialtyId = (await db.Specialties.FirstAsync(s => s.Key == "cardiology", ct)).Id, BranchId = branch.Id, LicenseNo = "SCFHS-38877",
                    YearsExperience = 12, PatientsCount = 2400, ConsultationPrice = 300, VideoPrice = 200, Languages = "العربية، الإنجليزية", Rating = 4.9m, ReviewsCount = 312,
                    About = "استشارية أمراض القلب والأوعية الدموية، متخصصة في ارتفاع ضغط الدم وقصور القلب والوقاية من أمراض الشرايين.",
                });
            created++;
        }
        await db.SaveChangesAsync(ct);
        await SeedDoctorsAsync(branch, ct);
        await SeedInsuranceAsync(ct);
        if (created > 0) logger.LogInformation("أُنشئ {Count} حسابًا تجريبيًا (كلمة المرور: {Pwd}).", created, options.Value.DemoPassword);
    }

    private sealed record DoctorSeed(string Key, string Name, string Email, string EmployeeNo, string Phone, string Avatar, int Years, int Patients, decimal Price, decimal? Video, decimal Rating, int Reviews, string About, DayOfWeek[]? Days = null, bool RequiresConfirmation = false);

    private static readonly DayOfWeek[] SunToThu = [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday];

    private static readonly DoctorSeed[] Doctors =
    [
        new("neurology", "د. عمر الشامي", "omar@claudcare.test", "DR-1043", "0500000101", "avatars/doc-m1", 15, 3100, 350, 250, 4.8m, 204, "استشاري أمراض الدماغ والأعصاب، خبرة واسعة في الصداع النصفي والصرع واضطرابات النوم."),
        new("pediatrics", "د. ريم العتيبي", "reem.otaibi@claudcare.test", "DR-1044", "0500000102", "avatars/doc-f2", 9, 5200, 250, 150, 4.9m, 458, "أخصائية طب الأطفال وحديثي الولادة، تهتم بالتطعيمات والنمو والتغذية السليمة."),
        new("dermatology", "د. هالة منصور", "hala@claudcare.test", "DR-1045", "0500000103", "avatars/doc-f3", 8, 1900, 280, 180, 4.7m, 176, "أخصائية الأمراض الجلدية والتجميل الطبي، متخصصة في حب الشباب والأكزيما وأمراض الشعر.", RequiresConfirmation: true),
        new("orthopedics", "د. زياد النجار", "ziad@claudcare.test", "DR-1046", "0500000104", "avatars/doc-m2", 14, 2800, 320, null, 4.8m, 233, "استشاري جراحة العظام والمفاصل، متخصص في إصابات الملاعب وتنظير الركبة والكتف.", [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Thursday]),
        new("ophthalmology", "د. سامي الراشد", "sami@claudcare.test", "DR-1047", "0500000105", "avatars/doc-m3", 10, 2200, 260, 160, 4.6m, 142, "أخصائي طب وجراحة العيون، يعالج المياه البيضاء وجفاف العين وأخطاء الانكسار."),
        new("obgyn", "د. لينا الحربي", "lina@claudcare.test", "DR-1048", "0500000106", "avatars/doc-f2", 13, 4100, 330, 220, 4.9m, 389, "استشارية النساء والولادة، متخصصة في متابعة الحمل وعلاج العقم والمناظير النسائية."),
        new("ent", "د. ياسر العمر", "yasser@claudcare.test", "DR-1049", "0500000107", "avatars/doc-m1", 7, 1300, 240, null, 4.5m, 98, "أخصائي أنف وأذن وحنجرة، يعالج التهاب اللوزتين والجيوب الأنفية واضطرابات السمع."),
    ];

    /// <summary>أطباء الدليل (نفس بيانات تطبيق المريض) مع أوقات دوام أحد–خميس 08:00–16:00.</summary>
    private async Task SeedDoctorsAsync(Branch branch, CancellationToken ct)
    {
        var specialties = await db.Specialties.ToDictionaryAsync(x => x.Key, ct);
        foreach (var d in Doctors)
        {
            if (await db.Users.AnyAsync(u => u.Email == d.Email, ct)) continue;
            var u = new User { FullName = d.Name, Email = d.Email, Phone = d.Phone, EmployeeNo = d.EmployeeNo, Role = UserRole.Doctor, BranchId = branch.Id, PhoneConfirmed = true, AvatarKey = d.Avatar, Gender = d.Name.Contains("ريم") || d.Name.Contains("هالة") || d.Name.Contains("لينا") ? Gender.Female : Gender.Male };
            u.PasswordHash = hasher.HashPassword(u, options.Value.DemoPassword);
            db.Users.Add(u);
            await db.SaveChangesAsync(ct);
            db.DoctorProfiles.Add(new DoctorProfile
            {
                UserId = u.Id, SpecialtyId = specialties[d.Key].Id, BranchId = branch.Id, YearsExperience = d.Years, PatientsCount = d.Patients, ConsultationPrice = d.Price, VideoPrice = d.Video,
                Rating = d.Rating, ReviewsCount = d.Reviews, About = d.About, Languages = "العربية، الإنجليزية", RequiresConfirmation = d.RequiresConfirmation, LicenseNo = $"SCFHS-{u.Id + 40000}",
            });
            AddHours(u.Id, d.Days ?? SunToThu, 8 * 60, 16 * 60);
        }
        await db.SaveChangesAsync(ct);

        // د. منى القاسم: الأربعاء حتى 20:00 (كما في تطبيق الطبيب)
        var mona = await db.Users.FirstOrDefaultAsync(u => u.Email == "mona@hospital.com", ct);
        if (mona is not null && !await db.DoctorWorkingHours.AnyAsync(h => h.DoctorId == mona.Id, ct))
        {
            mona.AvatarKey ??= "avatars/doc-f1";
            AddHours(mona.Id, SunToThu.Where(x => x != DayOfWeek.Wednesday).ToArray(), 8 * 60, 16 * 60);
            AddHours(mona.Id, [DayOfWeek.Wednesday], 8 * 60, 20 * 60);
            await db.SaveChangesAsync(ct);
        }
    }

    private void AddHours(int doctorId, DayOfWeek[] days, int start, int end)
    {
        foreach (var day in days) db.DoctorWorkingHours.Add(new DoctorWorkingHour { DoctorId = doctorId, DayOfWeek = day, StartMinutes = start, EndMinutes = end });
    }

    /// <summary>شركات التأمين ووثيقة المريضة التجريبية (التعاونية – ذهبي – تحمّل 20%).</summary>
    private async Task SeedInsuranceAsync(CancellationToken ct)
    {
        // كل شركة تُضاف إن غابت بالاسم (لا نفترض قاعدة فارغة)
        var companies = new[]
        {
            new InsuranceCompany { Name = "التعاونية", ContractEndsAt = new DateOnly(2027, 6, 30), DiscountPct = 12, AveragePayDays = 38, ContactEmail = "claims@tawuniya.example" },
            new InsuranceCompany { Name = "بوبا", ContractEndsAt = new DateOnly(2027, 2, 28), DiscountPct = 10, AveragePayDays = 45, ContactEmail = "provider@bupa.example" },
            new InsuranceCompany { Name = "ميدغلف", ContractEndsAt = new DateOnly(2026, 12, 31), DiscountPct = 8, AveragePayDays = 52, Status = ContractStatus.Renewing, ContactEmail = "network@medgulf.example" },
            new InsuranceCompany { Name = "ملاذ", ContractEndsAt = new DateOnly(2027, 9, 30), DiscountPct = 9, AveragePayDays = 33, ContactEmail = "care@malath.example" },
        };
        foreach (var c in companies)
            if (!await db.InsuranceCompanies.AnyAsync(x => x.Name == c.Name, ct)) db.InsuranceCompanies.Add(c);
        await db.SaveChangesAsync(ct);

        var reem = await db.Users.FirstOrDefaultAsync(u => u.Email == "reem@claudcare.test", ct);
        if (reem is not null && !await db.PatientInsurances.AnyAsync(p => p.PatientId == reem.Id, ct))
        {
            var company = await db.InsuranceCompanies.FirstAsync(c => c.Name == "التعاونية", ct);
            db.PatientInsurances.Add(new PatientInsurance { PatientId = reem.Id, CompanyId = company.Id, PolicyNo = "POL-8841-229", Tier = "الذهبية", CopayRate = 0.20m, AnnualLimit = 100_000, UsedAmount = 62_000, ExpiresAt = new DateOnly(2027, 3, 31) });
            await db.SaveChangesAsync(ct);
        }
    }
}
