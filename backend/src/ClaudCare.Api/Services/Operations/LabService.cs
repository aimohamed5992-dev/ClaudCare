using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Clinical;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Operations;

/// <summary>
/// سير عمل المختبر: طلب ← سحب العينة ← قيد التحليل ← النتيجة. المختبر فرعه فقط (خارجه 404)، والتمريض يسحب العينات.
/// الأعلام تُحسب من الخادم، والنتيجة الحرجة تُنبّه الطبيب فورًا.
/// </summary>
public sealed class LabService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, INotificationService notifications, IAuditService audit, OrdersService orders, TimeProvider time)
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<LabOrderDto> CollectAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Lab, UserRole.Nurse);
        var o = await FindAsync(id, ct);
        Expect(o, LabOrderStatus.Ordered);
        o.Status = LabOrderStatus.SampleCollected;
        o.SampleCollectedAt = UtcNow;
        o.TechnicianId = me.Id;
        audit.Add("lab.collect", me.Id, nameof(LabOrder), o.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return await orders.LabAsync(id, ct);
    }

    public async Task<LabOrderDto> StartAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Lab);
        var o = await FindAsync(id, ct);
        Expect(o, LabOrderStatus.SampleCollected);
        o.Status = LabOrderStatus.InProgress;
        o.TechnicianId = me.Id;
        await db.SaveChangesAsync(ct);
        return await orders.LabAsync(id, ct);
    }

    public async Task<LabOrderDto> EnterResultsAsync(int id, List<LabResultInput> inputs, CancellationToken ct)
    {
        scope.Require(UserRole.Lab);
        var o = await FindAsync(id, ct);
        Expect(o, LabOrderStatus.InProgress);

        var def = ClinicalCatalog.LabTests.FirstOrDefault(t => t.Name.Equals(o.TestName, StringComparison.OrdinalIgnoreCase));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in inputs)
        {
            if (!seen.Add(i.Name.Trim())) throw ApiException.Validation(nameof(inputs), $"البند «{i.Name}» مكرر");
            var d = def?.Items.FirstOrDefault(x => x.Name.Equals(i.Name.Trim(), StringComparison.OrdinalIgnoreCase));
            var min = i.RefMin ?? d?.Min;
            var max = i.RefMax ?? d?.Max;
            if (min > max) throw ApiException.Validation(nameof(inputs), $"المدى المرجعي للبند «{i.Name}» غير صالح");
            o.Items.Add(new LabResultItem { Name = i.Name.Trim(), Value = i.Value!.Value, Unit = (i.Unit ?? d?.Unit ?? "").Trim(), RefMin = min, RefMax = max, Flag = ResultFlagCalculator.Flag(i.Value.Value, min, max), Remark = i.Remark?.Trim() });
        }
        o.Status = LabOrderStatus.ResultReady;
        o.ResultAt = UtcNow;
        o.TechnicianId = me.Id;

        notifications.Add(o.PatientId, NotificationType.Result, "نتيجة تحليل جاهزة", o.TestName, nameof(LabOrder), o.Id);
        if (o.DoctorId is { } doc)
        {
            var critical = o.Items.Where(x => x.Flag == ResultFlag.Critical).Select(x => x.Name).ToList();
            if (critical.Count > 0) notifications.Add(doc, NotificationType.Alert, "نتيجة حرجة", $"{o.TestName}: {string.Join("، ", critical)}", nameof(LabOrder), o.Id);
            else notifications.Add(doc, NotificationType.Result, "نتيجة تحليل جاهزة", o.TestName, nameof(LabOrder), o.Id);
        }
        audit.Add("lab.result", me.Id, nameof(LabOrder), o.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return await orders.LabAsync(id, ct);
    }

    public async Task<LabOrderDto> CancelAsync(int id, string reason, CancellationToken ct)
    {
        scope.Require(UserRole.Lab, UserRole.Doctor);
        var o = await FindAsync(id, ct);
        if (me.Role == UserRole.Doctor && o.DoctorId != me.Id) throw ApiException.NotFound("order_not_found", "الطلب غير موجود.");
        if (o.Status is LabOrderStatus.ResultReady or LabOrderStatus.Cancelled) throw ApiException.Conflict("invalid_state", "لا يمكن إلغاء طلب نتيجته جاهزة أو ملغى.");
        o.Status = LabOrderStatus.Cancelled;
        o.Notes = reason.Trim();
        notifications.Add(o.PatientId, NotificationType.Result, "أُلغي طلب تحليل", $"{o.TestName} · {reason.Trim()}", nameof(LabOrder), o.Id);
        await db.SaveChangesAsync(ct);
        return await orders.LabAsync(id, ct);
    }

    private async Task<LabOrder> FindAsync(int id, CancellationToken ct)
    {
        var o = await db.LabOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        // الطبيب لا يصل إلا لطلباته، والمختبر/التمريض لفرعهم؛ غيرهم لا يعلم بوجوده
        var visible = o is not null && (me.Role == UserRole.Doctor ? o.DoctorId == me.Id : scope.InBranch(o.BranchId));
        return visible ? o! : throw ApiException.NotFound("order_not_found", "الطلب غير موجود.");
    }

    private static void Expect(LabOrder o, LabOrderStatus expected)
    {
        if (o.Status != expected) throw ApiException.Conflict("invalid_state", $"لا يمكن تنفيذ هذا الإجراء والطلب في حالة «{o.Status}».");
    }
}
