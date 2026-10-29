using System.ComponentModel.DataAnnotations;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Contracts;

public sealed record InvoiceItemDto(string Description, int Quantity, decimal UnitPrice, decimal Total);

public sealed record PaymentDto(int Id, decimal Amount, PaymentMethod Method, DateTime PaidAt, string? Reference, decimal ChangeGiven);

public sealed record InvoiceDto(
    int Id, string No, string Title, PersonRefDto Patient, int BranchId, int? AppointmentId, DateOnly IssueDate, DateOnly DueDate, InvoiceStatus Status,
    decimal Subtotal, decimal InsuranceCovered, decimal Discount, decimal Paid, decimal Remaining, string? Notes,
    IReadOnlyList<InvoiceItemDto> Items, IReadOnlyList<PaymentDto> Payments);

public sealed record InvoiceListDto(PagedResult<InvoiceDto> Page, decimal OutstandingTotal, int UnpaidCount);

/// <summary>المريض: الطريقة فقط (يُسدَّد كامل المتبقي). الموظف: المبلغ اختياري (دفعة جزئية) و«المستلَم» للنقد لحساب الباقي.</summary>
public sealed record PayInvoiceRequest([Required] PaymentMethod? Method, [Range(0.01, 10000000)] decimal? Amount, [Range(0.01, 10000000)] decimal? Tendered, [StringLength(60)] string? Reference);

public sealed record PatientInsuranceDto(
    int Id, string Insurer, string Tier, string PolicyNo, DateOnly ExpiresAt, decimal CopayRate, decimal AnnualLimit, decimal Used, decimal Remaining, bool Active, bool Expired);
