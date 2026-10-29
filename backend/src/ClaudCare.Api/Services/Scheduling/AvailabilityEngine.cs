using ClaudCare.Api.Configuration;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Services.Scheduling;

/// <summary>يحسب الخانات المتاحة لأطباء على نطاق أيام، دفعةً واحدة بأقل عدد استعلامات.</summary>
public sealed class AvailabilityEngine(ClaudCareDbContext db, IClinicClock clock, IOptions<BookingOptions> options)
{
    public sealed record DayPlan(DateOnly Date, bool Works, bool OnLeave, int StartMinutes, int EndMinutes, int SlotMinutes, List<int> Available, List<int> Grid);

    /// <summary>خانات كل طبيب لكل يوم في [start, start+days).</summary>
    public async Task<Dictionary<int, List<DayPlan>>> PlanAsync(IReadOnlyCollection<(int DoctorId, int SlotMinutes)> doctors, DateOnly start, int days, CancellationToken ct, int? ignoreAppointmentId = null)
    {
        var ids = doctors.Select(d => d.DoctorId).ToList();
        var to = start.AddDays(days - 1);

        var hours = (await db.DoctorWorkingHours.AsNoTracking().Where(h => ids.Contains(h.DoctorId) && h.IsActive).ToListAsync(ct))
            .ToLookup(h => h.DoctorId);

        var taken = (await db.Appointments.AsNoTracking()
                .Where(a => ids.Contains(a.DoctorId) && a.ActiveSlotKey != null && a.Date >= start && a.Date <= to && a.Id != ignoreAppointmentId)
                .Select(a => new { a.DoctorId, a.Date, a.StartMinutes }).ToListAsync(ct))
            .GroupBy(a => (a.DoctorId, a.Date)).ToDictionary(g => g.Key, g => g.Select(x => x.StartMinutes).ToHashSet());

        // إجازات معتمدة للأطباء المرتبطين بسجل موظف
        var leaves = await (from l in db.LeaveRequests.AsNoTracking()
                            join e in db.Employees.AsNoTracking() on l.EmployeeId equals e.Id
                            where e.UserId != null && ids.Contains(e.UserId!.Value) && l.Status == LeaveStatus.Approved && l.From <= to && l.To >= start
                            select new { UserId = e.UserId!.Value, l.From, l.To }).ToListAsync(ct);

        var o = options.Value;
        var result = new Dictionary<int, List<DayPlan>>();
        foreach (var (doctorId, slotMinutes) in doctors)
        {
            var plans = new List<DayPlan>();
            for (var i = 0; i < days; i++)
            {
                var date = start.AddDays(i);
                var wh = hours[doctorId].FirstOrDefault(h => h.DayOfWeek == date.DayOfWeek);
                var onLeave = leaves.Any(l => l.UserId == doctorId && l.From <= date && l.To >= date);
                if (wh is null)
                {
                    plans.Add(new DayPlan(date, false, onLeave, 0, 0, slotMinutes, [], []));
                    continue;
                }
                var isToday = date == clock.Today;
                var grid = SlotCalculator.Generate(wh.StartMinutes, wh.EndMinutes, slotMinutes, new HashSet<int>(), isToday, clock.NowMinutes, o.MinLeadMinutes);
                var takenSet = taken.TryGetValue((doctorId, date), out var t) ? t : [];
                var available = onLeave ? [] : grid.Where(m => !takenSet.Contains(m)).ToList();
                plans.Add(new DayPlan(date, true, onLeave, wh.StartMinutes, wh.EndMinutes, slotMinutes, available, grid));
            }
            result[doctorId] = plans;
        }
        return result;
    }
}
