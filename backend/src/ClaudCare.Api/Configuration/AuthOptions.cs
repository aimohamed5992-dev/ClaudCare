namespace ClaudCare.Api.Configuration;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "claudcare-api";
    public string Audience { get; set; } = "claudcare-app";
    /// <summary>مفتاح التوقيع (32 حرفًا على الأقل). في الإنتاج يُمرَّر عبر متغير بيئة Jwt__SigningKey.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
    public int ChallengeMinutes { get; set; } = 10;
}

public sealed class AuthOptions
{
    public const string Section = "Auth";
    public int OtpLength { get; set; } = 6;
    public int OtpMinutes { get; set; } = 5;
    public int OtpMaxAttempts { get; set; } = 5;
    public int OtpResendSeconds { get; set; } = 30;
    public int OtpMaxPerHour { get; set; } = 6;
    /// <summary>رمز ثابت للتجربة (مثل 123456). يُرفض بدء التشغيل خارج Development إن كان مضبوطًا.</summary>
    public string? FixedOtpCode { get; set; }
    /// <summary>الأدوار غير المريض تتطلب رمز تحقق بعد كلمة المرور.</summary>
    public bool RequireOtpForStaff { get; set; } = true;
    public int MaxFailedLogins { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int MaxActiveSessions { get; set; } = 10;
    public int MinPasswordLength { get; set; } = 8;
}

public sealed class RateLimitOptions
{
    public const string Section = "RateLimit";
    public const string AuthPolicy = "auth";
    public int AuthPermitPerMinute { get; set; } = 30;
    /// <summary>سقف عام لكل مستخدم (أو عنوان IP للزائر) في الدقيقة على كل المسارات.</summary>
    public int GlobalPermitPerMinute { get; set; } = 600;
}

public sealed class SeedOptions
{
    public const string Section = "Seed";
    /// <summary>ينشئ حسابات تجريبية لكل دور (Development فقط).</summary>
    public bool DemoAccounts { get; set; }
    public string DemoPassword { get; set; } = "Demo@1234";
}
