using System.Globalization;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Operations;

internal static class BackOffice
{
    public static string D(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static async Task<int> EmployeeAsync(this Clinic c, string hr, string name = "موظف اختبار", decimal salary = 8000, int? userId = null) =>
        (await c.Http.PostAsync("/api/hr/employees", new { fullName = name, jobTitle = "ممرض", hireDate = D(Clinic.Today.AddYears(-1)), baseSalary = salary, userId }, hr))["id"].GetInt32();
}

[Collection(ApiCollection.Name)]
public class HrTests(ApiFactory root)
{
    [Fact]
    public async Task Employees_are_branch_scoped_and_termination_disables_the_login()
    {
        var c = await Clinic.CreateAsync(root);
        var hr = await c.TokenAsync(UserRole.Hr);
        var nurseUser = await TestUsers.CreateAsync(c.Factory, UserRole.Nurse, branchId: c.BranchId);
        var id = await c.EmployeeAsync(hr, userId: nurseUser.Id);

        Assert.Equal("user_already_employee", (await c.Http.PostAsync("/api/hr/employees", new { fullName = "مكرر", jobTitle = "ممرض", hireDate = BackOffice.D(Clinic.Today), userId = nurseUser.Id }, hr)).Code);
        Assert.Equal(400, (await c.Http.PostAsync("/api/hr/employees", new { fullName = "ن", jobTitle = "x" }, hr)).Status);
        var foreign = await c.TokenAsync(UserRole.Hr, branchId: await c.OtherBranchAsync());
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/hr/employees/{id}", foreign)).Status);
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/hr/employees", foreign))["total"].GetInt32());
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/hr/employees", await c.TokenAsync(UserRole.Nurse))).Status);

        var login = await c.Http.PostAsync("/api/auth/login", new { identifier = nurseUser.Email, password = TestUsers.Password });
        Assert.Equal(200, login.Status);
        Assert.Equal("Terminated", (await c.Http.PostAsync($"/api/hr/employees/{id}/status", new { status = "Terminated" }, hr)).Str("status"));
        Assert.NotEqual(200, (await c.Http.PostAsync("/api/auth/login", new { identifier = nurseUser.Email, password = TestUsers.Password })).Status);
        Assert.Equal(0, await c.WithDb(db => db.RefreshTokens.CountAsync(t => t.UserId == nurseUser.Id && t.RevokedAt == null)));
    }

    [Fact]
    public async Task Leave_request_overlap_balance_and_approval_deduct_exactly_once()
    {
        var c = await Clinic.CreateAsync(root);
        var hr = await c.TokenAsync(UserRole.Hr);
        var id = await c.EmployeeAsync(hr);
        string Leave(int from, int days, string type = "Annual") => $"{{\"employeeId\":{id},\"type\":\"{type}\",\"from\":\"{BackOffice.D(Clinic.Today.AddDays(from))}\",\"to\":\"{BackOffice.D(Clinic.Today.AddDays(from + days - 1))}\"}}";
        async Task<ClaudCare.Api.Tests.Infrastructure.ApiResponse> Post(string json) => await c.Http.PostAsync("/api/hr/leaves", System.Text.Json.JsonDocument.Parse(json).RootElement, hr);

        var l1 = await Post(Leave(10, 5));
        Assert.Equal(201, l1.Status);
        Assert.Equal(5, l1["days"].GetInt32());
        Assert.Equal("leave_overlap", (await Post(Leave(12, 3))).Code);
        Assert.Equal("insufficient_balance", (await Post(Leave(30, 25))).Code); // الرصيد 21
        Assert.Equal(400, (await c.Http.PostAsync("/api/hr/leaves", new { employeeId = id, type = "Annual", from = BackOffice.D(Clinic.Today.AddDays(5)), to = BackOffice.D(Clinic.Today.AddDays(2)) }, hr)).Status);

        var lid = l1["id"].GetInt32();
        Assert.Equal(400, (await c.Http.PostAsync($"/api/hr/leaves/{lid}/reject", new { }, hr)).Status); // السبب إلزامي
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => c.Http.PostAsync($"/api/hr/leaves/{lid}/approve", new { }, hr)));
        Assert.Single(results, r => r.Status == 200);
        Assert.Equal(16, await c.WithDb(async db => (await db.Employees.AsNoTracking().FirstAsync(e => e.Id == id)).LeaveBalanceDays));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/hr/leaves/{lid}/reject", new { reason = "x y" }, hr)).Status);

