using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Communication;

/// <summary>إشعارات المستخدم الحالي فقط. الحذف ناعم ليتيح «تراجع».</summary>
public sealed class NotificationCenter(ClaudCareDbContext db, ICurrentUser me, TimeProvider time)
{
    private IQueryable<Notification> Mine => db.Notifications.Where(n => n.UserId == me.Id && n.DeletedAt == null);

    public async Task<NotificationListDto> ListAsync(bool unreadOnly, NotificationType? type, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Clamp(page, pageSize);
        var q = Mine.AsNoTracking();
        var unread = await q.CountAsync(n => n.ReadAt == null, ct);
        if (unreadOnly) q = q.Where(n => n.ReadAt == null);
        if (type is { } t) q = q.Where(n => n.Type == t);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new NotificationListDto(new PagedResult<NotificationDto>(rows.Select(ToDto).ToList(), p, size, total), unread);
    }

    public Task<int> UnreadCountAsync(CancellationToken ct) => Mine.CountAsync(n => n.ReadAt == null, ct);

    public async Task MarkReadAsync(int id, CancellationToken ct)
    {
        var n = await Mine.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("notification_not_found", "الإشعار غير موجود.");
        n.ReadAt ??= time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return await Mine.Where(n => n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var n = await Mine.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiException.NotFound("notification_not_found", "الإشعار غير موجود.");
        n.DeletedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task<NotificationDto> RestoreAsync(int id, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == me.Id && x.DeletedAt != null, ct) ?? throw ApiException.NotFound("notification_not_found", "الإشعار غير موجود.");
        n.DeletedAt = null;
        await db.SaveChangesAsync(ct);
        return ToDto(n);
    }

    private static NotificationDto ToDto(Notification n) => new(n.Id, n.Type, n.Title, n.Text, n.CreatedAt, n.ReadAt != null, n.RefType, n.RefId);
}
