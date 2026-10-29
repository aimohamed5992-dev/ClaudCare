using System.ComponentModel.DataAnnotations;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Contracts;

public sealed record LoginRequest(
    [Required, StringLength(200)] string Identifier,
    [Required, StringLength(200)] string Password,
    [StringLength(100)] string? DeviceName);

public sealed record RegisterRequest(
    [Required, StringLength(120, MinimumLength = 3)] string FullName,
    [Required, StringLength(20)] string Phone,
    [Required, StringLength(10, MinimumLength = 10)] string NationalId,
    [Required, StringLength(200)] string Password,
    Gender? Gender,
    int? BirthYear);

public sealed record OtpVerifyRequest([Required] string ChallengeToken, [Required, StringLength(10)] string Code, [StringLength(100)] string? DeviceName);
public sealed record OtpResendRequest([Required] string ChallengeToken);
public sealed record ForgotPasswordRequest([Required, StringLength(200)] string Identifier);
public sealed record ResetPasswordRequest([Required] string ResetToken, [Required, StringLength(200)] string NewPassword);
public sealed record RefreshRequest([Required] string RefreshToken, [StringLength(100)] string? DeviceName);
public sealed record LogoutRequest([Required] string RefreshToken);
public sealed record ChangePasswordRequest([Required, StringLength(200)] string CurrentPassword, [Required, StringLength(200)] string NewPassword);

public sealed record UserDto(int Id, string FullName, UserRole Role, string? Email, string? Phone, string? EmployeeNo, int? BranchId, string? AvatarKey, Gender? Gender, int? BirthYear, bool PhoneConfirmed);

public sealed record AuthResultDto(string AccessToken, string RefreshToken, int ExpiresInSeconds, string TokenType, UserDto User);

/// <summary>تحدٍّ يتطلب إدخال رمز OTP (الدخول بخطوتين أو تأكيد الجوال أو استعادة كلمة المرور).</summary>
public sealed record OtpChallengeDto(string ChallengeToken, OtpPurpose Purpose, string Destination, int ExpiresInSeconds, int ResendAfterSeconds);

/// <summary>Status = "authenticated" (ومعه Auth) أو "otp_required" (ومعه Challenge).</summary>
public sealed record LoginResponseDto(string Status, AuthResultDto? Auth, OtpChallengeDto? Challenge);

/// <summary>نتيجة التحقق من الرمز: إما جلسة (Auth) أو رمز استعادة (ResetToken).</summary>
public sealed record OtpVerifyResponseDto(AuthResultDto? Auth, string? ResetToken, int? ResetExpiresInSeconds);
