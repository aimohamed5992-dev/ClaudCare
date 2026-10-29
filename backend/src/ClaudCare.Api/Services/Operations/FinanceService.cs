using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ClaudCare.Api.Services.Operations;

/// <summary>
/// المالية: المصروفات (سقف اعتماد المالية 10,000؛ فوقه للمستشفى/الإدارة)، الرواتب بدورة مسودة ← اعتماد ← صرف، الإقفال اليومي من الدفعات الفعلية، والملخص.
/// </summary>
public sealed class FinanceService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, IClinicClock clock, IAuditService audit, TimeProvider time)
{
    public const decimal FinanceApprovalLimit = 10_000m;
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------ المصروفات

    public async Task<ExpenseDto> CreateExpenseAsync(ExpenseInput r, CancellationToken ct)
    {
        scope.Require(UserRole.Finance, UserRole.Branch, UserRole.Pharmacy);
        var e = new Expense { No = "TMP" + Guid.NewGuid().ToString("N")[..16], BranchId = scope.BranchForNew(), Title = r.Title.Trim(), Category = r.Category.Trim(), Amount = r.Amount!.Value, ExpenseDate = r.ExpenseDate!.Value,
            RequestedBy = string.IsNullOrWhiteSpace(r.RequestedBy) ? (await db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id, ct)).FullName : r.RequestedBy.Trim() };
        db.Expenses.Add(e);
        await db.SaveChangesAsync(ct);
        e.No = $"EXP-{e.Id:D5}";
        await db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task<PagedResult<ExpenseDto>> ExpensesAsync(ExpenseStatus? status, int? page, int? pageSize, CancellationToken ct)
    {
        scope.Require(UserRole.Finance, UserRole.Branch);
        var (p, size) = Paging.Clamp(page, pageSize);
        var b = scope.BranchFilter;
        var q = db.Expenses.AsNoTracking().Where(e => b == null || e.BranchId == b);
        if (status is { } s) q = q.Where(e => e.Status == s);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(e => e.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<ExpenseDto>(rows.Select(ToDto).ToList(), p, size, total);
    }

        public Task<ExpenseDto> DecideExpenseAsync(int id, bool approve, string? reason, CancellationToken ct) => RowLock.RunAsync(db, "Expenses", id, () => DecideExpenseCoreAsync(id, approve, reason, ct), ct);

    private async Task<ExpenseDto> DecideExpenseCoreAsync(int id, bool approve, string? reason, CancellationToken ct)
    {
        scope.Require(UserRole.Finance);
        if (!approve && string.IsNullOrWhiteSpace(reason)) throw ApiException.Validation(nameof(reason), "اذكر سبب الرفض");
        var e = await FindExpenseAsync(id, ct);
        if (e.Status != ExpenseStatus.Pending) throw ApiException.Conflict("invalid_state", "سبق البتّ في المصروف.");
        if (approve && e.Amount > FinanceApprovalLimit && !scope.IsGlobal)
            throw new ApiException(StatusCodes.Status403Forbidden, "approval_limit_exceeded", $"المصروفات فوق {FinanceApprovalLimit:0,0} ر.س يعتمدها مدير المستشفى أو النظام.");
        e.Status = approve ? ExpenseStatus.Approved : ExpenseStatus.Rejected;
        e.DecidedByUserId = me.Id;
        audit.Add(approve ? "finance.expense_approve" : "finance.expense_reject", me.Id, nameof(Expense), e.Id, reason, me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(e);
    }

        public Task<ExpenseDto> PayExpenseAsync(int id, CancellationToken ct) => RowLock.RunAsync(db, "Expenses", id, () => PayExpenseCoreAsync(id, ct), ct);

    private async Task<ExpenseDto> PayExpenseCoreAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Finance);
        var e = await FindExpenseAsync(id, ct);
        if (e.Status != ExpenseStatus.Approved) throw ApiException.Conflict("invalid_state", "يُصرف المعتمد فقط.");
        e.Status = ExpenseStatus.Paid;
        audit.Add("finance.expense_pay", me.Id, nameof(Expense), e.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    private async Task<Expense> FindExpenseAsync(int id, CancellationToken ct)
    {
        var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is not null && scope.InBranch(e.BranchId) ? e : throw ApiException.NotFound("expense_not_found", "المصروف غير موجود.");
    }

    private static ExpenseDto ToDto(Expense e) => new(e.Id, e.No, e.Title, e.Category, e.Amount, e.ExpenseDate, e.RequestedBy, e.Status, e.BranchId);

    // ------------------------------------------------------------ الرواتب

    public async Task<IReadOnlyList<PayrollDto>> GeneratePayrollAsync(string period, CancellationToken ct)
    {
        scope.Require(UserRole.Finance, UserRole.Hr);
        var b = scope.BranchForNew();
        var emps = await db.Employees.AsNoTracking().Where(e => e.BranchId == b && e.Status != EmployeeStatus.Terminated).Select(e => new { e.Id, e.BaseSalary }).ToListAsync(ct);
        var existing = (await db.PayrollRecords.Where(p => p.Period == period && p.Employee.BranchId == b).Select(p => p.EmployeeId).ToListAsync(ct)).ToHashSet();
        foreach (var e in emps.Where(e => !existing.Contains(e.Id))) db.PayrollRecords.Add(new PayrollRecord { EmployeeId = e.Id, Period = period, Base = e.BaseSalary });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.GetBaseException() is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry }) { /* توليد متزامن: الفريد يمنع التكرار */ }
        return await PayrollAsync(period, ct);
    }

