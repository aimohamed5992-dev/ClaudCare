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
/// الموارد البشرية: الموظفون، الإجازات (رصيد سنوي يُخصم ذريًا عند الاعتماد)، المناوبات، تراخيص المهنة. الموارد البشرية لفرعها،
/// وأي موظف يطلب إجازته ويرى مناوباته فقط.
/// </summary>
public sealed class HrService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, IClinicClock clock, INotificationService notifications, IAuditService audit, TimeProvider time)
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
    private bool IsHr => me.Role == UserRole.Hr || scope.IsGlobal;

    // ------------------------------------------------------------ الموظفون

    public async Task<PagedResult<EmployeeDto>> EmployeesAsync(string? search, EmployeeStatus? status, int? page, int? pageSize, CancellationToken ct)
    {
        scope.Require(UserRole.Hr, UserRole.Branch, UserRole.Finance);
        var (p, size) = Paging.Clamp(page, pageSize);
        var b = scope.BranchFilter;
        var q = db.Employees.AsNoTracking().Where(e => b == null || e.BranchId == b);
        if (status is { } s) q = q.Where(e => e.Status == s);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(e => e.FullName.Contains(search.Trim()) || e.JobTitle.Contains(search.Trim()));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(e => e.FullName).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<EmployeeDto>(await ToDtosAsync(rows, ct), p, size, total);
    }

    public async Task<EmployeeDto> GetEmployeeAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Hr, UserRole.Branch, UserRole.Finance);
        return (await ToDtosAsync([await FindEmployeeAsync(id, tracked: false, ct)], ct))[0];
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(EmployeeRequest r, CancellationToken ct)
    {
        scope.Require(UserRole.Hr);
        var branch = scope.BranchForNew(r.BranchId);
        if (r.UserId is { } uid)
        {
            if (!await db.Users.AnyAsync(u => u.Id == uid && u.Role != UserRole.Patient, ct)) throw ApiException.NotFound("user_not_found", "حساب الطاقم غير موجود.");
            if (await db.Employees.AnyAsync(e => e.UserId == uid, ct)) throw ApiException.Conflict("user_already_employee", "الحساب مرتبط بموظف آخر.");
        }
        if (r.DepartmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) throw ApiException.NotFound("department_not_found", "القسم غير موجود.");
        var e = new Employee { BranchId = branch, FullName = r.FullName.Trim(), JobTitle = r.JobTitle.Trim(), Phone = r.Phone?.Trim(), HireDate = r.HireDate!.Value, BaseSalary = r.BaseSalary, DepartmentId = r.DepartmentId, UserId = r.UserId };
        db.Employees.Add(e);
        audit.Add("hr.employee_create", me.Id, nameof(Employee), details: e.FullName, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([e], ct))[0];
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(int id, EmployeeRequest r, CancellationToken ct)
    {
        scope.Require(UserRole.Hr);
        var e = await FindEmployeeAsync(id, tracked: true, ct);
        if (r.DepartmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) throw ApiException.NotFound("department_not_found", "القسم غير موجود.");
        e.FullName = r.FullName.Trim(); e.JobTitle = r.JobTitle.Trim(); e.Phone = r.Phone?.Trim(); e.HireDate = r.HireDate!.Value; e.BaseSalary = r.BaseSalary; e.DepartmentId = r.DepartmentId;
        audit.Add("hr.employee_update", me.Id, nameof(Employee), e.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([e], ct))[0];
    }

    /// <summary>إنهاء الخدمة يعطّل حساب الدخول المرتبط فورًا ويُلغي رموز التحديث.</summary>
    public async Task<EmployeeDto> SetStatusAsync(int id, EmployeeStatus status, CancellationToken ct)
    {
        scope.Require(UserRole.Hr);
        var e = await FindEmployeeAsync(id, tracked: true, ct);
        e.Status = status;
        if (status == EmployeeStatus.Terminated && e.UserId is { } uid)
        {
            await db.Users.Where(u => u.Id == uid).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false), ct);
            await db.RefreshTokens.Where(t => t.UserId == uid && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, UtcNow), ct);
        }
        audit.Add("hr.employee_status", me.Id, nameof(Employee), e.Id, status.ToString(), me.Ip);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([e], ct))[0];
    }

    // ------------------------------------------------------------ الإجازات

    public async Task<LeaveDto> RequestLeaveAsync(LeaveRequestInput r, CancellationToken ct)
    {
        Employee emp;
        if (IsHr && r.EmployeeId is { } eid) emp = await FindEmployeeAsync(eid, tracked: false, ct);
        else
        {
            if (me.Role == UserRole.Patient) throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية.");
            if (r.EmployeeId is { } other && !IsHr && other != (await db.Employees.Where(e => e.UserId == me.Id).Select(e => (int?)e.Id).FirstOrDefaultAsync(ct))) throw ApiException.Forbidden("action_not_allowed", "تطلب إجازتك فقط.");
            emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == me.Id, ct) ?? throw ApiException.NotFound("employee_not_found", "حسابك غير مرتبط بسجل موظف.");
        }
        var from = r.From!.Value; var to = r.To!.Value;
        if (to < from) throw ApiException.Validation(nameof(r.To), "تاريخ النهاية قبل البداية");
        if (from < clock.Today.AddDays(-30)) throw ApiException.Validation(nameof(r.From), "التاريخ قديم جدًا");
        var days = to.DayNumber - from.DayNumber + 1;
        if (days > 60) throw ApiException.Validation(nameof(r.To), "المدة تتجاوز 60 يومًا");
        if (emp.Status == EmployeeStatus.Terminated) throw ApiException.Conflict("invalid_state", "الموظف منتهي الخدمة.");
        if (await db.LeaveRequests.AnyAsync(l => l.EmployeeId == emp.Id && l.Status != LeaveStatus.Rejected && l.From <= to && from <= l.To, ct)) throw ApiException.Conflict("leave_overlap", "تتداخل مع إجازة أخرى.");
        if (r.Type == LeaveType.Annual && emp.LeaveBalanceDays < days) throw ApiException.Conflict("insufficient_balance", $"الرصيد المتبقي {emp.LeaveBalanceDays} يومًا فقط.");

        var l = new LeaveRequest { EmployeeId = emp.Id, Type = r.Type!.Value, From = from, To = to, Days = days, Reason = r.Reason?.Trim() };
        db.LeaveRequests.Add(l);
        await db.SaveChangesAsync(ct);
        return await LeaveAsync(l.Id, ct);
    }

    public async Task<IReadOnlyList<LeaveDto>> LeavesAsync(LeaveStatus? status, CancellationToken ct)
    {
        var q = db.LeaveRequests.AsNoTracking().AsQueryable();
        if (IsHr || me.Role is UserRole.Branch) { var b = scope.BranchFilter; q = q.Where(l => b == null || l.Employee.BranchId == b); }
        else q = q.Where(l => l.Employee.UserId == me.Id);
        if (status is { } s) q = q.Where(l => l.Status == s);
        return await q.OrderByDescending(l => l.Id).Take(200).Select(l => new LeaveDto(l.Id, l.EmployeeId, l.Employee.FullName, l.Type, l.From, l.To, l.Days, l.Status, l.Reason, l.DecisionReason, l.CoverageException)).ToListAsync(ct);
    }

        public Task<LeaveDto> DecideLeaveAsync(int id, bool approve, string? reason, bool coverage, CancellationToken ct) => RowLock.RunAsync(db, "LeaveRequests", id, () => DecideLeaveCoreAsync(id, approve, reason, coverage, ct), ct);

    private async Task<LeaveDto> DecideLeaveCoreAsync(int id, bool approve, string? reason, bool coverage, CancellationToken ct)
    {
        scope.Require(UserRole.Hr);
        if (!approve && string.IsNullOrWhiteSpace(reason)) throw ApiException.Validation(nameof(reason), "اذكر سبب الرفض");
        var l = await db.LeaveRequests.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null || !scope.InBranch(l.Employee.BranchId)) throw ApiException.NotFound("leave_not_found", "طلب الإجازة غير موجود.");
        if (l.Status != LeaveStatus.Pending) throw ApiException.Conflict("invalid_state", "سبق البتّ في الطلب.");
        if (approve && l.Type == LeaveType.Annual)
        {
            var days = l.Days; var empId = l.EmployeeId;
            var rows = await db.Employees.Where(e => e.Id == empId && e.LeaveBalanceDays >= days).ExecuteUpdateAsync(s => s.SetProperty(e => e.LeaveBalanceDays, e => e.LeaveBalanceDays - days), ct);
            if (rows == 0) throw ApiException.Conflict("insufficient_balance", "رصيد الموظف لا يكفي.");
        }
        l.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        l.DecisionReason = reason?.Trim();
        l.CoverageException = approve && coverage;
        l.DecidedByUserId = me.Id;
        l.DecidedAt = UtcNow;
        if (l.Employee.UserId is { } uid) notifications.Add(uid, NotificationType.System, approve ? "اعتُمدت إجازتك" : "رُفضت إجازتك", $"{l.From.Iso()} → {l.To.Iso()}", nameof(LeaveRequest), l.Id);
        audit.Add(approve ? "hr.leave_approve" : "hr.leave_reject", me.Id, nameof(LeaveRequest), l.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return await LeaveAsync(id, ct);
    }

    private async Task<LeaveDto> LeaveAsync(int id, CancellationToken ct) =>
        await db.LeaveRequests.AsNoTracking().Where(l => l.Id == id).Select(l => new LeaveDto(l.Id, l.EmployeeId, l.Employee.FullName, l.Type, l.From, l.To, l.Days, l.Status, l.Reason, l.DecisionReason, l.CoverageException)).FirstAsync(ct);

    // ------------------------------------------------------------ المناوبات

    public async Task<IReadOnlyList<ShiftDto>> ShiftsAsync(DateOnly? from, DateOnly? to, int? employeeId, CancellationToken ct)
    {
        var q = db.Shifts.AsNoTracking().AsQueryable();
        if (IsHr || me.Role == UserRole.Branch) { var b = scope.BranchFilter; q = q.Where(s => b == null || s.Employee.BranchId == b); if (employeeId is { } e) q = q.Where(s => s.EmployeeId == e); }
        else q = q.Where(s => s.Employee.UserId == me.Id);
        var f = from ?? clock.Today; var t = to ?? f.AddDays(30);
        if (t < f || t.DayNumber - f.DayNumber > 92) throw ApiException.Validation(nameof(to), "المدى غير صالح (حتى 92 يومًا)");
        return await q.Where(s => s.Date >= f && s.Date <= t).OrderBy(s => s.Date).ThenBy(s => s.StartMinutes).Select(s => new ShiftDto(s.Id, s.EmployeeId, s.Employee.FullName, s.Date, s.Kind, s.StartMinutes, s.EndMinutes, s.Status)).ToListAsync(ct);
    }

    public async Task<ShiftDto> CreateShiftAsync(ShiftInput r, CancellationToken ct)
    {
        scope.Require(UserRole.Hr);
        var emp = await FindEmployeeAsync(r.EmployeeId!.Value, tracked: false, ct);
        if (emp.Status is EmployeeStatus.Terminated or EmployeeStatus.OnLeave) throw ApiException.Conflict("invalid_state", "الموظف غير متاح للمناوبة.");
        var date = r.Date!.Value;
        if (await db.LeaveRequests.AnyAsync(l => l.EmployeeId == emp.Id && l.Status == LeaveStatus.Approved && l.From <= date && date <= l.To, ct)) throw ApiException.Conflict("on_leave", "الموظف في إجازة معتمدة في هذا اليوم.");
        var s = new Shift { EmployeeId = emp.Id, Date = date, Kind = r.Kind!.Value, StartMinutes = r.StartMinutes!.Value, EndMinutes = r.EndMinutes!.Value };
        db.Shifts.Add(s);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.GetBaseException() is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry }) { throw ApiException.Conflict("shift_exists", "للموظف مناوبة مماثلة في هذا اليوم."); }
        return new ShiftDto(s.Id, emp.Id, emp.FullName, s.Date, s.Kind, s.StartMinutes, s.EndMinutes, s.Status);
    }

    public async Task DeleteShiftAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Hr);
        var s = await db.Shifts.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null || !scope.InBranch(s.Employee.BranchId)) throw ApiException.NotFound("shift_not_found", "المناوبة غير موجودة.");
        db.Shifts.Remove(s);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>طلب تبديل: الموظف لمناوبته، ثم تؤكده الموارد البشرية أو تعيده.</summary>
    public async Task<ShiftDto> SwapAsync(int id, bool confirm, CancellationToken ct)
    {
        var s = await db.Shifts.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id, ct);
        var visible = s is not null && (IsHr ? scope.InBranch(s.Employee.BranchId) : s.Employee.UserId == me.Id);
        if (!visible) throw ApiException.NotFound("shift_not_found", "المناوبة غير موجودة.");
        if (confirm) { scope.Require(UserRole.Hr); s!.Status = ShiftStatus.Confirmed; }
        else
        {
            if (s!.Date < clock.Today) throw ApiException.Conflict("invalid_state", "مضت المناوبة.");
            s.Status = ShiftStatus.SwapRequested;
        }
        await db.SaveChangesAsync(ct);
        return new ShiftDto(s.Id, s.EmployeeId, s.Employee.FullName, s.Date, s.Kind, s.StartMinutes, s.EndMinutes, s.Status);
    }

    // ------------------------------------------------------------ التراخيص

    public async Task<IReadOnlyList<LicenseDto>> ExpiringLicensesAsync(int? days, CancellationToken ct)
    {
        scope.Require(UserRole.Hr, UserRole.Branch);
        var b = scope.BranchFilter;
        var today = clock.Today; var limit = today.AddDays(Math.Clamp(days ?? 90, 1, 730));
        var rows = await db.ProfessionalLicenses.AsNoTracking().Where(l => (b == null || l.Employee.BranchId == b) && l.ExpiresAt <= limit && l.Employee.Status != EmployeeStatus.Terminated)
            .OrderBy(l => l.ExpiresAt).Select(l => new { l.EmployeeId, l.Employee.FullName, l.Number, l.Authority, l.ExpiresAt }).Take(200).ToListAsync(ct);
        return rows.Select(l => new LicenseDto(l.EmployeeId, l.FullName, l.Number, l.Authority, l.ExpiresAt, l.ExpiresAt.DayNumber - today.DayNumber)).ToList();
    }

    // ------------------------------------------------------------ مساعدات

    private async Task<Employee> FindEmployeeAsync(int id, bool tracked, CancellationToken ct)
    {
        var e = await (tracked ? db.Employees : db.Employees.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is not null && scope.InBranch(e.BranchId) ? e : throw ApiException.NotFound("employee_not_found", "الموظف غير موجود.");
    }

    private async Task<List<EmployeeDto>> ToDtosAsync(List<Employee> rows, CancellationToken ct)
    {
        var ids = rows.Select(e => e.Id).ToList();
        var lic = (await db.ProfessionalLicenses.AsNoTracking().Where(l => ids.Contains(l.EmployeeId)).GroupBy(l => l.EmployeeId).Select(g => new { g.Key, Max = g.Max(x => x.ExpiresAt) }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.Max);
        return rows.Select(e => new EmployeeDto(e.Id, e.FullName, e.JobTitle, e.Phone, e.HireDate, e.Status, e.BaseSalary, e.LeaveBalanceDays, e.BranchId, e.DepartmentId, e.UserId, lic.TryGetValue(e.Id, out var x) ? x : null)).ToList();
    }
}
