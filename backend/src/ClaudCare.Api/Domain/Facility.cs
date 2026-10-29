using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

// ---------- الأسرّة والإدخال ----------

[Index(nameof(BranchId), nameof(Code), IsUnique = true)]
public class Bed : Entity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    [MaxLength(60)] public string Ward { get; set; } = string.Empty;
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    public BedStatus Status { get; set; } = BedStatus.Available;
    [MaxLength(150)] public string? ReservedFor { get; set; }
}

[Index(nameof(No), IsUnique = true)]
public class Admission : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int BranchId { get; set; }
    public int? BedId { get; set; }
    public Bed? Bed { get; set; }
    public int? DoctorId { get; set; }
    [ForeignKey(nameof(DoctorId))] public User? Doctor { get; set; }
    public AdmissionType Type { get; set; }
    [MaxLength(200)] public string Reason { get; set; } = string.Empty;
    /// <summary>القسم المطلوب طبيًا.</summary>
    [MaxLength(60)] public string RequestedWard { get; set; } = string.Empty;
    [MaxLength(60)] public string? RequestedFrom { get; set; }
    public AdmissionStatus Status { get; set; } = AdmissionStatus.Pending;
    public DateTime RequestedAt { get; set; }
    public DateTime? AdmittedAt { get; set; }
    public DateTime? DischargedAt { get; set; }
    public bool GuardianConsent { get; set; }
    public bool InsuranceApprovalVerified { get; set; }
    // خطوات الخروج
    public bool DischargeOrderConfirmed { get; set; }
    public bool MedsReconciled { get; set; }
    public bool BillSettled { get; set; }
    public bool InstructionsGiven { get; set; }
    [Column(TypeName = "text")] public string? DischargeSummary { get; set; }
}

[Index(nameof(No), IsUnique = true)]
public class BedTransfer : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int AdmissionId { get; set; }
    public Admission Admission { get; set; } = null!;
    [MaxLength(60)] public string FromWard { get; set; } = string.Empty;
    [MaxLength(60)] public string ToWard { get; set; } = string.Empty;
    public int? ToBedId { get; set; }
    public Bed? ToBed { get; set; }
    [MaxLength(200)] public string Reason { get; set; } = string.Empty;
    public TransferStatus Status { get; set; } = TransferStatus.WaitingBed;
    public DateTime? CompletedAt { get; set; }
}

// ---------- التمريض ----------

