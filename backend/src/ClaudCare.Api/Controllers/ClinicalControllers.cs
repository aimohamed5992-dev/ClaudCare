using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Clinical;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Controllers;

/// <summary>ملفات المرضى للطاقم السريري. مريض خارج نطاقك ⇒ 404. (المريض يقرأ ملفه من /api/me/*.)</summary>
[ApiController, Authorize, Route("api/patients"), Produces("application/json")]
public class PatientsController(PatientRecordService records) : ControllerBase
{
    /// <summary>بحث مرضاك: q (اسم/جوال/هوية/رقم ملف/مرض) · filter=chronic|kids|seniors|allergy.</summary>
    [HttpGet]
    public async Task<PagedResult<PatientSummaryDto>> Search([FromQuery] string? q, [FromQuery] string? filter, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        await records.SearchAsync(q, filter, page, pageSize, ct);

    [HttpGet("{id:int}/record")]
    public async Task<PatientRecordDto> Record(int id, CancellationToken ct) => await records.RecordAsync(id, ct);

    [HttpGet("{id:int}/health-summary")]
    public async Task<HealthSummaryDto> Health(int id, CancellationToken ct) => await records.HealthSummaryAsync(id, ct);

    /// <summary>تحديث فصيلة الدم والقياسات والحساسية والأمراض المزمنة (قوائم الحساسية والأمراض تُستبدل بالكامل).</summary>
    [HttpPut("{id:int}/clinical-profile")]
    public async Task<HealthSummaryDto> UpdateProfile(int id, UpdateClinicalProfileRequest req, CancellationToken ct) => await records.UpdateClinicalProfileAsync(id, req, ct);

    [HttpGet("{id:int}/visits")]
    public async Task<PagedResult<VisitDto>> Visits(int id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) => await records.VisitsAsync(id, page, pageSize, ct);

    [HttpGet("{id:int}/prescriptions")]
    public async Task<PagedResult<PrescriptionDto>> Prescriptions(int id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) => await records.PrescriptionsAsync(id, page, pageSize, ct);

    [HttpGet("{id:int}/lab-orders")]
    public async Task<PagedResult<LabOrderDto>> Labs(int id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) => await records.LabOrdersAsync(id, page, pageSize, ct);

    [HttpGet("{id:int}/lab-orders/{orderId:int}")]
    public async Task<LabOrderDto> Lab(int id, int orderId, CancellationToken ct) => await records.LabOrderAsync(id, orderId, ct);

    [HttpGet("{id:int}/imaging-orders")]
    public async Task<PagedResult<ImagingOrderDto>> Imaging(int id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) => await records.ImagingOrdersAsync(id, page, pageSize, ct);

    [HttpGet("{id:int}/imaging-orders/{orderId:int}")]
    public async Task<ImagingOrderDto> ImagingOne(int id, int orderId, CancellationToken ct) => await records.ImagingOrderAsync(id, orderId, ct);
}

/// <summary>سجل المريض الصحي وأدويته (للمريض نفسه).</summary>
[ApiController, Authorize(Policy = Policies.Patient), Route("api/me"), Produces("application/json")]
public class MyHealthController(PatientRecordService records, MedicationService meds, ICurrentUser me) : ControllerBase
{
    [HttpGet("health-summary")]
    public async Task<HealthSummaryDto> Summary(CancellationToken ct) => await records.HealthSummaryAsync(me.Id, ct);

