using System.Security.Cryptography;
using System.Text;
using ClaudCare.Api.Configuration;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Services;

/// <summary>إصدار رموز التحقق والتحقق منها: مُجزّأة (HMAC)، صالحة لدقائق، بمحاولات محدودة وتقييد لإعادة الإرسال.</summary>
public sealed class OtpService(ClaudCareDbContext db, IOtpSender sender, IOptions<AuthOptions> authOptions, IOptions<JwtOptions> jwt, TimeProvider clock)
{
    private AuthOptions O => authOptions.Value;

    private string Hmac(int otpId, string code) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwt.Value.SigningKey), Encoding.UTF8.GetBytes($"{otpId}:{code}"))).ToLowerInvariant();

    public async Task<OtpCode> IssueAsync(OtpPurpose purpose, string target, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        // لا تُرسل رموز كثيرة للهدف نفسه
        var recent = await db.OtpCodes.Where(o => o.Target == target && o.CreatedAt > now.AddHours(-1)).OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        if (recent.Count >= O.OtpMaxPerHour)
            throw ApiException.TooMany("otp_limit", "تجاوزت عدد محاولات الإرسال المسموحة. حاول لاحقًا.", 3600);
        if (recent.FirstOrDefault() is { } last && (now - last.CreatedAt).TotalSeconds < O.OtpResendSeconds)
            throw ApiException.TooMany("otp_throttled", "انتظر قليلًا قبل طلب رمز جديد.", O.OtpResendSeconds - (int)(now - last.CreatedAt).TotalSeconds);

        // أي رمز سابق غير مستخدم يبطل
        foreach (var old in recent.Where(o => o.ConsumedAt is null)) old.ConsumedAt = now;

        var code = !string.IsNullOrEmpty(O.FixedOtpCode)
            ? O.FixedOtpCode
            : RandomNumberGenerator.GetInt32((int)Math.Pow(10, O.OtpLength - 1), (int)Math.Pow(10, O.OtpLength)).ToString();

        var otp = new OtpCode { Purpose = purpose, Target = target, CreatedAt = now, ExpiresAt = now.AddMinutes(O.OtpMinutes) };
        db.OtpCodes.Add(otp);
        await db.SaveChangesAsync(ct); // للحصول على Id
        otp.CodeHash = Hmac(otp.Id, code);
        await db.SaveChangesAsync(ct);

        await sender.SendAsync(target, $"رمز التحقق الخاص بك في ClaudCare: {code}. صالح لمدة {O.OtpMinutes} دقائق.", ct);
        return otp;
    }

    /// <summary>يتحقق من الرمز ويستهلكه. يرمي ApiException عند الفشل.</summary>
    public async Task VerifyAsync(int otpId, OtpPurpose purpose, string code, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var otp = await db.OtpCodes.FirstOrDefaultAsync(o => o.Id == otpId && o.Purpose == purpose, ct)
                  ?? throw ApiException.BadRequest("otp_invalid", "رمز التحقق غير صحيح.");

        if (otp.ConsumedAt is not null) throw ApiException.BadRequest("otp_invalid", "رمز التحقق مستخدم أو ملغى. اطلب رمزًا جديدًا.");
        if (otp.ExpiresAt <= now) throw ApiException.BadRequest("otp_expired", "انتهت صلاحية الرمز. اطلب رمزًا جديدًا.");
        if (otp.Attempts >= O.OtpMaxAttempts) throw ApiException.BadRequest("otp_attempts_exceeded", "تجاوزت عدد المحاولات. اطلب رمزًا جديدًا.");

        otp.Attempts++;
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hmac(otp.Id, code.Trim())), Encoding.ASCII.GetBytes(otp.CodeHash));
        if (!ok)
        {
            await db.SaveChangesAsync(ct);
            throw ApiException.BadRequest("otp_invalid", "رمز التحقق غير صحيح.");
        }

        otp.ConsumedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public int ResendAfterSeconds => O.OtpResendSeconds;
    public int ExpiresInSeconds => O.OtpMinutes * 60;
}
