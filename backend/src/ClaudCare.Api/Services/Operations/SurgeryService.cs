using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Operations;

/// <summary>
/// غرف العمليات: الجدولة بلا تعارض (غرفة/جراح/تخدير)، قائمة ما قبل العملية، الاعتماد (مع إقرار التنبيه)، البدء (غرفة متاحة بحجز ذري)، الإنهاء.
/// </summary>
public sealed class SurgeryService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, IClinicClock clock, INotificationService notifications, IAuditService audit, TimeProvider time)
{
    public static readonly IReadOnlyList<(string Key, string Label)> Checklist =
    [
        ("consent", "الموافقة الخطية على العملية"), ("identity", "التحقق من هوية المريض"), ("site", "تحديد موضع العملية"), ("fasting", "الصيام"),
        ("labs", "نتائج التحاليل"), ("imaging", "الأشعة المطلوبة"), ("allergies", "مراجعة الحساسية"), ("anesthesia", "تقييم التخدير"),
    ];
    private static readonly UserRole[] Readers = [UserRole.Surgery, UserRole.Doctor, UserRole.Nurse, UserRole.Branch];
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<OperatingRoomDto>> RoomsAsync(CancellationToken ct)
    {
        scope.Require(Readers);
        var b = scope.BranchFilter;
        return await db.OperatingRooms.AsNoTracking().Where(r => b == null || r.BranchId == b).OrderBy(r => r.Code).Select(r => new OperatingRoomDto(r.Id, r.Code, r.State, r.Note)).ToListAsync(ct);
    }

