using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

[Index(nameof(No), IsUnique = true)]
[Index(nameof(PatientId), nameof(Status))]
public class Invoice : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int? AppointmentId { get; set; }
    public int? AdmissionId { get; set; }
    [MaxLength(200)] public string Title { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
    public decimal Subtotal { get; set; }
    public decimal InsuranceCovered { get; set; }
    public decimal Discount { get; set; }
    [MaxLength(200)] public string? DiscountReason { get; set; }
    public decimal PaidAmount { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
    public List<InvoiceItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];

    [NotMapped] public decimal Total => Subtotal - Discount;

    /// <summary>
    /// يعيد حساب الحالة من المبالغ: ملغاة تبقى ملغاة؛ لا متبقٍ ⇒ «مغطاة بالتأمين» إن لم يدفع المريض شيئًا وإلا «مدفوعة»؛ دفع جزئي ⇒ «مدفوعة جزئيًا»؛ وإلا «غير مدفوعة».
    /// </summary>
    public void Recalculate()
    {
        if (Status == InvoiceStatus.Cancelled) return;
        Status = Remaining <= 0 ? (PaidAmount <= 0 ? InvoiceStatus.Covered : InvoiceStatus.Paid) : PaidAmount > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Unpaid;
    }
    /// <summary>المتبقي على المريض بعد التأمين والمدفوع.</summary>
    [NotMapped] public decimal Remaining => Math.Max(0, Subtotal - Discount - InsuranceCovered - PaidAmount);
}

public class InvoiceItem : Entity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    [MaxLength(200)] public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public class Payment : AuditedEntity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime PaidAt { get; set; }
    public int? ReceivedByUserId { get; set; }
    [MaxLength(60)] public string? Reference { get; set; }
    public decimal ChangeGiven { get; set; }
}

[Index(nameof(No), IsUnique = true)]
public class Expense : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int BranchId { get; set; }
    [MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(60)] public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; }
    [MaxLength(120)] public string RequestedBy { get; set; } = string.Empty;
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Pending;
    public int? DecidedByUserId { get; set; }
}

[Index(nameof(EmployeeId), nameof(Period), IsUnique = true)]
public class PayrollRecord : AuditedEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    /// <summary>الفترة بصيغة yyyy-MM.</summary>
    [MaxLength(7)] public string Period { get; set; } = string.Empty;
    public decimal Base { get; set; }
    public decimal Extras { get; set; }
    public decimal Deductions { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;
    public DateTime? PaidAt { get; set; }
    [NotMapped] public decimal Net => Base + Extras - Deductions;
}

[Index(nameof(BranchId), nameof(Date), IsUnique = true)]
public class DailyClose : AuditedEntity
{
    public int BranchId { get; set; }
    public DateOnly Date { get; set; }
    public decimal CashExpected { get; set; }
    public decimal CashCounted { get; set; }
    public decimal CardExpected { get; set; }
    public decimal CardCounted { get; set; }
    public decimal InsuranceExpected { get; set; }
    [MaxLength(300)] public string? DifferenceNote { get; set; }
    public int? ClosedByUserId { get; set; }
    public DateTime ClosedAt { get; set; }
    [NotMapped] public decimal Difference => (CashCounted - CashExpected) + (CardCounted - CardExpected);
}
