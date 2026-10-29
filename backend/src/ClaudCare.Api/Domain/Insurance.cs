using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

[Index(nameof(Name), IsUnique = true)]
public class InsuranceCompany : Entity
{
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public DateOnly ContractEndsAt { get; set; }
    [Precision(5, 2)] public decimal DiscountPct { get; set; }
    public int AveragePayDays { get; set; }
    [MaxLength(200)] public string? ContactEmail { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Active;
}

[Index(nameof(PolicyNo), IsUnique = true)]
public class PatientInsurance : AuditedEntity
{
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int CompanyId { get; set; }
    public InsuranceCompany Company { get; set; } = null!;
    [MaxLength(40)] public string PolicyNo { get; set; } = string.Empty;
    [MaxLength(40)] public string Tier { get; set; } = string.Empty;
    /// <summary>نسبة تحمّل المريض (0.20 = 20%).</summary>
    [Precision(4, 2)] public decimal CopayRate { get; set; }
    public decimal AnnualLimit { get; set; }
    public decimal UsedAmount { get; set; }
    public DateOnly ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}

[Index(nameof(No), IsUnique = true)]
[Index(nameof(Status))]
public class PreAuthorization : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int CompanyId { get; set; }
    public InsuranceCompany Company { get; set; } = null!;
    public int? PolicyId { get; set; }
    public PatientInsurance? Policy { get; set; }
    public int? DoctorId { get; set; }
    [ForeignKey(nameof(DoctorId))] public User? Doctor { get; set; }
    [MaxLength(200)] public string ProcedureName { get; set; } = string.Empty;
    [MaxLength(80)] public string? Diagnosis { get; set; }
    public decimal Amount { get; set; }
    public OrderPriority Priority { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public DateTime SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int? DecidedByUserId { get; set; }
    public decimal? ApprovedAmount { get; set; }
    [MaxLength(300)] public string? RejectReason { get; set; }
    public List<PreAuthDocument> Documents { get; set; } = [];
}

public class PreAuthDocument : Entity
{
    public int PreAuthorizationId { get; set; }
    public PreAuthorization PreAuthorization { get; set; } = null!;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    public bool Received { get; set; }
    [MaxLength(300)] public string? FileKey { get; set; }
}

[Index(nameof(No), IsUnique = true)]
public class InsuranceClaim : AuditedEntity
{
    [MaxLength(20)] public string No { get; set; } = string.Empty;
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    public int CompanyId { get; set; }
    public InsuranceCompany Company { get; set; } = null!;
    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    [MaxLength(300)] public string? RejectReason { get; set; }
}
