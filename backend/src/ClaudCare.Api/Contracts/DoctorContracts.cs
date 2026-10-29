using System.ComponentModel.DataAnnotations;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Contracts;

public sealed record SpecialtyDto(int Id, string Key, string NameAr, string NameEn, int DoctorCount);

public sealed record NextSlotDto(DateOnly Date, int StartMinutes);

public sealed record DoctorSummaryDto(
    int Id, string FullName, string SpecialtyKey, string SpecialtyName, string? AvatarKey,
    decimal Rating, int ReviewsCount, int YearsExperience, decimal ConsultationPrice, decimal? VideoPrice,
    NextSlotDto? NextSlot, bool IsFavorite);

public sealed record WorkingHourDto(DayOfWeek DayOfWeek, bool IsActive, int StartMinutes, int EndMinutes);

public sealed record ReviewDto(int Id, string PatientName, int Rating, string? Comment, DateTime CreatedAt);

public sealed record DoctorDetailDto(
    DoctorSummaryDto Summary, string About, IReadOnlyList<string> Languages, int PatientsCount,
    string BranchName, int BranchId, int SlotMinutes, bool RequiresConfirmation,
    IReadOnlyList<WorkingHourDto> WorkingHours, IReadOnlyList<ReviewDto> LatestReviews);

public sealed record AvailabilityDayDto(DateOnly Date, DayOfWeek DayOfWeek, IReadOnlyList<int> Slots);

public sealed record AvailabilityDto(int DoctorId, VisitType Type, int SlotMinutes, IReadOnlyList<AvailabilityDayDto> Days);

public sealed record WorkingHourInput([Required] DayOfWeek DayOfWeek, bool IsActive, [Range(0, 1439)] int StartMinutes, [Range(1, 1440)] int EndMinutes);
public sealed record UpdateWorkingHoursRequest([Required, MinLength(1), MaxLength(7)] List<WorkingHourInput> Days);
public sealed record WorkingHoursResultDto(IReadOnlyList<WorkingHourDto> Days, int FutureAppointmentsOutsideHours);
