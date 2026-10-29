using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Communication;

/// <summary>الملف الشخصي وأفراد العائلة والإعدادات للمستخدم الحالي.</summary>
public sealed class AccountService(ClaudCareDbContext db, ICurrentUser me, IAuditService audit)
{
    // مفاتيح الإعدادات المسموحة وقيمها الافتراضية (تنبيهات المريض والطبيب + الدخول بالبصمة)
    public static readonly IReadOnlyDictionary<string, bool> SettingDefaults = new Dictionary<string, bool>
    {
        ["appointments"] = true, ["medications"] = true, ["results"] = true, ["offers"] = false,
        ["newAppointments"] = true, ["messages"] = true, ["labResults"] = true, ["sound"] = true, ["biometric"] = false,
    };

    // ------------------------------------------------------------ الملف الشخصي

    public async Task<ProfileDto> GetProfileAsync(CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == me.Id, ct);
        return await ToProfileAsync(u, ct);
    }

    public async Task<ProfileDto> UpdateProfileAsync(UpdateProfileRequest req, CancellationToken ct)
    {
        var u = await db.Users.FirstAsync(x => x.Id == me.Id, ct);
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLowerInvariant();
        if (email is not null && await db.Users.AnyAsync(x => x.Email == email && x.Id != u.Id, ct)) throw ApiException.Conflict("email_taken", "البريد الإلكتروني مستخدم.");
        u.FullName = req.FullName.Trim();
        u.Email = email;
        if (req.BirthYear is { } by) u.BirthYear = by;
        if (req.Gender is { } g) u.Gender = g;
        if (u.Role == UserRole.Doctor && req.About is not null)
        {
            var prof = await db.DoctorProfiles.FirstOrDefaultAsync(d => d.UserId == u.Id, ct);
            if (prof is not null) prof.About = req.About.Trim();
        }
        audit.Add("profile.update", me.Id, nameof(User), u.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return await ToProfileAsync(u, ct);
    }

    private async Task<ProfileDto> ToProfileAsync(User u, CancellationToken ct)
    {
        string? about = null, specialty = null;
        if (u.Role == UserRole.Doctor)
        {
            var d = await db.DoctorProfiles.AsNoTracking().Include(x => x.Specialty).FirstOrDefaultAsync(x => x.UserId == u.Id, ct);
            about = d?.About; specialty = d?.Specialty.NameAr;
        }
        return new ProfileDto(u.Id, u.FullName, u.Phone, u.Email, u.BirthYear, u.Gender, u.Role, u.AvatarKey, about, specialty);
    }

    // ------------------------------------------------------------ العائلة

    public async Task<IReadOnlyList<FamilyMemberDto>> FamilyAsync(CancellationToken ct)
    {
        RequirePatient();
        var rows = await db.FamilyMembers.AsNoTracking().Where(f => f.PatientId == me.Id).OrderBy(f => f.Id).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<FamilyMemberDto> AddFamilyAsync(FamilyMemberRequest req, CancellationToken ct)
    {
        RequirePatient();
        if (await db.FamilyMembers.CountAsync(f => f.PatientId == me.Id, ct) >= 10) throw ApiException.Conflict("family_limit", "بلغت الحد الأقصى لأفراد العائلة (10).");
        var f = new FamilyMember { PatientId = me.Id, Name = req.Name.Trim(), Relation = req.Relation.Trim(), BirthYear = req.BirthYear, Gender = req.Gender };
        db.FamilyMembers.Add(f);
        await db.SaveChangesAsync(ct);
        return ToDto(f);
    }

    public async Task<FamilyMemberDto> UpdateFamilyAsync(int id, FamilyMemberRequest req, CancellationToken ct)
    {
        var f = await FindFamilyAsync(id, ct);
        f.Name = req.Name.Trim(); f.Relation = req.Relation.Trim(); f.BirthYear = req.BirthYear; f.Gender = req.Gender;
        await db.SaveChangesAsync(ct);
        return ToDto(f);
    }

    public async Task DeleteFamilyAsync(int id, CancellationToken ct)
    {
        var f = await FindFamilyAsync(id, ct);
        if (await db.Appointments.AnyAsync(a => a.FamilyMemberId == id, ct)) throw ApiException.Conflict("family_member_in_use", "لا يمكن حذف فرد لديه مواعيد مسجلة.");
        db.FamilyMembers.Remove(f);
        await db.SaveChangesAsync(ct);
    }

    private async Task<FamilyMember> FindFamilyAsync(int id, CancellationToken ct)
    {
        RequirePatient();
        return await db.FamilyMembers.FirstOrDefaultAsync(f => f.Id == id && f.PatientId == me.Id, ct) ?? throw ApiException.NotFound("family_member_not_found", "فرد العائلة غير موجود.");
    }

    private void RequirePatient()
    {
        if (me.Role != UserRole.Patient) throw ApiException.Forbidden("action_not_allowed", "متاح للمرضى فقط.");
    }

    private static FamilyMemberDto ToDto(FamilyMember f) => new(f.Id, f.Name, f.Relation, f.BirthYear, f.Gender);

    // ------------------------------------------------------------ الإعدادات

    public async Task<SettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        var saved = await db.UserSettings.AsNoTracking().Where(s => s.UserId == me.Id).ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        return new SettingsDto(SettingDefaults.ToDictionary(kv => kv.Key, kv => saved.TryGetValue(kv.Key, out var v) ? v == "true" : kv.Value));
    }

    public async Task<SettingsDto> UpdateSettingsAsync(Dictionary<string, bool> values, CancellationToken ct)
    {
        var unknown = values.Keys.FirstOrDefault(k => !SettingDefaults.ContainsKey(k));
        if (unknown is not null) throw ApiException.Validation(nameof(values), $"إعداد غير معروف: {unknown}");
        var existing = await db.UserSettings.Where(s => s.UserId == me.Id).ToDictionaryAsync(s => s.Key, ct);
        foreach (var (key, on) in values)
        {
            var text = on ? "true" : "false";
            if (existing.TryGetValue(key, out var row)) row.Value = text;
            else db.UserSettings.Add(new UserSetting { UserId = me.Id, Key = key, Value = text });
        }
        await db.SaveChangesAsync(ct);
        return await GetSettingsAsync(ct);
    }
}
