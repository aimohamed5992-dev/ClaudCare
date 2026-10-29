using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

/// <summary>كشف طبي (زيارة) مع العلامات الحيوية والتشخيص.</summary>
public class Visit : AuditedEntity
{
    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    [ForeignKey(nameof(DoctorId))] public User Doctor { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public DateOnly VisitDate { get; set; }

    [MaxLength(500)] public string Complaint { get; set; } = string.Empty;
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? HeartRate { get; set; }
    [Precision(4, 1)] public decimal? TemperatureC { get; set; }
    public int? Spo2 { get; set; }
    [MaxLength(300)] public string Diagnosis { get; set; } = string.Empty;
    [Column(TypeName = "text")] public string? Notes { get; set; }
    [MaxLength(40)] public string? FollowUp { get; set; }
}

[Index(nameof(Name), IsUnique = true)]
public class Drug : Entity
{
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    /// <summary>العائلة الدوائية (للتحقق من الحساسية) مثل: البنسلين.</summary>
    [MaxLength(80)] public string Group { get; set; } = string.Empty;
    [MaxLength(60)] public string? Form { get; set; }
    [MaxLength(40)] public string? Category { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; } = true;
}

[Index(nameof(No), IsUnique = true)]
public class Prescription : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    [ForeignKey(nameof(DoctorId))] public User Doctor { get; set; } = null!;
    public int? VisitId { get; set; }
    public Visit? Visit { get; set; }
    public int? BranchId { get; set; }
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Pending;
    public OrderPriority Priority { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    [MaxLength(300)] public string? RejectionReason { get; set; }
    public int? DispensedByUserId { get; set; }
    public DateTime? DispensedAt { get; set; }
    public List<PrescriptionItem> Items { get; set; } = [];
}

public class PrescriptionItem : Entity
{
    public int PrescriptionId { get; set; }
    public Prescription Prescription { get; set; } = null!;
    public int? DrugId { get; set; }
    public Drug? Drug { get; set; }
    [MaxLength(150)] public string DrugName { get; set; } = string.Empty;
    [MaxLength(80)] public string? Dose { get; set; }
    [MaxLength(60)] public string Frequency { get; set; } = string.Empty;
    public int TimesPerDay { get; set; } = 1;
    public int DurationDays { get; set; }
    public int Quantity { get; set; }
    [MaxLength(200)] public string? Instructions { get; set; }
    /// <summary>أقرّ الطبيب بمراجعة تعارض الحساسية.</summary>
    public bool AllergyOverride { get; set; }
    [MaxLength(150)] public string? SubstituteDrugName { get; set; }
    public decimal UnitPrice { get; set; }
    public bool Dispensed { get; set; }
}

/// <summary>دواء المريض في تطبيقه: أوقات الجرعات والتذكير وطلب إعادة الصرف.</summary>
[Index(nameof(PrescriptionItemId), IsUnique = true)]
public class PatientMedication : AuditedEntity
{
    public int PatientId { get; set; }
    public User Patient { get; set; } = null!;
    public int PrescriptionItemId { get; set; }
    public PrescriptionItem PrescriptionItem { get; set; } = null!;
    /// <summary>أوقات الجرعات اليومية بالدقائق مفصولة بفاصلة (480,1200).</summary>
    [MaxLength(60)] public string TimesCsv { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool ReminderEnabled { get; set; } = true;
    public RefillStatus RefillStatus { get; set; }
    public DateTime? RefillRequestedAt { get; set; }
}

[Index(nameof(PatientMedicationId), nameof(Date), nameof(SlotIndex), IsUnique = true)]
public class DoseLog : Entity
{
    public int PatientMedicationId { get; set; }
    public PatientMedication PatientMedication { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int SlotIndex { get; set; }
    public DateTime TakenAt { get; set; }
}

// ---------- المختبر ----------

[Index(nameof(No), IsUnique = true)]
[Index(nameof(Status))]
public class LabOrder : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int? DoctorId { get; set; }
    [ForeignKey(nameof(DoctorId))] public User? Doctor { get; set; }
    public int? VisitId { get; set; }
    public int? BranchId { get; set; }
    [MaxLength(150)] public string TestName { get; set; } = string.Empty;
    [MaxLength(60)] public string? Category { get; set; }
    public OrderPriority Priority { get; set; }
    public LabOrderStatus Status { get; set; } = LabOrderStatus.Ordered;
    public DateTime OrderedAt { get; set; }
    public DateTime? SampleCollectedAt { get; set; }
    public DateTime? ResultAt { get; set; }
    public int? TechnicianId { get; set; }
    public int? ReviewedByUserId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public List<LabResultItem> Items { get; set; } = [];
}

public class LabResultItem : Entity
{
    public int LabOrderId { get; set; }
    public LabOrder LabOrder { get; set; } = null!;
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    [Precision(12, 3)] public decimal Value { get; set; }
    [MaxLength(30)] public string Unit { get; set; } = string.Empty;
    [Precision(12, 3)] public decimal? RefMin { get; set; }
    [Precision(12, 3)] public decimal? RefMax { get; set; }
    public ResultFlag Flag { get; set; }
    [MaxLength(200)] public string? Remark { get; set; }
}

// ---------- الأشعة ----------

public class ImagingDevice : Entity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    [MaxLength(80)] public string Name { get; set; } = string.Empty;
    [MaxLength(30)] public string Modality { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
}

[Index(nameof(No), IsUnique = true)]
public class ImagingOrder : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int? DoctorId { get; set; }
    [ForeignKey(nameof(DoctorId))] public User? Doctor { get; set; }
    public int? BranchId { get; set; }
    [MaxLength(120)] public string StudyName { get; set; } = string.Empty;
    [MaxLength(30)] public string Modality { get; set; } = string.Empty;
    public OrderPriority Priority { get; set; }
    public ImagingOrderStatus Status { get; set; } = ImagingOrderStatus.Ordered;
    public DateTime OrderedAt { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public int? DeviceId { get; set; }
    public ImagingDevice? Device { get; set; }
    [MaxLength(500)] public string? ClinicalNotes { get; set; }
    public ImagingReport? Report { get; set; }
}

[Index(nameof(ImagingOrderId), IsUnique = true)]
public class ImagingReport : AuditedEntity
{
    public int ImagingOrderId { get; set; }
    public ImagingOrder ImagingOrder { get; set; } = null!;
    [Column(TypeName = "text")] public string Findings { get; set; } = string.Empty;
    [MaxLength(500)] public string Impression { get; set; } = string.Empty;
    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    public int? RadiologistId { get; set; }
    public DateTime? ReportedAt { get; set; }
    /// <summary>مفتاح ملف الصورة في التخزين.</summary>
    [MaxLength(300)] public string? ImageKey { get; set; }
}
