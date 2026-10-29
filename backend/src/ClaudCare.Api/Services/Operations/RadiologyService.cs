using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Clinical;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Operations;

/// <summary>الأشعة: جدولة ← بدء الفحص ← تقرير (مسودة قابلة للتعديل ثم نهائي لا يُعدَّل). الفرع فقط.</summary>
public sealed class RadiologyService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, INotificationService notifications, IAuditService audit, OrdersService orders, TimeProvider time)
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ImagingDeviceDto>> DevicesAsync(CancellationToken ct)
    {
        scope.Require(UserRole.Radiology, UserRole.Doctor, UserRole.Reception);
        var b = scope.BranchFilter;
        if (b is null) return await db.ImagingDevices.AsNoTracking().OrderBy(d => d.Name).Select(d => new ImagingDeviceDto(d.Id, d.Name, d.Modality, d.IsAvailable)).ToListAsync(ct);
        return await db.ImagingDevices.AsNoTracking().Where(d => d.BranchId == b).OrderBy(d => d.Name).Select(d => new ImagingDeviceDto(d.Id, d.Name, d.Modality, d.IsAvailable)).ToListAsync(ct);
    }

    public async Task<ImagingOrderDto> ScheduleAsync(int id, DateTime at, int? deviceId, CancellationToken ct)
    {
        scope.Require(UserRole.Radiology, UserRole.Reception);
        var o = await FindAsync(id, ct);
        if (o.Status is not (ImagingOrderStatus.Ordered or ImagingOrderStatus.Scheduled)) throw ApiException.Conflict("invalid_state", $"لا يمكن الجدولة والطلب في حالة «{o.Status}».");
        if (at < UtcNow.AddMinutes(-5)) throw ApiException.Validation(nameof(at), "الموعد في الماضي");
        if (deviceId is { } d)
        {
            var dev = await db.ImagingDevices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == d && (o.BranchId == null || x.BranchId == o.BranchId), ct)
                      ?? throw ApiException.NotFound("device_not_found", "الجهاز غير موجود.");
            if (!dev.IsAvailable) throw ApiException.Conflict("device_unavailable", "الجهاز خارج الخدمة.");
            if (!dev.Modality.Equals(o.Modality, StringComparison.OrdinalIgnoreCase)) throw ApiException.Validation(nameof(deviceId), "الجهاز لا يناسب نوع الفحص");
            // نافذة 30 دقيقة لا تتداخل مع فحص آخر على الجهاز نفسه
            var from = at.AddMinutes(-30); var to = at.AddMinutes(30);
            if (await db.ImagingOrders.AnyAsync(x => x.DeviceId == d && x.Id != o.Id && x.Status == ImagingOrderStatus.Scheduled && x.ScheduledAt > from && x.ScheduledAt < to, ct))
                throw ApiException.Conflict("device_busy", "الجهاز محجوز في هذا الوقت.");
            o.DeviceId = d;
        }
        o.ScheduledAt = at;
        o.Status = ImagingOrderStatus.Scheduled;
        notifications.Add(o.PatientId, NotificationType.Result, "تم تحديد موعد الأشعة", o.StudyName, nameof(ImagingOrder), o.Id);
        await db.SaveChangesAsync(ct);
        return await orders.ImagingAsync(id, ct);
    }

    public async Task<ImagingOrderDto> StartAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Radiology);
        var o = await FindAsync(id, ct);
        if (o.Status is not (ImagingOrderStatus.Ordered or ImagingOrderStatus.Scheduled)) throw ApiException.Conflict("invalid_state", $"لا يمكن بدء الفحص والطلب في حالة «{o.Status}».");
        o.Status = ImagingOrderStatus.InProgress;
        await db.SaveChangesAsync(ct);
        return await orders.ImagingAsync(id, ct);
    }

    public async Task<ImagingOrderDto> ReportAsync(int id, ImagingReportRequest req, CancellationToken ct)
    {
        scope.Require(UserRole.Radiology);
        var o = await FindAsync(id, ct);
        if (o.Status is not ImagingOrderStatus.InProgress) throw ApiException.Conflict("invalid_state", o.Status == ImagingOrderStatus.Reported ? "التقرير نهائي ولا يُعدَّل." : "ابدأ الفحص قبل كتابة التقرير.");
        if (o.Report is { Status: ReportStatus.Final }) throw ApiException.Conflict("report_final", "التقرير نهائي ولا يُعدَّل.");

        var r = o.Report ??= new ImagingReport();
        r.Findings = req.Findings.Trim();
        r.Impression = req.Impression.Trim();
        r.ImageKey = req.ImageKey?.Trim();
        r.RadiologistId = me.Id;
        if (req.Final)
        {
            r.Status = ReportStatus.Final;
            r.ReportedAt = UtcNow;
            o.Status = ImagingOrderStatus.Reported;
            notifications.Add(o.PatientId, NotificationType.Result, "تقرير الأشعة جاهز", o.StudyName, nameof(ImagingOrder), o.Id);
            if (o.DoctorId is { } doc) notifications.Add(doc, NotificationType.Result, "تقرير أشعة جاهز", o.StudyName, nameof(ImagingOrder), o.Id);
            audit.Add("imaging.report", me.Id, nameof(ImagingOrder), o.Id, ip: me.Ip);
        }
        await db.SaveChangesAsync(ct);
        return await orders.ImagingAsync(id, ct);
    }

    public async Task<ImagingOrderDto> CancelAsync(int id, string reason, CancellationToken ct)
    {
        scope.Require(UserRole.Radiology, UserRole.Doctor);
        var o = await FindAsync(id, ct);
        if (o.Status is ImagingOrderStatus.Reported or ImagingOrderStatus.Cancelled) throw ApiException.Conflict("invalid_state", "لا يمكن إلغاء طلب منتهٍ أو ملغى.");
        o.Status = ImagingOrderStatus.Cancelled;
        o.ClinicalNotes = reason.Trim();
        notifications.Add(o.PatientId, NotificationType.Result, "أُلغي طلب أشعة", $"{o.StudyName} · {reason.Trim()}", nameof(ImagingOrder), o.Id);
        await db.SaveChangesAsync(ct);
        return await orders.ImagingAsync(id, ct);
    }

    private async Task<ImagingOrder> FindAsync(int id, CancellationToken ct)
    {
        var o = await db.ImagingOrders.Include(x => x.Report).FirstOrDefaultAsync(x => x.Id == id, ct);
        var visible = o is not null && (me.Role == UserRole.Doctor ? o.DoctorId == me.Id : scope.InBranch(o.BranchId));
        return visible ? o! : throw ApiException.NotFound("order_not_found", "الطلب غير موجود.");
    }
}
