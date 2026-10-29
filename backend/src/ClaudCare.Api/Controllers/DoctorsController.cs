using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaudCare.Api.Controllers;

/// <summary>دليل الأطباء: التخصصات، البحث، الملف، والخانات المتاحة. (للمصادَقين: مريض أو طاقم.)</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public class DoctorsController(DoctorCatalogService catalog, AppointmentService appointments) : ControllerBase
{
    [HttpGet("api/specialties")]
    public async Task<IReadOnlyList<SpecialtyDto>> Specialties(CancellationToken ct) => await catalog.SpecialtiesAsync(ct);

    /// <summary>بحث وتصفية الأطباء: q (اسم/تخصص/نبذة) · specialty (المفتاح) · sort=rating|price|experience · favoritesOnly.</summary>
    [HttpGet("api/doctors")]
    public async Task<PagedResult<DoctorSummaryDto>> Search([FromQuery] string? q, [FromQuery] string? specialty, [FromQuery] DoctorSort sort = DoctorSort.Rating,
        [FromQuery] bool favoritesOnly = false, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await catalog.SearchAsync(q, specialty, sort, favoritesOnly, page, pageSize, ct);

    [HttpGet("api/doctors/{id:int}")]
    public async Task<DoctorDetailDto> Detail(int id, CancellationToken ct) => await catalog.DetailAsync(id, ct);

    /// <summary>الخانات المتاحة لكل يوم (الأيام الخالية لا تُعاد). type=Clinic|Video.</summary>
    [HttpGet("api/doctors/{id:int}/availability")]
    public async Task<AvailabilityDto> Availability(int id, [FromQuery] DateOnly? from, [FromQuery] int? days, [FromQuery] VisitType type = VisitType.Clinic, CancellationToken ct = default) =>
        await catalog.AvailabilityAsync(id, from, days, type, ct);

    // ---- الطبيب نفسه ----

    [HttpGet("api/doctors/me/working-hours"), Authorize(Policy = Policies.Doctor)]
    public async Task<IReadOnlyList<WorkingHourDto>> MyHours(CancellationToken ct) => await catalog.MyWorkingHoursAsync(ct);

    /// <summary>يحدّث أيام وساعات الدوام؛ تُعاد في النتيجة أعداد المواعيد المستقبلية الواقعة خارج الدوام الجديد.</summary>
    [HttpPut("api/doctors/me/working-hours"), Authorize(Policy = Policies.Doctor)]
    public async Task<WorkingHoursResultDto> UpdateMyHours(UpdateWorkingHoursRequest req, CancellationToken ct) => await catalog.UpdateMyWorkingHoursAsync(req, ct);

    /// <summary>جدول اليوم للطبيب مع عدّادات الحالات (للرئيسية).</summary>
    [HttpGet("api/doctors/me/agenda"), Authorize(Policy = Policies.Doctor)]
    public async Task<AppointmentAgendaDto> Agenda([FromQuery] DateOnly? date, CancellationToken ct) => await appointments.MyAgendaAsync(date, ct);

    // ---- المفضلة (المريض) ----

    [HttpGet("api/me/favorites/doctors"), Authorize(Policy = Policies.Patient)]
    public async Task<IReadOnlyList<DoctorSummaryDto>> Favorites(CancellationToken ct) => await catalog.FavoritesAsync(ct);

    [HttpPut("api/me/favorites/doctors/{id:int}"), Authorize(Policy = Policies.Patient)]
    public async Task<IActionResult> AddFavorite(int id, CancellationToken ct)
    {
        await catalog.SetFavoriteAsync(id, true, ct);
        return NoContent();
    }

    [HttpDelete("api/me/favorites/doctors/{id:int}"), Authorize(Policy = Policies.Patient)]
    public async Task<IActionResult> RemoveFavorite(int id, CancellationToken ct)
    {
        await catalog.SetFavoriteAsync(id, false, ct);
        return NoContent();
    }
}