        var sick = (await Post(Leave(40, 3, "Sick")))["id"].GetInt32();
        await c.Http.PostAsync($"/api/hr/leaves/{sick}/approve", new { }, hr);
        Assert.Equal(16, await c.WithDb(async db => (await db.Employees.AsNoTracking().FirstAsync(e => e.Id == id)).LeaveBalanceDays)); // المرضية لا تخصم
    }

    [Fact]
    public async Task A_staff_member_requests_only_their_own_leave_and_sees_only_their_own()
    {
        var c = await Clinic.CreateAsync(root);
        var hr = await c.TokenAsync(UserRole.Hr);
        var u1 = await TestUsers.CreateAsync(c.Factory, UserRole.Nurse, branchId: c.BranchId);
        var u2 = await TestUsers.CreateAsync(c.Factory, UserRole.Nurse, branchId: c.BranchId);
        var e1 = await c.EmployeeAsync(hr, "موظفة أولى", userId: u1.Id); var e2 = await c.EmployeeAsync(hr, "موظفة ثانية", userId: u2.Id);
        var t1 = (await TestUsers.LoginAsync(c.Http, u1.Email)).Access;
        var body = new { type = "Annual", from = BackOffice.D(Clinic.Today.AddDays(3)), to = BackOffice.D(Clinic.Today.AddDays(4)) };

        Assert.Equal(201, (await c.Http.PostAsync("/api/hr/leaves", body, t1)).Status);
        Assert.Equal(403, (await c.Http.PostAsync("/api/hr/leaves", new { employeeId = e2, body.type, body.from, body.to }, t1)).Status);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/hr/leaves", t1)).Body.GetArrayLength());
        Assert.Equal(404, (await c.Http.PostAsync($"/api/hr/leaves/{(await c.Http.GetApiAsync("/api/hr/leaves", hr)).Body[0].GetProperty("id").GetInt32()}/approve", new { }, t1)).Status == 403 ? 404 : 0); // الموظف لا يعتمد
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/hr/leaves?status=Pending", hr)).Body.GetArrayLength());
        Assert.Equal(e1, (await c.Http.GetApiAsync("/api/hr/leaves", hr)).Body[0].GetProperty("employeeId").GetInt32());
        Assert.Contains("اعتُمدت إجازتك", await ApproveAndTitles(c, hr, t1, u1.Id));
    }

    private static async Task<List<string>> ApproveAndTitles(Clinic c, string hr, string staff, int userId)
    {
        var id = (await c.Http.GetApiAsync("/api/hr/leaves", hr)).Body[0].GetProperty("id").GetInt32();
        await c.Http.PostAsync($"/api/hr/leaves/{id}/approve", new { coverageException = true }, hr);
        return await c.NotificationTitlesAsync(userId);
    }

    [Fact]
    public async Task Shifts_are_unique_blocked_during_leave_and_swap_requests_need_hr_confirmation()
    {
        var c = await Clinic.CreateAsync(root);
        var hr = await c.TokenAsync(UserRole.Hr);
        var u = await TestUsers.CreateAsync(c.Factory, UserRole.Nurse, branchId: c.BranchId);
        var emp = await c.EmployeeAsync(hr, userId: u.Id);
        var ut = (await TestUsers.LoginAsync(c.Http, u.Email)).Access;
        object Shift(int day, string kind = "Morning") => new { employeeId = emp, date = BackOffice.D(Clinic.Today.AddDays(day)), kind, startMinutes = 420, endMinutes = 900 };

        var s = await c.Http.PostAsync("/api/hr/shifts", Shift(2), hr);
        Assert.Equal(201, s.Status);
        Assert.Equal("shift_exists", (await c.Http.PostAsync("/api/hr/shifts", Shift(2), hr)).Code);
        Assert.Equal(201, (await c.Http.PostAsync("/api/hr/shifts", Shift(2, "Night"), hr)).Status);

        var leave = (await c.Http.PostAsync("/api/hr/leaves", new { employeeId = emp, type = "Annual", from = BackOffice.D(Clinic.Today.AddDays(5)), to = BackOffice.D(Clinic.Today.AddDays(6)) }, hr))["id"].GetInt32();
        await c.Http.PostAsync($"/api/hr/leaves/{leave}/approve", new { }, hr);
        Assert.Equal("on_leave", (await c.Http.PostAsync("/api/hr/shifts", Shift(5), hr)).Code);

        var sid = s["id"].GetInt32();
        Assert.Equal(2, (await c.Http.GetApiAsync("/api/hr/shifts", ut)).Body.GetArrayLength()); // يرى مناوباته
        Assert.Equal("SwapRequested", (await c.Http.PostAsync($"/api/hr/shifts/{sid}/request-swap", null, ut)).Str("status"));
        Assert.Equal(403, (await c.Http.PostAsync($"/api/hr/shifts/{sid}/confirm", null, ut)).Status);
        Assert.Equal("Confirmed", (await c.Http.PostAsync($"/api/hr/shifts/{sid}/confirm", null, hr)).Str("status"));
        Assert.Equal(403, (await c.Http.PostAsync("/api/hr/shifts", Shift(3), ut)).Status);
        Assert.Equal(204, (await c.Http.DeleteAsync($"/api/hr/shifts/{sid}", hr)).Status);
        Assert.Equal(400, (await c.Http.GetApiAsync("/api/hr/shifts?from=2026-01-01&to=2026-12-31", hr)).Status);
    }

    [Fact]
    public async Task Expiring_licenses_list_is_sorted_with_days_left()
    {
        var c = await Clinic.CreateAsync(root);
        var hr = await c.TokenAsync(UserRole.Hr);
        var soon = await c.EmployeeAsync(hr, "ترخيص قريب"); var far = await c.EmployeeAsync(hr, "ترخيص بعيد");
        await c.WithDb(async db =>
        {
            db.ProfessionalLicenses.AddRange(new ProfessionalLicense { EmployeeId = soon, Number = "L-1", ExpiresAt = Clinic.Today.AddDays(20) }, new ProfessionalLicense { EmployeeId = far, Number = "L-2", ExpiresAt = Clinic.Today.AddDays(400) });
            await db.SaveChangesAsync(); return 0;
        });
        var r = await c.Http.GetApiAsync("/api/hr/licenses/expiring?days=90", hr);
        Assert.Equal(20, r.Body.EnumerateArray().First(x => x.GetProperty("employeeId").GetInt32() == soon).GetProperty("daysLeft").GetInt32());
        Assert.DoesNotContain(r.Body.EnumerateArray(), x => x.GetProperty("employeeId").GetInt32() == far);
    }
}

