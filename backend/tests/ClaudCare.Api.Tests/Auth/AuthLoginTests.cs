using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;

namespace ClaudCare.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class AuthLoginTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();

    [Theory]
    [InlineData("phone")]
    [InlineData("nid")]
    [InlineData("arabic-digits")]
    [InlineData("intl")]
    public async Task Patient_logs_in_directly_by_phone_or_national_id(string kind)
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var identifier = kind switch
        {
            "phone" => u.Phone,
            "nid" => u.NationalId,
            "arabic-digits" => new string(u.Phone.Select(c => (char)('٠' + (c - '0'))).ToArray()),
            _ => "+966" + u.Phone[1..],
        };

        var res = await _http.PostAsync("/api/auth/login", new { identifier, password = TestUsers.Password });

        Assert.Equal(200, res.Status);
        Assert.Equal("authenticated", res["status"].GetString());
        Assert.Equal("Patient", res.Str("auth", "user", "role"));
        Assert.Equal("Bearer", res.Str("auth", "tokenType"));
        Assert.False(string.IsNullOrEmpty(res.Str("auth", "refreshToken")));

        var me = await _http.GetApiAsync("/api/auth/me", res.Str("auth", "accessToken"));
        Assert.Equal(200, me.Status);
        Assert.Equal(u.Id, me["id"].GetInt32());
    }

    [Theory]
    [InlineData("email")]
    [InlineData("employee-no")]
    public async Task Staff_gets_an_otp_challenge_and_only_then_a_session(string kind)
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Doctor);
        var login = await _http.PostAsync("/api/auth/login", new { identifier = kind == "email" ? u.Email.ToUpperInvariant() : u.EmployeeNo.ToLowerInvariant(), password = TestUsers.Password });

        Assert.Equal(200, login.Status);
        Assert.Equal("otp_required", login["status"].GetString());
        Assert.Equal("Login2Fa", login.Str("challenge", "purpose"));
        Assert.Contains("•", login.Str("challenge", "destination")); // مخفي جزئيًا
        Assert.DoesNotContain(u.Phone, login.Body.ToString());
        Assert.True(login["auth"].ValueKind is System.Text.Json.JsonValueKind.Null);

        var verify = await _http.PostAsync("/api/auth/otp/verify", new { challengeToken = login.Str("challenge", "challengeToken"), code = "123456" });
        Assert.Equal(200, verify.Status);
        Assert.Equal("Doctor", verify.Str("auth", "user", "role"));
    }


    [Fact]
    public async Task Wrong_password_and_unknown_user_look_identical()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var wrong = await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Wrong@1234" });
        var unknown = await _http.PostAsync("/api/auth/login", new { identifier = "0500000000", password = "Wrong@1234" });

        Assert.Equal(401, wrong.Status);
        Assert.Equal(401, unknown.Status);
        Assert.Equal("invalid_credentials", wrong.Code);
        Assert.Equal(wrong.Code, unknown.Code);
        Assert.Equal(wrong.Str("title"), unknown.Str("title"));
    }

    [Fact]
    public async Task Missing_fields_return_a_validation_problem()
    {
        var res = await _http.PostAsync("/api/auth/login", new { identifier = "", password = "" });
        Assert.Equal(400, res.Status);
        Assert.Equal("validation_failed", res.Code);
        Assert.True(res["errors"].TryGetProperty("Identifier", out _));
        Assert.True(res["errors"].TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task Disabled_accounts_cannot_log_in_even_with_the_right_password()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient, active: false);
        var res = await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = TestUsers.Password });
        Assert.Equal(403, res.Status);
        Assert.Equal("account_disabled", res.Code);
    }

    [Fact]
    public async Task Patient_with_unconfirmed_phone_must_confirm_it_first()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient, phoneConfirmed: false);
        var login = await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = TestUsers.Password });
        Assert.Equal("otp_required", login["status"].GetString());
        Assert.Equal("Register", login.Str("challenge", "purpose"));
    }

    [Fact]
    public async Task Five_failures_lock_the_account_until_the_lockout_ends()
    {
        var (f, clock) = factory.WithFakeClock();
        var http = f.CreateClient();
        var u = await TestUsers.CreateAsync(f, UserRole.Patient);

        for (var i = 0; i < 4; i++)
            Assert.Equal(401, (await http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Bad@12345" })).Status);
        var fifth = await http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Bad@12345" });
        Assert.Equal(423, fifth.Status);
        Assert.Equal("account_locked", fifth.Code);
        Assert.True(fifth["retryAfterSeconds"].GetInt32() > 0);

        // حتى كلمة المرور الصحيحة مرفوضة أثناء الإقفال
        Assert.Equal(423, (await http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = TestUsers.Password })).Status);

        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(200, (await http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = TestUsers.Password })).Status);
    }

    [Fact]
    public async Task A_successful_login_resets_the_failure_counter()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        for (var i = 0; i < 3; i++) await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Bad@12345" });
        Assert.Equal(200, (await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = TestUsers.Password })).Status);
        // بعد النجاح نحتاج 5 إخفاقات جديدة للإقفال
        for (var i = 0; i < 4; i++) Assert.Equal(401, (await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Bad@12345" })).Status);
    }

    [Fact]
    public async Task Login_attempts_are_audited()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Bad@12345" });
        await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = TestUsers.Password });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaudCare.Api.Data.ClaudCareDbContext>();
        var actions = await db.AuditLogs.Where(a => a.UserId == u.Id).Select(a => a.Action).ToListAsync();
        Assert.Contains("auth.login_failed", actions);
        Assert.Contains("auth.login", actions);
    }
}
