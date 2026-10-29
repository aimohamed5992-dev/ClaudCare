using System.ComponentModel.DataAnnotations;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Contracts;

// ---------- التمريض ----------
public sealed record RecordVitalsRequest(
    [Required] int? PatientId, int? AdmissionId,
    [Range(40, 300)] int? SystolicBp, [Range(20, 200)] int? DiastolicBp, [Range(20, 300)] int? HeartRate,
    [Range(30, 45)] decimal? TemperatureC, [Range(30, 100)] int? Spo2, [Range(4, 80)] int? RespiratoryRate, [StringLength(300)] string? Notes);
public sealed record NurseVitalsDto(int Id, DateTime RecordedAt, int? SystolicBp, int? DiastolicBp, int? HeartRate, decimal? TemperatureC, int? Spo2, int? RespiratoryRate, string? Notes, IReadOnlyList<string> Alerts);
public sealed record AddNursingNoteRequest([Required, StringLength(4000, MinimumLength = 2)] string Note, int? AdmissionId);
public sealed record NursingNoteDto(int Id, DateTime CreatedAt, string NurseName, string Note, int? AdmissionId);
public sealed record ScheduleDoseRequest([Required, StringLength(150, MinimumLength = 2)] string DrugName, [StringLength(80)] string? Dose, [Required] DateTime? DueAt, int? PrescriptionItemId);
public sealed record DoseActionRequest([StringLength(300)] string? Notes);
public sealed record AdminDoseDto(int Id, int AdmissionId, string DrugName, string? Dose, DateTime DueAt, DateTime? GivenAt, DoseStatus Status, string? Notes, bool Overdue);

// ---------- التنويم ----------
public sealed record BedDto(int Id, string Ward, string Code, BedStatus Status, string? ReservedFor, string? Patient);
public sealed record BedBoardDto(int Total, int Available, int Occupied, int Cleaning, int Reserved, IReadOnlyList<BedDto> Beds);
public sealed record SetBedStatusRequest([Required] BedStatus? Status, [StringLength(150)] string? ReservedFor);
public sealed record RequestAdmissionRequest([Required] int? PatientId, [Required] AdmissionType? Type, [Required, StringLength(200, MinimumLength = 2)] string Reason, [Required, StringLength(60, MinimumLength = 2)] string RequestedWard, int? DoctorId, [StringLength(60)] string? RequestedFrom);
public sealed record AdmissionChecksRequest(bool? GuardianConsent, bool? InsuranceApprovalVerified);
public sealed record AdmitRequest([Required] int? BedId);
public sealed record DischargeRequest(bool DischargeOrderConfirmed, bool MedsReconciled, bool BillSettled, bool InstructionsGiven, [Required, StringLength(4000, MinimumLength = 2)] string Summary);
public sealed record AdmissionDto(int Id, string No, PersonRefDto Patient, AdmissionType Type, AdmissionStatus Status, string Reason, string RequestedWard, string? BedCode, string? Ward, string? DoctorName,
    DateTime RequestedAt, DateTime? AdmittedAt, DateTime? DischargedAt, bool GuardianConsent, bool InsuranceApprovalVerified, bool DischargeOrderConfirmed, bool MedsReconciled, bool BillSettled, bool InstructionsGiven, string? DischargeSummary);
public sealed record TransferRequest([Required, StringLength(60, MinimumLength = 2)] string ToWard, [Required, StringLength(200, MinimumLength = 2)] string Reason);
public sealed record CompleteTransferRequest([Required] int? BedId);
public sealed record TransferDto(int Id, string No, int AdmissionId, string PatientName, string FromWard, string ToWard, string Reason, TransferStatus Status, DateTime CreatedAt, DateTime? CompletedAt);

