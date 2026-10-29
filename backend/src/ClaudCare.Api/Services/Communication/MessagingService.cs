using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Communication;

/// <summary>
/// المراسلة. المريض يراسل أطباءه فقط (من له معهم موعد غير ملغى أو كشف) ويفتح محادثة دعم؛ الطاقم يراسل بعضه.
/// الاستقبال/الإدارة يردّون على محادثات الدعم وينضمون إليها عند أول رد. غير المشارك ⇒ 404.
/// </summary>
public sealed class MessagingService(ClaudCareDbContext db, ICurrentUser me, INotificationService notifications, TimeProvider time)
{
    private static readonly UserRole[] SupportDesk = [UserRole.Reception, UserRole.Branch, UserRole.Hospital, UserRole.Admin];
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<ConversationDto> StartDirectAsync(int otherId, string? text, CancellationToken ct)
    {
        if (otherId == me.Id) throw ApiException.BadRequest("invalid_recipient", "لا يمكنك مراسلة نفسك.");
        var other = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == otherId && u.IsActive, ct) ?? throw ApiException.NotFound("user_not_found", "المستخدم غير موجود.");
        if (me.Role == UserRole.Patient)
        {
            var related = other.Role == UserRole.Doctor && (await db.Appointments.AnyAsync(a => a.PatientId == me.Id && a.DoctorId == otherId && a.Status != AppointmentStatus.Cancelled, ct)
                                                             || await db.Visits.AnyAsync(v => v.PatientId == me.Id && v.DoctorId == otherId, ct));
            if (!related) throw ApiException.Forbidden("action_not_allowed", "يمكنك مراسلة أطبائك فقط.");
        }
        else if (other.Role == UserRole.Patient)
        {
            if (me.Role != UserRole.Doctor || !(await db.Appointments.AnyAsync(a => a.PatientId == otherId && a.DoctorId == me.Id && a.Status != AppointmentStatus.Cancelled, ct)
                                                 || await db.Visits.AnyAsync(v => v.PatientId == otherId && v.DoctorId == me.Id, ct)))
                throw ApiException.NotFound("user_not_found", "المستخدم غير موجود.");
        }

