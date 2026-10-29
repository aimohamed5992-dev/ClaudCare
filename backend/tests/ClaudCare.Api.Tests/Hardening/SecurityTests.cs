using System.Net;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using ClaudCare.Api.Tests.Scheduling;

namespace ClaudCare.Api.Tests.Hardening;

[Collection(ApiCollection.Name)]
public class AuthorizationSweepTests(ApiFactory root)
{
    /// <summary>المسارات المفتوحة بقصد: الدخول والتسجيل واستعادة كلمة المرور وفحوص الصحة وتوثيق التطوير.</summary>
    private static readonly string[] ExpectedAnonymous =
    [
        "api/auth/login", "api/auth/register", "api/auth/otp/verify", "api/auth/otp/resend", "api/auth/password/forgot", "api/auth/password/reset", "api/auth/refresh", "api/auth/logout",
        "api/health", "api/health/live", "api/doctors", "api/doctors/{id}", "api/doctors/{id}/availability", "api/doctors/{id}/reviews", "api/specialties",
        "openapi/{documentName}.json", "scalar/{documentName}", "scalar",
    ];

    private static IEnumerable<RouteEndpoint> Routes(IServiceProvider sp) =>
        sp.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null);

    [Fact]
    public void Only_the_intended_endpoints_are_anonymous()
    {
        var anonymous = Routes(root.Services).Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.RoutePattern.RawText!.Trim('/')).Distinct().Order().ToList();
        var unexpected = anonymous.Except(ExpectedAnonymous, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(unexpected.Count == 0, "مسارات مفتوحة غير متوقعة: " + string.Join(", ", unexpected));
    }

    [Fact]
    public async Task Every_protected_endpoint_rejects_requests_without_a_token_with_401()
    {
        var http = root.CreateClient();
        var bad = new List<string>();
        var protectedRoutes = Routes(root.Services).Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null).ToList();
        Assert.True(protectedRoutes.Count > 150, $"عدد المسارات المحمية قليل بشكل مريب: {protectedRoutes.Count}");
        foreach (var e in protectedRoutes)
        {
            var url = "/" + Regex.Replace(e.RoutePattern.RawText!.Trim('/'), @"\{[^}]+\}", "1");
            foreach (var method in e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)
            {
                var res = await http.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
                if (res.StatusCode != HttpStatusCode.Unauthorized) bad.Add($"{method} {url} -> {(int)res.StatusCode}");
            }
        }
        Assert.True(bad.Count == 0, "مسارات لا ترفض الزائر: " + string.Join(" | ", bad));
    }

    [Fact]
    public async Task A_tampered_or_wrong_purpose_token_never_works()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        Assert.Equal(401, (await c.Http.GetApiAsync("/api/me/profile", p.Token + "x")).Status);
        Assert.Equal(401, (await c.Http.GetApiAsync("/api/me/profile", "not.a.jwt")).Status);
        var login = await c.Http.PostAsync("/api/auth/login", new { identifier = (await TestUsers.CreateAsync(c.Factory, UserRole.Doctor, branchId: c.BranchId)).Email, password = TestUsers.Password });
        Assert.Equal(200, login.Status);
        // رمز تحدي OTP ليس رمز وصول
        Assert.Equal(401, (await c.Http.GetApiAsync("/api/me/profile", login.Str("challenge", "challengeToken"))).Status);
    }
}

[Collection(ApiCollection.Name)]
public class RoleIsolationTests(ApiFactory root)
{
    /// <summary>قوائم تشغيلية لا تخص المريض ولا الأدوار الأخرى.</summary>
    private static readonly (UserRole Allowed, string Url)[] Restricted =
    [
        (UserRole.Pharmacy, "/api/pharmacy/prescriptions"), (UserRole.Pharmacy, "/api/pharmacy/inventory"), (UserRole.Pharmacy, "/api/pharmacy/sales"), (UserRole.Pharmacy, "/api/pharmacy/purchase-orders"),
        (UserRole.Hr, "/api/hr/employees"), (UserRole.Hr, "/api/hr/licenses/expiring"), (UserRole.Finance, "/api/finance/expenses"), (UserRole.Finance, "/api/finance/summary"),
        (UserRole.Insurance, "/api/insurance/claims"), (UserRole.Insurance, "/api/insurance/pre-auths"), (UserRole.Surgery, "/api/surgery/rooms"), (UserRole.Surgery, "/api/surgery/cases"),
        (UserRole.Emergency, "/api/emergency/cases"), (UserRole.Emergency, "/api/emergency/ambulances"), (UserRole.Reception, "/api/reception/today"), (UserRole.Admissions, "/api/beds"),
        (UserRole.Admissions, "/api/admissions"), (UserRole.Branch, "/api/dashboard/overview"), (UserRole.Radiology, "/api/imaging-devices"),
    ];

