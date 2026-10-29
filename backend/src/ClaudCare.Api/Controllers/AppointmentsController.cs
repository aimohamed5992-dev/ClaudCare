using ClaudCare.Api.Contracts;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaudCare.Api.Controllers;

/// <summary>
/// المواعيد. النطاق حسب الدور: المريض مواعيده، الطبيب مواعيده، الاستقبال/التمريض/مدير الفرع مواعيد فرعهم،
/// مدير النظام/المستشفى الكل. موعد خارج نطاقك يُرجع 404.
/// </summary>
[ApiController]
[Authorize]
[Route("api/appointments")]
[Produces("application/json")]
public class AppointmentsController(AppointmentService appointments) : ControllerBase
{
    /// <summary>حجز موعد (المريض لنفسه، أو الاستقبال/الإدارة نيابةً عنه بـ patientId).</summary>
    [HttpPost]
    [ProducesResponseType<AppointmentDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Book(BookAppointmentRequest req, CancellationToken ct)
    {
        var dto = await appointments.BookAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>القائمة: scope=upcoming|past|cancelled|all · date · from/to · status · doctorId · patientId.</summary>
    [HttpGet]
    public async Task<PagedResult<AppointmentDto>> List([FromQuery] AppointmentScope scope = AppointmentScope.All, [FromQuery] DateOnly? date = null, [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null, [FromQuery] AppointmentStatus? status = null, [FromQuery] int? doctorId = null, [FromQuery] int? patientId = null,
        [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await appointments.ListAsync(new AppointmentQuery(scope, date, from, to, status, doctorId, patientId, page, pageSize), ct);

    [HttpGet("{id:int}")]
    public async Task<AppointmentDto> Get(int id, CancellationToken ct) => await appointments.GetAsync(id, ct);

    /// <summary>إلغاء بسبب إلزامي. يُعاد LateCancellation=true مع الرسوم إن كان قبل أقل من 24 ساعة (للمريض).</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<CancelResultDto> Cancel(int id, CancelAppointmentRequest req, CancellationToken ct) => await appointments.CancelAsync(id, req.Reason, ct);

    [HttpPost("{id:int}/reschedule")]
    public async Task<AppointmentDto> Reschedule(int id, RescheduleAppointmentRequest req, CancellationToken ct) => await appointments.RescheduleAsync(id, req.Date!.Value, req.StartMinutes!.Value, ct);

    /// <summary>تأكيد (الطبيب/الاستقبال) من «بانتظار التأكيد».</summary>
    [HttpPost("{id:int}/confirm")]
    public async Task<AppointmentDto> Confirm(int id, CancellationToken ct) => await appointments.TransitionAsync(id, AppointmentAction.Confirm, ct);

    /// <summary>تسجيل وصول المريض (الاستقبال/التمريض) يوم الموعد.</summary>
    [HttpPost("{id:int}/arrive")]
    public async Task<AppointmentDto> Arrive(int id, CancellationToken ct) => await appointments.TransitionAsync(id, AppointmentAction.Arrive, ct);

    /// <summary>بدء الكشف (الطبيب): بعد الوصول، أو من «مؤكد» للاستشارة عن بُعد.</summary>
    [HttpPost("{id:int}/start")]
    public async Task<AppointmentDto> Start(int id, CancellationToken ct) => await appointments.TransitionAsync(id, AppointmentAction.Start, ct);

    [HttpPost("{id:int}/complete")]
    public async Task<AppointmentDto> Complete(int id, CancellationToken ct) => await appointments.TransitionAsync(id, AppointmentAction.Complete, ct);

    [HttpPost("{id:int}/no-show")]
    public async Task<AppointmentDto> NoShow(int id, CancellationToken ct) => await appointments.TransitionAsync(id, AppointmentAction.NoShow, ct);

    /// <summary>تقييم الزيارة المكتملة (المريض، مرة واحدة).</summary>
    [HttpPost("{id:int}/review")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Review(int id, ReviewRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await appointments.ReviewAsync(id, req.Rating!.Value, req.Comment, ct));
}
