using ClaudCare.Api.Auth;
using ClaudCare.Api.Configuration;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Services;

public enum DoctorSort { Rating, Price, Experience }

public sealed class DoctorCatalogService(ClaudCareDbContext db, AvailabilityEngine engine, IClinicClock clock, ICurrentUser me, IOptions<BookingOptions> booking, TimeProvider time)
{
    private const int NextSlotLookaheadDays = 14;

    public async Task<IReadOnlyList<SpecialtyDto>> SpecialtiesAsync(CancellationToken ct)
    {
        var counts = await db.DoctorProfiles.AsNoTracking().Where(d => d.IsListed && d.User.IsActive).GroupBy(d => d.SpecialtyId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return (await db.Specialties.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct))
            .Select(s => new SpecialtyDto(s.Id, s.Key, s.NameAr, s.NameEn, counts.GetValueOrDefault(s.Id)))
            .ToList();
    }

    public async Task<PagedResult<DoctorSummaryDto>> SearchAsync(string? q, string? specialty, DoctorSort sort, bool favoritesOnly, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Clamp(page, pageSize);
        var query = db.DoctorProfiles.AsNoTracking().Where(d => d.IsListed && d.User.IsActive);

        if (!string.IsNullOrWhiteSpace(specialty)) query = query.Where(d => d.Specialty.Key == specialty);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(d => d.User.FullName.Contains(s) || d.Specialty.NameAr.Contains(s) || d.Specialty.NameEn.Contains(s) || d.About.Contains(s));
        }

        var favorites = await FavoriteIdsAsync(ct);
        if (favoritesOnly) query = query.Where(d => favorites.Contains(d.UserId));

        var total = await query.CountAsync(ct);
        query = sort switch
        {
            DoctorSort.Price => query.OrderBy(d => d.ConsultationPrice).ThenByDescending(d => d.Rating),
            DoctorSort.Experience => query.OrderByDescending(d => d.YearsExperience).ThenByDescending(d => d.Rating),
            _ => query.OrderByDescending(d => d.Rating).ThenByDescending(d => d.ReviewsCount),
        };
        var rows = await query.Include(d => d.User).Include(d => d.Specialty).Skip((p - 1) * size).Take(size).ToListAsync(ct);

