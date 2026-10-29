using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClaudCare.Api.Auth;
using ClaudCare.Api.Configuration;
using ClaudCare.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClaudCare.Api.Services;

public sealed record ChallengePayload(int UserId, OtpPurpose Purpose, int OtpId);

public interface ITokenService
{
    (string Token, int ExpiresInSeconds) CreateAccessToken(User user);
    string CreateChallengeToken(int userId, OtpPurpose purpose, int otpId);
    ChallengePayload? ReadChallengeToken(string token);
    (string Token, int ExpiresInSeconds) CreateResetToken(User user);
    (int UserId, string Stamp)? ReadResetToken(string token);
    string NewRefreshToken();
    string Hash(string token);
    string PasswordStamp(string passwordHash);
}

/// <summary>
/// رموز JWT بثلاثة جماهير منفصلة (وصول / تحدي OTP / استعادة) فلا يُقبل رمز تحدٍّ أو استعادة كرمز وصول.
/// </summary>
public sealed class TokenService : ITokenService
{
    public const string ChallengeAudienceSuffix = ":challenge";
    public const string ResetAudienceSuffix = ":reset";

    private readonly JwtOptions _o;
    private readonly TimeProvider _clock;
    private readonly SymmetricSecurityKey _key;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _o = options.Value;
        _clock = clock;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey));
    }

    private string Create(string audience, IEnumerable<Claim> claims, TimeSpan life)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _o.Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = now.Add(life),
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        });
    }

    private TokenValidationParameters Params(string audience) => new()
    {
        ValidIssuer = _o.Issuer,
        ValidAudience = audience,
        IssuerSigningKey = _key,
        ClockSkew = TimeSpan.FromSeconds(30),
        LifetimeValidator = JwtLifetime.Validator(_clock),
    };

    private ClaimsIdentity? Validate(string token, string audience)
    {
        try
        {
            var r = _handler.ValidateTokenAsync(token, Params(audience)).GetAwaiter().GetResult();
            return r.IsValid ? r.ClaimsIdentity : null;
        }
        catch
        {
            return null;
        }
    }

    public (string Token, int ExpiresInSeconds) CreateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(ClaimNames.TokenUse, "access"),
        };
        if (user.BranchId is { } b) claims.Add(new Claim(ClaimNames.BranchId, b.ToString()));
        var life = TimeSpan.FromMinutes(_o.AccessTokenMinutes);
        return (Create(_o.Audience, claims, life), (int)life.TotalSeconds);
    }

    public string CreateChallengeToken(int userId, OtpPurpose purpose, int otpId) =>
        Create(_o.Audience + ChallengeAudienceSuffix,
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimNames.Purpose, purpose.ToString()),
            new Claim(ClaimNames.OtpId, otpId.ToString()),
        ], TimeSpan.FromMinutes(_o.ChallengeMinutes));

    public ChallengePayload? ReadChallengeToken(string token)
    {
        var id = Validate(token, _o.Audience + ChallengeAudienceSuffix);
        if (id is null) return null;
        return int.TryParse(id.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
               && Enum.TryParse<OtpPurpose>(id.FindFirst(ClaimNames.Purpose)?.Value, out var purpose)
               && int.TryParse(id.FindFirst(ClaimNames.OtpId)?.Value, out var otp)
            ? new ChallengePayload(uid, purpose, otp)
            : null;
    }

    public (string Token, int ExpiresInSeconds) CreateResetToken(User user)
    {
        var life = TimeSpan.FromMinutes(10);
        return (Create(_o.Audience + ResetAudienceSuffix,
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            // يربط الرمز بكلمة المرور الحالية: بعد تغييرها يبطل الرمز فلا يُستخدم مرتين
            new Claim(ClaimNames.PasswordStamp, PasswordStamp(user.PasswordHash)),
        ], life), (int)life.TotalSeconds);
    }

    public (int UserId, string Stamp)? ReadResetToken(string token)
    {
        var id = Validate(token, _o.Audience + ResetAudienceSuffix);
        if (id is null) return null;
        return int.TryParse(id.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) && id.FindFirst(ClaimNames.PasswordStamp)?.Value is { } s ? (uid, s) : null;
    }

    public string NewRefreshToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public string PasswordStamp(string passwordHash) => Hash(passwordHash)[..16];

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
