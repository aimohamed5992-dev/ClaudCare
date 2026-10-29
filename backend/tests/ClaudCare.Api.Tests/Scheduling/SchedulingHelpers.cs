using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ClaudCare.Api.Tests.Scheduling;

/// <summary>
/// بيئة اختبار بساعة ثابتة: الثلاثاء 2026-10-27 الساعة 09:15 بتوقيت الرياض (06:15 UTC).
/// أيام الدوام الافتراضية أحد–خميس؛ الجمعة والسبت عطلة.
/// </summary>
public sealed class Clinic
{
    public static readonly DateOnly Today = new(2026, 10, 27);   // الثلاثاء
    public static readonly DateOnly Wed = new(2026, 10, 28);
    public static readonly DateOnly Thu = new(2026, 10, 29);
    public static readonly DateOnly Fri = new(2026, 10, 30);
    public static readonly DateOnly Sat = new(2026, 10, 31);
    public static readonly DateOnly Sun = new(2026, 11, 1);
    public static readonly DateOnly Mon = new(2026, 11, 2);
    public const int H09 = 9 * 60, H0930 = 9 * 60 + 30, H10 = 10 * 60, H11 = 11 * 60;

    public WebApplicationFactory<Program> Factory { get; }
    public FakeTimeProvider Clock { get; }
    public HttpClient Http { get; }
    public int BranchId { get; private set; }

    public Clinic(ApiFactory root)
    {
        (Factory, Clock) = root.WithFakeClock(new DateTimeOffset(2026, 10, 27, 6, 15, 0, TimeSpan.Zero));
        Http = Factory.CreateClient();
    }

