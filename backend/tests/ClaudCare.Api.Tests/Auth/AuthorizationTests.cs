using ClaudCare.Api.Auth;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudCare.Api.Tests.Auth;

/// <summary>نقاط نهاية اختبارية تحمل سياسات المشروع لفحص مصفوفة الصلاحيات قبل بناء نقاط النهاية الحقيقية.</summary>
[ApiController, Route("api/_test")]
public class TestAuthzController(ICurrentUser me) : ControllerBase
{
    [HttpGet("any"), Authorize] public IActionResult Any() => Ok(new { me.Id, role = me.Role.ToString(), branch = me.BranchId });
    [HttpGet("patient"), Authorize(Policy = Policies.Patient)] public IActionResult P() => Ok();
    [HttpGet("doctor"), Authorize(Policy = Policies.Doctor)] public IActionResult D() => Ok();
    [HttpGet("staff"), Authorize(Policy = Policies.Staff)] public IActionResult S() => Ok();
    [HttpGet("admin"), Authorize(Policy = Policies.Admin)] public IActionResult A() => Ok();
    [HttpGet("management"), Authorize(Policy = Policies.Management)] public IActionResult M() => Ok();
    [HttpGet("clinical"), Authorize(Policy = Policies.Clinical)] public IActionResult C() => Ok();
    [HttpGet("open"), AllowAnonymous] public IActionResult Open() => Ok();
    [HttpGet("default")] public IActionResult Default() => Ok(); // بلا وسم: يجب أن يُحمى افتراضيًا
}

[Collection(ApiCollection.Name)]
public class AuthorizationTests(ApiFactory factory)
{
    private readonly WebApplicationFactory<Program> _f = factory.WithWebHostBuilder(b =>
        b.ConfigureTestServices(s => s.AddControllers().AddApplicationPart(typeof(TestAuthzController).Assembly)));

    private async Task<string> TokenAsync(HttpClient http, UserRole role)
    {
        var u = await TestUsers.CreateAsync(_f, role);
        return (await TestUsers.LoginAsync(http, role == UserRole.Patient ? u.Phone : u.Email)).Access;
    }

    [Theory]
    // الدور: patient, doctor, staff, admin, management, clinical
    [InlineData(UserRole.Patient, 200, 403, 403, 403, 403, 403)]
    [InlineData(UserRole.Doctor, 403, 200, 200, 403, 403, 200)]
    [InlineData(UserRole.Admin, 403, 403, 200, 200, 200, 403)]
    [InlineData(UserRole.Hospital, 403, 403, 200, 403, 200, 403)]
    [InlineData(UserRole.Finance, 403, 403, 200, 403, 403, 403)]
    [InlineData(UserRole.Pharmacy, 403, 403, 200, 403, 403, 200)]
    [InlineData(UserRole.Reception, 403, 403, 200, 403, 403, 403)]
    public async Task Policy_matrix(UserRole role, int patient, int doctor, int staff, int admin, int management, int clinical)
    {
        var http = _f.CreateClient();
        var t = await TokenAsync(http, role);
        Assert.Equal(patient, (await http.GetApiAsync("/api/_test/patient", t)).Status);
        Assert.Equal(doctor, (await http.GetApiAsync("/api/_test/doctor", t)).Status);
        Assert.Equal(staff, (await http.GetApiAsync("/api/_test/staff", t)).Status);
        Assert.Equal(admin, (await http.GetApiAsync("/api/_test/admin", t)).Status);
        Assert.Equal(management, (await http.GetApiAsync("/api/_test/management", t)).Status);
        Assert.Equal(clinical, (await http.GetApiAsync("/api/_test/clinical", t)).Status);
        Assert.Equal(200, (await http.GetApiAsync("/api/_test/any", t)).Status);
    }

    [Fact]
    public async Task Anonymous_callers_are_blocked_everywhere_except_explicit_AllowAnonymous()
    {
        var http = _f.CreateClient();
        foreach (var p in new[] { "any", "patient", "doctor", "staff", "admin", "management", "clinical", "default" })
            Assert.Equal(401, (await http.GetApiAsync($"/api/_test/{p}")).Status);
        Assert.Equal(200, (await http.GetApiAsync("/api/_test/open")).Status);
        Assert.Equal(200, (await http.GetApiAsync("/api/health")).Status);
    }

    [Fact]
    public async Task Current_user_exposes_id_role_and_branch_from_the_token()
    {
        var http = _f.CreateClient();
        var u = await TestUsers.CreateAsync(_f, UserRole.Patient);
        var (access, _) = await TestUsers.LoginAsync(http, u.Phone);
        var res = await http.GetApiAsync("/api/_test/any", access);
        Assert.Equal(u.Id, res["id"].GetInt32());
        Assert.Equal("Patient", res.Str("role"));
    }

    [Fact]
    public async Task Challenge_tokens_and_garbage_are_never_accepted_as_access_tokens()
    {
        var http = _f.CreateClient();
        var u = await TestUsers.CreateAsync(_f, UserRole.Doctor);
        var login = await http.PostAsync("/api/auth/login", new { identifier = u.Email, password = TestUsers.Password });
        var challenge = login.Str("challenge", "challengeToken");
        Assert.Equal(401, (await http.GetApiAsync("/api/_test/any", challenge)).Status);
        Assert.Equal(401, (await http.GetApiAsync("/api/_test/any", "abc.def.ghi")).Status);

        var other = await TestUsers.CreateAsync(_f, UserRole.Doctor); // مستخدم آخر: إرسال رمز جديد لنفس الهدف مقيَّد زمنيًا
        var forgot = await http.PostAsync("/api/auth/password/forgot", new { identifier = other.Email });
        var reset = (await http.PostAsync("/api/auth/otp/verify", new { challengeToken = forgot.Str("challengeToken"), code = "123456" })).Str("resetToken");
        Assert.Equal(401, (await http.GetApiAsync("/api/_test/any", reset)).Status);
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var http = _f.CreateClient();
        var token = await TokenAsync(http, UserRole.Patient);
        var parts = token.Split('.');
        // نبدّل الدور في الحمولة دون إعادة التوقيع
        var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1].PadRight((parts[1].Length + 3) / 4 * 4, '=').Replace('-', '+').Replace('_', '/')));
        var forged = payload.Replace("\"Patient\"", "\"Admin\"");
        Assert.NotEqual(payload, forged);
        var forgedPart = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(forged)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(401, (await http.GetApiAsync("/api/_test/admin", $"{parts[0]}.{forgedPart}.{parts[2]}")).Status);
    }
}
