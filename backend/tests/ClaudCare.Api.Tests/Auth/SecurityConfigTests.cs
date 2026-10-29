using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace ClaudCare.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class SecurityConfigTests(ApiFactory factory)
{
    [Fact]
    public async Task Rate_limiter_returns_429_with_retry_after_beyond_the_limit()
    {
        var f = factory.WithWebHostBuilder(b => b.UseSetting("RateLimit:AuthPermitPerMinute", "3"));
        var http = f.CreateClient();
        var statuses = new List<int>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await http.PostAsync("/api/auth/login", new { identifier = "0500000000", password = "x12345678" })).Status);

        Assert.Equal([401, 401, 401, 429, 429], statuses);
        var limited = await http.PostAsync("/api/auth/login", new { identifier = "0500000000", password = "x" });
        Assert.Equal("rate_limited", limited.Code);
        Assert.True(limited.Raw.Headers.Contains("Retry-After"));
        // نقاط النهاية غير المقيّدة لا تتأثر
        Assert.Equal(200, (await http.GetApiAsync("/api/health/live")).Status);
    }

    [Fact]
    public void Production_refuses_the_development_signing_key()
    {
        var f = factory.WithWebHostBuilder(b => b.UseEnvironment("Production").UseSetting("Jwt:SigningKey", "dev-only-signing-key-change-me-0123456789abcdef"));
        var ex = Assert.ThrowsAny<Exception>(() => f.CreateClient());
        Assert.Contains("مفتاح التوقيع التجريبي", ex.GetBaseException().Message);
    }

    [Fact]
    public void Production_refuses_a_fixed_otp_code_and_demo_accounts()
    {
        var otp = factory.WithWebHostBuilder(b => b.UseEnvironment("Production").UseSetting("Jwt:SigningKey", new string('k', 40)).UseSetting("Auth:FixedOtpCode", "123456"));
        Assert.Contains("FixedOtpCode", Assert.ThrowsAny<Exception>(() => otp.CreateClient()).GetBaseException().Message);

        var seed = factory.WithWebHostBuilder(b => b.UseEnvironment("Production").UseSetting("Jwt:SigningKey", new string('k', 40)).UseSetting("Auth:FixedOtpCode", "").UseSetting("Seed:DemoAccounts", "true"));
        Assert.Contains("DemoAccounts", Assert.ThrowsAny<Exception>(() => seed.CreateClient()).GetBaseException().Message);
    }

    [Fact]
    public void A_short_or_missing_signing_key_stops_startup()
    {
        var f = factory.WithWebHostBuilder(b => b.UseSetting("Jwt:SigningKey", "short"));
        Assert.Contains("32", Assert.ThrowsAny<Exception>(() => f.CreateClient()).GetBaseException().Message);
    }
}
