using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudCare.Api.Tests.Infrastructure;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
}

public sealed record ApiResponse(HttpResponseMessage Raw, JsonElement Body)
{
    public int Status => (int)Raw.StatusCode;
    public string? Code => Body.ValueKind == JsonValueKind.Object && Body.TryGetProperty("code", out var c) ? c.GetString() : null;
    public JsonElement this[string name] => Body.GetProperty(name);
    public string Str(params string[] path)
    {
        var e = Body;
        foreach (var p in path) e = e.GetProperty(p);
        return e.GetString()!;
    }
}

public static class HttpExtensions
{
    public static JsonElement J(this JsonElement e, string name) => e.GetProperty(name);

    public static async Task<ApiResponse> SendJsonAsync(this HttpClient http, HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body, options: Json.Options);
        if (bearer is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var res = await http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        var doc = string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
        return new ApiResponse(res, doc);
    }

    public static Task<ApiResponse> PostAsync(this HttpClient http, string url, object? body = null, string? bearer = null) => http.SendJsonAsync(HttpMethod.Post, url, body, bearer);
    public static Task<ApiResponse> PutAsync(this HttpClient http, string url, object? body = null, string? bearer = null) => http.SendJsonAsync(HttpMethod.Put, url, body, bearer);
    public static Task<ApiResponse> DeleteAsync(this HttpClient http, string url, string? bearer = null) => http.SendJsonAsync(HttpMethod.Delete, url, null, bearer);
    public static Task<ApiResponse> GetApiAsync(this HttpClient http, string url, string? bearer = null) => http.SendJsonAsync(HttpMethod.Get, url, null, bearer);
}

/// <summary>مستخدمون اختباريون بكلمة مرور معروفة (لكل اختبار مستخدمه كي لا تتشارك الحالة، فالقاعدة مشتركة).</summary>
public static class TestUsers
{
    public const string Password = "Test@12345";

    public sealed record Created(int Id, string Email, string Phone, string EmployeeNo, string NationalId);

    public static async Task<Created> CreateAsync(WebApplicationFactory<Program> factory, UserRole role, bool active = true, bool phoneConfirmed = true, int? branchId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaudCareDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var n = Random.Shared.NextInt64(10_000_000, 99_999_999);
        var u = new User
        {
            FullName = $"مستخدم {role} {n}",
            Email = $"u{n}@t.test",
            Phone = "05" + n,
            NationalId = "1" + Random.Shared.NextInt64(100_000_000, 999_999_999),
            EmployeeNo = $"T-{n}",
            Role = role,
            IsActive = active,
            PhoneConfirmed = phoneConfirmed,
            BranchId = branchId,
        };
        u.PasswordHash = hasher.HashPassword(u, Password);
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return new Created(u.Id, u.Email!, u.Phone!, u.EmployeeNo!, u.NationalId!);
    }

    /// <summary>يسجّل الدخول (بما فيه OTP للطاقم) ويعيد الرموز.</summary>
    public static async Task<(string Access, string Refresh)> LoginAsync(HttpClient http, string identifier, string password = Password, string otp = "123456")
    {
        var login = await http.PostAsync("/api/auth/login", new { identifier, password });
        if (login.Status != 200) throw new InvalidOperationException($"فشل الدخول: {login.Status} {login.Body}");
        var auth = login["status"].GetString() == "authenticated"
            ? login["auth"]
            : (await http.PostAsync("/api/auth/otp/verify", new { challengeToken = login.Str("challenge", "challengeToken"), code = otp }))["auth"];
        return (auth.GetProperty("accessToken").GetString()!, auth.GetProperty("refreshToken").GetString()!);
    }
}