// ---------- الطوارئ ----------
public sealed record CreateEmergencyCaseRequest(int? PatientId, [Required, StringLength(120, MinimumLength = 2)] string PatientName, [Range(0, 130)] int? Age, [Required, StringLength(200, MinimumLength = 2)] string Complaint,
    [Required, Range(1, 5)] int? TriageLevel, [Required, StringLength(60, MinimumLength = 2)] string Zone, [StringLength(30)] string? Consciousness, [Range(40, 300)] int? SystolicBp, [Range(20, 300)] int? HeartRate,
    [Range(30, 100)] int? Spo2, [Range(4, 80)] int? RespiratoryRate, [Range(30, 45)] decimal? TemperatureC, [Range(0, 10)] int? PainScore, [StringLength(200)] string? RedFlags);
public sealed record EmergencyCaseDto(int Id, string PatientName, int? PatientId, int? Age, string Complaint, int TriageLevel, bool Escalated, string Zone, EmergencyStatus Status, DateTime ArrivedAt, int WaitingMinutes,
    string? Consciousness, int? SystolicBp, int? HeartRate, int? Spo2, int? RespiratoryRate, decimal? TemperatureC, int? PainScore, string? RedFlags, int? AdmissionId);
public sealed record EmergencyBoardDto(int Waiting, int InExam, IReadOnlyDictionary<int, int> ByLevel, IReadOnlyList<EmergencyCaseDto> Cases);
public sealed record EmergencyAdmitRequest([Required, StringLength(60, MinimumLength = 2)] string Ward, [StringLength(200)] string? Reason);
public sealed record CreateAmbulanceRequest([Required, StringLength(20, MinimumLength = 1)] string UnitNo, [Required, StringLength(120, MinimumLength = 2)] string PatientDescription, [Required, StringLength(200, MinimumLength = 2)] string Condition, [Required, Range(1, 5)] int? TriageLevel, [Range(0, 240)] int? EtaMinutes);
public sealed record AmbulanceDto(int Id, string UnitNo, string PatientDescription, string Condition, int TriageLevel, int? EtaMinutes, AmbulanceStatus Status);

// ---------- الاستقبال ----------
public sealed record IssueTicketRequest(int? PatientId, [StringLength(120)] string? PatientName, int? DoctorId);
public sealed record TicketDto(int Id, int TicketNo, string PatientName, int? PatientId, int? DoctorId, QueueStatus Status, DateTime CreatedAt, DateTime? CalledAt, int WaitingMinutes);
public sealed record RegisterPatientRequest([Required, StringLength(120, MinimumLength = 2)] string FullName, [Required] string Phone, string? NationalId, [Range(1900, 2100)] int? BirthYear, Gender? Gender);
public sealed record ReceptionTodayDto(DateOnly Date, int Appointments, int Pending, int Confirmed, int Arrived, int InProgress, int Done, int Cancelled, int QueueWaiting, int QueueCalled, int QueueDone);

// ---------- العمليات ----------
public sealed record OperatingRoomDto(int Id, string Code, OperatingRoomState State, string? Note);
public sealed record SetRoomStateRequest([Required] OperatingRoomState? State, [StringLength(200)] string? Note);
public sealed record ScheduleSurgeryRequest([Required] int? PatientId, [Required, StringLength(200, MinimumLength = 2)] string ProcedureName, [Required] int? RoomId, [Required] DateOnly? Date,
    [Required, Range(0, 1439)] int? StartMinutes, [Required, Range(15, 720)] int? DurationMinutes, [Required] int? SurgeonId, int? AnesthetistId, [StringLength(300)] string? Notes, [StringLength(300)] string? PreOpAlert);
public sealed record ChecklistItemDto(string Key, string Label, bool Done, DateTime? DoneAt);
public sealed record SurgeryDto(int Id, string No, PersonRefDto Patient, string ProcedureName, string RoomCode, DateOnly Date, int StartMinutes, int DurationMinutes, string SurgeonName, string? AnesthetistName,
    SurgeryStatus Status, bool PreOpReady, string? PreOpAlert, bool PreOpAlertAcknowledged, string? Notes, IReadOnlyList<ChecklistItemDto> Checklist);
public sealed record SetChecklistRequest([Required] bool? Done);
public sealed record ApprovePreOpRequest(bool AcknowledgeAlert = false);
