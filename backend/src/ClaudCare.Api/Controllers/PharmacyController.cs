using ClaudCare.Api.Contracts;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaudCare.Api.Controllers;

/// <summary>الصيدلية (للصيدلي ضمن فرعه، والإدارة العليا للإشراف). غير الصيدلي 403.</summary>
[ApiController]
[Authorize]
[Route("api/pharmacy")]
[Produces("application/json")]
public class PharmacyController(PharmacyService pharmacy) : ControllerBase
{
    /// <summary>قائمة الوصفات (الافتراضي: بانتظار/قيد التجهيز) مع توفر المخزون لكل صنف.</summary>
    [HttpGet("prescriptions")]
    public async Task<PagedResult<RxQueueDto>> Queue([FromQuery] PrescriptionStatus? status = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await pharmacy.QueueAsync(status, page, pageSize, ct);

    [HttpPost("prescriptions/{id:int}/prepare")]
    public async Task<RxQueueDto> Prepare(int id, CancellationToken ct) => await pharmacy.PrepareAsync(id, ct);

    [HttpPost("prescriptions/{id:int}/reject")]
    public async Task<RxQueueDto> Reject(int id, RejectRxRequest req, CancellationToken ct) => await pharmacy.RejectAsync(id, req.Reason, ct);

    /// <summary>صرف الوصفة: يخصم المخزون ويسجل عملية بيع. نقص المخزون ⇒ 409 insufficient_stock.</summary>
    [HttpPost("prescriptions/{id:int}/dispense")]
    public async Task<SaleDto> Dispense(int id, DispenseRequest req, CancellationToken ct) => await pharmacy.DispenseAsync(id, req.Method!.Value, ct);

    [HttpGet("inventory")]
    public async Task<PagedResult<InventoryDto>> Inventory([FromQuery] bool lowOnly = false, [FromQuery] string? search = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await pharmacy.InventoryAsync(lowOnly, search, page, pageSize, ct);

    [HttpPost("inventory")]
    [ProducesResponseType<InventoryDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddInventory(AddInventoryRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await pharmacy.AddInventoryAsync(req, ct));

    [HttpPut("inventory/{id:int}")]
    public async Task<InventoryDto> UpdateInventory(int id, UpdateInventoryRequest req, CancellationToken ct) => await pharmacy.UpdateInventoryAsync(id, req, ct);

    /// <summary>تسوية بالزيادة/النقص بسبب إلزامي (جرد/تالف).</summary>
    [HttpPost("inventory/{id:int}/adjust")]
    public async Task<InventoryDto> Adjust(int id, AdjustStockRequest req, CancellationToken ct) => await pharmacy.AdjustAsync(id, req.Delta!.Value, req.Reason, ct);

    [HttpGet("sales")]
    public async Task<PagedResult<SaleDto>> Sales([FromQuery] DateOnly? date = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await pharmacy.SalesAsync(date, page, pageSize, ct);

    [HttpGet("sales/{id:int}")]
    public async Task<SaleDto> Sale(int id, CancellationToken ct) => await pharmacy.GetSaleAsync(id, ct);

    /// <summary>بيع مباشر بدون وصفة (يخصم المخزون بسعر الصنف في الفرع).</summary>
    [HttpPost("sales")]
    [ProducesResponseType<SaleDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSale(CreateSaleRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await pharmacy.CreateSaleAsync(req, ct));

    [HttpGet("purchase-orders")]
    public async Task<PagedResult<PurchaseOrderDto>> Pos([FromQuery] PurchaseOrderStatus? status = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await pharmacy.PosAsync(status, page, pageSize, ct);

    [HttpGet("purchase-orders/{id:int}")]
    public async Task<PurchaseOrderDto> Po(int id, CancellationToken ct) => await pharmacy.GetPoAsync(id, ct);

    [HttpPost("purchase-orders")]
    [ProducesResponseType<PurchaseOrderDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePo(CreatePurchaseOrderRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await pharmacy.CreatePoAsync(req, ct));

    [HttpPost("purchase-orders/{id:int}/send")]
    public async Task<PurchaseOrderDto> SendPo(int id, CancellationToken ct) => await pharmacy.SendPoAsync(id, ct);

    /// <summary>استلام (كلي أو جزئي) يزيد المخزون.</summary>
    [HttpPost("purchase-orders/{id:int}/receive")]
    public async Task<PurchaseOrderDto> Receive(int id, ReceivePurchaseOrderRequest req, CancellationToken ct) => await pharmacy.ReceiveAsync(id, req.Lines, ct);
}
