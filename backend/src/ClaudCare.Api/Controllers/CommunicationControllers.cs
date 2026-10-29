using ClaudCare.Api.Contracts;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Communication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaudCare.Api.Controllers;

/// <summary>إشعارات المستخدم الحالي.</summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationsController(NotificationCenter center) : ControllerBase
{
    [HttpGet]
    public async Task<NotificationListDto> List([FromQuery] bool unread = false, [FromQuery] NotificationType? type = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await center.ListAsync(unread, type, page, pageSize, ct);

    [HttpGet("unread-count")]
    public async Task<object> UnreadCount(CancellationToken ct) => new { unread = await center.UnreadCountAsync(ct) };

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct) { await center.MarkReadAsync(id, ct); return NoContent(); }

    [HttpPost("read-all")]
    public async Task<object> ReadAll(CancellationToken ct) => new { marked = await center.MarkAllReadAsync(ct) };

    /// <summary>حذف ناعم (يمكن التراجع عنه).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await center.DeleteAsync(id, ct); return NoContent(); }

    [HttpPost("{id:int}/restore")]
    public async Task<NotificationDto> Restore(int id, CancellationToken ct) => await center.RestoreAsync(id, ct);
}

/// <summary>المحادثات: مباشرة (مريض↔طبيب، طاقم↔طاقم) ودعم (مريض↔الاستقبال).</summary>
[ApiController]
[Authorize]
[Route("api/conversations")]
[Produces("application/json")]
public class ConversationsController(MessagingService messaging) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ConversationDto>> List(CancellationToken ct) => await messaging.ListAsync(false, ct);

    /// <summary>قائمة محادثات الدعم لموظفي الاستقبال/الإدارة.</summary>
    [HttpGet("support")]
    public async Task<IReadOnlyList<ConversationDto>> SupportQueue(CancellationToken ct) => await messaging.ListAsync(true, ct);

    [HttpGet("unread-count")]
    public async Task<object> Unread(CancellationToken ct) => new { unread = await messaging.UnreadTotalAsync(ct) };

    /// <summary>يفتح (أو يعيد) المحادثة المباشرة مع مستخدم، مع رسالة أولى اختيارية.</summary>
    [HttpPost]
    public async Task<ConversationDto> Start(StartConversationRequest req, CancellationToken ct) => await messaging.StartDirectAsync(req.UserId!.Value, req.Text, ct);

    [HttpPost("support")]
    public async Task<ConversationDto> StartSupport([FromBody] StartSupportRequest? req, CancellationToken ct) => await messaging.StartSupportAsync(req?.Text, ct);

    /// <summary>الرسائل الأحدث أولًا للصفحة (before=معرّف رسالة · take) مرتبة تصاعديًا؛ تفتحها فتصبح مقروءة.</summary>
    [HttpGet("{id:int}/messages")]
    public async Task<MessagePageDto> Messages(int id, [FromQuery] int? before = null, [FromQuery] int? take = null, CancellationToken ct = default) =>
        await messaging.MessagesAsync(id, before, take, ct);

    [HttpPost("{id:int}/messages")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Send(int id, SendMessageRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await messaging.SendAsync(id, req.Text, ct));

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct) { await messaging.MarkReadAsync(id, ct); return NoContent(); }
}

/// <summary>الملف الشخصي وأفراد العائلة والإعدادات.</summary>
[ApiController]
[Authorize]
[Route("api/me")]
[Produces("application/json")]
public class AccountController(AccountService account) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ProfileDto> Profile(CancellationToken ct) => await account.GetProfileAsync(ct);

    [HttpPut("profile")]
    public async Task<ProfileDto> UpdateProfile(UpdateProfileRequest req, CancellationToken ct) => await account.UpdateProfileAsync(req, ct);

    [HttpGet("family")]
    public async Task<IReadOnlyList<FamilyMemberDto>> Family(CancellationToken ct) => await account.FamilyAsync(ct);

    [HttpPost("family")]
    [ProducesResponseType<FamilyMemberDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddFamily(FamilyMemberRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await account.AddFamilyAsync(req, ct));

    [HttpPut("family/{id:int}")]
    public async Task<FamilyMemberDto> UpdateFamily(int id, FamilyMemberRequest req, CancellationToken ct) => await account.UpdateFamilyAsync(id, req, ct);

    [HttpDelete("family/{id:int}")]
    public async Task<IActionResult> DeleteFamily(int id, CancellationToken ct) { await account.DeleteFamilyAsync(id, ct); return NoContent(); }

    [HttpGet("settings")]
    public async Task<SettingsDto> Settings(CancellationToken ct) => await account.GetSettingsAsync(ct);

    /// <summary>تحديث جزئي: {"values":{"offers":true}}. مفتاح غير معروف ⇒ 400.</summary>
    [HttpPut("settings")]
    public async Task<SettingsDto> UpdateSettings(UpdateSettingsRequest req, CancellationToken ct) => await account.UpdateSettingsAsync(req.Values!, ct);
}