[Collection(ApiCollection.Name)]
public class FinanceTests(ApiFactory root)
{
    [Fact]
    public async Task Expense_flow_with_the_finance_approval_limit()
    {
        var c = await Clinic.CreateAsync(root);
        var fin = await c.TokenAsync(UserRole.Finance);
        var branch = await c.TokenAsync(UserRole.Branch);
        object Exp(decimal amount) => new { title = "صيانة أجهزة", category = "صيانة", amount, expenseDate = BackOffice.D(Clinic.Today) };

        var small = await c.Http.PostAsync("/api/finance/expenses", Exp(2500), branch);
        Assert.Equal(201, small.Status);
        Assert.StartsWith("EXP-", small.Str("no"));
        var id = small["id"].GetInt32();
        Assert.Equal(409, (await c.Http.PostAsync($"/api/finance/expenses/{id}/pay", null, fin)).Status); // لم يُعتمد
        Assert.Equal(403, (await c.Http.PostAsync($"/api/finance/expenses/{id}/approve", null, branch)).Status);
        Assert.Equal("Approved", (await c.Http.PostAsync($"/api/finance/expenses/{id}/approve", null, fin)).Str("status"));
        Assert.Equal("Paid", (await c.Http.PostAsync($"/api/finance/expenses/{id}/pay", null, fin)).Str("status"));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/finance/expenses/{id}/approve", null, fin)).Status);

        var big = (await c.Http.PostAsync("/api/finance/expenses", Exp(25_000), branch))["id"].GetInt32();
        Assert.Equal("approval_limit_exceeded", (await c.Http.PostAsync($"/api/finance/expenses/{big}/approve", null, fin)).Code);
        Assert.Equal("Approved", (await c.Http.PostAsync($"/api/finance/expenses/{big}/approve", null, await c.TokenAsync(UserRole.Hospital))).Str("status"));

        var rej = (await c.Http.PostAsync("/api/finance/expenses", Exp(100), branch))["id"].GetInt32();
        Assert.Equal(400, (await c.Http.PostAsync($"/api/finance/expenses/{rej}/reject", new { }, fin)).Status);
        Assert.Equal("Rejected", (await c.Http.PostAsync($"/api/finance/expenses/{rej}/reject", new { reason = "غير مبرر" }, fin)).Str("status"));
        Assert.Equal(400, (await c.Http.PostAsync("/api/finance/expenses", Exp(0), branch)).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/finance/expenses", await c.TokenAsync(UserRole.Nurse))).Status);
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/finance/expenses", await c.TokenAsync(UserRole.Finance, branchId: await c.OtherBranchAsync())))["total"].GetInt32());
    }

    [Fact]
    public async Task Payroll_generates_once_adjusts_only_drafts_and_pays_through_the_approval_cycle()
    {
        var c = await Clinic.CreateAsync(root);
        var hr = await c.TokenAsync(UserRole.Hr); var fin = await c.TokenAsync(UserRole.Finance);
        var e1 = await c.EmployeeAsync(hr, "موظف أ", 8000); var e2 = await c.EmployeeAsync(hr, "موظف ب", 6000);
        var terminated = await c.EmployeeAsync(hr, "موظف ج", 5000);
        await c.Http.PostAsync($"/api/hr/employees/{terminated}/status", new { status = "Terminated" }, hr);

        var gen = await c.Http.PostAsync("/api/finance/payroll/generate", new { period = "2026-10" }, fin);
        Assert.Equal(2, gen.Body.GetArrayLength());
        var again = await c.Http.PostAsync("/api/finance/payroll/generate", new { period = "2026-10" }, fin);
        Assert.Equal(2, again.Body.GetArrayLength()); // بلا تكرار
        Assert.Equal(400, (await c.Http.PostAsync("/api/finance/payroll/generate", new { period = "2026-13" }, fin)).Status);

        var rec = (await c.Http.GetApiAsync("/api/finance/payroll?period=2026-10", fin)).Body.EnumerateArray().First(x => x.GetProperty("employeeId").GetInt32() == e1);
        var id = rec.GetProperty("id").GetInt32();
        Assert.Equal(8300m, (await c.Http.PutAsync($"/api/finance/payroll/{id}", new { extras = 500, deductions = 200 }, fin))["net"].GetDecimal());
        Assert.Equal(400, (await c.Http.PutAsync($"/api/finance/payroll/{id}", new { extras = 0, deductions = 9000 }, fin)).Status);
        Assert.Equal(409, (await c.Http.PostAsync($"/api/finance/payroll/{id}/pay", null, fin)).Status); // لم يُرسل
        Assert.Equal("PendingApproval", (await c.Http.PostAsync($"/api/finance/payroll/{id}/submit", null, hr)).Str("status"));
        Assert.Equal(409, (await c.Http.PutAsync($"/api/finance/payroll/{id}", new { extras = 1, deductions = 0 }, fin)).Status);
        Assert.Equal(403, (await c.Http.PostAsync($"/api/finance/payroll/{id}/pay", null, hr)).Status); // الموارد البشرية لا تصرف
        var paid = await c.Http.PostAsync($"/api/finance/payroll/{id}/pay", null, fin);
        Assert.Equal("Paid", paid.Str("status"));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/finance/payroll/{id}/pay", null, fin)).Status);
    }

    [Fact]
    public async Task Daily_close_expects_the_actual_payments_requires_a_note_on_difference_and_is_once_per_day()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 300);
        var p = await c.PatientAsync();
        var desk = await c.TokenAsync(UserRole.Reception);
        var fin = await c.TokenAsync(UserRole.Finance);
        await c.BookAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H10, payment: "PayAtClinic");
        await c.BookAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H11, payment: "Card"); // بطاقة 300
        var inv = (await c.Http.GetApiAsync("/api/invoices?status=Unpaid", p.Token))["page"].J("items")[0].GetProperty("id").GetInt32();
        await c.Http.PostAsync($"/api/invoices/{inv}/pay", new { method = "Cash", amount = 120 }, desk);

        var preview = await c.Http.GetApiAsync($"/api/finance/daily-close/preview?date={BackOffice.D(Clinic.Today)}", fin);
        Assert.True(preview.Status == 200, preview.Body.ToString());
        Assert.Equal(120m, preview["cashExpected"].GetDecimal());
        Assert.Equal(300m, preview["cardExpected"].GetDecimal());
        Assert.False(preview["closed"].GetBoolean());

        Assert.Equal(400, (await c.Http.PostAsync("/api/finance/daily-close", new { date = BackOffice.D(Clinic.Today), cashCounted = 100, cardCounted = 300 }, fin)).Status); // فرق بلا سبب
        Assert.Equal(400, (await c.Http.PostAsync("/api/finance/daily-close", new { date = BackOffice.D(Clinic.Wed), cashCounted = 0, cardCounted = 0 }, fin)).Status); // مستقبلي
        var ok = await c.Http.PostAsync("/api/finance/daily-close", new { date = BackOffice.D(Clinic.Today), cashCounted = 100, cardCounted = 300, note = "نقص 20 ر.س" }, fin);
        Assert.Equal(201, ok.Status);
        Assert.Equal(-20m, ok["difference"].GetDecimal());
        Assert.Equal("already_closed", (await c.Http.PostAsync("/api/finance/daily-close", new { date = BackOffice.D(Clinic.Today), cashCounted = 120, cardCounted = 300 }, fin)).Code);
        Assert.True((await c.Http.GetApiAsync($"/api/finance/daily-close/preview?date={BackOffice.D(Clinic.Today)}", fin))["closed"].GetBoolean());
        Assert.Equal(403, (await c.Http.PostAsync("/api/finance/daily-close", new { date = BackOffice.D(Clinic.Today), cashCounted = 0, cardCounted = 0 }, desk)).Status);
    }

    [Fact]
    public async Task Summary_aggregates_revenue_refunds_expenses_and_outstanding()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 300); var p = await c.PatientAsync();
        var fin = await c.TokenAsync(UserRole.Finance);
        var paid = await c.BookOkAsync(p.Token, doc.Id, Clinic.Thu, Clinic.H10);
        await c.Act(p.Token, paid, "cancel", new { reason = "ظرف طارئ" }); // استرداد 300
        await c.BookAsync(p.Token, doc.Id, Clinic.Thu, Clinic.H11, payment: "PayAtClinic");
        await c.BookOkAsync(p.Token, doc.Id, Clinic.Thu, Clinic.H09);

        var s = await c.Http.GetApiAsync($"/api/finance/summary?from={BackOffice.D(Clinic.Today)}&to={BackOffice.D(Clinic.Today)}", fin);
        Assert.Equal(600m, s["revenue"].GetDecimal());
        Assert.Equal(300m, s["refunds"].GetDecimal());
        Assert.Equal(300m, s["outstandingInvoices"].GetDecimal());
        Assert.Equal(400, (await c.Http.GetApiAsync("/api/finance/summary?from=2026-12-01&to=2026-01-01", fin)).Status);
    }
}