    public async Task<OperatingRoomDto> SetRoomStateAsync(int id, OperatingRoomState state, string? note, CancellationToken ct)
    {
        scope.Require(UserRole.Surgery, UserRole.Nurse);
        if (state == OperatingRoomState.InSurgery) throw ApiException.Validation(nameof(state), "الحالة «قيد العملية» تُضبط ببدء العملية");
        var r = await db.OperatingRooms.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || !scope.InBranch(r.BranchId)) throw ApiException.NotFound("room_not_found", "الغرفة غير موجودة.");
        if (r.State == OperatingRoomState.InSurgery) throw ApiException.Conflict("room_in_use", "الغرفة قيد عملية جارية.");
        r.State = state;
        r.Note = note?.Trim();
        await db.SaveChangesAsync(ct);
        return new OperatingRoomDto(r.Id, r.Code, r.State, r.Note);
    }

    public async Task<SurgeryDto> ScheduleAsync(ScheduleSurgeryRequest r, CancellationToken ct)
    {
        scope.Require(UserRole.Surgery);
        var room = await db.OperatingRooms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.RoomId, ct);
        if (room is null || !scope.InBranch(room.BranchId)) throw ApiException.NotFound("room_not_found", "الغرفة غير موجودة.");
        if (room.State == OperatingRoomState.Maintenance) throw ApiException.Conflict("room_unavailable", "الغرفة تحت الصيانة.");
        if (r.Date < clock.Today) throw ApiException.Validation(nameof(r.Date), "التاريخ في الماضي");
        if (r.StartMinutes + r.DurationMinutes > 24 * 60) throw ApiException.Validation(nameof(r.DurationMinutes), "العملية تتجاوز نهاية اليوم");
        if (!await db.Users.AnyAsync(u => u.Id == r.PatientId && u.Role == UserRole.Patient, ct)) throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");
        if (!await db.Users.AnyAsync(u => u.Id == r.SurgeonId && u.Role == UserRole.Doctor && u.IsActive, ct)) throw ApiException.NotFound("doctor_not_found", "الجراح غير موجود.");
        if (r.AnesthetistId is { } an && !await db.Users.AnyAsync(u => u.Id == an && u.Role == UserRole.Doctor && u.IsActive, ct)) throw ApiException.NotFound("doctor_not_found", "طبيب التخدير غير موجود.");

        var date = r.Date!.Value; var start = r.StartMinutes!.Value; var end = start + r.DurationMinutes!.Value;
        var busy = db.SurgeryCases.Where(s => s.Date == date && s.Status != SurgeryStatus.Cancelled && s.StartMinutes < end && start < s.StartMinutes + s.DurationMinutes);
        if (await busy.AnyAsync(s => s.RoomId == r.RoomId, ct)) throw ApiException.Conflict("room_busy", "الغرفة محجوزة في هذا الوقت.");
        if (await busy.AnyAsync(s => s.SurgeonId == r.SurgeonId || s.AnesthetistId == r.SurgeonId, ct)) throw ApiException.Conflict("surgeon_busy", "الجراح لديه عملية في هذا الوقت.");
        if (r.AnesthetistId is { } a2 && await busy.AnyAsync(s => s.AnesthetistId == a2 || s.SurgeonId == a2, ct)) throw ApiException.Conflict("anesthetist_busy", "طبيب التخدير مشغول في هذا الوقت.");

        var s = new SurgeryCase { No = "TMP" + Guid.NewGuid().ToString("N")[..16], PatientId = r.PatientId!.Value, ProcedureName = r.ProcedureName.Trim(), RoomId = r.RoomId!.Value, Date = date, StartMinutes = start,
            DurationMinutes = r.DurationMinutes.Value, SurgeonId = r.SurgeonId!.Value, AnesthetistId = r.AnesthetistId, Notes = r.Notes?.Trim(), PreOpAlert = r.PreOpAlert?.Trim(),
            Checklist = Checklist.Select(c => new PreOpCheckItem { ItemKey = c.Key }).ToList() };
        db.SurgeryCases.Add(s);
        await db.SaveChangesAsync(ct);
        s.No = $"SRG-{s.Id:D5}";
        notifications.Add(s.SurgeonId, NotificationType.Appointment, "عملية مجدولة", $"{s.ProcedureName} · {date.Iso()}", nameof(SurgeryCase), s.Id);
        notifications.Add(s.PatientId, NotificationType.Appointment, "تم تحديد موعد عمليتك", $"{s.ProcedureName} · {date.Iso()}", nameof(SurgeryCase), s.Id);
        audit.Add("surgery.schedule", me.Id, nameof(SurgeryCase), s.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return await GetAsync(s.Id, ct);
    }

    public async Task<IReadOnlyList<SurgeryDto>> ListAsync(DateOnly? date, SurgeryStatus? status, CancellationToken ct)
    {
        scope.Require(Readers);
        var b = scope.BranchFilter;
        var q = db.SurgeryCases.AsNoTracking().Where(s => b == null || s.Room.BranchId == b);
        if (me.Role == UserRole.Doctor) q = q.Where(s => s.SurgeonId == me.Id || s.AnesthetistId == me.Id);
        if (date is { } d) q = q.Where(s => s.Date == d);
        if (status is { } st) q = q.Where(s => s.Status == st);
        var rows = await q.Include(s => s.Patient).Include(s => s.Room).Include(s => s.Surgeon).Include(s => s.Anesthetist).Include(s => s.Checklist)
            .OrderBy(s => s.Date).ThenBy(s => s.StartMinutes).Take(200).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<SurgeryDto> GetAsync(int id, CancellationToken ct)
    {
        scope.Require(Readers);
        return ToDto(await LoadAsync(id, tracked: false, ct));
    }

    public async Task<SurgeryDto> SetChecklistAsync(int id, string key, bool done, CancellationToken ct)
    {
        scope.Require(UserRole.Surgery, UserRole.Nurse);
        var s = await LoadAsync(id, tracked: true, ct);
        if (s.Status != SurgeryStatus.Scheduled) throw ApiException.Conflict("invalid_state", "القائمة مقفلة بعد بدء العملية أو إلغائها.");
        var item = s.Checklist.FirstOrDefault(c => c.ItemKey == key) ?? throw ApiException.NotFound("checklist_item_not_found", "بند القائمة غير موجود.");
        item.Done = done; item.DoneByUserId = done ? me.Id : null; item.DoneAt = done ? UtcNow : null;
        if (!done) { s.PreOpReady = false; s.PreOpApprovedByUserId = null; } // تعديل بعد الاعتماد يُسقطه
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<SurgeryDto> ApprovePreOpAsync(int id, bool acknowledge, CancellationToken ct)
    {
        scope.Require(UserRole.Surgery, UserRole.Doctor);
        var s = await LoadAsync(id, tracked: true, ct);
        if (me.Role == UserRole.Doctor && s.SurgeonId != me.Id) throw ApiException.Forbidden("action_not_allowed", "الاعتماد للجراح المسؤول فقط.");
        if (s.Status != SurgeryStatus.Scheduled) throw ApiException.Conflict("invalid_state", "لا يمكن الاعتماد في هذه الحالة.");
        var open = s.Checklist.Where(c => !c.Done).Select(c => Label(c.ItemKey)).ToList();
        if (open.Count > 0) throw new ApiException(StatusCodes.Status409Conflict, "checklist_incomplete", "أكمل قائمة ما قبل العملية: " + string.Join("، ", open), new Dictionary<string, object?> { ["missing"] = open });
        if (!string.IsNullOrEmpty(s.PreOpAlert) && !s.PreOpAlertAcknowledged)
        {
            if (!acknowledge) throw new ApiException(StatusCodes.Status409Conflict, "alert_not_acknowledged", $"يلزم إقرار التنبيه: {s.PreOpAlert}");
            s.PreOpAlertAcknowledged = true;
        }
        s.PreOpReady = true;
        s.PreOpApprovedByUserId = me.Id;
        audit.Add("surgery.preop_approve", me.Id, nameof(SurgeryCase), s.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<SurgeryDto> StartAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Surgery);
        var s = await LoadAsync(id, tracked: true, ct);
        if (s.Status != SurgeryStatus.Scheduled) throw ApiException.Conflict("invalid_state", $"لا يمكن البدء والعملية في حالة «{s.Status}».");
        if (!s.PreOpReady) throw ApiException.Conflict("preop_not_ready", "لم تُعتمد قائمة ما قبل العملية.");
        if (s.Date != clock.Today) throw ApiException.Conflict("not_today", "تبدأ العملية يوم موعدها فقط.");
        var rows = await db.OperatingRooms.Where(r => r.Id == s.RoomId && r.State == OperatingRoomState.Available).ExecuteUpdateAsync(x => x.SetProperty(r => r.State, OperatingRoomState.InSurgery), ct);
        if (rows == 0) throw ApiException.Conflict("room_unavailable", "الغرفة غير جاهزة (قيد عملية أو تنظيف أو صيانة).");
        s.Status = SurgeryStatus.InProgress;
        audit.Add("surgery.start", me.Id, nameof(SurgeryCase), s.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<SurgeryDto> CompleteAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Surgery);
        var s = await LoadAsync(id, tracked: true, ct);
        if (s.Status != SurgeryStatus.InProgress) throw ApiException.Conflict("invalid_state", "العملية غير جارية.");
        s.Status = SurgeryStatus.Done;
        await db.OperatingRooms.Where(r => r.Id == s.RoomId).ExecuteUpdateAsync(x => x.SetProperty(r => r.State, OperatingRoomState.Cleaning), ct);
        notifications.Add(s.PatientId, NotificationType.System, "انتهت عمليتك", s.ProcedureName, nameof(SurgeryCase), s.Id);
        audit.Add("surgery.complete", me.Id, nameof(SurgeryCase), s.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task<SurgeryDto> CancelAsync(int id, CancellationToken ct)
    {
        scope.Require(UserRole.Surgery);
        var s = await LoadAsync(id, tracked: true, ct);
        if (s.Status != SurgeryStatus.Scheduled) throw ApiException.Conflict("invalid_state", "لا يُلغى إلا المجدول.");
        s.Status = SurgeryStatus.Cancelled;
        notifications.Add(s.PatientId, NotificationType.System, "أُلغيت عمليتك المجدولة", s.ProcedureName, nameof(SurgeryCase), s.Id);
        notifications.Add(s.SurgeonId, NotificationType.System, "أُلغيت عملية مجدولة", s.ProcedureName, nameof(SurgeryCase), s.Id);
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    private async Task<SurgeryCase> LoadAsync(int id, bool tracked, CancellationToken ct)
    {
        var q = tracked ? db.SurgeryCases : db.SurgeryCases.AsNoTracking();
        var s = await q.Include(x => x.Patient).Include(x => x.Room).Include(x => x.Surgeon).Include(x => x.Anesthetist).Include(x => x.Checklist).FirstOrDefaultAsync(x => x.Id == id, ct);
        var visible = s is not null && scope.InBranch(s.Room.BranchId) && (me.Role != UserRole.Doctor || s.SurgeonId == me.Id || s.AnesthetistId == me.Id);
        return visible ? s! : throw ApiException.NotFound("surgery_not_found", "العملية غير موجودة.");
    }

    private static string Label(string key) => Checklist.FirstOrDefault(c => c.Key == key).Label ?? key;

    private static SurgeryDto ToDto(SurgeryCase s) => new(s.Id, s.No, new PersonRefDto(s.Patient.Id, s.Patient.FullName, s.Patient.AvatarKey), s.ProcedureName, s.Room.Code, s.Date, s.StartMinutes, s.DurationMinutes,
        s.Surgeon.FullName, s.Anesthetist?.FullName, s.Status, s.PreOpReady, s.PreOpAlert, s.PreOpAlertAcknowledged, s.Notes,
        Checklist.Select(c => s.Checklist.FirstOrDefault(i => i.ItemKey == c.Key)).Where(i => i is not null).Select(i => new ChecklistItemDto(i!.ItemKey, Label(i.ItemKey), i.Done, i.DoneAt)).ToList());
}
