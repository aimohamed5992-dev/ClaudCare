using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

/// <summary>ملف الطبيب (1:1 مع المستخدم).</summary>
public class DoctorProfile
{
    [Key, ForeignKey(nameof(User))] public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    [MaxLength(40)] public string? LicenseNo { get; set; }
    public DateOnly? LicenseExpiry { get; set; }
    public int YearsExperience { get; set; }
    public int PatientsCount { get; set; }
    public decimal ConsultationPrice { get; set; }
    public decimal? VideoPrice { get; set; }
    [Column(TypeName = "text")] public string About { get; set; } = string.Empty;
    /// <summary>اللغات مفصولة بفاصلة.</summary>
    [MaxLength(120)] public string Languages { get; set; } = "العربية";
    [Precision(3, 2)] public decimal Rating { get; set; }
    public int ReviewsCount { get; set; }
    public int SlotMinutes { get; set; } = 30;
    public bool IsListed { get; set; } = true;
    /// <summary>إن كان true تدخل الحجوزات «بانتظار التأكيد» ويؤكدها الطبيب؛ وإلا تُؤكَّد تلقائيًا.</summary>
    public bool RequiresConfirmation { get; set; }
}

/// <summary>الأطباء المفضلون للمريض.</summary>
[PrimaryKey(nameof(PatientId), nameof(DoctorId))]
public class FavoriteDoctor
{
    public int PatientId { get; set; }
    public User Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    public User Doctor { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

[Index(nameof(DoctorId), nameof(DayOfWeek), IsUnique = true)]
public class DoctorWorkingHour : Entity
{
    public int DoctorId { get; set; }
    public User Doctor { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    /// <summary>الدقائق من منتصف الليل (480 = 08:00).</summary>
    public int StartMinutes { get; set; }
    public int EndMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}

[Index(nameof(AppointmentId), IsUnique = true)]
public class DoctorReview : AuditedEntity
{
    public int DoctorId { get; set; }
    public User Doctor { get; set; } = null!;
    public int PatientId { get; set; }
    public User Patient { get; set; } = null!;
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    [Range(1, 5)] public int Rating { get; set; }
    [MaxLength(500)] public string? Comment { get; set; }
}

/// <summary>الملف الصحي للمريض (1:1 مع المستخدم).</summary>
[Index(nameof(MedicalRecordNo), IsUnique = true)]
public class PatientProfile
{
    [Key, ForeignKey(nameof(User))] public int UserId { get; set; }
    public User User { get; set; } = null!;
    [MaxLength(20)] public string MedicalRecordNo { get; set; } = string.Empty;
    [MaxLength(5)] public string? BloodType { get; set; }
    public int? HeightCm { get; set; }
    [Precision(5, 1)] public decimal? WeightKg { get; set; }
    [MaxLength(80)] public string? City { get; set; }
    [MaxLength(120)] public string? EmergencyContactName { get; set; }
    [MaxLength(20)] public string? EmergencyContactPhone { get; set; }
    public List<PatientAllergy> Allergies { get; set; } = [];
    public List<PatientCondition> Conditions { get; set; } = [];
}

public class PatientAllergy : Entity
{
    public int PatientId { get; set; }
    public PatientProfile Patient { get; set; } = null!;
    /// <summary>اسم العائلة الدوائية أو المادة (مثل: البنسلين) — تُطابَق مع Drug.Group.</summary>
    [MaxLength(80)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string? Reaction { get; set; }
}

public class PatientCondition : Entity
{
    public int PatientId { get; set; }
    public PatientProfile Patient { get; set; } = null!;
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public int? SinceYear { get; set; }
}

public class FamilyMember : AuditedEntity
{
    public int PatientId { get; set; }
    public User Patient { get; set; } = null!;
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    [MaxLength(40)] public string Relation { get; set; } = string.Empty;
    public int BirthYear { get; set; }
    public Gender? Gender { get; set; }
}

// ---------- الموارد البشرية ----------

[Index(nameof(UserId), IsUnique = true)]
public class Employee : AuditedEntity
{
    public int? UserId { get; set; }
    public User? User { get; set; }
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    [MaxLength(120)] public string FullName { get; set; } = string.Empty;
    [MaxLength(100)] public string JobTitle { get; set; } = string.Empty;
    [MaxLength(20)] public string? Phone { get; set; }
    public DateOnly HireDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public decimal BaseSalary { get; set; }
    public int LeaveBalanceDays { get; set; } = 21;
}

public class ProfessionalLicense : Entity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    [MaxLength(50)] public string Number { get; set; } = string.Empty;
    [MaxLength(80)] public string Authority { get; set; } = "الهيئة السعودية للتخصصات الصحية";
    public DateOnly ExpiresAt { get; set; }
}

public class LeaveRequest : AuditedEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public LeaveType Type { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int Days { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    [MaxLength(300)] public string? Reason { get; set; }
    [MaxLength(300)] public string? DecisionReason { get; set; }
    public bool CoverageException { get; set; }
    public int? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
}

[Index(nameof(EmployeeId), nameof(Date), nameof(Kind), IsUnique = true)]
public class Shift : Entity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly Date { get; set; }
    public ShiftKind Kind { get; set; }
    public int StartMinutes { get; set; }
    public int EndMinutes { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Confirmed;
}
