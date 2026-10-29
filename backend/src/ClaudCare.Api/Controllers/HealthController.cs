using ClaudCare.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class HealthController(ClaudCareDbContext db, IHostEnvironment env) : ControllerBase
{
    /// <summary>حالة الخدمة واتصال قاعدة البيانات. 503 عند تعذّر الاتصال.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var connected = false;
        string? error = null;
        try
        {
            connected = await db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            error = ex.GetBaseException().Message;
        }

        var body = new HealthResponse(
            Status: connected ? "healthy" : "degraded",
            Service: "ClaudCare.Api",
            Version: typeof(HealthController).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
            Environment: env.EnvironmentName,
            ServerTimeUtc: DateTime.UtcNow,
            Database: new DatabaseHealth(connected, db.Database.ProviderName, connected ? db.Database.GetDbConnection().Database : null, env.IsDevelopment() ? error : null));

        return connected ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }

    /// <summary>فحص خفيف لا يلمس قاعدة البيانات (للمراقبة).</summary>
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "alive" });
}

public sealed record HealthResponse(string Status, string Service, string Version, string Environment, DateTime ServerTimeUtc, DatabaseHealth Database);

public sealed record DatabaseHealth(bool Connected, string? Provider, string? Name, string? Error);
