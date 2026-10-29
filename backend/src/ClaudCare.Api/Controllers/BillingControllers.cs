using ClaudCare.Api.Contracts;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaudCare.Api.Controllers;

/// <summary>الفواتير والدفع. المريض فواتيره، والاستقبال/مدير الفرع فرعهم، والمالية/المستشفى/الإدارة الكل. خارج النطاق ⇒ 404.</summary>
[ApiController]
[Authorize]
[Route("api/invoices")]
[Produces("application/json")]
public class InvoicesController(BillingService billing) : ControllerBase
{
    /// <summary>القائمة مع إجمالي المستحق وعدد غير المدفوع (status · patientId · appointmentId).</summary>
    [HttpGet]
    public async Task<InvoiceListDto> List([FromQuery] InvoiceStatus? status = null, [FromQuery] int? patientId = null, [FromQuery] int? appointmentId = null,
        [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await billing.ListAsync(status, patientId, appointmentId, page, pageSize, ct);

    [HttpGet("{id:int}")]
    public async Task<InvoiceDto> Get(int id, CancellationToken ct) => await billing.GetAsync(id, ct);

    /// <summary>سداد الفاتورة. المريض: Card/ApplePay لكامل المتبقي. الموظف: أي طريقة ومبلغ جزئي اختياري.</summary>
    [HttpPost("{id:int}/pay")]
    public async Task<InvoiceDto> Pay(int id, PayInvoiceRequest req, CancellationToken ct) => await billing.PayAsync(id, req, ct);
}

[ApiController]
[Authorize]
[Route("api/me/insurance")]
[Produces("application/json")]
public class MyInsuranceController(BillingService billing) : ControllerBase
{
    /// <summary>وثيقة تأمين المريض: الشركة، الفئة، التحمّل، الحد السنوي والمستهلك.</summary>
    [HttpGet]
    public async Task<PatientInsuranceDto> Get(CancellationToken ct) => await billing.MyInsuranceAsync(ct);
}
