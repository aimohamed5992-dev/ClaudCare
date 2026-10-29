using System.ComponentModel.DataAnnotations;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Contracts;

// ---------------------------------------------------------------- الدليل الطبي

public sealed record DrugDto(int Id, string Name, string Group, string? Form, decimal UnitPrice);
public sealed record LabTestDto(string Key, string Name, string Category, IReadOnlyList<LabTestItemDto> Items);
public sealed record LabTestItemDto(string Name, string Unit, decimal? Min, decimal? Max);
public sealed record ImagingStudyDto(string Key, string Name, string Modality);

// ---------------------------------------------------------------- المرضى

public sealed record PatientSummaryDto(
    int Id, string FullName, int? Age, Gender? Gender, string? AvatarKey, string Mrn, string? BloodType, string? Phone,
    string? MainCondition, bool HasAllergies, DateOnly? LastVisit);

public sealed record AllergyDto(string Name, string? Reaction);
public sealed record ConditionDto(string Name, int? SinceYear);
public sealed record VitalsDto(int? SystolicBp, int? DiastolicBp, int? HeartRate, decimal? TemperatureC, int? Spo2, DateOnly? RecordedOn);
public sealed record ActiveMedicationDto(string Name, string? Dose, string Frequency, DateOnly EndDate);

public sealed record PatientRecordDto(
    PatientSummaryDto Summary, int? HeightCm, decimal? WeightKg, IReadOnlyList<AllergyDto> Allergies, IReadOnlyList<ConditionDto> Conditions,
    VitalsDto? LatestVitals, IReadOnlyList<ActiveMedicationDto> ActiveMedications, int VisitsCount, int LabResultsCount, int PrescriptionsCount);

public sealed record UpdateClinicalProfileRequest(
    [StringLength(5)] string? BloodType, [Range(30, 260)] int? HeightCm, [Range(1, 500)] decimal? WeightKg,
    [MaxLength(30)] List<AllergyInput>? Allergies, [MaxLength(50)] List<ConditionInput>? Conditions);
public sealed record AllergyInput([Required, StringLength(80, MinimumLength = 2)] string Name, [StringLength(200)] string? Reaction);
public sealed record ConditionInput([Required, StringLength(120, MinimumLength = 2)] string Name, [Range(1900, 2100)] int? SinceYear);

// ---------------------------------------------------------------- الكشف

public sealed record VitalsInput(
    [Range(60, 260)] int? SystolicBp, [Range(30, 160)] int? DiastolicBp, [Range(30, 220)] int? HeartRate,
    [Range(34, 42)] decimal? TemperatureC, [Range(70, 100)] int? Spo2);

public sealed record PrescriptionItemInput(
    int? DrugId, [StringLength(150)] string? DrugName, [StringLength(80)] string? Dose,
    [Required, StringLength(60)] string Frequency, [Range(0, 4)] int TimesPerDay,
    [Required, Range(1, 365)] int? DurationDays, [StringLength(200)] string? Instructions, bool AllergyOverride = false);

public sealed record CreateVisitRequest(
    int? AppointmentId, int? PatientId,
    [Required, StringLength(500, MinimumLength = 3)] string Complaint,
    VitalsInput? Vitals,
    [Required, StringLength(300, MinimumLength = 3)] string Diagnosis,
    [StringLength(4000)] string? Notes,
    [StringLength(40)] string? FollowUp,
    [MaxLength(20)] List<string>? LabTests,
    [MaxLength(20)] List<string>? ImagingStudies,
    [MaxLength(20)] List<PrescriptionItemInput>? Prescription);

public sealed record OrderRefDto(int Id, string No, string Name);

public sealed record VisitResultDto(
    int VisitId, int? AppointmentId, string? PrescriptionNo, IReadOnlyList<OrderRefDto> LabOrders, IReadOnlyList<OrderRefDto> ImagingOrders,
    IReadOnlyList<string> Warnings);

public sealed record DrugCheckRequest([Required] int? PatientId, [Required, MinLength(1), MaxLength(20)] List<int> DrugIds);
public sealed record DrugCheckItemDto(int DrugId, string DrugName, string? AllergyConflict, bool DuplicateActive);

// ---------------------------------------------------------------- السجلات

public sealed record PrescriptionItemDto(int Id, string DrugName, string? Dose, string Frequency, int TimesPerDay, int DurationDays, int Quantity, string? Instructions, bool Dispensed);

public sealed record PrescriptionDto(
    int Id, string No, DateTime IssuedAt, string DoctorName, PrescriptionStatus Status, IReadOnlyList<PrescriptionItemDto> Items);

public sealed record VisitDto(
    int Id, DateOnly Date, string DoctorName, string? Specialty, string Complaint, string Diagnosis, string? Notes, string? FollowUp,
    VitalsDto Vitals, string? PrescriptionNo, IReadOnlyList<string> Medications);

public sealed record LabResultItemDto(string Name, decimal Value, string Unit, decimal? RefMin, decimal? RefMax, ResultFlag Flag, string? Remark);

public sealed record LabOrderDto(
    int Id, string No, string TestName, string? Category, OrderPriority Priority, LabOrderStatus Status, DateTime OrderedAt, DateTime? ResultAt,
    string? DoctorName, PersonRefDto? Patient, bool Ready, int AbnormalCount, IReadOnlyList<LabResultItemDto> Items);

public sealed record ImagingReportDto(string Findings, string Impression, ReportStatus Status, DateTime? ReportedAt, string? ImageKey);

public sealed record ImagingOrderDto(
    int Id, string No, string StudyName, string Modality, OrderPriority Priority, ImagingOrderStatus Status, DateTime OrderedAt, string? DoctorName,
    PersonRefDto? Patient, ImagingReportDto? Report);

public sealed record HealthSummaryDto(string? BloodType, int? HeightCm, decimal? WeightKg, IReadOnlyList<AllergyDto> Allergies, IReadOnlyList<ConditionDto> Conditions);

// ---------------------------------------------------------------- أدوية المريض

public sealed record MedicationDto(
    int Id, string Name, string? Dose, string Frequency, int TimesPerDay, IReadOnlyList<int> Times, DateOnly StartDate, DateOnly EndDate, int DaysLeft,
    bool EndingSoon, bool Active, string DoctorName, string? Instructions, bool ReminderEnabled, RefillStatus RefillStatus);

public enum DoseState { Taken, Missed, Upcoming }
public sealed record DoseDto(int MedicationId, string Name, string? Dose, string? Instructions, int SlotIndex, int Minutes, DoseState State);
public sealed record TodayDosesDto(DateOnly Date, IReadOnlyList<DoseDto> Doses, int Taken, int Total, decimal Adherence);

public sealed record LogDoseRequest([Required, Range(0, 9)] int? SlotIndex, bool Taken = true, DateOnly? Date = null);
public sealed record UpdateMedicationRequest([Required] bool? ReminderEnabled);

public sealed record RefillRequestDto(int MedicationId, string DrugName, PersonRefDto Patient, DateTime? RequestedAt, DateOnly EndDate);
public sealed record ApproveRefillRequest([Range(1, 365)] int? Days);
public sealed record RejectRefillRequest([Required, StringLength(300, MinimumLength = 2)] string Reason);
