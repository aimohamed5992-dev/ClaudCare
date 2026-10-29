using System.ComponentModel.DataAnnotations;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Scheduling;

namespace ClaudCare.Api.Contracts;

public sealed record BookAppointmentRequest(
    [Required] int? DoctorId,
    [Required] DateOnly? Date,
    [Required, Range(0, 1439)] int? StartMinutes,
    [Required] VisitType? Type,
    [StringLength(500)] string? Reason,
    [StringLength(500)] string? Notes,
    int? FamilyMemberId,
    bool UseInsurance = true,
    PaymentMethod? PaymentMethod = null,
    /// <summary>للاستقبال/الإدارة فقط: الحجز نيابةً عن مريض.</summary>
    int? PatientId = null);

public sealed record CancelAppointmentRequest([Required, StringLength(300, MinimumLength = 2)] string Reason);
public sealed record RescheduleAppointmentRequest([Required] DateOnly? Date, [Required, Range(0, 1439)] int? StartMinutes);
public sealed record ReviewRequest([Required, Range(1, 5)] int? Rating, [StringLength(500)] string? Comment);

public sealed record PersonRefDto(int Id, string FullName, string? AvatarKey, int? Age = null);

public sealed record AppointmentDto(
    int Id, PersonRefDto Patient, string? FamilyMemberName, PersonRefDto Doctor, string SpecialtyName, int BranchId,
    DateOnly Date, int StartMinutes, int DurationMinutes, VisitType Type, AppointmentStatus Status, string Reason, string? Notes,
    decimal Price, decimal InsuranceCovered, decimal Due, PaymentMethod? PaymentMethod, bool Paid, string? CancelReason,
    DateTime CreatedAt, IReadOnlyList<AppointmentAction> AllowedActions, bool Reviewed);

public sealed record CancelResultDto(AppointmentDto Appointment, bool LateCancellation, decimal CancellationFee);

public sealed record AppointmentAgendaDto(DateOnly Date, int Total, int Pending, int Confirmed, int Arrived, int InProgress, int Done, int Cancelled, int Video, IReadOnlyList<AppointmentDto> Appointments);