[Collection(ApiCollection.Name)]
public class InsuranceOpsTests(ApiFactory root)
{
    [Fact]
    public async Task Pre_auth_requires_a_policy_documents_and_valid_decisions()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(); var dt = await c.DoctorTokenAsync(doc);
        var p = await c.PatientAsync();
        var ins = await c.TokenAsync(UserRole.Insurance);
        object Req(int pid) => new { patientId = pid, procedureName = "رنين مغناطيسي", diagnosis = "ألم أسفل الظهر", amount = 2500, documents = new[] { "تقرير الطبيب", "صورة الهوية" } };

        Assert.Equal("no_active_policy", (await c.Http.PostAsync("/api/insurance/pre-auths", Req(p.Id), dt)).Code);
        await c.InsureAsync(p.Id);
        var pa = await c.Http.PostAsync("/api/insurance/pre-auths", Req(p.Id), dt);
        Assert.Equal(201, pa.Status);
        Assert.StartsWith("PA-", pa.Str("no"));
        var id = pa["id"].GetInt32(); var docs = pa["documents"].EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();

        Assert.Equal(1, (await c.Http.GetApiAsync("/api/insurance/pre-auths", dt))["total"].GetInt32());
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/insurance/pre-auths", await c.DoctorTokenAsync(await c.DoctorAsync())))["total"].GetInt32());
        Assert.Equal(403, (await c.Http.PostAsync($"/api/insurance/pre-auths/{id}/decide", new { decision = "Approved" }, dt)).Status);

