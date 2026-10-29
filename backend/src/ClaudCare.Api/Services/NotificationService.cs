using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Services;

public interface INotificationService
{
    /// <summary>يضيف إشعارًا لمستخدم (يُحفظ مع أول SaveChanges للمستدعي).</summary>
    void Add(int userId, NotificationType type, string title, string text, string? refType = null, int? refId = null);
}

public sealed class NotificationService(ClaudCareDbContext db, TimeProvider clock) : INotificationService
{
    public void Add(int userId, NotificationType type, string title, string text, string? refType = null, int? refId = null) =>
        db.Notifications.Add(new Notification { UserId = userId, Type = type, Title = title, Text = text, RefType = refType, RefId = refId, CreatedAt = clock.GetUtcNow().UtcDateTime });
}
