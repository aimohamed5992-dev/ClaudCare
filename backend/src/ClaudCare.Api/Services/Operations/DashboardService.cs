using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Operations;

/// <summary>
/// لوحات المؤشرات. النظرة العامة للإدارة العليا (كل الفروع أو فرع محدد) ولمدير الفرع (فرعه فقط)،
/// ولوحتي «me» أعداد سريعة مخصصة لدور المستخدم.
/// </summary>
public sealed class DashboardService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, IClinicClock clock)
{
    public async Task<OverviewDto> OverviewAsync(int? branchId, CancellationToken ct)
    {
        scope.Require(UserRole.Branch, UserRole.Finance);
        var b = scope.IsGlobal ? branchId : scope.BranchFilter;
        var today = clock.Today;
        var from = clock.DayStartUtc(today); var to = clock.DayStartUtc(today.AddDays(1));

        var appts = await db.Appointments.AsNoTracking().Where(a => (b == null || a.BranchId == b) && a.Date == today).GroupBy(a => a.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var revenueToday = await db.Payments.AsNoTracking().Where(p => (b == null || p.Invoice.BranchId == b) && p.PaidAt >= from && p.PaidAt < to).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var outstanding = await db.Invoices.AsNoTracking().Where(i => (b == null || i.BranchId == b) && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid)).SumAsync(i => (decimal?)(i.Subtotal - i.Discount - i.InsuranceCovered - i.PaidAmount), ct) ?? 0;
        var beds = await db.Beds.AsNoTracking().Where(x => b == null || x.BranchId == b).GroupBy(x => x.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);

        var days = Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6)).ToList();
        var series = new List<DayPointDto>();
        foreach (var d in days)
        {
            var f = clock.DayStartUtc(d); var t = clock.DayStartUtc(d.AddDays(1));
            var n = await db.Appointments.AsNoTracking().CountAsync(a => (b == null || a.BranchId == b) && a.Date == d && a.Status != AppointmentStatus.Cancelled, ct);
            var rev = await db.Payments.AsNoTracking().Where(p => (b == null || p.Invoice.BranchId == b) && p.PaidAt >= f && p.PaidAt < t).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
            series.Add(new DayPointDto(d, n, rev));
        }

        return new OverviewDto(b, today, appts.ToDictionary(x => x.Key.ToString(), x => x.N), revenueToday, outstanding,
            beds.Sum(x => x.N), beds.FirstOrDefault(x => x.Key == BedStatus.Occupied)?.N ?? 0,
            await db.EmergencyCases.CountAsync(c => (b == null || c.BranchId == b) && c.Status == EmergencyStatus.Waiting, ct),
            await db.LabOrders.CountAsync(o => (b == null || o.BranchId == b) && (o.Status == LabOrderStatus.Ordered || o.Status == LabOrderStatus.SampleCollected || o.Status == LabOrderStatus.InProgress), ct),
            await db.ImagingOrders.CountAsync(o => (b == null || o.BranchId == b) && (o.Status == ImagingOrderStatus.Ordered || o.Status == ImagingOrderStatus.Scheduled || o.Status == ImagingOrderStatus.InProgress), ct),
            await db.Prescriptions.CountAsync(r => (b == null || r.BranchId == b) && (r.Status == PrescriptionStatus.Pending || r.Status == PrescriptionStatus.Preparing), ct),
            await db.InventoryItems.CountAsync(i => (b == null || i.BranchId == b) && i.Quantity <= i.MinQuantity, ct),
            await db.Expenses.CountAsync(e => (b == null || e.BranchId == b) && e.Status == ExpenseStatus.Pending, ct),
            await db.PreAuthorizations.CountAsync(p => p.Status == ApprovalStatus.Pending || p.Status == ApprovalStatus.MoreInfoNeeded, ct),
            await db.LeaveRequests.CountAsync(l => (b == null || l.Employee.BranchId == b) && l.Status == LeaveStatus.Pending, ct),
            await db.SurgeryCases.CountAsync(s => (b == null || s.Room.BranchId == b) && s.Date == today && s.Status != SurgeryStatus.Cancelled, ct),
            series);
    }

    /// <summary>أعداد سريعة لدور المستخدم الحالي لشاشته الرئيسية.</summary>
    public async Task<IReadOnlyDictionary<string, int>> MineAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var r = new Dictionary<string, int>();
        var branch = me.BranchId;
        r["unreadNotifications"] = await db.Notifications.CountAsync(n => n.UserId == me.Id && n.ReadAt == null && n.DeletedAt == null, ct);
        r["unreadMessages"] = await db.ConversationParticipants.Where(p => p.UserId == me.Id).SumAsync(p => p.UnreadCount, ct);
        switch (me.Role)
        {
            case UserRole.Patient:
                r["upcomingAppointments"] = await db.Appointments.CountAsync(a => a.PatientId == me.Id && a.Date >= today && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed), ct);
                r["unpaidInvoices"] = await db.Invoices.CountAsync(i => i.PatientId == me.Id && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid), ct);
                r["readyResults"] = await db.LabOrders.CountAsync(o => o.PatientId == me.Id && o.Status == LabOrderStatus.ResultReady, ct);
                break;
            case UserRole.Doctor:
                r["todayAppointments"] = await db.Appointments.CountAsync(a => a.DoctorId == me.Id && a.Date == today && a.Status != AppointmentStatus.Cancelled, ct);
                r["pendingConfirmations"] = await db.Appointments.CountAsync(a => a.DoctorId == me.Id && a.Status == AppointmentStatus.Pending, ct);
                r["pendingLabResults"] = await db.LabOrders.CountAsync(o => o.DoctorId == me.Id && o.Status != LabOrderStatus.ResultReady && o.Status != LabOrderStatus.Cancelled, ct);
                break;
            case UserRole.Lab:
                r["pendingOrders"] = await db.LabOrders.CountAsync(o => o.BranchId == branch && (o.Status == LabOrderStatus.Ordered || o.Status == LabOrderStatus.SampleCollected || o.Status == LabOrderStatus.InProgress), ct);
                r["urgent"] = await db.LabOrders.CountAsync(o => o.BranchId == branch && o.Priority == OrderPriority.Urgent && o.Status != LabOrderStatus.ResultReady && o.Status != LabOrderStatus.Cancelled, ct);
                break;
            case UserRole.Radiology:
                r["pendingOrders"] = await db.ImagingOrders.CountAsync(o => o.BranchId == branch && (o.Status == ImagingOrderStatus.Ordered || o.Status == ImagingOrderStatus.Scheduled || o.Status == ImagingOrderStatus.InProgress), ct);
                break;
            case UserRole.Pharmacy:
                r["pendingPrescriptions"] = await db.Prescriptions.CountAsync(x => x.BranchId == branch && (x.Status == PrescriptionStatus.Pending || x.Status == PrescriptionStatus.Preparing), ct);
                r["lowStock"] = await db.InventoryItems.CountAsync(i => i.BranchId == branch && i.Quantity <= i.MinQuantity, ct);
                break;
            case UserRole.Nurse:
                var now = DateTime.UtcNow;
                r["dosesDue"] = await db.MedicationAdministrations.CountAsync(m => m.Admission.BranchId == branch && (m.Status == DoseStatus.Due || m.Status == DoseStatus.Delayed) && m.DueAt <= now.AddHours(1), ct);
                r["admitted"] = await db.Admissions.CountAsync(a => a.BranchId == branch && a.Status == AdmissionStatus.Admitted, ct);
                break;
            case UserRole.Reception:
                r["todayAppointments"] = await db.Appointments.CountAsync(a => a.BranchId == branch && a.Date == today && a.Status != AppointmentStatus.Cancelled, ct);
                r["queueWaiting"] = await db.QueueTickets.CountAsync(t => t.BranchId == branch && t.Date == today && t.Status == QueueStatus.Waiting, ct);
                break;
            case UserRole.Insurance:
                r["pendingPreAuths"] = await db.PreAuthorizations.CountAsync(p => p.Status == ApprovalStatus.Pending || p.Status == ApprovalStatus.MoreInfoNeeded, ct);
                r["openClaims"] = await db.InsuranceClaims.CountAsync(c => c.Status == ClaimStatus.Draft || c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.PartiallyPaid, ct);
                break;
            case UserRole.Finance:
                r["pendingExpenses"] = await db.Expenses.CountAsync(e => e.BranchId == branch && e.Status == ExpenseStatus.Pending, ct);
                r["unpaidInvoices"] = await db.Invoices.CountAsync(i => i.BranchId == branch && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.PartiallyPaid), ct);
                break;
            case UserRole.Hr:
                r["pendingLeaves"] = await db.LeaveRequests.CountAsync(l => l.Employee.BranchId == branch && l.Status == LeaveStatus.Pending, ct);
                r["expiringLicenses"] = await db.ProfessionalLicenses.CountAsync(l => l.Employee.BranchId == branch && l.ExpiresAt <= today.AddDays(90), ct);
                break;
            case UserRole.Emergency:
                r["waiting"] = await db.EmergencyCases.CountAsync(c => c.BranchId == branch && c.Status == EmergencyStatus.Waiting, ct);
                r["critical"] = await db.EmergencyCases.CountAsync(c => c.BranchId == branch && c.TriageLevel <= 2 && (c.Status == EmergencyStatus.Waiting || c.Status == EmergencyStatus.InExam), ct);
                break;
            case UserRole.Admissions:
                r["pendingAdmissions"] = await db.Admissions.CountAsync(a => a.BranchId == branch && a.Status == AdmissionStatus.Pending, ct);
                r["availableBeds"] = await db.Beds.CountAsync(x => x.BranchId == branch && x.Status == BedStatus.Available, ct);
                break;
            case UserRole.Surgery:
                r["todaySurgeries"] = await db.SurgeryCases.CountAsync(s => s.Room.BranchId == branch && s.Date == today && s.Status != SurgeryStatus.Cancelled, ct);
                break;
        }
        return r;
    }
}
