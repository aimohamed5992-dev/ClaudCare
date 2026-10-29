using Microsoft.IdentityModel.Tokens;

namespace ClaudCare.Api.Auth;

/// <summary>التحقق من عمر الرمز بساعة التطبيق (TimeProvider) بدل ساعة النظام، فتعمل الاختبارات بتقديم الوقت.</summary>
public static class JwtLifetime
{
    public static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);

    public static LifetimeValidator Validator(TimeProvider clock) => (notBefore, expires, _, _) =>
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return (notBefore is null || notBefore.Value <= now + Skew) && (expires is not null && expires.Value > now - Skew);
    };
}
