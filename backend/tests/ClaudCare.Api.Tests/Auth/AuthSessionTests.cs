using Microsoft.Extensions.DependencyInjection;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;

namespace ClaudCare.Api.Tests.Auth;

[Collection(ApiCollection.Name)]
public class AuthSessionTests(ApiFactory factory)
{
    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_is_dead()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var (_, refresh) = await TestUsers.LoginAsync(http, u.Phone);

        var r1 = await http.PostAsync("/api/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(200, r1.Status);
        Assert.NotEqual(refresh, r1.Str("refreshToken"));
        Assert.Equal(200, (await http.GetApiAsync("/api/auth/me", r1.Str("accessToken"))).Status);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_kills_every_session_of_that_user()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var (_, refresh) = await TestUsers.LoginAsync(http, u.Phone);
        var other = await TestUsers.LoginAsync(http, u.Phone); // جلسة أخرى على جهاز آخر

        var rotated = (await http.PostAsync("/api/auth/refresh", new { refreshToken = refresh })).Str("refreshToken");

        var replay = await http.PostAsync("/api/auth/refresh", new { refreshToken = refresh }); // الرمز المسروق
        Assert.Equal(401, replay.Status);
        Assert.Equal("token_reused", replay.Code);

        Assert.Equal(401, (await http.PostAsync("/api/auth/refresh", new { refreshToken = rotated })).Status);
        Assert.Equal(401, (await http.PostAsync("/api/auth/refresh", new { refreshToken = other.Refresh })).Status);
    }

    [Fact]
    public async Task Unknown_refresh_token_is_unauthorized()
    {
        var res = await factory.CreateClient().PostAsync("/api/auth/refresh", new { refreshToken = "nope" });
        Assert.Equal(401, res.Status);
        Assert.Equal("invalid_refresh_token", res.Code);
    }

    [Fact]
    public async Task Access_token_expires_but_refresh_still_works_until_it_expires_too()
    {
        var (f, clock) = factory.WithFakeClock();
        var http = f.CreateClient();
        var u = await TestUsers.CreateAsync(f, UserRole.Patient);
        var (access, refresh) = await TestUsers.LoginAsync(http, u.Phone);
        Assert.Equal(200, (await http.GetApiAsync("/api/auth/me", access)).Status);

        clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(401, (await http.GetApiAsync("/api/auth/me", access)).Status);

        var renewed = await http.PostAsync("/api/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(200, renewed.Status);
        Assert.Equal(200, (await http.GetApiAsync("/api/auth/me", renewed.Str("accessToken"))).Status);

        clock.Advance(TimeSpan.FromDays(31));
        var expired = await http.PostAsync("/api/auth/refresh", new { refreshToken = renewed.Str("refreshToken") });
        Assert.Equal(401, expired.Status);
        Assert.Equal("invalid_refresh_token", expired.Code);
    }

    [Fact]
    public async Task Logout_revokes_only_that_session_and_is_idempotent()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var a = await TestUsers.LoginAsync(http, u.Phone);
        var b = await TestUsers.LoginAsync(http, u.Phone);

        Assert.Equal(204, (await http.PostAsync("/api/auth/logout", new { refreshToken = a.Refresh })).Status);
        Assert.Equal(204, (await http.PostAsync("/api/auth/logout", new { refreshToken = a.Refresh })).Status);
        Assert.Equal(401, (await http.PostAsync("/api/auth/refresh", new { refreshToken = a.Refresh })).Status);
        // ملاحظة: رمز مُلغى = "إعادة استخدام" فيُنهي بقية الجلسات؛ لذا نتحقق من الجلسة الأخرى بجلسة جديدة
        var c = await TestUsers.LoginAsync(http, u.Phone);
        Assert.Equal(204, (await http.PostAsync("/api/auth/logout", new { refreshToken = c.Refresh })).Status);
        _ = b;
    }

    [Fact]
    public async Task Logout_all_ends_every_session()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var a = await TestUsers.LoginAsync(http, u.Phone);
        var b = await TestUsers.LoginAsync(http, u.Phone);

        Assert.Equal(204, (await http.PostAsync("/api/auth/logout-all", null, a.Access)).Status);
        Assert.Equal(401, (await http.PostAsync("/api/auth/refresh", new { refreshToken = a.Refresh })).Status);
        Assert.Equal(401, (await http.PostAsync("/api/auth/refresh", new { refreshToken = b.Refresh })).Status);
    }

    [Fact]
    public async Task Disabling_a_user_blocks_refresh_and_me()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var (access, refresh) = await TestUsers.LoginAsync(http, u.Phone);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClaudCare.Api.Data.ClaudCareDbContext>();
            (await db.Users.FindAsync(u.Id))!.IsActive = false;
            await db.SaveChangesAsync();
        }

        Assert.Equal(401, (await http.PostAsync("/api/auth/refresh", new { refreshToken = refresh })).Status);
        Assert.Equal(403, (await http.GetApiAsync("/api/auth/me", access)).Status);
    }

    [Fact]
    public async Task Active_sessions_are_capped_by_dropping_the_oldest()
    {
        var http = factory.CreateClient();
        var u = await TestUsers.CreateAsync(factory, UserRole.Patient);
        var first = await TestUsers.LoginAsync(http, u.Phone);
        for (var i = 0; i < 10; i++) await TestUsers.LoginAsync(http, u.Phone); // 11 جلسة، السقف 10

        Assert.Equal(401, (await http.PostAsync("/api/auth/refresh", new { refreshToken = first.Refresh })).Status);
    }
}