    [Fact]
    public async Task Patients_and_unrelated_roles_cannot_open_operational_lists()
    {
        var c = await Clinic.CreateAsync(root);
        var patient = await c.PatientAsync();
        // أدوار «بعيدة» عن كل القوائم: المختبر لا يرى المالية ولا الصيدلية… إلخ
        var stranger = await c.TokenAsync(UserRole.Lab);
        var bad = new List<string>();
        foreach (var (allowed, url) in Restricted)
        {
            var p = (await c.Http.GetApiAsync(url, patient.Token)).Status;
            if (p != 403) bad.Add($"patient {url} -> {p}");
            if (allowed != UserRole.Lab) { var s = (await c.Http.GetApiAsync(url, stranger)).Status; if (s != 403) bad.Add($"lab {url} -> {s}"); }
        }
        Assert.True(bad.Count == 0, string.Join(" | ", bad));
    }

    [Fact]
    public async Task Each_operational_role_can_open_its_own_list()
    {
        var c = await Clinic.CreateAsync(root);
        var bad = new List<string>();
        foreach (var (allowed, url) in Restricted)
        {
            var t = await c.TokenAsync(allowed);
            var status = (await c.Http.GetApiAsync(url, t)).Status;
            if (status != 200) bad.Add($"{allowed} {url} -> {status}");
        }
        Assert.True(bad.Count == 0, string.Join(" | ", bad));
    }
}

[Collection(ApiCollection.Name)]
public class TransportHardeningTests(ApiFactory root)
{
    [Fact]
    public async Task Responses_carry_security_headers_and_api_data_is_never_cached()
    {
        var http = root.CreateClient();
        var res = await http.GetAsync("/api/health/live");
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", res.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("no-store", res.Headers.CacheControl!.ToString());
        Assert.True(res.Headers.Contains("Permissions-Policy"));
    }

    [Fact]
    public async Task Oversized_bodies_are_rejected_with_413_before_any_work()
    {
        var http = root.CreateClient();
        var big = new StringContent("{\"identifier\":\"" + new string('a', 2_000_000) + "\"}", System.Text.Encoding.UTF8, "application/json");
        var res = await http.PostAsync("/api/auth/login", big);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, res.StatusCode);
        Assert.Contains("payload_too_large", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cors_allows_the_react_dev_origin_only_for_localhost_style_origins()
    {
        var http = root.CreateClient();
        var ok = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        ok.Headers.Add("Origin", "http://localhost:5173"); ok.Headers.Add("Access-Control-Request-Method", "POST");
        Assert.True((await http.SendAsync(ok)).Headers.Contains("Access-Control-Allow-Origin"));
        var evil = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        evil.Headers.Add("Origin", "https://evil.example"); evil.Headers.Add("Access-Control-Request-Method", "POST");
        Assert.False((await http.SendAsync(evil)).Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task The_global_rate_limit_returns_429_with_retry_after()
    {
        using var limited = root.WithWebHostBuilder(b => b.UseSetting("RateLimit:GlobalPermitPerMinute", "5"));
        var http = limited.CreateClient();
        var statuses = new List<int>();
        for (var i = 0; i < 8; i++) statuses.Add((int)(await http.GetAsync("/api/health/live")).StatusCode);
        Assert.Contains(429, statuses);
        Assert.Equal(5, statuses.Count(s => s == 200));
        var limitedRes = await http.GetAsync("/api/health/live");
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedRes.StatusCode);
        Assert.Equal("60", limitedRes.Headers.GetValues("Retry-After").Single());
    }

    [Fact]
    public async Task Errors_are_problem_details_and_never_leak_internals()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        var r = await c.Http.GetApiAsync("/api/appointments/not-a-number", p.Token);
        Assert.Equal(404, r.Status);
        var bad = await c.Http.PostAsync("/api/appointments", new { doctorId = "x" }, p.Token);
        Assert.Equal(400, bad.Status);
        Assert.DoesNotContain("at ClaudCare", bad.Body.ToString());
        Assert.DoesNotContain("Exception", bad.Body.ToString());
    }
}