        Assert.Equal("documents_missing", (await c.Http.PostAsync($"/api/insurance/pre-auths/{id}/decide", new { decision = "Approved" }, ins)).Code);
        foreach (var d in docs) await c.Http.PostAsync($"/api/insurance/pre-auths/{id}/documents/{d}/receive", null, ins);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/insurance/pre-auths/{id}/decide", new { decision = "Approved", approvedAmount = 9999 }, ins)).Status);
        var ok = await c.Http.PostAsync($"/api/insurance/pre-auths/{id}/decide", new { decision = "Approved", approvedAmount = 2000 }, ins);
        Assert.Equal("Approved", ok.Str("status"));
        Assert.Equal(2000m, ok["approvedAmount"].GetDecimal());
        Assert.Contains("اعتُمدت الموافقة المسبقة", await c.NotificationTitlesAsync(p.Id));
        Assert.Contains("اعتُمدت الموافقة المسبقة", await c.NotificationTitlesAsync(doc.Id));
        Assert.Equal(409, (await c.Http.PostAsync($"/api/insurance/pre-auths/{id}/decide", new { decision = "Rejected", rejectReason = "تغيير" }, ins)).Status);
    }

    [Fact]
    public async Task Reject_needs_a_reason_and_more_info_can_be_decided_again()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync(); await c.InsureAsync(p.Id);
        var ins = await c.TokenAsync(UserRole.Insurance);
        var id = (await c.Http.PostAsync("/api/insurance/pre-auths", new { patientId = p.Id, procedureName = "عملية", amount = 1000 }, ins))["id"].GetInt32();
        Assert.Equal(400, (await c.Http.PostAsync($"/api/insurance/pre-auths/{id}/decide", new { decision = "Rejected" }, ins)).Status);
        Assert.Equal("MoreInfoNeeded", (await c.Http.PostAsync($"/api/insurance/pre-auths/{id}/decide", new { decision = "MoreInfoNeeded" }, ins)).Str("status"));
        var rej = await c.Http.PostAsync($"/api/insurance/pre-auths/{id}/decide", new { decision = "Rejected", rejectReason = "خارج التغطية" }, ins);
        Assert.Equal("خارج التغطية", rej.Str("rejectReason"));
    }

    [Fact]
    public async Task Claims_are_built_from_insured_invoices_and_settled_without_overpaying()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync(price: 500); var p = await c.PatientAsync();
        await c.InsureAsync(p.Id, copay: 0.2m); // تغطية 400
        await c.BookOkAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H10);
        var invoiceId = (await c.Http.GetApiAsync("/api/invoices", p.Token))["page"].J("items")[0].GetProperty("id").GetInt32();
        var ins = await c.TokenAsync(UserRole.Insurance);

        var claim = await c.Http.PostAsync($"/api/insurance/claims/from-invoice/{invoiceId}", null, ins);
        Assert.Equal(201, claim.Status);
        Assert.Equal(400m, claim["amount"].GetDecimal());
        var id = claim["id"].GetInt32();
        Assert.Equal("claim_exists", (await c.Http.PostAsync($"/api/insurance/claims/from-invoice/{invoiceId}", null, ins)).Code);
        Assert.Equal(409, (await c.Http.PostAsync($"/api/insurance/claims/{id}/settle", new { paidAmount = 100 }, ins)).Status); // لم تُقدَّم
        Assert.Equal("Submitted", (await c.Http.PostAsync($"/api/insurance/claims/{id}/submit", null, ins)).Str("status"));
        Assert.Equal("PartiallyPaid", (await c.Http.PostAsync($"/api/insurance/claims/{id}/settle", new { paidAmount = 150 }, ins)).Str("status"));
        Assert.Equal(400, (await c.Http.PostAsync($"/api/insurance/claims/{id}/settle", new { paidAmount = 300 }, ins)).Status);
        var done = await c.Http.PostAsync($"/api/insurance/claims/{id}/settle", new { paidAmount = 250 }, ins);
        Assert.Equal("Paid", done.Str("status"));
        Assert.Equal(400m, done["paidAmount"].GetDecimal());
        Assert.Equal(409, (await c.Http.PostAsync($"/api/insurance/claims/{id}/reject", new { reason = "خطأ" }, ins)).Status);

        var plain = await c.PatientAsync(); await c.BookOkAsync(plain.Token, doc.Id, Clinic.Wed, Clinic.H11);
        var plainInv = (await c.Http.GetApiAsync("/api/invoices", plain.Token))["page"].J("items")[0].GetProperty("id").GetInt32();
        Assert.Equal("not_claimable", (await c.Http.PostAsync($"/api/insurance/claims/from-invoice/{plainInv}", null, ins)).Code);
        Assert.Equal(403, (await c.Http.PostAsync($"/api/insurance/claims/from-invoice/{invoiceId}", null, p.Token)).Status);
    }

    [Fact]
    public async Task Policy_lookup_is_for_desk_roles()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync(); await c.InsureAsync(p.Id, expires: Clinic.Today.AddDays(-1));
        var r = await c.Http.GetApiAsync($"/api/insurance/policies?patientId={p.Id}", await c.TokenAsync(UserRole.Reception));
        Assert.True(r.Body[0].GetProperty("expired").GetBoolean());
        Assert.Equal(403, (await c.Http.GetApiAsync($"/api/insurance/policies?patientId={p.Id}", p.Token)).Status);
        Assert.True((await c.Http.GetApiAsync("/api/insurance/companies", await c.TokenAsync(UserRole.Insurance))).Body.GetArrayLength() > 0);
    }
}

