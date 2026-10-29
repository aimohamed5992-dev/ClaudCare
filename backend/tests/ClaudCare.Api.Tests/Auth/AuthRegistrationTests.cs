using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudCare.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class AuthRegistrationTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();

    private static (string phone, string nid) Ids() => ("05" + Random.Shared.NextInt64(10_000_000, 99_999_999), "2" + Random.Shared.NextInt64(100_000_000, 999_999_999));

    private object Body(string phone, string nid, string password = "Reem@12345", string name = "ريم العلي الأحمد") =>
        new { fullName = name, phone, nationalId = nid, password, gender = "Female", birthYear = 1998 };

    [Fact]
    public async Task Register_then_verify_creates_a_confirmed_patient_with_a_medical_record_number()
    {
        var (phone, nid) = Ids();
        var reg = await _http.PostAsync("/api/auth/register", Body(phone, nid));
        Assert.Equal(201, reg.Status);
        Assert.Equal("Register", reg.Str("purpose"));
        Assert.EndsWith(phone[^2..], reg.Str("destination"));

        var verify = await _http.PostAsync("/api/auth/otp/verify", new { challengeToken = reg.Str("challengeToken"), code = "123456" });
        Assert.Equal(200, verify.Status);
        Assert.Equal("Patient", verify.Str("auth", "user", "role"));
        Assert.True(verify["auth"].GetProperty("user").GetProperty("phoneConfirmed").GetBoolean());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaudCareDbContext>();
        var user = await db.Users.SingleAsync(u => u.Phone == phone);
        var profile = await db.PatientProfiles.SingleAsync(p => p.UserId == user.Id);
        Assert.Matches(@"^MRN-\d{6}$", profile.MedicalRecordNo);
        Assert.NotEqual("Reem@12345", user.PasswordHash); // مُجزَّأة

        // ويدخل لاحقًا مباشرة بكلمة المرور
        Assert.Equal("authenticated", (await _http.PostAsync("/api/auth/login", new { identifier = phone, password = "Reem@12345" }))["status"].GetString());
    }

    [Fact]
    public async Task Cannot_log_in_before_confirming_the_phone()
    {
        var (f, clock) = factory.WithFakeClock();
        var http = f.CreateClient();
        var (phone, nid) = Ids();
        await http.PostAsync("/api/auth/register", Body(phone, nid));

        // فور التسجيل أُرسل رمز؛ طلب رمز جديد خلال 30 ثانية مقيَّد (والمستخدم يستعمل «إعادة الإرسال»)
        Assert.Equal("otp_throttled", (await http.PostAsync("/api/auth/login", new { identifier = phone, password = "Reem@12345" })).Code);

        clock.Advance(TimeSpan.FromSeconds(31));
        var login = await http.PostAsync("/api/auth/login", new { identifier = phone, password = "Reem@12345" });
        Assert.Equal("otp_required", login["status"].GetString());
        Assert.Equal("Register", login.Str("challenge", "purpose"));
        Assert.True(login["auth"].ValueKind is System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Duplicates_are_conflicts_with_stable_codes()
    {
        var (phone, nid) = Ids();
        var reg = await _http.PostAsync("/api/auth/register", Body(phone, nid));
        await _http.PostAsync("/api/auth/otp/verify", new { challengeToken = reg.Str("challengeToken"), code = "123456" });

        var samePhone = await _http.PostAsync("/api/auth/register", Body(phone, Ids().nid));
        Assert.Equal(409, samePhone.Status);
        Assert.Equal("phone_taken", samePhone.Code);

        var sameNid = await _http.PostAsync("/api/auth/register", Body(Ids().phone, nid));
        Assert.Equal(409, sameNid.Status);
        Assert.Equal("national_id_taken", sameNid.Code);
    }

    [Fact]
    public async Task An_abandoned_registration_can_be_retried_with_updated_details()
    {
        var (phone, nid) = Ids();
        await _http.PostAsync("/api/auth/register", Body(phone, nid, name: "اسم قديم تجريبي"));

        var (f, clock) = factory.WithFakeClock();
        var http = f.CreateClient();
        clock.Advance(TimeSpan.FromSeconds(31));
        var retry = await http.PostAsync("/api/auth/register", Body(phone, nid, password: "Newer@12345", name: "اسم جديد مصحح"));
        Assert.Equal(201, retry.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaudCareDbContext>();
        var users = await db.Users.Where(u => u.Phone == phone).ToListAsync();
        Assert.Single(users);
        Assert.Equal("اسم جديد مصحح", users[0].FullName);
    }

    [Theory]
    [InlineData("0412345678", "1098765432", "Reem@12345", "ريم العلي", "Phone")]
    [InlineData("0551234599", "3098765432", "Reem@12345", "ريم العلي", "NationalId")]
    [InlineData("0551234599", "1098765432", "short1", "ريم العلي", "Password")]
    [InlineData("0551234599", "1098765432", "onlyletters", "ريم العلي", "Password")]
    [InlineData("0551234599", "1098765432", "Reem@12345", "ريم", "FullName")]
    public async Task Invalid_input_is_rejected_per_field(string phone, string nid, string password, string name, string field)
    {
        var res = await _http.PostAsync("/api/auth/register", Body(phone, nid, password, name));
        Assert.Equal(400, res.Status);
        Assert.True(res["errors"].TryGetProperty(field, out _), $"متوقع خطأ في الحقل {field}: {res.Body}");
    }
}
