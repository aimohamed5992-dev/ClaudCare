using ClaudCare.Api.Contracts;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaudCare.Api.Controllers;

/// <summary>الموارد البشرية: الموظفون والإجازات والمناوبات والتراخيص. أي موظف يطلب إجازته ويرى مناوباته.</summary>
[ApiController]
[Authorize]
[Route("api/hr")]
[Produces("application/json")]
public class HrController(HrService hr) : ControllerBase
{
    [HttpGet("employees")]
    public async Task<PagedResult<EmployeeDto>> Employees([FromQuery] string? search = null, [FromQuery] EmployeeStatus? status = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await hr.EmployeesAsync(search, status, page, pageSize, ct);

    [HttpGet("employees/{id:int}")]
    public async Task<EmployeeDto> Employee(int id, CancellationToken ct) => await hr.GetEmployeeAsync(id, ct);

    [HttpPost("employees")]
    [ProducesResponseType<EmployeeDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(EmployeeRequest req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await hr.CreateEmployeeAsync(req, ct));

    [HttpPut("employees/{id:int}")]
    public async Task<EmployeeDto> Update(int id, EmployeeRequest req, CancellationToken ct) => await hr.UpdateEmployeeAsync(id, req, ct);

    /// <summary>تغيير الحالة؛ إنهاء الخدمة يعطّل حساب الدخول ويلغي جلساته.</summary>
    [HttpPost("employees/{id:int}/status")]
    public async Task<EmployeeDto> Status(int id, EmployeeStatusRequest req, CancellationToken ct) => await hr.SetStatusAsync(id, req.Status!.Value, ct);

    [HttpPost("leaves")]
    [ProducesResponseType<LeaveDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> RequestLeave(LeaveRequestInput req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await hr.RequestLeaveAsync(req, ct));

    [HttpGet("leaves")]
    public async Task<IReadOnlyList<LeaveDto>> Leaves([FromQuery] LeaveStatus? status = null, CancellationToken ct = default) => await hr.LeavesAsync(status, ct);

    [HttpPost("leaves/{id:int}/approve")]
    public async Task<LeaveDto> Approve(int id, LeaveDecisionRequest? req, CancellationToken ct) => await hr.DecideLeaveAsync(id, true, req?.Reason, req?.CoverageException ?? false, ct);

    [HttpPost("leaves/{id:int}/reject")]
    public async Task<LeaveDto> Reject(int id, LeaveDecisionRequest req, CancellationToken ct) => await hr.DecideLeaveAsync(id, false, req.Reason, false, ct);

    [HttpGet("shifts")]
    public async Task<IReadOnlyList<ShiftDto>> Shifts([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, [FromQuery] int? employeeId = null, CancellationToken ct = default) => await hr.ShiftsAsync(from, to, employeeId, ct);

    [HttpPost("shifts")]
    [ProducesResponseType<ShiftDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateShift(ShiftInput req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await hr.CreateShiftAsync(req, ct));

    [HttpDelete("shifts/{id:int}")]
    public async Task<IActionResult> DeleteShift(int id, CancellationToken ct) { await hr.DeleteShiftAsync(id, ct); return NoContent(); }

    [HttpPost("shifts/{id:int}/request-swap")]
    public async Task<ShiftDto> RequestSwap(int id, CancellationToken ct) => await hr.SwapAsync(id, false, ct);

    [HttpPost("shifts/{id:int}/confirm")]
    public async Task<ShiftDto> ConfirmShift(int id, CancellationToken ct) => await hr.SwapAsync(id, true, ct);

    [HttpGet("licenses/expiring")]
    public async Task<IReadOnlyList<LicenseDto>> Licenses([FromQuery] int? days = null, CancellationToken ct = default) => await hr.ExpiringLicensesAsync(days, ct);
}

/// <summary>المالية: المصروفات، الرواتب، الإقفال اليومي، الملخص.</summary>
[ApiController]
[Authorize]
[Route("api/finance")]
[Produces("application/json")]
public class FinanceController(FinanceService finance) : ControllerBase
{
    [HttpPost("expenses")]
    [ProducesResponseType<ExpenseDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateExpense(ExpenseInput req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await finance.CreateExpenseAsync(req, ct));

    [HttpGet("expenses")]
    public async Task<PagedResult<ExpenseDto>> Expenses([FromQuery] ExpenseStatus? status = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await finance.ExpensesAsync(status, page, pageSize, ct);

    /// <summary>اعتماد المصروف: فوق 10,000 ر.س يعتمده مدير المستشفى/النظام (403 approval_limit_exceeded للمالية).</summary>
    [HttpPost("expenses/{id:int}/approve")]
    public async Task<ExpenseDto> ApproveExpense(int id, CancellationToken ct) => await finance.DecideExpenseAsync(id, true, null, ct);

    [HttpPost("expenses/{id:int}/reject")]
    public async Task<ExpenseDto> RejectExpense(int id, ExpenseDecisionRequest req, CancellationToken ct) => await finance.DecideExpenseAsync(id, false, req.Reason, ct);

    [HttpPost("expenses/{id:int}/pay")]
    public async Task<ExpenseDto> PayExpense(int id, CancellationToken ct) => await finance.PayExpenseAsync(id, ct);

    /// <summary>يولّد مسودات رواتب الفترة (yyyy-MM) لموظفي الفرع النشطين دون تكرار.</summary>
    [HttpPost("payroll/generate")]
    public async Task<IReadOnlyList<PayrollDto>> GeneratePayroll(GeneratePayrollRequest req, CancellationToken ct) => await finance.GeneratePayrollAsync(req.Period, ct);

    [HttpGet("payroll")]
    public async Task<IReadOnlyList<PayrollDto>> Payroll([FromQuery] string period, CancellationToken ct) => await finance.PayrollAsync(period, ct);

    [HttpPut("payroll/{id:int}")]
    public async Task<PayrollDto> Adjust(int id, PayrollAdjustRequest req, CancellationToken ct) => await finance.AdjustPayrollAsync(id, req.Extras, req.Deductions, ct);

    [HttpPost("payroll/{id:int}/submit")]
    public async Task<PayrollDto> Submit(int id, CancellationToken ct) => await finance.SubmitPayrollAsync(id, ct);

    [HttpPost("payroll/{id:int}/pay")]
    public async Task<PayrollDto> PayPayroll(int id, CancellationToken ct) => await finance.PayPayrollAsync(id, ct);

    /// <summary>المتوقع من دفعات اليوم الفعلية قبل الإقفال.</summary>
    [HttpGet("daily-close/preview")]
    public async Task<DailyCloseDto> Preview([FromQuery] DateOnly date, [FromQuery] int? branchId = null, CancellationToken ct = default) => await finance.PreviewCloseAsync(date, branchId, ct);

    [HttpPost("daily-close")]
    [ProducesResponseType<DailyCloseDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Close(DailyCloseInput req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await finance.CloseAsync(req, ct));

    [HttpGet("daily-close")]
    public async Task<IReadOnlyList<DailyCloseDto>> Closes([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default) => await finance.ClosesAsync(from, to, ct);

    [HttpGet("summary")]
    public async Task<FinanceSummaryDto> Summary([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, [FromQuery] int? branchId = null, CancellationToken ct = default) => await finance.SummaryAsync(from, to, branchId, ct);
}

/// <summary>التأمين: الوثائق، الموافقات المسبقة، المطالبات.</summary>
[ApiController]
[Authorize]
[Route("api/insurance")]
[Produces("application/json")]
public class InsuranceController(InsuranceService insurance) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<IReadOnlyList<InsuranceCompanyDto>> Companies(CancellationToken ct) => await insurance.CompaniesAsync(ct);

    [HttpGet("policies")]
    public async Task<IReadOnlyList<PolicyLookupDto>> Policies([FromQuery] int patientId, CancellationToken ct) => await insurance.PoliciesAsync(patientId, ct);

    [HttpPost("pre-auths")]
    [ProducesResponseType<PreAuthDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> RequestPreAuth(PreAuthInput req, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await insurance.RequestPreAuthAsync(req, ct));

    [HttpGet("pre-auths")]
    public async Task<PagedResult<PreAuthDto>> PreAuths([FromQuery] ApprovalStatus? status = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await insurance.PreAuthsAsync(status, page, pageSize, ct);

    [HttpGet("pre-auths/{id:int}")]
    public async Task<PreAuthDto> PreAuth(int id, CancellationToken ct) => await insurance.PreAuthAsync(id, ct);

    [HttpPost("pre-auths/{id:int}/documents/{docId:int}/receive")]
    public async Task<PreAuthDto> Receive(int id, int docId, CancellationToken ct) => await insurance.ReceiveDocumentAsync(id, docId, ct);

    /// <summary>القرار: Approved (بعد استلام كل المستندات) · Rejected (بسبب) · MoreInfoNeeded.</summary>
    [HttpPost("pre-auths/{id:int}/decide")]
    public async Task<PreAuthDto> Decide(int id, PreAuthDecisionRequest req, CancellationToken ct) => await insurance.DecideAsync(id, req.Decision!.Value, req.ApprovedAmount, req.RejectReason, ct);

    [HttpPost("claims/from-invoice/{invoiceId:int}")]
    [ProducesResponseType<ClaimDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateClaim(int invoiceId, CancellationToken ct) => StatusCode(StatusCodes.Status201Created, await insurance.CreateClaimAsync(invoiceId, ct));

    [HttpGet("claims")]
    public async Task<PagedResult<ClaimDto>> Claims([FromQuery] ClaimStatus? status = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null, CancellationToken ct = default) =>
        await insurance.ClaimsAsync(status, page, pageSize, ct);

    [HttpGet("claims/{id:int}")]
    public async Task<ClaimDto> Claim(int id, CancellationToken ct) => await insurance.ClaimAsync(id, ct);

    [HttpPost("claims/{id:int}/submit")]
    public async Task<ClaimDto> Submit(int id, CancellationToken ct) => await insurance.SubmitClaimAsync(id, ct);

    [HttpPost("claims/{id:int}/settle")]
    public async Task<ClaimDto> Settle(int id, SettleClaimRequest req, CancellationToken ct) => await insurance.SettleClaimAsync(id, req.PaidAmount!.Value, ct);

    [HttpPost("claims/{id:int}/reject")]
    public async Task<ClaimDto> RejectClaim(int id, RejectClaimRequest req, CancellationToken ct) => await insurance.RejectClaimAsync(id, req.Reason, ct);
}

/// <summary>لوحات المؤشرات.</summary>
[ApiController]
[Authorize]
[Route("api/dashboard")]
[Produces("application/json")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>نظرة عامة للإدارة العليا (branchId اختياري) ولمدير الفرع (فرعه) وللمالية (فرعها).</summary>
    [HttpGet("overview")]
    public async Task<OverviewDto> Overview([FromQuery] int? branchId = null, CancellationToken ct = default) => await dashboard.OverviewAsync(branchId, ct);

    /// <summary>أعداد سريعة لشاشة المستخدم الرئيسية حسب دوره.</summary>
    [HttpGet("me")]
    public async Task<IReadOnlyDictionary<string, int>> Mine(CancellationToken ct) => await dashboard.MineAsync(ct);
}
