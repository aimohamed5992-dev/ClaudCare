using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaudCare.Api.Tests.Infrastructure;

namespace ClaudCare.Api.Tests;

[Collection(ApiCollection.Name)]
public class HealthTests(ApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();

    [Fact]
    public async Task Health_reports_healthy_and_connected_to_the_test_database()
    {
        var res = await _http.GetAsync("/api/health");
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", json.GetProperty("status").GetString());
        Assert.Equal("ClaudCare.Api", json.GetProperty("service").GetString());
        var db = json.GetProperty("database");
        Assert.True(db.GetProperty("connected").GetBoolean());
        Assert.Equal(ApiFactory.TestDatabase, db.GetProperty("name").GetString());
        Assert.Contains("MySql", db.GetProperty("provider").GetString());
    }

    [Fact]
    public async Task Live_does_not_need_the_database()
    {
        var res = await _http.GetAsync("/api/health/live");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_routes_do_not_reveal_anything_to_anonymous_callers()
    {
        // سياسة الأمان الافتراضية تُطبَّق حتى على المسارات غير الموجودة: الزائر يرى 401 دائمًا
        var res = await _http.GetAsync("/api/does-not-exist");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_lists_the_health_endpoints()
    {
        var doc = await _http.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var paths = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("/api/Health", paths);
    }

    [Theory]
    [InlineData("http://localhost:5173", true)] // React dev
    [InlineData("http://localhost:51234", true)] // Flutter web على منفذ عشوائي
    [InlineData("http://127.0.0.1:5173", true)]
    [InlineData("http://evil.example", false)]
    public async Task Cors_allows_local_frontends_only(string origin, bool allowed)
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/health");
        req.Headers.Add("Origin", origin);
        req.Headers.Add("Access-Control-Request-Method", "GET");
        var res = await _http.SendAsync(req);

        Assert.Equal(allowed, res.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
