using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

[Index(nameof(BranchId), nameof(DrugId), IsUnique = true)]
public class InventoryItem : AuditedEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int DrugId { get; set; }
    public Drug Drug { get; set; } = null!;
    public int Quantity { get; set; }
    public int MinQuantity { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    [MaxLength(120)] public string? Supplier { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
}

[Index(nameof(No), IsUnique = true)]
public class PurchaseOrder : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    [MaxLength(120)] public string Supplier { get; set; } = string.Empty;
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateOnly OrderDate { get; set; }
    public decimal Total { get; set; }
    public int? CreatedByUserId { get; set; }
    public List<PurchaseOrderLine> Lines { get; set; } = [];
}

public class PurchaseOrderLine : Entity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int DrugId { get; set; }
    public Drug Drug { get; set; } = null!;
    public int Quantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
}

[Index(nameof(No), IsUnique = true)]
public class PharmacySale : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int? PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User? Patient { get; set; }
    [MaxLength(120)] public string? CustomerName { get; set; }
    public int? PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Total { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Paid;
    public int? SoldByUserId { get; set; }
    public List<PharmacySaleLine> Lines { get; set; } = [];
}

public class PharmacySaleLine : Entity
{
    public int PharmacySaleId { get; set; }
    public PharmacySale PharmacySale { get; set; } = null!;
    public int? DrugId { get; set; }
    public Drug? Drug { get; set; }
    [MaxLength(150)] public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