public class VitalSignRecord : Entity
{
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int? AdmissionId { get; set; }
    public int? RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? HeartRate { get; set; }
    [Precision(4, 1)] public decimal? TemperatureC { get; set; }
    public int? Spo2 { get; set; }
    public int? RespiratoryRate { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
}

public class NursingNote : Entity
{
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int? AdmissionId { get; set; }
    public int NurseId { get; set; }
    [ForeignKey(nameof(NurseId))] public User Nurse { get; set; } = null!;
    [Column(TypeName = "text")] public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

[Index(nameof(DueAt))]
public class MedicationAdministration : Entity
{
    public int AdmissionId { get; set; }
    public Admission Admission { get; set; } = null!;
    public int? PrescriptionItemId { get; set; }
    [MaxLength(150)] public string DrugName { get; set; } = string.Empty;
    [MaxLength(80)] public string? Dose { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? GivenAt { get; set; }
    public int? GivenByUserId { get; set; }
    public DoseStatus Status { get; set; } = DoseStatus.Due;
    [MaxLength(300)] public string? Notes { get; set; }
}

// ---------- الاستقبال ----------

[Index(nameof(BranchId), nameof(Date), nameof(TicketNo), IsUnique = true)]
public class QueueTicket : Entity
{
    public int BranchId { get; set; }
    public DateOnly Date { get; set; }
    public int TicketNo { get; set; }
    public int? PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User? Patient { get; set; }
    [MaxLength(120)] public string? PatientName { get; set; }
    public int? DoctorId { get; set; }
    [ForeignKey(nameof(DoctorId))] public User? Doctor { get; set; }
    public QueueStatus Status { get; set; } = QueueStatus.Waiting;
    public DateTime CreatedAt { get; set; }
    public DateTime? CalledAt { get; set; }
}

// ---------- الطوارئ ----------

[Index(nameof(Status), nameof(TriageLevel))]
public class EmergencyCase : AuditedEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int? PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User? Patient { get; set; }
    [MaxLength(120)] public string PatientName { get; set; } = string.Empty;
    public int? Age { get; set; }
    [MaxLength(200)] public string Complaint { get; set; } = string.Empty;
    /// <summary>مستوى الفرز 1 (حرج) إلى 5 (غير عاجل).</summary>
    public int TriageLevel { get; set; }
    [MaxLength(60)] public string Zone { get; set; } = string.Empty;
    public EmergencyStatus Status { get; set; } = EmergencyStatus.Waiting;
    public DateTime ArrivedAt { get; set; }
    [MaxLength(30)] public string? Consciousness { get; set; }
    public int? SystolicBp { get; set; }
    public int? HeartRate { get; set; }
    public int? Spo2 { get; set; }
    public int? RespiratoryRate { get; set; }
    [Precision(4, 1)] public decimal? TemperatureC { get; set; }
    public int? PainScore { get; set; }
    [MaxLength(200)] public string? RedFlags { get; set; }
    public int? TriagedByUserId { get; set; }
    public int? AdmissionId { get; set; }
}

public class Ambulance : AuditedEntity
{
    public int BranchId { get; set; }
    [MaxLength(20)] public string UnitNo { get; set; } = string.Empty;
    [MaxLength(120)] public string PatientDescription { get; set; } = string.Empty;
    [MaxLength(200)] public string Condition { get; set; } = string.Empty;
    public int TriageLevel { get; set; }
    public int? EtaMinutes { get; set; }
    public AmbulanceStatus Status { get; set; } = AmbulanceStatus.EnRoute;
}

// ---------- غرفة العمليات ----------

[Index(nameof(BranchId), nameof(Code), IsUnique = true)]
public class OperatingRoom : Entity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    public OperatingRoomState State { get; set; } = OperatingRoomState.Available;
    [MaxLength(200)] public string? Note { get; set; }
}

[Index(nameof(No), IsUnique = true)]
[Index(nameof(RoomId), nameof(Date))]
public class SurgeryCase : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    [MaxLength(200)] public string ProcedureName { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public OperatingRoom Room { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int StartMinutes { get; set; }
    public int DurationMinutes { get; set; }
    public int SurgeonId { get; set; }
    [ForeignKey(nameof(SurgeonId))] public User Surgeon { get; set; } = null!;
    public int? AnesthetistId { get; set; }
    [ForeignKey(nameof(AnesthetistId))] public User? Anesthetist { get; set; }
    public SurgeryStatus Status { get; set; } = SurgeryStatus.Scheduled;
    public bool PreOpReady { get; set; }
    public int? PreOpApprovedByUserId { get; set; }
    public bool PreOpAlertAcknowledged { get; set; }
    [MaxLength(300)] public string? PreOpAlert { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public List<PreOpCheckItem> Checklist { get; set; } = [];
    public List<SurgeryTeamMember> Team { get; set; } = [];
}

[Index(nameof(SurgeryCaseId), nameof(ItemKey), IsUnique = true)]
public class PreOpCheckItem : Entity
{
    public int SurgeryCaseId { get; set; }
    public SurgeryCase SurgeryCase { get; set; } = null!;
    [MaxLength(40)] public string ItemKey { get; set; } = string.Empty;
    public bool Done { get; set; }
    public int? DoneByUserId { get; set; }
    public DateTime? DoneAt { get; set; }
}

public class SurgeryTeamMember : Entity
{
    public int SurgeryCaseId { get; set; }
    public SurgeryCase SurgeryCase { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    [MaxLength(60)] public string RoleInTeam { get; set; } = string.Empty;
}
