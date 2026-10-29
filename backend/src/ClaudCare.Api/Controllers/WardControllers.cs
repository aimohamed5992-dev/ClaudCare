using ClaudCare.Api.Contracts;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaudCare.Api.Controllers;

/// <summary>التمريض: العلامات الحيوية، الملاحظات، سجل إعطاء الأدوية. الصلاحية بحسب الدور ونطاق الفرع.</summary>
[ApiController]
[Authorize]
[Route("api")]
[Produces("application/json")]
public class NursingController(NursingService nursing) : ControllerBase
{
    [HttpPost("vitals")]
    [ProducesResponseType<NurseVitalsDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Record(RecordVitalsRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await nursing.RecordVitalsAsync(req, ct));

    [HttpGet("patients/{id:int}/vitals")]
    public async Task<IReadOnlyList<NurseVitalsDto>> Vitals(int id, [FromQuery] int? take = null, CancellationToken ct = default) => await nursing.VitalsAsync(id, take, ct);

    [HttpPost("patients/{id:int}/nursing-notes")]
    [ProducesResponseType<NursingNoteDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddNote(int id, AddNursingNoteRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await nursing.AddNoteAsync(id, req.Note, req.AdmissionId, ct));

    [HttpGet("patients/{id:int}/nursing-notes")]
    public async Task<IReadOnlyList<NursingNoteDto>> Notes(int id, CancellationToken ct) => await nursing.NotesAsync(id, ct);

    [HttpPost("admissions/{id:int}/medications")]
    [ProducesResponseType<AdminDoseDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> ScheduleDose(int id, ScheduleDoseRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await nursing.ScheduleDoseAsync(id, req, ct));

    [HttpGet("admissions/{id:int}/medications")]
    public async Task<IReadOnlyList<AdminDoseDto>> Doses(int id, [FromQuery] DateOnly? date = null, CancellationToken ct = default) => await nursing.DosesAsync(id, date, ct);

    [HttpPost("medication-administrations/{id:int}/give")]
    public async Task<AdminDoseDto> Give(int id, DoseActionRequest req, CancellationToken ct) => await nursing.ActAsync(id, DoseStatus.Given, req.Notes, ct);

    [HttpPost("medication-administrations/{id:int}/skip")]
    public async Task<AdminDoseDto> Skip(int id, DoseActionRequest req, CancellationToken ct) => await nursing.ActAsync(id, DoseStatus.Skipped, req.Notes, ct);

    [HttpPost("medication-administrations/{id:int}/delay")]
    public async Task<AdminDoseDto> Delay(int id, DoseActionRequest req, CancellationToken ct) => await nursing.ActAsync(id, DoseStatus.Delayed, req.Notes, ct);
}

/// <summary>الأسرّة والتنويم والنقل والخروج.</summary>
[ApiController]
[Authorize]
[Route("api")]
[Produces("application/json")]
public class AdmissionsController(AdmissionsService admissions) : ControllerBase
{
    [HttpGet("beds")]
    public async Task<BedBoardDto> Beds([FromQuery] string? ward = null, [FromQuery] BedStatus? status = null, CancellationToken ct = default) => await admissions.BedsAsync(ward, status, ct);

    [HttpPost("beds/{id:int}/status")]
    public async Task<BedDto> SetBed(int id, SetBedStatusRequest req, CancellationToken ct) => await admissions.SetBedStatusAsync(id, req.Status!.Value, req.ReservedFor, ct);

    [HttpPost("admissions")]
    [ProducesResponseType<AdmissionDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> RequestAdmission(RequestAdmissionRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await admissions.RequestAsync(req, ct));

    [HttpGet("admissions")]
    public async Task<PagedResult<AdmissionDto>> List([FromQuery] AdmissionStatus? status = null, [FromQuery] string? ward = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await admissions.ListAsync(status, ward, page, pageSize, ct);

    [HttpGet("admissions/{id:int}")]
    public async Task<AdmissionDto> Get(int id, CancellationToken ct) => await admissions.GetAsync(id, ct);

    /// <summary>موافقة ولي الأمر (للقاصر) والتحقق من موافقة التأمين (للمخطط).</summary>
    [HttpPut("admissions/{id:int}/checks")]
    public async Task<AdmissionDto> Checks(int id, AdmissionChecksRequest req, CancellationToken ct) => await admissions.SetChecksAsync(id, req, ct);

    /// <summary>تنويم في سرير متاح. السرير يُحجز ذريًا؛ مشغول ⇒ 409 bed_unavailable.</summary>
    [HttpPost("admissions/{id:int}/admit")]
    public async Task<AdmissionDto> Admit(int id, AdmitRequest req, CancellationToken ct) => await admissions.AdmitAsync(id, req.BedId!.Value, ct);

    /// <summary>الخروج: الخطوات الأربع كلها + سداد فواتير التنويم، وإلا 409.</summary>
    [HttpPost("admissions/{id:int}/discharge")]
    public async Task<AdmissionDto> Discharge(int id, DischargeRequest req, CancellationToken ct) => await admissions.DischargeAsync(id, req, ct);

    [HttpPost("admissions/{id:int}/transfers")]
    [ProducesResponseType<TransferDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> RequestTransfer(int id, TransferRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await admissions.RequestTransferAsync(id, req, ct));

    [HttpGet("bed-transfers")]
    public async Task<IReadOnlyList<TransferDto>> Transfers([FromQuery] TransferStatus? status = null, CancellationToken ct = default) => await admissions.TransfersAsync(status, ct);

    [HttpPost("bed-transfers/{id:int}/approve")]
    public async Task<TransferDto> Approve(int id, CancellationToken ct) => await admissions.ApproveTransferAsync(id, ct);

    [HttpPost("bed-transfers/{id:int}/complete")]
    public async Task<TransferDto> CompleteTransfer(int id, CompleteTransferRequest req, CancellationToken ct) => await admissions.CompleteTransferAsync(id, req.BedId!.Value, ct);
}

/// <summary>الطوارئ والإسعاف.</summary>
[ApiController]
[Authorize]
[Route("api/emergency")]
[Produces("application/json")]
public class EmergencyController(EmergencyService emergency) : ControllerBase
{
    /// <summary>تسجيل حالة بمستوى الفرز؛ العلامات الحرجة ترفع المستوى إلى 2 (escalated=true).</summary>
    [HttpPost("cases")]
    [ProducesResponseType<EmergencyCaseDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateEmergencyCaseRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await emergency.CreateAsync(req, ct));

    /// <summary>اللوحة مرتبة بالأخطر ثم الأقدم (الافتراضي: النشطة فقط).</summary>
    [HttpGet("cases")]
    public async Task<EmergencyBoardDto> Board([FromQuery] bool activeOnly = true, [FromQuery] EmergencyStatus? status = null, CancellationToken ct = default) => await emergency.BoardAsync(activeOnly, status, ct);

    [HttpPut("cases/{id:int}/triage")]
    public async Task<EmergencyCaseDto> Retriage(int id, RetriageRequest req, CancellationToken ct) => await emergency.RetriageAsync(id, req.TriageLevel!.Value, req.RedFlags, ct);

    [HttpPost("cases/{id:int}/start-exam")]
    public async Task<EmergencyCaseDto> Start(int id, CancellationToken ct) => await emergency.StartExamAsync(id, ct);

    [HttpPost("cases/{id:int}/discharge")]
    public async Task<EmergencyCaseDto> Discharge(int id, CancellationToken ct) => await emergency.DischargeAsync(id, ct);

    [HttpPost("cases/{id:int}/admit")]
    public async Task<EmergencyCaseDto> Admit(int id, EmergencyAdmitRequest req, CancellationToken ct) => await emergency.AdmitAsync(id, req.Ward, req.Reason, ct);

    [HttpGet("ambulances")]
    public async Task<IReadOnlyList<AmbulanceDto>> Ambulances(CancellationToken ct) => await emergency.AmbulancesAsync(ct);

    [HttpPost("ambulances")]
    [ProducesResponseType<AmbulanceDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAmbulance(CreateAmbulanceRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await emergency.CreateAmbulanceAsync(req, ct));

    /// <summary>وصول الإسعاف: ينشئ حالة طوارئ بانتظار الفحص.</summary>
    [HttpPost("ambulances/{id:int}/arrive")]
    public async Task<EmergencyCaseDto> Arrive(int id, CancellationToken ct) => await emergency.ArriveAsync(id, ct);
}

public sealed record RetriageRequest([System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.Range(1, 5)] int? TriageLevel, [System.ComponentModel.DataAnnotations.StringLength(200)] string? RedFlags);

/// <summary>الاستقبال: طابور اليوم، تسجيل المرضى، لوحة اليوم.</summary>
[ApiController]
[Authorize]
[Route("api/reception")]
[Produces("application/json")]
public class ReceptionController(ReceptionService reception) : ControllerBase
{
    [HttpGet("today")]
    public async Task<ReceptionTodayDto> Today(CancellationToken ct) => await reception.TodayAsync(ct);

    [HttpPost("queue")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Issue(IssueTicketRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await reception.IssueAsync(req, ct));

    [HttpGet("queue")]
    public async Task<IReadOnlyList<TicketDto>> Queue([FromQuery] DateOnly? date = null, [FromQuery] QueueStatus? status = null, CancellationToken ct = default) => await reception.QueueAsync(date, status, ct);

    [HttpPost("queue/{id:int}/call")]
    public async Task<TicketDto> Call(int id, CancellationToken ct) => await reception.TransitionAsync(id, QueueStatus.Called, ct);

    [HttpPost("queue/{id:int}/done")]
    public async Task<TicketDto> Done(int id, CancellationToken ct) => await reception.TransitionAsync(id, QueueStatus.Done, ct);

    [HttpPost("queue/{id:int}/cancel")]
    public async Task<TicketDto> Cancel(int id, CancellationToken ct) => await reception.TransitionAsync(id, QueueStatus.Cancelled, ct);

    /// <summary>تسجيل مريض زائر (حساب غير مفعّل الدخول حتى يستعيد كلمة المرور).</summary>
    [HttpPost("patients")]
    [ProducesResponseType<PersonRefDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterPatientRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await reception.RegisterPatientAsync(req, ct));
}

/// <summary>غرف العمليات.</summary>
[ApiController]
[Authorize]
[Route("api/surgery")]
[Produces("application/json")]
public class SurgeryController(SurgeryService surgery) : ControllerBase
{
    [HttpGet("rooms")]
    public async Task<IReadOnlyList<OperatingRoomDto>> Rooms(CancellationToken ct) => await surgery.RoomsAsync(ct);

    [HttpPost("rooms/{id:int}/state")]
    public async Task<OperatingRoomDto> RoomState(int id, SetRoomStateRequest req, CancellationToken ct) => await surgery.SetRoomStateAsync(id, req.State!.Value, req.Note, ct);

    [HttpGet("cases")]
    public async Task<IReadOnlyList<SurgeryDto>> List([FromQuery] DateOnly? date = null, [FromQuery] SurgeryStatus? status = null, CancellationToken ct = default) => await surgery.ListAsync(date, status, ct);

    [HttpGet("cases/{id:int}")]
    public async Task<SurgeryDto> Get(int id, CancellationToken ct) => await surgery.GetAsync(id, ct);

    [HttpPost("cases")]
    [ProducesResponseType<SurgeryDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Schedule(ScheduleSurgeryRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await surgery.ScheduleAsync(req, ct));

    [HttpPut("cases/{id:int}/checklist/{key}")]
    public async Task<SurgeryDto> Checklist(int id, string key, SetChecklistRequest req, CancellationToken ct) => await surgery.SetChecklistAsync(id, key, req.Done!.Value, ct);

    /// <summary>اعتماد ما قبل العملية: القائمة كاملة، وإقرار التنبيه إن وُجد.</summary>
    [HttpPost("cases/{id:int}/preop/approve")]
    public async Task<SurgeryDto> Approve(int id, ApprovePreOpRequest? req, CancellationToken ct) => await surgery.ApprovePreOpAsync(id, req?.AcknowledgeAlert ?? false, ct);

    [HttpPost("cases/{id:int}/start")]
    public async Task<SurgeryDto> Start(int id, CancellationToken ct) => await surgery.StartAsync(id, ct);

    [HttpPost("cases/{id:int}/complete")]
    public async Task<SurgeryDto> Complete(int id, CancellationToken ct) => await surgery.CompleteAsync(id, ct);

    [HttpPost("cases/{id:int}/cancel")]
    public async Task<SurgeryDto> Cancel(int id, CancellationToken ct) => await surgery.CancelAsync(id, ct);
}