        var next = await NextSlotsAsync(rows, ct);
        var items = rows.Select(d => Summary(d, next.GetValueOrDefault(d.UserId), favorites.Contains(d.UserId))).ToList();
        return new PagedResult<DoctorSummaryDto>(items, p, size, total);
    }

    public async Task<DoctorDetailDto> DetailAsync(int doctorId, CancellationToken ct)
    {
        var d = await db.DoctorProfiles.AsNoTracking().Include(x => x.User).Include(x => x.Specialty).Include(x => x.Branch)
                    .FirstOrDefaultAsync(x => x.UserId == doctorId && x.IsListed && x.User.IsActive, ct)
                ?? throw ApiException.NotFound("doctor_not_found", "الطبيب غير موجود.");

        var next = await NextSlotsAsync([d], ct);
        var favorites = await FavoriteIdsAsync(ct);
        var hours = await db.DoctorWorkingHours.AsNoTracking().Where(h => h.DoctorId == doctorId).OrderBy(h => h.DayOfWeek).ToListAsync(ct);
        var reviews = await db.DoctorReviews.AsNoTracking().Where(r => r.DoctorId == doctorId).Include(r => r.Patient).OrderByDescending(r => r.CreatedAt).Take(10).ToListAsync(ct);

        return new DoctorDetailDto(
            Summary(d, next.GetValueOrDefault(d.UserId), favorites.Contains(d.UserId)),
            d.About, d.Languages.Split(['،', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), d.PatientsCount,
            d.Branch.Name, d.BranchId, d.SlotMinutes, d.RequiresConfirmation,
            hours.Select(h => new WorkingHourDto(h.DayOfWeek, h.IsActive, h.StartMinutes, h.EndMinutes)).ToList(),
            reviews.Select(r => new ReviewDto(r.Id, ShortName(r.Patient.FullName), r.Rating, r.Comment, r.CreatedAt)).ToList());
    }

    public async Task<AvailabilityDto> AvailabilityAsync(int doctorId, DateOnly? from, int? days, VisitType type, CancellationToken ct)
    {
        var d = await db.DoctorProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == doctorId && x.IsListed && x.User.IsActive, ct)
                ?? throw ApiException.NotFound("doctor_not_found", "الطبيب غير موجود.");
        if (type == VisitType.Video && d.VideoPrice is null) throw ApiException.BadRequest("video_unavailable", "هذا الطبيب لا يقدّم استشارات عن بُعد.");

        var o = booking.Value;
        var start = from is { } f && f >= clock.Today ? f : clock.Today;
        var span = Math.Clamp(days ?? o.DefaultAvailabilityDays, 1, o.MaxAdvanceDays);
        var plan = (await engine.PlanAsync([(doctorId, d.SlotMinutes)], start, span, ct))[doctorId];

        return new AvailabilityDto(doctorId, type, d.SlotMinutes,
            plan.Where(x => x.Available.Count > 0).Select(x => new AvailabilityDayDto(x.Date, x.Date.DayOfWeek, x.Available)).ToList());
    }

    // ---------- المفضلة ----------

    public async Task<IReadOnlyList<DoctorSummaryDto>> FavoritesAsync(CancellationToken ct)
    {
        var ids = (await FavoriteIdsAsync(ct)).ToList();
        var rows = await db.DoctorProfiles.AsNoTracking().Where(d => ids.Contains(d.UserId) && d.IsListed && d.User.IsActive).Include(d => d.User).Include(d => d.Specialty).ToListAsync(ct);
        var next = await NextSlotsAsync(rows, ct);
        return rows.OrderByDescending(d => d.Rating).Select(d => Summary(d, next.GetValueOrDefault(d.UserId), true)).ToList();
    }

    public async Task SetFavoriteAsync(int doctorId, bool favorite, CancellationToken ct)
    {
        if (!await db.DoctorProfiles.AnyAsync(d => d.UserId == doctorId, ct)) throw ApiException.NotFound("doctor_not_found", "الطبيب غير موجود.");
        var existing = await db.FavoriteDoctors.FindAsync([me.Id, doctorId], ct);
        if (favorite && existing is null) db.FavoriteDoctors.Add(new FavoriteDoctor { PatientId = me.Id, DoctorId = doctorId, CreatedAt = time.GetUtcNow().UtcDateTime });
        else if (!favorite && existing is not null) db.FavoriteDoctors.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    // ---------- أوقات دوام الطبيب (الطبيب نفسه) ----------

    public async Task<IReadOnlyList<WorkingHourDto>> MyWorkingHoursAsync(CancellationToken ct) =>
        (await db.DoctorWorkingHours.AsNoTracking().Where(h => h.DoctorId == me.Id).OrderBy(h => h.DayOfWeek).ToListAsync(ct))
        .Select(h => new WorkingHourDto(h.DayOfWeek, h.IsActive, h.StartMinutes, h.EndMinutes)).ToList();

    public async Task<WorkingHoursResultDto> UpdateMyWorkingHoursAsync(UpdateWorkingHoursRequest req, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (req.Days.Select(d => d.DayOfWeek).Distinct().Count() != req.Days.Count) errors["Days"] = ["لا يمكن تكرار اليوم نفسه."];
        for (var i = 0; i < req.Days.Count; i++)
        {
            var d = req.Days[i];
            if (d.IsActive && d.EndMinutes - d.StartMinutes < 60) errors[$"Days[{i}]"] = ["يجب أن تكون مدة الدوام ساعة على الأقل ونهايته بعد بدايته."];
        }
        if (errors.Count > 0) throw new ValidationApiException(errors);

        var profile = await db.DoctorProfiles.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == me.Id, ct) ?? throw ApiException.NotFound("doctor_not_found", "لا يوجد ملف طبيب لهذا الحساب.");
        var current = await db.DoctorWorkingHours.Where(h => h.DoctorId == me.Id).ToListAsync(ct);
        foreach (var d in req.Days)
        {
            var row = current.FirstOrDefault(h => h.DayOfWeek == d.DayOfWeek);
            if (row is null) db.DoctorWorkingHours.Add(new DoctorWorkingHour { DoctorId = me.Id, DayOfWeek = d.DayOfWeek, IsActive = d.IsActive, StartMinutes = d.StartMinutes, EndMinutes = d.EndMinutes });
            else { row.IsActive = d.IsActive; row.StartMinutes = d.StartMinutes; row.EndMinutes = d.EndMinutes; }
        }
        await db.SaveChangesAsync(ct);

        // المواعيد المستقبلية خارج الدوام الجديد تبقى كما هي لكن نُنبّه الطبيب بعددها
        var hours = await db.DoctorWorkingHours.AsNoTracking().Where(h => h.DoctorId == me.Id).ToListAsync(ct);
        var future = await db.Appointments.AsNoTracking().Where(a => a.DoctorId == me.Id && a.ActiveSlotKey != null && a.Date >= clock.Today).Select(a => new { a.Date, a.StartMinutes, a.DurationMinutes }).ToListAsync(ct);
        var outside = future.Count(a => hours.FirstOrDefault(h => h.DayOfWeek == a.Date.DayOfWeek) is not { IsActive: true } h || a.StartMinutes < h.StartMinutes || a.StartMinutes + a.DurationMinutes > h.EndMinutes);
        _ = profile;
        return new WorkingHoursResultDto(hours.OrderBy(h => h.DayOfWeek).Select(h => new WorkingHourDto(h.DayOfWeek, h.IsActive, h.StartMinutes, h.EndMinutes)).ToList(), outside);
    }

    // ---------- مساعدات ----------

    private async Task<HashSet<int>> FavoriteIdsAsync(CancellationToken ct) =>
        me.IsAuthenticated && me.Role == UserRole.Patient
            ? (await db.FavoriteDoctors.AsNoTracking().Where(f => f.PatientId == me.Id).Select(f => f.DoctorId).ToListAsync(ct)).ToHashSet()
            : [];

    private async Task<Dictionary<int, NextSlotDto>> NextSlotsAsync(IReadOnlyCollection<DoctorProfile> doctors, CancellationToken ct)
    {
        if (doctors.Count == 0) return [];
        var plans = await engine.PlanAsync(doctors.Select(d => (d.UserId, d.SlotMinutes)).ToList(), clock.Today, NextSlotLookaheadDays, ct);
        var result = new Dictionary<int, NextSlotDto>();
        foreach (var (id, days) in plans)
            if (days.FirstOrDefault(d => d.Available.Count > 0) is { } day) result[id] = new NextSlotDto(day.Date, day.Available[0]);
        return result;
    }

    private static DoctorSummaryDto Summary(DoctorProfile d, NextSlotDto? next, bool fav) =>
        new(d.UserId, d.User.FullName, d.Specialty.Key, d.Specialty.NameAr, d.User.AvatarKey, d.Rating, d.ReviewsCount, d.YearsExperience, d.ConsultationPrice, d.VideoPrice, next, fav);

    /// <summary>«محمد علي الأحمد» → «محمد ع.» (خصوصية المراجعين).</summary>
    public static string ShortName(string full)
    {
        var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length < 2 ? full : $"{parts[0]} {parts[1][0]}.";
    }
}
