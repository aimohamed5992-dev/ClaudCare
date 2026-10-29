using System.ComponentModel.DataAnnotations;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Contracts;

public sealed record RxItemQueueDto(int Id, string DrugName, string? Dose, string Frequency, int Quantity, bool Dispensed, int? DrugId, int? InStock, bool Allergy);
public sealed record RxQueueDto(int Id, string No, PrescriptionStatus Status, OrderPriority Priority, DateTime IssuedAt, PersonRefDto Patient, string DoctorName, decimal Total, IReadOnlyList<RxItemQueueDto> Items, string? RejectionReason);
public sealed record RejectRxRequest([Required, StringLength(300, MinimumLength = 2)] string Reason);
public sealed record DispenseRequest([Required] PaymentMethod? Method);

public sealed record InventoryDto(int Id, int DrugId, string DrugName, string? Form, int Quantity, int MinQuantity, bool Low, DateOnly? ExpiryDate, bool ExpiringSoon, string? Supplier, decimal UnitCost, decimal UnitPrice);
public sealed record AdjustStockRequest([Required] int? Delta, [Required, StringLength(200, MinimumLength = 2)] string Reason);
public sealed record UpdateInventoryRequest([Range(0, 100000)] int? MinQuantity, [Range(0, 1000000)] decimal? UnitPrice, [Range(0, 1000000)] decimal? UnitCost, DateOnly? ExpiryDate, [StringLength(120)] string? Supplier);
public sealed record AddInventoryRequest([Required] int? DrugId, [Range(0, 100000)] int Quantity, [Range(0, 100000)] int MinQuantity, [Range(0, 1000000)] decimal UnitCost, [Range(0, 1000000)] decimal UnitPrice, DateOnly? ExpiryDate, [StringLength(120)] string? Supplier);

public sealed record SaleLineInput([Required] int? DrugId, [Range(1, 1000)] int Quantity);
public sealed record CreateSaleRequest([Required, MinLength(1), MaxLength(40)] List<SaleLineInput> Lines, [Required] PaymentMethod? Method, int? PatientId, [StringLength(120)] string? CustomerName);
public sealed record SaleLineDto(string ItemName, int Quantity, decimal UnitPrice, decimal Total);
public sealed record SaleDto(int Id, string No, DateTime At, PaymentMethod Method, decimal Total, SaleStatus Status, string? CustomerName, int? PrescriptionId, IReadOnlyList<SaleLineDto> Lines);

public sealed record PoLineInput([Required] int? DrugId, [Range(1, 100000)] int Quantity, [Range(0, 1000000)] decimal UnitCost);
public sealed record CreatePurchaseOrderRequest([Required, StringLength(120, MinimumLength = 2)] string Supplier, [Required, MinLength(1), MaxLength(60)] List<PoLineInput> Lines);
public sealed record ReceiveLineInput([Required] int? LineId, [Range(1, 100000)] int Quantity);
public sealed record ReceivePurchaseOrderRequest([Required, MinLength(1)] List<ReceiveLineInput> Lines);
public sealed record PoLineDto(int Id, int DrugId, string DrugName, int Quantity, int Received, decimal UnitCost);
public sealed record PurchaseOrderDto(int Id, string No, string Supplier, PurchaseOrderStatus Status, DateOnly OrderDate, decimal Total, IReadOnlyList<PoLineDto> Lines);
