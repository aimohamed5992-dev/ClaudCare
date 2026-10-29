using ClaudCare.Api.Configuration;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClaudCare.Api.Controllers;

/// <summary>المصادقة: الدخول (مع OTP للطاقم)، تسجيل المريض، استعادة كلمة المرور، وإدارة الجلسات.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>الدخول بالبريد أو الجوال أو الهوية أو رقم الموظف. الطاقم يستلم تحدي OTP.</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    public async Task<LoginResponseDto> Login(LoginRequest req, CancellationToken ct) => await auth.LoginAsync(req, ct);

    /// <summary>تسجيل مريض جديد؛ يُرسل رمز تأكيد إلى الجوال.</summary>
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    [ProducesResponseType<OtpChallengeDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await auth.RegisterPatientAsync(req, ct));

    /// <summary>التحقق من الرمز: يعيد جلسة (دخول/تسجيل) أو رمز استعادة.</summary>
    [HttpPost("otp/verify"), AllowAnonymous, EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    public async Task<OtpVerifyResponseDto> VerifyOtp(OtpVerifyRequest req, CancellationToken ct) => await auth.VerifyOtpAsync(req, ct);

    /// <summary>إعادة إرسال الرمز (مقيّدة زمنيًا وبعدد في الساعة).</summary>
    [HttpPost("otp/resend"), AllowAnonymous, EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    public async Task<OtpChallengeDto> ResendOtp(OtpResendRequest req, CancellationToken ct) => await auth.ResendOtpAsync(req, ct);

    /// <summary>بدء استعادة كلمة المرور. تُعيد دائمًا استجابة ناجحة حتى لا يُكشف وجود الحساب.</summary>
    [HttpPost("password/forgot"), AllowAnonymous, EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    public async Task<OtpChallengeDto> Forgot(ForgotPasswordRequest req, CancellationToken ct) => await auth.ForgotPasswordAsync(req, ct);

    /// <summary>تعيين كلمة مرور جديدة برمز الاستعادة. ينهي كل الجلسات.</summary>
    [HttpPost("password/reset"), AllowAnonymous, EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    public async Task<IActionResult> Reset(ResetPasswordRequest req, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(req, ct);
        return NoContent();
    }

    /// <summary>تجديد الجلسة (يُدوَّر رمز التجديد في كل مرة).</summary>
    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    public async Task<AuthResultDto> Refresh(RefreshRequest req, CancellationToken ct) => await auth.RefreshAsync(req, ct);

    /// <summary>إنهاء الجلسة الحالية.</summary>
    [HttpPost("logout"), AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest req, CancellationToken ct)
    {
        await auth.LogoutAsync(req.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>إنهاء كل جلسات المستخدم على كل الأجهزة.</summary>
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await auth.LogoutAllAsync(ct);
        return NoContent();
    }

    /// <summary>بيانات المستخدم الحالي.</summary>
    [HttpGet("me")]
    public async Task<UserDto> Me(CancellationToken ct) => await auth.MeAsync(ct);

    /// <summary>تغيير كلمة المرور من الإعدادات؛ تُعيد جلسة جديدة وتُنهي الجلسات الأخرى.</summary>
    [HttpPost("password/change"), EnableRateLimiting(RateLimitOptions.AuthPolicy)]
    public async Task<AuthResultDto> Change(ChangePasswordRequest req, [FromHeader(Name = "X-Device-Name")] string? device, CancellationToken ct) =>
        await auth.ChangePasswordAsync(req, device, ct);
}