        var existing = await db.Conversations.Where(c => c.Kind == ConversationKind.Direct && c.Participants.Count == 2
                && c.Participants.Any(p => p.UserId == me.Id) && c.Participants.Any(p => p.UserId == otherId)).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        int convId;
        if (existing is { } e) convId = e;
        else
        {
            var now = UtcNow;
            var conv = new Conversation { Kind = ConversationKind.Direct, CreatedAt = now, LastMessageAt = now,
                Participants = [new ConversationParticipant { UserId = me.Id }, new ConversationParticipant { UserId = otherId }] };
            db.Conversations.Add(conv);
            await db.SaveChangesAsync(ct);
            convId = conv.Id;
        }
        if (!string.IsNullOrWhiteSpace(text)) await SendAsync(convId, text, ct);
        return await GetDtoAsync(convId, ct);
    }

    public async Task<ConversationDto> StartSupportAsync(string? text, CancellationToken ct)
    {
        if (me.Role != UserRole.Patient) throw ApiException.Forbidden("action_not_allowed", "الدعم متاح للمرضى.");
        var existing = await db.Conversations.Where(c => c.Kind == ConversationKind.Support && c.Participants.Any(p => p.UserId == me.Id)).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        int convId;
        if (existing is { } e) convId = e;
        else
        {
            var now = UtcNow;
            var conv = new Conversation { Kind = ConversationKind.Support, Title = "الدعم", CreatedAt = now, LastMessageAt = now, Participants = [new ConversationParticipant { UserId = me.Id }] };
            db.Conversations.Add(conv);
            await db.SaveChangesAsync(ct);
            convId = conv.Id;
        }
        if (!string.IsNullOrWhiteSpace(text)) await SendAsync(convId, text, ct);
        return await GetDtoAsync(convId, ct);
    }

    public async Task<IReadOnlyList<ConversationDto>> ListAsync(bool supportQueue, CancellationToken ct)
    {
        IQueryable<Conversation> q;
        if (supportQueue)
        {
            if (!SupportDesk.Contains(me.Role)) throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية عرض محادثات الدعم.");
            q = db.Conversations.Where(c => c.Kind == ConversationKind.Support);
        }
        else q = db.Conversations.Where(c => c.Participants.Any(p => p.UserId == me.Id));
        var rows = await q.AsNoTracking().OrderByDescending(c => c.LastMessageAt).Take(100).ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<MessagePageDto> MessagesAsync(int id, int? before, int? take, CancellationToken ct)
    {
        var conv = await RequireAsync(id, ct);
        var n = Math.Clamp(take ?? 30, 1, 100);
        var q = db.Messages.AsNoTracking().Where(m => m.ConversationId == id);
        if (before is { } b) q = q.Where(m => m.Id < b);
        var rows = await q.OrderByDescending(m => m.Id).Take(n + 1).Include(m => m.Sender).ToListAsync(ct);
        var hasMore = rows.Count > n;
        var page = rows.Take(n).Reverse().Select(m => new MessageDto(m.Id, m.SenderId, m.Sender.FullName, m.Text, m.SentAt, m.SenderId == me.Id)).ToList();

        var mine = conv.Participants.FirstOrDefault(p => p.UserId == me.Id);
        if (mine is { UnreadCount: > 0 }) await MarkReadInternalAsync(id, ct);
        return new MessagePageDto(page, hasMore);
    }

    public async Task<MessageDto> SendAsync(int id, string text, CancellationToken ct)
    {
        var body = text.Trim();
        if (body.Length == 0) throw ApiException.Validation(nameof(text), "اكتب رسالة");
        var conv = await RequireAsync(id, ct, track: true);
        var now = UtcNow;

        if (conv.Participants.All(p => p.UserId != me.Id)) // موظف دعم ينضم عند أول رد
            conv.Participants.Add(new ConversationParticipant { UserId = me.Id, LastReadAt = now });

        var msg = new Message { ConversationId = id, SenderId = me.Id, Text = body, SentAt = now };
        db.Messages.Add(msg);
        conv.LastMessageAt = now;
        var sender = await db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id, ct);
        foreach (var p in conv.Participants.Where(p => p.UserId != me.Id))
        {
            // إشعار واحد لكل دفعة رسائل غير مقروءة بدل إشعار لكل رسالة
            if (p.UnreadCount == 0) notifications.Add(p.UserId, NotificationType.Message, $"رسالة من {sender.FullName}", body.Length > 80 ? body[..80] + "…" : body, nameof(Conversation), id);
            p.UnreadCount++;
        }
        var mine = conv.Participants.First(p => p.UserId == me.Id);
        mine.UnreadCount = 0;
        mine.LastReadAt = now;
        await db.SaveChangesAsync(ct);
        return new MessageDto(msg.Id, me.Id, sender.FullName, body, now, true);
    }

    public async Task MarkReadAsync(int id, CancellationToken ct)
    {
        await RequireAsync(id, ct);
        await MarkReadInternalAsync(id, ct);
    }

    public Task<int> UnreadTotalAsync(CancellationToken ct) =>
        db.ConversationParticipants.Where(p => p.UserId == me.Id).SumAsync(p => p.UnreadCount, ct);

    private async Task MarkReadInternalAsync(int id, CancellationToken ct)
    {
        var now = UtcNow;
        await db.ConversationParticipants.Where(p => p.ConversationId == id && p.UserId == me.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.UnreadCount, 0).SetProperty(p => p.LastReadAt, now), ct);
    }

    private async Task<Conversation> RequireAsync(int id, CancellationToken ct, bool track = false)
    {
        var q = track ? db.Conversations : db.Conversations.AsNoTracking();
        var conv = await q.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == id, ct);
        var allowed = conv is not null && (conv.Participants.Any(p => p.UserId == me.Id) || (conv.Kind == ConversationKind.Support && SupportDesk.Contains(me.Role)));
        return allowed ? conv! : throw ApiException.NotFound("conversation_not_found", "المحادثة غير موجودة.");
    }

    private async Task<ConversationDto> GetDtoAsync(int id, CancellationToken ct) =>
        (await ToDtosAsync([await db.Conversations.AsNoTracking().Include(c => c.Participants).FirstAsync(c => c.Id == id, ct)], ct))[0];

    private async Task<List<ConversationDto>> ToDtosAsync(List<Conversation> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(r => r.Id).ToList();
        var parts = await db.ConversationParticipants.AsNoTracking().Include(p => p.User).Where(p => ids.Contains(p.ConversationId)).ToListAsync(ct);
        var lastIds = await db.Messages.AsNoTracking().Where(m => ids.Contains(m.ConversationId)).GroupBy(m => m.ConversationId).Select(g => g.Max(m => m.Id)).ToListAsync(ct);
        var last = await db.Messages.AsNoTracking().Where(m => lastIds.Contains(m.Id)).ToDictionaryAsync(m => m.ConversationId, m => m.Text, ct);

        return rows.Select(c =>
        {
            var mine = parts.FirstOrDefault(p => p.ConversationId == c.Id && p.UserId == me.Id);
            var other = parts.FirstOrDefault(p => p.ConversationId == c.Id && p.UserId != me.Id);
            var patient = parts.FirstOrDefault(p => p.ConversationId == c.Id && p.User.Role == UserRole.Patient);
            var title = c.Kind == ConversationKind.Support
                ? (me.Role == UserRole.Patient ? "الدعم" : $"دعم — {patient?.User.FullName}")
                : other?.User.FullName ?? "";
            return new ConversationDto(c.Id, c.Kind, title, c.Kind == ConversationKind.Support ? null : other?.User.Role.ToString(), c.Kind == ConversationKind.Support ? null : other?.User.AvatarKey,
                c.Kind == ConversationKind.Support ? null : other?.UserId, last.GetValueOrDefault(c.Id), c.LastMessageAt, mine?.UnreadCount ?? 0);
        }).ToList();
    }
}
