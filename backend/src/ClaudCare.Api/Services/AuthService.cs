using System.Text.RegularExpressions;
using ClaudCare.Api.Auth;
using ClaudCare.Api.Configuration;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Services;

public sealed partial class AuthService(
    ClaudCareDbContext db,
    ITokenService tokens,
    OtpService otps,
    PasswordPolicy passwordPolicy,
    IPasswordHasher<User> hasher,
    IAuditService audit,
    IOptions<AuthOptions> authOptions,
    IOptions<JwtOptions> jwtOptions,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    private AuthOptions O => authOptions.Value;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- الدخول

    public async Task<LoginResponseDto> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await FindByIdentifierAsync(req.Identifier, ct);
        if (user is null)
        {
            hasher.HashPassword(new User(), req.Password); // تقليل فرق التوقيت بين وجود الحساب وعدمه
            throw ApiException.Unauthorized("invalid_credentials", "بيانات الدخول غير صحيحة.");
        }

        if (user.LockoutEnd is { } until && until > Now)
            throw ApiException.Locked("account_locked", "الحساب مقفل مؤقتًا بسبب محاولات دخول فاشلة.", (int)(until - Now).TotalSeconds + 1);

        var verify = hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (verify == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            audit.Add("auth.login_failed", user.Id, ip: currentUser.Ip);
            if (user.FailedLoginCount >= O.MaxFailedLogins)
            {
                user.LockoutEnd = Now.AddMinutes(O.LockoutMinutes);
                user.FailedLoginCount = 0;
                audit.Add("auth.locked", user.Id, ip: currentUser.Ip);
                await db.SaveChangesAsync(ct);
                throw ApiException.Locked("account_locked", "الحساب مقفل مؤقتًا بسبب محاولات دخول فاشلة.", O.LockoutMinutes * 60);
            }
            await db.SaveChangesAsync(ct);
            throw ApiException.Unauthorized("invalid_credentials", "بيانات الدخول غير صحيحة.");
        }

        if (!user.IsActive) throw ApiException.Forbidden("account_disabled", "هذا الحساب معطّل. تواصل مع الإدارة.");

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        if (verify == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = hasher.HashPassword(user, req.Password);

        // الطاقم: خطوة ثانية دائمًا. المريض: فقط إن لم يؤكد جواله بعد.
        OtpPurpose? purpose = user.Role != UserRole.Patient && O.RequireOtpForStaff ? OtpPurpose.Login2Fa
            : user.Role == UserRole.Patient && !user.PhoneConfirmed ? OtpPurpose.Register
            : null;

        if (purpose is { } p)
        {
            var challenge = await StartChallengeAsync(user, p, ct);
            audit.Add("auth.otp_required", user.Id, ip: currentUser.Ip);
            await db.SaveChangesAsync(ct);
            return new LoginResponseDto("otp_required", null, challenge);
        }

        var auth = await IssueSessionAsync(user, req.DeviceName, ct);
        return new LoginResponseDto("authenticated", auth, null);
    }

    // ---------------------------------------------------------------- التسجيل

    public async Task<OtpChallengeDto> RegisterPatientAsync(RegisterRequest req, CancellationToken ct)
    {
        var phone = PhoneNormalizer.TryNormalize(req.Phone) ?? throw ApiException.Validation(nameof(req.Phone), "رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام");
        var nid = PhoneNormalizer.TryNationalId(req.NationalId) ?? throw ApiException.Validation(nameof(req.NationalId), "رقم الهوية 10 أرقام ويبدأ بـ 1 أو 2");
        if (passwordPolicy.Validate(req.Password) is { } pwErr) throw ApiException.Validation(nameof(req.Password), pwErr);
        var name = req.FullName.Trim();
        if (NameWords().Matches(name).Count < 2) throw ApiException.Validation(nameof(req.FullName), "أدخل اسمك الثنائي على الأقل");
        if (req.BirthYear is { } by && (by < 1900 || by > Now.Year)) throw ApiException.Validation(nameof(req.BirthYear), "سنة الميلاد غير صحيحة");

        var byPhone = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct);
        var byNid = await db.Users.FirstOrDefaultAsync(u => u.NationalId == nid, ct);

        // تسجيل سابق لم يكتمل (الجوال لم يُؤكَّد): نُحدّث بياناته ونعيد إرسال الرمز بدل رفض المستخدم
        var abandoned = byPhone is { Role: UserRole.Patient, PhoneConfirmed: false } ? byPhone : null;
        if (byPhone is not null && abandoned is null) throw ApiException.Conflict("phone_taken", "رقم الجوال مسجّل مسبقًا. جرّب تسجيل الدخول.");
        if (byNid is not null && byNid.Id != abandoned?.Id) throw ApiException.Conflict("national_id_taken", "رقم الهوية مسجّل مسبقًا. جرّب تسجيل الدخول.");

        var user = abandoned ?? new User { Role = UserRole.Patient, Phone = phone };
        user.FullName = name;
        user.NationalId = nid;
        user.Gender = req.Gender;
        user.BirthYear = req.BirthYear;
        user.PasswordHash = hasher.HashPassword(user, req.Password);

        try
        {
            // EnableRetryOnFailure يمنع المعاملات اليدوية إلا داخل استراتيجية التنفيذ
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (abandoned is null)
                {
                    db.Users.Add(user);
                    await db.SaveChangesAsync(ct);
                    db.PatientProfiles.Add(new PatientProfile { UserId = user.Id, MedicalRecordNo = $"MRN-{user.Id:D6}" });
                    audit.Add("auth.registered", user.Id, ip: currentUser.Ip);
                }
                var challenge = await StartChallengeAsync(user, OtpPurpose.Register, ct);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return challenge;
            });
        }
        catch (DbUpdateException)
        {
            // سباق على الفهرس الفريد (تسجيل متزامن بنفس الجوال/الهوية)
            throw ApiException.Conflict("already_registered", "هذه البيانات مسجّلة مسبقًا.");
        }
    }

    // ---------------------------------------------------------------- رمز التحقق

    public async Task<OtpVerifyResponseDto> VerifyOtpAsync(OtpVerifyRequest req, CancellationToken ct)
    {
        var ch = tokens.ReadChallengeToken(req.ChallengeToken) ?? throw ApiException.BadRequest("challenge_invalid", "انتهت الجلسة. أعد المحاولة من البداية.");
        if (ch.UserId == 0) throw ApiException.BadRequest("otp_invalid", "رمز التحقق غير صحيح."); // تحدٍّ وهمي (استعادة لحساب غير موجود)

        await otps.VerifyAsync(ch.OtpId, ch.Purpose, req.Code, ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == ch.UserId, ct) ?? throw ApiException.BadRequest("challenge_invalid", "انتهت الجلسة. أعد المحاولة من البداية.");
        if (!user.IsActive) throw ApiException.Forbidden("account_disabled", "هذا الحساب معطّل. تواصل مع الإدارة.");

        switch (ch.Purpose)
        {
            case OtpPurpose.ResetPassword:
                var (token, secs) = tokens.CreateResetToken(user);
                audit.Add("auth.reset_verified", user.Id, ip: currentUser.Ip);
                await db.SaveChangesAsync(ct);
                return new OtpVerifyResponseDto(null, token, secs);

            case OtpPurpose.Register:
                user.PhoneConfirmed = true;
                goto default;

            default:
                var auth = await IssueSessionAsync(user, req.DeviceName, ct);
                return new OtpVerifyResponseDto(auth, null, null);
        }
    }

    public async Task<OtpChallengeDto> ResendOtpAsync(OtpResendRequest req, CancellationToken ct)
    {
        var ch = tokens.ReadChallengeToken(req.ChallengeToken) ?? throw ApiException.BadRequest("challenge_invalid", "انتهت الجلسة. أعد المحاولة من البداية.");
        if (ch.UserId == 0) return FakeChallenge(ch.Purpose, "جوالك المسجّل");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == ch.UserId, ct) ?? throw ApiException.BadRequest("challenge_invalid", "انتهت الجلسة. أعد المحاولة من البداية.");
        var challenge = await StartChallengeAsync(user, ch.Purpose, ct);
        await db.SaveChangesAsync(ct);
        return challenge;
    }

    // ---------------------------------------------------------------- استعادة كلمة المرور

    /// <summary>يعيد دائمًا تحديًا (حتى لحساب غير موجود) كي لا يُكشف وجود الحسابات.</summary>
    public async Task<OtpChallengeDto> ForgotPasswordAsync(ForgotPasswordRequest req, CancellationToken ct)
    {
        var user = await FindByIdentifierAsync(req.Identifier, ct);
        if (user is null || !user.IsActive)
            return FakeChallenge(OtpPurpose.ResetPassword, req.Identifier.Contains('@') ? "بريدك المسجّل" : "جوالك المسجّل");

        var challenge = await StartChallengeAsync(user, OtpPurpose.ResetPassword, ct);
        audit.Add("auth.forgot_password", user.Id, ip: currentUser.Ip);
        await db.SaveChangesAsync(ct);
        return challenge;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct)
    {
        var parsed = tokens.ReadResetToken(req.ResetToken) ?? throw ApiException.BadRequest("reset_token_invalid", "انتهت صلاحية رابط الاستعادة. ابدأ من جديد.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == parsed.UserId, ct);
        if (user is null || !user.IsActive || tokens.PasswordStamp(user.PasswordHash) != parsed.Stamp)
            throw ApiException.BadRequest("reset_token_invalid", "انتهت صلاحية رابط الاستعادة. ابدأ من جديد.");
        if (passwordPolicy.Validate(req.NewPassword) is { } err) throw ApiException.Validation(nameof(req.NewPassword), err);

        user.PasswordHash = hasher.HashPassword(user, req.NewPassword);
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        await RevokeAllAsync(user.Id, ct);
        audit.Add("auth.password_reset", user.Id, ip: currentUser.Ip);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- الجلسات

    public async Task<AuthResultDto> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        var hash = tokens.Hash(req.RefreshToken);
        var rt = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                 ?? throw ApiException.Unauthorized("invalid_refresh_token", "انتهت الجلسة. سجّل الدخول من جديد.");

        if (rt.RevokedAt is not null)
        {
            // إعادة استخدام رمز سبق تدويره/إلغاؤه ⇒ يُحتمل أنه سُرق: نُنهي كل جلسات المستخدم
            await RevokeAllAsync(rt.UserId, ct);
            audit.Add("auth.refresh_reuse", rt.UserId, ip: currentUser.Ip);
            await db.SaveChangesAsync(ct);
            throw ApiException.Unauthorized("token_reused", "انتهت الجلسة لأسباب أمنية. سجّل الدخول من جديد.");
        }
        if (rt.ExpiresAt <= Now || !rt.User.IsActive)
            throw ApiException.Unauthorized("invalid_refresh_token", "انتهت الجلسة. سجّل الدخول من جديد.");

        var next = tokens.NewRefreshToken();
        rt.RevokedAt = Now;
        rt.ReplacedByHash = tokens.Hash(next);
        db.RefreshTokens.Add(NewRefreshRow(rt.User, next, req.DeviceName ?? rt.Device));
        var (access, secs) = tokens.CreateAccessToken(rt.User);
        await db.SaveChangesAsync(ct);
        return new AuthResultDto(access, next, secs, "Bearer", ToDto(rt.User));
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = tokens.Hash(refreshToken);
        var rt = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct);
        if (rt is null) return; // متكرر الأثر: لا خطأ
        rt.RevokedAt = Now;
        audit.Add("auth.logout", rt.UserId, ip: currentUser.Ip);
        await db.SaveChangesAsync(ct);
    }

    public async Task LogoutAllAsync(CancellationToken ct)
    {
        await RevokeAllAsync(currentUser.Id, ct);
        audit.Add("auth.logout_all", currentUser.Id, ip: currentUser.Ip);
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> MeAsync(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUser.Id, ct) ?? throw ApiException.Unauthorized("unauthenticated", "يجب تسجيل الدخول.");
        if (!user.IsActive) throw ApiException.Forbidden("account_disabled", "هذا الحساب معطّل. تواصل مع الإدارة.");
        return ToDto(user);
    }

    /// <summary>يغيّر كلمة المرور وينهي كل الجلسات الأخرى، ويعيد جلسة جديدة لهذا الجهاز.</summary>
    public async Task<AuthResultDto> ChangePasswordAsync(ChangePasswordRequest req, string? deviceName, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == currentUser.Id, ct);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
            throw ApiException.Validation(nameof(req.CurrentPassword), "كلمة المرور الحالية غير صحيحة");
        if (passwordPolicy.Validate(req.NewPassword) is { } err) throw ApiException.Validation(nameof(req.NewPassword), err);
        if (req.NewPassword == req.CurrentPassword) throw ApiException.Validation(nameof(req.NewPassword), "يجب أن تختلف كلمة المرور الجديدة عن الحالية");

        user.PasswordHash = hasher.HashPassword(user, req.NewPassword);
        await RevokeAllAsync(user.Id, ct);
        audit.Add("auth.password_changed", user.Id, ip: currentUser.Ip);
        return await IssueSessionAsync(user, deviceName, ct);
    }

    // ---------------------------------------------------------------- مساعدات

    private async Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct)
    {
        var id = PhoneNormalizer.Digits(identifier.Trim());
        if (id.Length == 0) return null;
        if (id.Contains('@')) { var e = id.ToLowerInvariant(); return await db.Users.FirstOrDefaultAsync(u => u.Email == e, ct); }
        if (PhoneNormalizer.TryNormalize(id) is { } phone) return await db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct);
        if (PhoneNormalizer.TryNationalId(id) is { } nid) return await db.Users.FirstOrDefaultAsync(u => u.NationalId == nid, ct);
        var no = id.ToUpperInvariant();
        return await db.Users.FirstOrDefaultAsync(u => u.EmployeeNo == no, ct);
    }

    private async Task<OtpChallengeDto> StartChallengeAsync(User user, OtpPurpose purpose, CancellationToken ct)
    {
        var (target, destination) = user.Phone is { } p ? (p, PhoneNormalizer.MaskPhone(p))
            : user.Email is { } e ? (e, PhoneNormalizer.MaskEmail(e))
            : throw ApiException.BadRequest("no_contact", "لا يوجد جوال أو بريد مسجّل لإرسال الرمز. تواصل مع الإدارة.");
        var otp = await otps.IssueAsync(purpose, target, ct);
        return new OtpChallengeDto(tokens.CreateChallengeToken(user.Id, purpose, otp.Id), purpose, destination, otps.ExpiresInSeconds, otps.ResendAfterSeconds);
    }

    private OtpChallengeDto FakeChallenge(OtpPurpose purpose, string destination) =>
        new(tokens.CreateChallengeToken(0, purpose, 0), purpose, destination, otps.ExpiresInSeconds, otps.ResendAfterSeconds);

    private async Task<AuthResultDto> IssueSessionAsync(User user, string? device, CancellationToken ct)
    {
        user.LastLoginAt = Now;
        var refresh = tokens.NewRefreshToken();
        db.RefreshTokens.Add(NewRefreshRow(user, refresh, device));

        // سقف للجلسات النشطة: نُلغي الأقدم
        var active = await db.RefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > Now).OrderBy(t => t.CreatedAt).ToListAsync(ct);
        foreach (var old in active.Take(Math.Max(0, active.Count - (O.MaxActiveSessions - 1)))) old.RevokedAt = Now;

        audit.Add("auth.login", user.Id, ip: currentUser.Ip);
        var (access, secs) = tokens.CreateAccessToken(user);
        await db.SaveChangesAsync(ct);
        return new AuthResultDto(access, refresh, secs, "Bearer", ToDto(user));
    }

    private RefreshToken NewRefreshRow(User user, string token, string? device) => new()
    {
        UserId = user.Id,
        TokenHash = tokens.Hash(token),
        CreatedAt = Now,
        ExpiresAt = Now.AddDays(jwtOptions.Value.RefreshTokenDays),
        Device = device,
        Ip = currentUser.Ip,
    };

    private async Task RevokeAllAsync(int userId, CancellationToken ct)
    {
        var now = Now;
        foreach (var t in await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct)) t.RevokedAt = now;
    }

    public static UserDto ToDto(User u) => new(u.Id, u.FullName, u.Role, u.Email, u.Phone, u.EmployeeNo, u.BranchId, u.AvatarKey, u.Gender, u.BirthYear, u.PhoneConfirmed);

    [GeneratedRegex(@"[\p{L}]{2,}")] private static partial Regex NameWords();
}