    public async Task InitAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        BranchId = (await TestData.BranchAsync(scope.ServiceProvider.GetRequiredService<ClaudCareDbContext>())).Id;
    }

    public static async Task<Clinic> CreateAsync(ApiFactory root)
    {
        var c = new Clinic(root);
        await c.InitAsync();
        return c;
    }

    /// <summary>يضبط الوقت المحلي (الرياض) لليوم نفسه.</summary>
    public void SetLocal(int hour, int minute = 0) =>
        Clock.SetUtcNow(new DateTimeOffset(2026, 10, 27, hour, minute, 0, TimeSpan.Zero).AddHours(-3));

    public async Task<T> WithDb<T>(Func<ClaudCareDbContext, Task<T>> work)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<ClaudCareDbContext>());
    }

    // ---------------------------------------------------------------- منشئات

    public sealed record Doc(int Id, string Email, string Name);

    public async Task<Doc> DoctorAsync(decimal price = 300, decimal? video = 200, bool requiresConfirmation = false, DayOfWeek[]? days = null,
        int start = 480, int end = 960, int slot = 30, string specialty = "cardiology", decimal rating = 4.5m, int years = 5, int reviews = 10, string? name = null, bool listed = true, bool active = true)
    {
        var u = await TestUsers.CreateAsync(Factory, UserRole.Doctor, active: active, branchId: BranchId);
        await WithDb(async db =>
        {
            var user = await db.Users.FirstAsync(x => x.Id == u.Id);
            if (name is not null) user.FullName = name;
            var sp = await db.Specialties.FirstAsync(x => x.Key == specialty);
            db.DoctorProfiles.Add(new DoctorProfile { UserId = u.Id, SpecialtyId = sp.Id, BranchId = BranchId, ConsultationPrice = price, VideoPrice = video, RequiresConfirmation = requiresConfirmation, SlotMinutes = slot,
                Rating = rating, YearsExperience = years, ReviewsCount = reviews, About = "طبيب اختبار", IsListed = listed });
            foreach (var d in days ?? [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday])
                db.DoctorWorkingHours.Add(new DoctorWorkingHour { DoctorId = u.Id, DayOfWeek = d, StartMinutes = start, EndMinutes = end });
            await db.SaveChangesAsync();
            return 0;
        });
        return new Doc(u.Id, u.Email, name ?? $"مستخدم Doctor");
    }

    public sealed record Pat(int Id, string Phone, string Token);

    public async Task<Pat> PatientAsync(Action<ClaudCareDbContext, int>? setup = null)
    {
        var u = await TestUsers.CreateAsync(Factory, UserRole.Patient, branchId: BranchId);
        if (setup is not null) await WithDb(async db => { setup(db, u.Id); await db.SaveChangesAsync(); return 0; });
        var (access, _) = await TestUsers.LoginAsync(Http, u.Phone);
        return new Pat(u.Id, u.Phone, access);
    }

    public async Task<string> TokenAsync(UserRole role, int? branchId = null, string? email = null)
    {
        var u = await TestUsers.CreateAsync(Factory, role, branchId: branchId ?? BranchId);
        return (await TestUsers.LoginAsync(Http, u.Email)).Access;
    }

    private readonly Dictionary<int, (string Token, DateTimeOffset At)> _doctorTokens = [];

    /// <summary>توكن الطبيب مخزَّن مؤقتًا (تسجيل الدخول المتكرر مقيّد بـ OTP كل 30 ثانية)، ويتجدد إن مضى 20 دقيقة على ساعة الاختبار.</summary>
    public async Task<string> DoctorTokenAsync(Doc d)
    {
        if (_doctorTokens.TryGetValue(d.Id, out var t) && Clock.GetUtcNow() - t.At < TimeSpan.FromMinutes(20)) return t.Token;
        var token = (await TestUsers.LoginAsync(Http, d.Email)).Access;
        _doctorTokens[d.Id] = (token, Clock.GetUtcNow());
        return token;
    }

    public async Task<int> InsureAsync(int patientId, decimal copay = 0.2m, decimal limit = 100_000, decimal used = 0, DateOnly? expires = null, bool active = true)
    {
        return await WithDb(async db =>
        {
            var co = await db.InsuranceCompanies.FirstOrDefaultAsync();
            if (co is null) { co = new InsuranceCompany { Name = "ت" + TestData.Uniq(), ContractEndsAt = new DateOnly(2027, 12, 31) }; db.Add(co); await db.SaveChangesAsync(); }
            var p = new PatientInsurance { PatientId = patientId, CompanyId = co.Id, PolicyNo = "POL-" + TestData.Uniq(), Tier = "ذهبية", CopayRate = copay, AnnualLimit = limit, UsedAmount = used, ExpiresAt = expires ?? new DateOnly(2027, 12, 31), IsActive = active };
            db.Add(p);
            await db.SaveChangesAsync();
            return p.Id;
        });
    }

    // ---------------------------------------------------------------- واجهات

    public Task<ApiResponse> BookAsync(string token, int doctorId, DateOnly date, int start, string type = "Clinic", string? payment = "Card", bool insurance = true, int? familyMemberId = null, int? patientId = null, string? reason = "كشف") =>
        Http.PostAsync("/api/appointments", new { doctorId, date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), startMinutes = start, type, reason, paymentMethod = payment, useInsurance = insurance, familyMemberId, patientId }, token);

    public async Task<int> BookOkAsync(string token, int doctorId, DateOnly date, int start, string type = "Clinic")
    {
        var r = await BookAsync(token, doctorId, date, start, type);
        Assert.True(r.Status == 201, $"فشل الحجز: {r.Status} {r.Body}");
        return r["id"].GetInt32();
    }

    public Task<ApiResponse> Act(string token, int id, string action, object? body = null) => Http.PostAsync($"/api/appointments/{id}/{action}", body, token);

    public async Task<List<string>> NotificationTitlesAsync(int userId) =>
        await WithDb(db => db.Notifications.Where(n => n.UserId == userId).OrderBy(n => n.Id).Select(n => n.Title).ToListAsync());

    public async Task<AppointmentStatus> StatusAsync(int apptId) => await WithDb(async db => (await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == apptId)).Status);
}
