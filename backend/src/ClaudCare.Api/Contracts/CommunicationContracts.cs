using System.ComponentModel.DataAnnotations;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Contracts;

public sealed record NotificationDto(int Id, NotificationType Type, string Title, string Text, DateTime CreatedAt, bool Read, string? RefType, int? RefId);
public sealed record NotificationListDto(PagedResult<NotificationDto> Page, int Unread);

public sealed record StartConversationRequest([Required] int? UserId, [StringLength(2000)] string? Text);
public sealed record StartSupportRequest([StringLength(2000)] string? Text);
public sealed record SendMessageRequest([Required, StringLength(2000, MinimumLength = 1)] string Text);

public sealed record ConversationDto(int Id, ConversationKind Kind, string Title, string? Role, string? AvatarKey, int? OtherUserId, string? LastMessage, DateTime LastMessageAt, int Unread);
public sealed record MessageDto(int Id, int SenderId, string SenderName, string Text, DateTime SentAt, bool Mine);
public sealed record MessagePageDto(IReadOnlyList<MessageDto> Messages, bool HasMore);

public sealed record FamilyMemberRequest([Required, StringLength(120, MinimumLength = 2)] string Name, [Required, StringLength(40, MinimumLength = 2)] string Relation, [Range(1900, 2100)] int BirthYear, Gender? Gender);
public sealed record FamilyMemberDto(int Id, string Name, string Relation, int BirthYear, Gender? Gender);

public sealed record UpdateProfileRequest(
    [Required, StringLength(120, MinimumLength = 2)] string FullName,
    [EmailAddress, StringLength(200)] string? Email,
    [Range(1900, 2100)] int? BirthYear,
    Gender? Gender,
    /// <summary>للطبيب فقط: النبذة التعريفية.</summary>
    [StringLength(2000)] string? About);

public sealed record ProfileDto(int Id, string FullName, string? Phone, string? Email, int? BirthYear, Gender? Gender, UserRole Role, string? AvatarKey, string? About, string? Specialty);

public sealed record SettingsDto(IReadOnlyDictionary<string, bool> Values);
public sealed record UpdateSettingsRequest([Required] Dictionary<string, bool>? Values);
