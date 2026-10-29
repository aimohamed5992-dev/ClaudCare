using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;

namespace ClaudCare.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class AuthOtpTests(ApiFactory factory)
{
    private static async Task<string> StaffChallengeAsync(HttpClient http, TestUsers.Created u)
    {
        var login = await http.PostAsync("/api/auth/login", new { identifier = u.Email, password = TestUsers.Password });
        Assert.Equal("otp_required", login["status"].GetString());
        return login.Str("challenge", "challengeToken");
    }

    [Fact]
    public async Task Wrong_code_is_rejected_and_the_right_code_still_works_afterwards()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Nurse);
        var ct = await StaffChallengeAsync(http, u);

        var bad = await http.PostAsync("/api/auth/otp/verify", new { challengeToken = ct, code = "000000" });
        Assert.Equal(400, bad.Status);
        Assert.Equal("otp_invalid", bad.Code);

        var ok = await http.PostAsync("/api/auth/otp/verify", new { challengeToken = ct, code = "123456" });
        Assert.Equal(200, ok.Status);
    }

    [Fact]
    public async Task A_code_works_only_once()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Nurse);
        var ct = await StaffChallengeAsync(http, u);
        Assert.Equal(200, (await http.PostAsync("/api/auth/otp/verify", new { challengeToken = ct, code = "123456" })).Status);

        var again = await http.PostAsync("/api/auth/otp/verify", new { challengeToken = ct, code = "123456" });
        Assert.Equal(400, again.Status);
        Assert.Equal("otp_invalid", again.Code);
    }

    [Fact]
    public async Task After_five_wrong_attempts_even_the_right_code_is_refused()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Nurse);
        var ct = await StaffChallengeAsync(http, u);

        for (var i = 0; i < 5; i++)
            Assert.Equal("otp_invalid", (await http.PostAsync("/api/auth/otp/verify", new { challengeToken = ct, code = "111111" })).Code);

        var res = await http.PostAsync("/api/auth/otp/verify", new { challengeToken = ct, code = "123456" });
        Assert.Equal("otp_attempts_exceeded", res.Code);
    }

    [Fact]
    public async Task Code_and_challenge_expire()
    {
        var (f, clock) = factory.WithFakeClock();
        var http = f.CreateClient();
        var u = await TestUsers.CreateAsync(f, UserRole.Nurse);
        var ct = await StaffChallengeAsync(http, u);

        clock.Advance(TimeSpan.FromMinutes(6)); // الرمز 5 دقائق، والتحدي 10
        var res = await http.PostAsync("/api/auth/otp/verify", new { challengeToken = ct, code = "123456" });
        Assert.Equal("otp_expired", res.Code);

        clock.Advance(TimeSpan.FromMinutes(10)); // انتهى التحدي نفسه
        var res2 = await http.PostAsync("/api/auth/otp/verify", new { challengeToken = ct, code = "123456" });
        Assert.Equal("challenge_invalid", res2.Code);
    }

    [Fact]
    public async Task Garbage_challenge_tokens_are_rejected()
    {
        var http = factory.CreateClient();
        var res = await http.PostAsync("/api/auth/otp/verify", new { challengeToken = "not-a-jwt", code = "123456" });
        Assert.Equal(400, res.Status);
        Assert.Equal("challenge_invalid", res.Code);
    }

    [Fact]
    public async Task Resend_is_throttled_then_replaces_the_previous_code()
    {
        var (f, clock) = factory.WithFakeClock();
        var http = f.CreateClient();
        var u = await TestUsers.CreateAsync(f, UserRole.Nurse);
        var first = await StaffChallengeAsync(http, u);

        var tooSoon = await http.PostAsync("/api/auth/otp/resend", new { challengeToken = first });
        Assert.Equal(429, tooSoon.Status);
        Assert.Equal("otp_throttled", tooSoon.Code);
        Assert.True(tooSoon.Raw.Headers.Contains("Retry-After"));

        clock.Advance(TimeSpan.FromSeconds(31));
        var resend = await http.PostAsync("/api/auth/otp/resend", new { challengeToken = first });
        Assert.Equal(200, resend.Status);
        var second = resend.Str("challengeToken");

        // الرمز السابق أُبطل بإصدار الجديد
        Assert.Equal("otp_invalid", (await http.PostAsync("/api/auth/otp/verify", new { challengeToken = first, code = "123456" })).Code);
        Assert.Equal(200, (await http.PostAsync("/api/auth/otp/verify", new { challengeToken = second, code = "123456" })).Status);
    }

    [Fact]
    public async Task Too_many_codes_per_hour_for_the_same_target_are_refused()
    {
        var (f, clock) = factory.WithFakeClock();
        var http = f.CreateClient();
        var u = await TestUsers.CreateAsync(f, UserRole.Nurse);
        var token = await StaffChallengeAsync(http, u); // 1
        for (var i = 0; i < 5; i++) // 2..6
        {
            clock.Advance(TimeSpan.FromSeconds(31));
            token = (await http.PostAsync("/api/auth/otp/resend", new { challengeToken = token })).Str("challengeToken");
        }
        clock.Advance(TimeSpan.FromSeconds(31));
        var seventh = await http.PostAsync("/api/auth/otp/resend", new { challengeToken = token });
        Assert.Equal(429, seventh.Status);
        Assert.Equal("otp_limit", seventh.Code);
    }

    [Fact]
    public async Task Otp_codes_are_stored_hashed_not_in_plain_text()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Nurse);
        await StaffChallengeAsync(http, u);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaudCare.Api.Data.ClaudCareDbContext>();
        var row = await db.OtpCodes.OrderByDescending(o => o.Id).FirstAsync(o => o.Target == u.Phone);
        Assert.DoesNotContain("123456", row.CodeHash);
        Assert.Equal(64, row.CodeHash.Length); // HMAC-SHA256 hex
    }
}
