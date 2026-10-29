using System.ComponentModel.DataAnnotations;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Contracts;

// ---------- الموارد البشرية ----------
public sealed record EmployeeRequest([Required, StringLength(120, MinimumLength = 2)] string FullName, [Required, StringLength(100, MinimumLength = 2)] string JobTitle, [StringLength(20)] string? Phone,
    [Required] DateOnly? HireDate, [Range(0, 10000000)] decimal BaseSalary, int? DepartmentId, int? UserId, int? BranchId);
public sealed record EmployeeStatusRequest([Required] EmployeeStatus? Status);
public sealed record EmployeeDto(int Id, string FullName, string JobTitle, string? Phone, DateOnly HireDate, EmployeeStatus Status, decimal BaseSalary, int LeaveBalanceDays, int BranchId, int? DepartmentId, int? UserId, DateOnly? LicenseExpiresAt);
public sealed record LeaveRequestInput(int? EmployeeId, [Required] LeaveType? Type, [Required] DateOnly? From, [Required] DateOnly? To, [StringLength(300)] string? Reason);
public sealed record LeaveDecisionRequest([StringLength(300)] string? Reason, bool CoverageException = false);
public sealed record LeaveDto(int Id, int EmployeeId, string EmployeeName, LeaveType Type, DateOnly From, DateOnly To, int Days, LeaveStatus Status, string? Reason, string? DecisionReason, bool CoverageException);
public sealed record ShiftInput([Required] int? EmployeeId, [Required] DateOnly? Date, [Required] ShiftKind? Kind, [Required, Range(0, 1439)] int? StartMinutes, [Required, Range(1, 1440)] int? EndMinutes);
public sealed record ShiftDto(int Id, int EmployeeId, string EmployeeName, DateOnly Date, ShiftKind Kind, int StartMinutes, int EndMinutes, ShiftStatus Status);
public sealed record LicenseDto(int EmployeeId, string EmployeeName, string Number, string Authority, DateOnly ExpiresAt, int DaysLeft);

// ---------- المالية ----------
public sealed record ExpenseInput([Required, StringLength(200, MinimumLength = 2)] string Title, [Required, StringLength(60, MinimumLength = 2)] string Category, [Required, Range(0.01, 100000000)] decimal? Amount, [Required] DateOnly? ExpenseDate, [StringLength(120)] string? RequestedBy);
public sealed record ExpenseDecisionRequest([StringLength(300)] string? Reason);
public sealed record ExpenseDto(int Id, string No, string Title, string Category, decimal Amount, DateOnly ExpenseDate, string RequestedBy, ExpenseStatus Status, int BranchId);
public sealed record GeneratePayrollRequest([Required, RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$")] string Period);
public sealed record PayrollAdjustRequest([Range(0, 10000000)] decimal Extras, [Range(0, 10000000)] decimal Deductions);
public sealed record PayrollDto(int Id, int EmployeeId, string EmployeeName, string Period, decimal Base, decimal Extras, decimal Deductions, decimal Net, PayrollStatus Status, DateTime? PaidAt);
public sealed record DailyCloseInput([Required] DateOnly? Date, [Range(0, 100000000)] decimal CashCounted, [Range(0, 100000000)] decimal CardCounted, [StringLength(300)] string? Note, int? BranchId);
public sealed record DailyCloseDto(int? Id, int BranchId, DateOnly Date, decimal CashExpected, decimal CashCounted, decimal CardExpected, decimal CardCounted, decimal InsuranceExpected, decimal Difference, string? Note, bool Closed);
public sealed record FinanceSummaryDto(DateOnly From, DateOnly To, decimal Revenue, decimal Refunds, decimal ExpensesPaid, decimal PayrollPaid, decimal Net, decimal OutstandingInvoices, decimal InsuranceReceivable);

// ---------- التأمين ----------
public sealed record PreAuthInput([Required] int? PatientId, [Required, StringLength(200, MinimumLength = 2)] string ProcedureName, [StringLength(80)] string? Diagnosis, [Required, Range(0.01, 100000000)] decimal? Amount, OrderPriority Priority, List<string>? Documents);
public sealed record PreAuthDecisionRequest([Required] ApprovalStatus? Decision, decimal? ApprovedAmount, [StringLength(300)] string? RejectReason);
public sealed record PreAuthDocDto(int Id, string Name, bool Received);
public sealed record PreAuthDto(int Id, string No, PersonRefDto Patient, string Insurer, string? PolicyNo, string? DoctorName, string ProcedureName, string? Diagnosis, decimal Amount, OrderPriority Priority, ApprovalStatus Status,
    DateTime SubmittedAt, DateTime? DecidedAt, decimal? ApprovedAmount, string? RejectReason, IReadOnlyList<PreAuthDocDto> Documents);
public sealed record ClaimDto(int Id, string No, PersonRefDto Patient, string Insurer, int? InvoiceId, decimal Amount, decimal PaidAmount, ClaimStatus Status, DateTime? SubmittedAt, DateTime? PaidAt, string? RejectReason);
public sealed record SettleClaimRequest([Required, Range(0, 100000000)] decimal? PaidAmount);
public sealed record RejectClaimRequest([Required, StringLength(300, MinimumLength = 2)] string Reason);
public sealed record InsuranceCompanyDto(int Id, string Name, ContractStatus Status, DateOnly ContractEndsAt, decimal DiscountPct);
public sealed record PolicyLookupDto(int Id, string Insurer, string Tier, string PolicyNo, DateOnly ExpiresAt, decimal CopayRate, decimal AnnualLimit, decimal Used, decimal Remaining, bool Active, bool Expired);

// ---------- لوحات ----------
public sealed record DayPointDto(DateOnly Date, int Appointments, decimal Revenue);
public sealed record OverviewDto(int? BranchId, DateOnly Today, IReadOnlyDictionary<string, int> AppointmentsToday, decimal RevenueToday, decimal OutstandingInvoices, int BedsTotal, int BedsOccupied, int EmergencyWaiting,
    int LabPending, int ImagingPending, int PrescriptionsPending, int LowStock, int PendingExpenses, int PendingPreAuths, int PendingLeaves, int SurgeriesToday, IReadOnlyList<DayPointDto> Last7Days);