    [HttpGet("visits")]
    public async Task<PagedResult<VisitDto>> Visits([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) => await records.VisitsAsync(me.Id, page, pageSize, ct);

    [HttpGet("prescriptions")]
    public async Task<PagedResult<PrescriptionDto>> Prescriptions([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) => await records.PrescriptionsAsync(me.Id, page, pageSize, ct);

    /// <summary>نتائج التحاليل؛ الطلب غير الجاهز يظهر بحالته دون قيم.</summary>
    [HttpGet("lab-results")]
    public async Task<PagedResult<LabOrderDto>> Labs([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) => await records.LabOrdersAsync(me.Id, page, pageSize, ct);

    [HttpGet("lab-results/{orderId:int}")]
    public async Task<LabOrderDto> Lab(int orderId, CancellationToken ct) => await records.LabOrderAsync(me.Id, orderId, ct);

    [HttpGet("imaging")]
    public async Task<PagedResult<ImagingOrderDto>> Imaging([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) => await records.ImagingOrdersAsync(me.Id, page, pageSize, ct);

    [HttpGet("imaging/{orderId:int}")]
    public async Task<ImagingOrderDto> ImagingOne(int orderId, CancellationToken ct) => await records.ImagingOrderAsync(me.Id, orderId, ct);

    // ---- الأدوية ----

    /// <summary>active=true النشطة · false المنتهية · بدونه الكل.</summary>
    [HttpGet("medications")]
    public async Task<IReadOnlyList<MedicationDto>> Medications([FromQuery] bool? active, CancellationToken ct) => await meds.ListAsync(active, ct);

    /// <summary>جدول جرعات اليوم ونسبة الالتزام.</summary>
    [HttpGet("medications/today")]
    public async Task<TodayDosesDto> Today(CancellationToken ct) => await meds.TodayAsync(ct);

    /// <summary>تأكيد (taken=true) أو إلغاء تأكيد جرعة. اليوم أو أمس فقط.</summary>
    [HttpPost("medications/{id:int}/doses")]
    public async Task<TodayDosesDto> LogDose(int id, LogDoseRequest req, CancellationToken ct) => await meds.LogDoseAsync(id, req.SlotIndex!.Value, req.Taken, req.Date, ct);

    [HttpPatch("medications/{id:int}")]
    public async Task<MedicationDto> Update(int id, UpdateMedicationRequest req, CancellationToken ct) => await meds.SetReminderAsync(id, req.ReminderEnabled!.Value, ct);

    [HttpPost("medications/{id:int}/refill")]
    public async Task<MedicationDto> Refill(int id, CancellationToken ct) => await meds.RequestRefillAsync(id, ct);
}

/// <summary>الكشف الطبي وفحص الوصفة (الطبيب).</summary>
[ApiController, Authorize(Policy = Policies.Doctor), Produces("application/json")]
public class ConsultationController(ConsultationService consultation, MedicationService meds) : ControllerBase
{
    /// <summary>
    /// يحفظ الكشف كاملًا: العلامات والتشخيص والفحوصات المطلوبة والوصفة. مع appointmentId يجب أن يكون الموعد «قيد الكشف» ويُكمَل تلقائيًا.
    /// يُرجع 409 allergy_conflict إن تعارض دواء مع حساسية المريض دون allergyOverride.
    /// </summary>
    [HttpPost("api/visits")]
    [ProducesResponseType<VisitResultDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateVisitRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await consultation.CreateAsync(req, ct));

    /// <summary>فحص مسبق لأدوية مقترحة: تعارض الحساسية وتكرار الأدوية الحالية.</summary>
    [HttpPost("api/prescriptions/check")]
    public async Task<IReadOnlyList<DrugCheckItemDto>> Check(DrugCheckRequest req, CancellationToken ct) => await consultation.CheckAsync(req, ct);

    [HttpGet("api/refill-requests")]
    public async Task<IReadOnlyList<RefillRequestDto>> Refills(CancellationToken ct) => await meds.RefillQueueAsync(ct);

    [HttpPost("api/refill-requests/{medicationId:int}/approve")]
    public async Task<IActionResult> Approve(int medicationId, ApproveRefillRequest req, CancellationToken ct)
    {
        await meds.ApproveRefillAsync(medicationId, req.Days, ct);
        return NoContent();
    }

    [HttpPost("api/refill-requests/{medicationId:int}/reject")]
    public async Task<IActionResult> Reject(int medicationId, RejectRefillRequest req, CancellationToken ct)
    {
        await meds.RejectRefillAsync(medicationId, req.Reason, ct);
        return NoContent();
    }
}

/// <summary>دليل الأدوية والفحوصات.</summary>
[ApiController, Authorize, Produces("application/json")]
public class CatalogController(ClaudCareDbContext db) : ControllerBase
{
    [HttpGet("api/catalog/lab-tests")]
    public IReadOnlyList<LabTestDto> LabTests() =>
        ClinicalCatalog.LabTests.Select(t => new LabTestDto(t.Key, t.Name, t.Category, t.Items.Select(i => new LabTestItemDto(i.Name, i.Unit, i.Min, i.Max)).ToList())).ToList();

    [HttpGet("api/catalog/imaging-studies")]
    public IReadOnlyList<ImagingStudyDto> ImagingStudies() => ClinicalCatalog.ImagingStudies.Select(i => new ImagingStudyDto(i.Key, i.Name, i.Modality)).ToList();

    /// <summary>بحث في دليل الأدوية (الاسم أو العائلة الدوائية).</summary>
    [HttpGet("api/drugs")]
    public async Task<PagedResult<DrugDto>> Drugs([FromQuery] string? q, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Clamp(page, pageSize, 50);
        var query = db.Drugs.AsNoTracking().Where(d => d.IsActive);
        if (!string.IsNullOrWhiteSpace(q)) { var s = q.Trim(); query = query.Where(d => d.Name.Contains(s) || d.Group.Contains(s)); }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(d => d.Name).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<DrugDto>(rows.Select(d => new DrugDto(d.Id, d.Name, d.Group, d.Form, d.UnitPrice)).ToList(), p, size, total);
    }
}

/// <summary>طلبات التحاليل والأشعة بنطاق الدور.</summary>
[ApiController, Authorize, Produces("application/json")]
public class OrdersController(OrdersService orders) : ControllerBase
{
    [HttpGet("api/lab-orders")]
    public async Task<PagedResult<LabOrderDto>> Labs([FromQuery] LabOrderStatus? status, [FromQuery] int? patientId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        await orders.LabAsync(status, patientId, page, pageSize, ct);

    [HttpGet("api/lab-orders/{id:int}")]
    public async Task<LabOrderDto> Lab(int id, CancellationToken ct) => await orders.LabAsync(id, ct);

    [HttpGet("api/imaging-orders")]
    public async Task<PagedResult<ImagingOrderDto>> Imaging([FromQuery] ImagingOrderStatus? status, [FromQuery] int? patientId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        await orders.ImagingAsync(status, patientId, page, pageSize, ct);

    [HttpGet("api/imaging-orders/{id:int}")]
    public async Task<ImagingOrderDto> ImagingOne(int id, CancellationToken ct) => await orders.ImagingAsync(id, ct);
}
