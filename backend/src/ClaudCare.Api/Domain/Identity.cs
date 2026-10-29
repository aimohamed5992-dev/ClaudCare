using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

[Index(nameof(Email), IsUnique = true)]
[Index(nameof(Phone), IsUnique = true)]
[Index(nameof(NationalId), IsUnique = true)]
[Index(nameof(EmployeeNo), IsUnique = true)]
[Index(nameof(Role))]
public class User : AuditedEntity
{
    [MaxLength(120)] public string FullName { get; set; } = string.Empty;
    [MaxLength(200)] public string? Email { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    [MaxLength(10)] public string? NationalId { get; set; }
    /// <summary>رقم الموظف لدخول الطاقم (مثل DR-1042).</summary>
    [MaxLength(30)] public string? EmployeeNo { get; set; }
    [MaxLength(200)] public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool PhoneConfirmed { get; set; }
    public Gender? Gender { get; set; }
    public int? BirthYear { get; set; }
    [MaxLength(300)] public string? AvatarKey { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

[Index(nameof(TokenHash), IsUnique = true)]
public class RefreshToken : Entity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [MaxLength(100)] public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    [MaxLength(100)] public string? ReplacedByHash { get; set; }
    [MaxLength(200)] public string? Device { get; set; }
    [MaxLength(45)] public string? Ip { get; set; }
    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}

[Index(nameof(Target), nameof(Purpose))]
public class OtpCode : Entity
{
    public OtpPurpose Purpose { get; set; }
    /// <summary>الجوال أو البريد الذي أُرسل إليه الرمز.</summary>
    [MaxLength(200)] public string Target { get; set; } = string.Empty;
    [MaxLength(100)] public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedAt { get; set; }
}

[Index(nameof(UserId), nameof(Key), IsUnique = true)]
public class UserSetting : Entity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    [MaxLength(60)] public string Key { get; set; } = string.Empty;
    [MaxLength(500)] public string Value { get; set; } = string.Empty;
}

[Index(nameof(At))]
public class AuditLog : Entity
{
    public int? UserId { get; set; }
    [MaxLength(80)] public string Action { get; set; } = string.Empty;
    [MaxLength(80)] public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    [MaxLength(500)] public string? Details { get; set; }
    [MaxLength(45)] public string? Ip { get; set; }
    public DateTime At { get; set; }
}