    public async Task<IReadOnlyList<PayrollDto>> PayrollAsync(string period, CancellationToken ct)
    {
        scope.Require(UserRole.Finance, UserRole.Hr);
        var b = scope.BranchFilter;
        var rows = await db.PayrollRecords.AsNoTracking().Where(p => p.Period == period && (b == null || p.Employee.BranchId == b)).Include(p => p.Employee).OrderBy(p => p.Employee.FullName).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<PayrollDto> AdjustPayrollAsync(int id, decimal extras, decimal deductions, CancellationToken ct)
    {
        scope.Require(UserRole.Finance, UserRole.Hr);
        var p = await FindPayrollAsync(id, ct);
        if (p.Status != PayrollStatus.Draft) throw ApiException.Conflict("invalid_state", "التعديل للمسودة فقط.");
        if (p.Base + extras - deductions < 0) throw ApiException.Validation(nameof(deductions), "الصافي لا يكون سالبًا");
        p.Extras = extras; p.Deductions = deductions;
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<PayrollDto> SubmitPayrollAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Finance, UserRole.Hr);
        var p = await FindPayrollAsync(id, ct);
        if (p.Status != PayrollStatus.Draft) throw ApiException.Conflict("invalid_state", "أُرسل للاعتماد سابقًا.");
        p.Status = PayrollStatus.PendingApproval;
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    /// <summary>الاعتماد والصرف يتمان من المالية (غير من أعدّ المسودة إن كان من الموارد البشرية) أو الإدارة العليا.</summary>
        public Task<PayrollDto> PayPayrollAsync(int id, CancellationToken ct) => RowLock.RunAsync(db, "PayrollRecords", id, () => PayPayrollCoreAsync(id, ct), ct);

    private async Task<PayrollDto> PayPayrollCoreAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Finance);
        var p = await FindPayrollAsync(id, ct);
        if (p.Status != PayrollStatus.PendingApproval) throw ApiException.Conflict("invalid_state", "يُصرف المرسَل للاعتماد فقط.");
        p.Status = PayrollStatus.Paid; p.PaidAt = UtcNow;
        audit.Add("finance.payroll_pay", me.Id, nameof(PayrollRecord), p.Id, $"{p.Net}", me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    private async Task<PayrollRecord> FindPayrollAsync(int id, CancellationToken ct)
    {
        var p = await db.PayrollRecords.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is not null && scope.InBranch(p.Employee.BranchId) ? p : throw ApiException.NotFound("payroll_not_found", "سجل الراتب غير موجود.");
    }

    private static PayrollDto ToDto(PayrollRecord p) => new(p.Id, p.EmployeeId, p.Employee.FullName, p.Period, p.Base, p.Extras, p.Deductions, p.Net, p.Status, p.PaidAt);

    // ------------------------------------------------------------ الإقفال اليومي

    private async Task<(decimal cash, decimal card, decimal insurance)> ExpectedAsync(int branch, DateOnly date, CancellationToken ct)
    {
        var from = clock.DayStartUtc(date); var to = clock.DayStartUtc(date.AddDays(1));
        var pay = db.Payments.AsNoTracking().Where(p => p.Invoice.BranchId == branch && p.PaidAt >= from && p.PaidAt < to);
        var cash = await pay.Where(p => p.Method == PaymentMethod.Cash).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var card = await pay.Where(p => p.Method == PaymentMethod.Card || p.Method == PaymentMethod.ApplePay || p.Method == PaymentMethod.BankTransfer).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var ins = await db.Invoices.AsNoTracking().Where(i => i.BranchId == branch && i.Status != InvoiceStatus.Cancelled && i.CreatedAt >= from && i.CreatedAt < to).SumAsync(i => (decimal?)i.InsuranceCovered, ct) ?? 0;
        return (cash, card, ins);
    }

    public async Task<DailyCloseDto> PreviewCloseAsync(DateOnly date, int? branchId, CancellationToken ct)
    {
        scope.Require(UserRole.Finance, UserRole.Branch);
        var b = scope.BranchForNew(branchId);
        var closed = await db.DailyCloses.AsNoTracking().FirstOrDefaultAsync(c => c.BranchId == b && c.Date == date, ct);
        if (closed is not null) return ToDto(closed);
        var (cash, card, ins) = await ExpectedAsync(b, date, ct);
        return new DailyCloseDto(null, b, date, cash, 0, card, 0, ins, 0, null, false);
    }

    public async Task<DailyCloseDto> CloseAsync(DailyCloseInput r, CancellationToken ct)
    {
        scope.Require(UserRole.Finance);
        var b = scope.BranchForNew(r.BranchId);
        var date = r.Date!.Value;
        if (date > clock.Today) throw ApiException.Validation(nameof(r.Date), "لا يُقفل يوم مستقبلي");
        var (cash, card, ins) = await ExpectedAsync(b, date, ct);
        var diff = (r.CashCounted - cash) + (r.CardCounted - card);
        if (diff != 0 && string.IsNullOrWhiteSpace(r.Note)) throw ApiException.Validation(nameof(r.Note), "يوجد فرق؛ اذكر سببه");
        var c = new DailyClose { BranchId = b, Date = date, CashExpected = cash, CashCounted = r.CashCounted, CardExpected = card, CardCounted = r.CardCounted, InsuranceExpected = ins, DifferenceNote = r.Note?.Trim(), ClosedByUserId = me.Id, ClosedAt = UtcNow };
        db.DailyCloses.Add(c);
        audit.Add("finance.daily_close", me.Id, nameof(DailyClose), details: $"{date.Iso()} diff={diff}", ip: me.Ip);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.GetBaseException() is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry }) { throw ApiException.Conflict("already_closed", "أُقفل هذا اليوم سابقًا."); }
        return ToDto(c);
    }

    public async Task<IReadOnlyList<DailyCloseDto>> ClosesAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        scope.Require(UserRole.Finance, UserRole.Branch);
        var b = scope.BranchFilter;
        var f = from ?? clock.Today.AddDays(-30); var t = to ?? clock.Today;
        return (await db.DailyCloses.AsNoTracking().Where(c => (b == null || c.BranchId == b) && c.Date >= f && c.Date <= t).OrderByDescending(c => c.Date).Take(100).ToListAsync(ct)).Select(ToDto).ToList();
    }

    private static DailyCloseDto ToDto(DailyClose c) => new(c.Id, c.BranchId, c.Date, c.CashExpected, c.CashCounted, c.CardExpected, c.CardCounted, c.InsuranceExpected, c.Difference, c.DifferenceNote, true);

    // ------------------------------------------------------------ الملخص

    public async Task<FinanceSummaryDto> SummaryAsync(DateOnly? from, DateOnly? to, int? branchId, CancellationToken ct)
    {
        scope.Require(UserRole.Finance, UserRole.Branch);
        var b = scope.IsGlobal ? branchId : scope.BranchFilter;
        var f = from ?? clock.Today.AddDays(-30); var t = to ?? clock.Today;
        if (t < f) throw ApiException.Validation(nameof(to), "المدى غير صالح");
        var fromUtc = clock.DayStartUtc(f); var toUtc = clock.DayStartUtc(t.AddDays(1));
        var pay = db.Payments.AsNoTracking().Where(p => (b == null || p.Invoice.BranchId == b) && p.PaidAt >= fromUtc && p.PaidAt < toUtc);
        var revenue = await pay.Where(p => p.Amount > 0).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var refunds = -(await pay.Where(p => p.Amount < 0).SumAsync(p => (decimal?)p.Amount, ct) ?? 0);
        var expenses = await db.Expenses.AsNoTracking().Where(e => (b == null || e.BranchId == b) && e.Status == ExpenseStatus.Paid && e.ExpenseDate >= f && e.ExpenseDate <= t).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var payroll = (await db.PayrollRecords.AsNoTracking().Where(p => (b == null || p.Employee.BranchId == b) && p.Status == PayrollStatus.Paid && p.PaidAt >= fromUtc && p.PaidAt < toUtc).ToListAsync(ct)).Sum(p => p.Net);
        var outstanding = await db.Invoices.AsNoTracking().Where(i => (b == null || i.BranchId == b) && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid)).SumAsync(i => (decimal?)(i.Subtotal - i.Discount - i.InsuranceCovered - i.PaidAmount), ct) ?? 0;
        var receivable = await db.InsuranceClaims.AsNoTracking().Where(c => (b == null || c.Invoice!.BranchId == b) && (c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.PartiallyPaid)).SumAsync(c => (decimal?)(c.Amount - c.PaidAmount), ct) ?? 0;
        return new FinanceSummaryDto(f, t, revenue, refunds, expenses, payroll, revenue - refunds - expenses - payroll, outstanding, receivable);
    }
}
