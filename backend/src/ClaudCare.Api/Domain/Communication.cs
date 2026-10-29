using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

public class Conversation : Entity
{
    public ConversationKind Kind { get; set; } = ConversationKind.Direct;
    [MaxLength(120)] public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public List<ConversationParticipant> Participants { get; set; } = [];
    public List<Message> Messages { get; set; } = [];
}

[PrimaryKey(nameof(ConversationId), nameof(UserId))]
public class ConversationParticipant
{
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int UnreadCount { get; set; }
    public DateTime? LastReadAt { get; set; }
}

[Index(nameof(ConversationId), nameof(SentAt))]
public class Message : Entity
{
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;
    [Column(TypeName = "text")] public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    [MaxLength(300)] public string? AttachmentKey { get; set; }
}

[Index(nameof(UserId), nameof(CreatedAt))]
public class Notification : Entity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public NotificationType Type { get; set; }
    [MaxLength(150)] public string Title { get; set; } = string.Empty;
    [MaxLength(500)] public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    /// <summary>حذف ناعم يتيح «تراجع» فوريًا.</summary>
    public DateTime? DeletedAt { get; set; }
    /// <summary>الكيان المرتبط للتوجيه (Appointment/LabOrder/Invoice/...).</summary>
    [MaxLength(40)] public string? RefType { get; set; }
    public int? RefId { get; set; }
}
