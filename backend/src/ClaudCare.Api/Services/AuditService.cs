using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Services;

public interface IAuditService
{
    /// <summary>يضيف سجل تدقيق إلى السياق (يُحفظ مع أول SaveChanges).</summary>
    void Add(string action, int? userId, string entityType = "User", int? entityId = null, string? details = null, string? ip = null);
}

public sealed class AuditService(ClaudCareDbContext db, TimeProvider clock) : IAuditService
{
    public void Add(string action, int? userId, string entityType = "User", int? entityId = null, string? details = null, string? ip = null) =>
        db.AuditLogs.Add(new AuditLog { Action = action, UserId = userId, EntityType = entityType, EntityId = entityId ?? userId, Details = details, Ip = ip, At = clock.GetUtcNow().UtcDateTime });
}