[Collection(ApiCollection.Name)]
public class DashboardTests(ApiFactory root)
{
    [Fact]
    public async Task Overview_aggregates_a_branch_and_is_forbidden_to_clinical_roles()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        await c.StockAsync(k.DrugId, quantity: 2, min: 5);
        var branch = await c.TokenAsync(UserRole.Branch);
        await c.Http.PostAsync("/api/emergency/cases", new { patientName = "طارئ", complaint = "ألم", triageLevel = 2, zone = "أحمر" }, await c.TokenAsync(UserRole.Emergency));

        var o = await c.Http.GetApiAsync("/api/dashboard/overview", branch);
        Assert.Equal(200, o.Status);
        Assert.Equal(1, o["labPending"].GetInt32());
        Assert.Equal(1, o["imagingPending"].GetInt32());
        Assert.Equal(1, o["prescriptionsPending"].GetInt32());
        Assert.Equal(1, o["lowStock"].GetInt32());
        Assert.Equal(1, o["emergencyWaiting"].GetInt32());
        Assert.Equal(7, o["last7Days"].GetArrayLength());
        Assert.Equal(1, o["appointmentsToday"].J("Done").GetInt32() + 0 * 0 + (o["appointmentsToday"].TryGetProperty("InProgress", out var ip) ? ip.GetInt32() : 0));
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/dashboard/overview", await c.TokenAsync(UserRole.Pharmacy))).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/dashboard/overview", k.Patient.Token)).Status);
    }

    [Fact]
    public async Task Admin_can_filter_by_branch_and_a_foreign_branch_manager_sees_zero()
    {
        var c = await Clinic.CreateAsync(root);
        await c.ConsultAsync();
        var admin = await c.TokenAsync(UserRole.Admin);
        var other = await c.OtherBranchAsync();
        Assert.Equal(1, (await c.Http.GetApiAsync($"/api/dashboard/overview?branchId={c.BranchId}", admin))["labPending"].GetInt32());
        Assert.Equal(0, (await c.Http.GetApiAsync($"/api/dashboard/overview?branchId={other}", admin))["labPending"].GetInt32());
        // مدير فرع آخر يتجاهل branchId ويرى فرعه
        var foreign = await c.TokenAsync(UserRole.Branch, branchId: other);
        Assert.Equal(0, (await c.Http.GetApiAsync($"/api/dashboard/overview?branchId={c.BranchId}", foreign))["labPending"].GetInt32());
    }

    [Fact]
    public async Task Me_returns_counters_tailored_to_the_role()
    {
        var c = await Clinic.CreateAsync(root);
        var k = await c.ConsultAsync();
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/dashboard/me", await c.TokenAsync(UserRole.Lab)))["pendingOrders"].GetInt32());
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/dashboard/me", await c.TokenAsync(UserRole.Pharmacy)))["pendingPrescriptions"].GetInt32());
        var patient = await c.Http.GetApiAsync("/api/dashboard/me", k.Patient.Token);
        Assert.True(patient.Body.TryGetProperty("upcomingAppointments", out _));
        Assert.True(patient.Body.TryGetProperty("unreadNotifications", out _));
        Assert.True((await c.Http.GetApiAsync("/api/dashboard/me", await c.DoctorTokenAsync(k.Doctor))).Body.TryGetProperty("todayAppointments", out _));
        Assert.Equal(401, (await c.Http.GetApiAsync("/api/dashboard/me")).Status);
    }
}
