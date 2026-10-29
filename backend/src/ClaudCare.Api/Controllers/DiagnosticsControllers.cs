using ClaudCare.Api.Contracts;
using ClaudCare.Api.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaudCare.Api.Controllers;

/// <summary>إجراءات المختبر على الطلبات (القراءة في <c>GET /api/lab-orders</c>).</summary>
[ApiController]
[Authorize]
[Route("api/lab-orders")]
[Produces("application/json")]
public class LabActionsController(LabService lab) : ControllerBase
{
    /// <summary>سحب العينة (المختبر/التمريض): Ordered ⇒ SampleCollected.</summary>
    [HttpPost("{id:int}/collect")]
    public async Task<LabOrderDto> Collect(int id, CancellationToken ct) => await lab.CollectAsync(id, ct);

    [HttpPost("{id:int}/start")]
    public async Task<LabOrderDto> Start(int id, CancellationToken ct) => await lab.StartAsync(id, ct);

    /// <summary>إدخال النتائج ⇒ ResultReady. الأعلام من الخادم؛ والحرجة تُنبّه الطبيب.</summary>
    [HttpPut("{id:int}/results")]
    public async Task<LabOrderDto> Results(int id, EnterLabResultsRequest req, CancellationToken ct) => await lab.EnterResultsAsync(id, req.Items, ct);

    [HttpPost("{id:int}/cancel")]
    public async Task<LabOrderDto> Cancel(int id, CancelOrderRequest req, CancellationToken ct) => await lab.CancelAsync(id, req.Reason, ct);
}

/// <summary>إجراءات الأشعة على الطلبات (القراءة في <c>GET /api/imaging-orders</c>).</summary>
[ApiController]
[Authorize]
[Route("api")]
[Produces("application/json")]
public class RadiologyActionsController(RadiologyService radiology) : ControllerBase
{
    [HttpGet("imaging-devices")]
    public async Task<IReadOnlyList<ImagingDeviceDto>> Devices(CancellationToken ct) => await radiology.DevicesAsync(ct);

    [HttpPost("imaging-orders/{id:int}/schedule")]
    public async Task<ImagingOrderDto> Schedule(int id, ScheduleImagingRequest req, CancellationToken ct) => await radiology.ScheduleAsync(id, req.ScheduledAt!.Value, req.DeviceId, ct);

    [HttpPost("imaging-orders/{id:int}/start")]
    public async Task<ImagingOrderDto> Start(int id, CancellationToken ct) => await radiology.StartAsync(id, ct);

    /// <summary>مسودة أو نهائي (<c>final=true</c> ⇒ Reported ويصبح غير قابل للتعديل).</summary>
    [HttpPut("imaging-orders/{id:int}/report")]
    public async Task<ImagingOrderDto> Report(int id, ImagingReportRequest req, CancellationToken ct) => await radiology.ReportAsync(id, req, ct);

    [HttpPost("imaging-orders/{id:int}/cancel")]
    public async Task<ImagingOrderDto> Cancel(int id, CancelOrderRequest req, CancellationToken ct) => await radiology.CancelAsync(id, req.Reason, ct);
}
