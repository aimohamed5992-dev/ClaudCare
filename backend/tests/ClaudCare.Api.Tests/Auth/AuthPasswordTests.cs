using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;

namespace ClaudCare.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class AuthPasswordTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();

    private async Task<string> ResetTokenAsync(string identifier)
    {
        var forgot = await _http.PostAsync("/api/auth/password/forgot", new { identifier });
        Assert.Equal(200, forgot.Status);
        Assert.Equal("ResetPassword", forgot.Str("purpose"));
        var verify = await _http.PostAsync("/api/auth/otp/verify", new { challengeToken = forgot.Str("challengeToken"), code = "123456" });
        Assert.Equal(200, verify.Status);
        Assert.True(verify["auth"].ValueKind is System.Text.Json.JsonValueKind.Null);
        return verify.Str("resetToken");
    }

    [Fact]
    public async Task Forgot_verify_reset_changes_the_password_and_ends_all_sessions()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var (_, refresh) = await TestUsers.LoginAsync(_http, u.Phone);

        var token = await ResetTokenAsync(u.Phone);
        var reset = await _http.PostAsync("/api/auth/password/reset", new { resetToken = token, newPassword = "Brand@New99" });
        Assert.Equal(204, reset.Status);

        Assert.Equal(401, (await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = TestUsers.Password })).Status);
        Assert.Equal(200, (await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Brand@New99" })).Status);
        Assert.Equal(401, (await _http.PostAsync("/api/auth/refresh", new { refreshToken = refresh })).Status);
    }

    [Fact]
    public async Task A_reset_token_cannot_be_used_twice()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var token = await ResetTokenAsync(u.Phone);
        Assert.Equal(204, (await _http.PostAsync("/api/auth/password/reset", new { resetToken = token, newPassword = "First@Pass99" })).Status);

        var again = await _http.PostAsync("/api/auth/password/reset", new { resetToken = token, newPassword = "Second@Pass99" });
        Assert.Equal(400, again.Status);
        Assert.Equal("reset_token_invalid", again.Code);
    }

    [Fact]
    public async Task Reset_enforces_the_password_policy()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var token = await ResetTokenAsync(u.Phone);
        var res = await _http.PostAsync("/api/auth/password/reset", new { resetToken = token, newPassword = "weak" });
        Assert.Equal(400, res.Status);
        Assert.True(res["errors"].TryGetProperty("NewPassword", out _));
    }

    [Fact]
    public async Task Reset_unlocks_a_locked_account()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        for (var i = 0; i < 5; i++) await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Bad@12345" });
        Assert.Equal(423, (await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = TestUsers.Password })).Status);

        var token = await ResetTokenAsync(u.Phone);
        await _http.PostAsync("/api/auth/password/reset", new { resetToken = token, newPassword = "Unlocked@99" });
        Assert.Equal(200, (await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Unlocked@99" })).Status);
    }

    [Fact]
    public async Task Forgot_for_an_unknown_account_looks_the_same_but_can_never_succeed()
    {
        var real = await _http.PostAsync("/api/auth/password/forgot", new { identifier = (await TestUsers.CreateAsync(factory, UserRole.Patient)).Phone });
        var fake = await _http.PostAsync("/api/auth/password/forgot", new { identifier = "0500000099" });

        Assert.Equal(real.Status, fake.Status);
        Assert.Equal(real.Str("purpose"), fake.Str("purpose"));
        Assert.Equal(real["expiresInSeconds"].GetInt32(), fake["expiresInSeconds"].GetInt32());

        var verify = await _http.PostAsync("/api/auth/otp/verify", new { challengeToken = fake.Str("challengeToken"), code = "123456" });
        Assert.Equal(400, verify.Status);
        Assert.Equal("otp_invalid", verify.Code);
        var resend = await _http.PostAsync("/api/auth/otp/resend", new { challengeToken = fake.Str("challengeToken") });
        Assert.Equal(200, resend.Status);
    }

    [Fact]
    public async Task Tokens_of_the_wrong_kind_are_rejected_as_reset_tokens()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var (access, _) = await TestUsers.LoginAsync(_http, u.Phone);
        var res = await _http.PostAsync("/api/auth/password/reset", new { resetToken = access, newPassword = "Brand@New99" });
        Assert.Equal("reset_token_invalid", res.Code);
    }

    [Fact]
    public async Task Change_password_requires_the_current_one_and_returns_a_fresh_session()
    {
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var (access, refresh) = await TestUsers.LoginAsync(_http, u.Phone);
        var other = await TestUsers.LoginAsync(_http, u.Phone);

        var wrong = await _http.PostAsync("/api/auth/password/change", new { currentPassword = "Nope@12345", newPassword = "Changed@99" }, access);
        Assert.Equal(400, wrong.Status);
        Assert.True(wrong["errors"].TryGetProperty("CurrentPassword", out _));

        var same = await _http.PostAsync("/api/auth/password/change", new { currentPassword = TestUsers.Password, newPassword = TestUsers.Password }, access);
        Assert.True(same["errors"].TryGetProperty("NewPassword", out _));

        var ok = await _http.PostAsync("/api/auth/password/change", new { currentPassword = TestUsers.Password, newPassword = "Changed@99" }, access);
        Assert.Equal(200, ok.Status);
        Assert.Equal(200, (await _http.GetApiAsync("/api/auth/me", ok.Str("accessToken"))).Status);
        Assert.Equal(200, (await _http.PostAsync("/api/auth/refresh", new { refreshToken = ok.Str("refreshToken") })).Status);
        Assert.Equal(401, (await _http.PostAsync("/api/auth/refresh", new { refreshToken = refresh })).Status);
        Assert.Equal(401, (await _http.PostAsync("/api/auth/refresh", new { refreshToken = other.Refresh })).Status);
        Assert.Equal(200, (await _http.PostAsync("/api/auth/login", new { identifier = u.Phone, password = "Changed@99" })).Status);
    }

    [Fact]
    public async Task Change_password_needs_authentication()
    {
        var res = await _http.PostAsync("/api/auth/password/change", new { currentPassword = "a", newPassword = "b" });
        Assert.Equal(401, res.Status);
    }
}
