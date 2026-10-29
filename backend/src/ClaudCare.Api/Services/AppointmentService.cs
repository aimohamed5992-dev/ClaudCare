using ClaudCare.Api.Auth;
using ClaudCare.Api.Configuration;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace ClaudCare.Api.Services;

public sealed class AppointmentService(
    ClaudCareDbContext db,
    AvailabilityEngine engine,
    IClinicClock clock,
    ICurrentUser me,
    INotificationService notifications,
    IAuditService audit,
    Billing.BillingService billing,
    IOptions<BookingOptions> booking,
    TimeProvider time)
{
    private static readonly UserRole[] DeskRoles = [UserRole.Reception, UserRole.Branch, UserRole.Hospital, UserRole.Admin];

    private BookingOptions O => booking.Value;
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------ الحجز

    public async Task<AppointmentDto> BookAsync(BookAppointmentRequest req, CancellationToken ct)
    {
        var patientId = ResolvePatientId(req);
        var patient = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == patientId && u.Role == UserRole.Patient && u.IsActive, ct)
                      ?? throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");

        var doctor = await db.DoctorProfiles.Include(d => d.User).Include(d => d.Specialty).FirstOrDefaultAsync(d => d.UserId == req.DoctorId && d.IsListed && d.User.IsActive, ct)
                     ?? throw ApiException.NotFound("doctor_not_found", "الطبيب غير موجود.");
        var type = req.Type!.Value;
        if (type == VisitType.Video && doctor.VideoPrice is null) throw ApiException.BadRequest("video_unavailable", "هذا الطبيب لا يقدّم استشارات عن بُعد.");

        var date = req.Date!.Value;
        var start = req.StartMinutes!.Value;
        await EnsureSlotAsync(doctor, date, start, ignoreAppointmentId: null, ct);

        if (req.FamilyMemberId is { } fm && !await db.FamilyMembers.AnyAsync(f => f.Id == fm && f.PatientId == patientId, ct))
            throw ApiException.NotFound("family_member_not_found", "فرد العائلة غير موجود.");
        await EnsurePatientFreeAsync(patientId, req.FamilyMemberId, date, start, doctor.SlotMinutes, ignoreId: null, ct);

        var policy = await db.PatientInsurances.AsNoTracking().Where(p => p.PatientId == patientId && p.IsActive).OrderByDescending(p => p.ExpiresAt).FirstOrDefaultAsync(ct);
        var price = type == VisitType.Video ? doctor.VideoPrice!.Value : doctor.ConsultationPrice;
        var quote = PricingCalculator.Quote(price, policy, clock.Today, req.UseInsurance);

        var (method, paid) = ResolvePayment(quote, req.PaymentMethod);

        var appt = new Appointment
        {
            PatientId = patientId,
            FamilyMemberId = req.FamilyMemberId,
            DoctorId = doctor.UserId,
            BranchId = doctor.BranchId,
            Date = date,
            StartMinutes = start,
            DurationMinutes = doctor.SlotMinutes,
            Type = type,
            Status = doctor.RequiresConfirmation ? AppointmentStatus.Pending : AppointmentStatus.Confirmed,
            Reason = string.IsNullOrWhiteSpace(req.Reason) ? "كشف" : req.Reason.Trim(),
            Notes = req.Notes?.Trim(),
            Price = quote.Price,
            InsuranceCovered = quote.InsuranceCovered,
            PaymentMethod = method,
            Paid = paid,
            ActiveSlotKey = Appointment.SlotKey(doctor.UserId, date, start),
        };
        db.Appointments.Add(appt);
        var invoice = await billing.StageForAppointmentAsync(appt, doctor.User.FullName, quote, method, paid, ct);

        var when = $"{date.Iso()} {FormatTime(start)}";
        notifications.Add(doctor.UserId, NotificationType.Appointment, appt.Status == AppointmentStatus.Pending ? "طلب موعد جديد" : "حجز جديد", $"{patient.FullName} · {when}", nameof(Appointment), null);
        notifications.Add(patientId, NotificationType.Appointment, appt.Status == AppointmentStatus.Pending ? "حجزك بانتظار تأكيد الطبيب" : "تم تأكيد حجزك", $"{doctor.User.FullName} · {when}", nameof(Appointment), null);
        audit.Add("appointment.book", me.Id, nameof(Appointment), details: $"doctor={doctor.UserId} {when}", ip: me.Ip);

        await SaveSlotAsync(ct);
        await billing.LinkAsync(invoice, appt, ct);
        return (await ToDtosAsync([appt], ct))[0];
    }

    private int ResolvePatientId(BookAppointmentRequest req)
    {
        if (me.Role == UserRole.Patient)
        {
            if (req.PatientId is { } other && other != me.Id) throw ApiException.Forbidden("action_not_allowed", "لا يمكنك الحجز نيابةً عن مريض آخر.");
            return me.Id;
        }
        if (!DeskRoles.Contains(me.Role)) throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية الحجز.");
        return req.PatientId ?? throw ApiException.Validation(nameof(req.PatientId), "حدّد المريض");
    }

    private (PaymentMethod? method, bool paid) ResolvePayment(PriceQuote quote, PaymentMethod? requested)
    {
        if (quote.Due <= 0) return (PaymentMethod.Insurance, true); // مغطى بالكامل
        var allowed = me.Role == UserRole.Patient
            ? new[] { PaymentMethod.Card, PaymentMethod.ApplePay, PaymentMethod.PayAtClinic }
            : [PaymentMethod.Card, PaymentMethod.Cash, PaymentMethod.ApplePay, PaymentMethod.BankTransfer, PaymentMethod.PayAtClinic];
        if (requested is null) throw ApiException.Validation(nameof(BookAppointmentRequest.PaymentMethod), "اختر طريقة الدفع");
        if (!allowed.Contains(requested.Value)) throw ApiException.Validation(nameof(BookAppointmentRequest.PaymentMethod), "طريقة الدفع غير مدعومة");
        // الدفع الإلكتروني يُحاكى ويُسجَّل كدفعة فعلية في الفاتورة
        return (requested, requested is PaymentMethod.Card or PaymentMethod.ApplePay or PaymentMethod.BankTransfer);
    }

    /// <summary>يتحقق أن الخانة صالحة (ضمن دوام الطبيب وعلى الشبكة وفي المستقبل) وغير محجوزة.</summary>
    private async Task EnsureSlotAsync(DoctorProfile doctor, DateOnly date, int start, int? ignoreAppointmentId, CancellationToken ct)
    {
        if (date < clock.Today || date > clock.Today.AddDays(O.MaxAdvanceDays))
            throw ApiException.BadRequest("slot_invalid", $"يمكن الحجز من اليوم وحتى {O.MaxAdvanceDays} يومًا قادمة.");

        var plan = (await engine.PlanAsync([(doctor.UserId, doctor.SlotMinutes)], date, 1, ct, ignoreAppointmentId))[doctor.UserId][0];
        if (plan.OnLeave) throw ApiException.BadRequest("doctor_on_leave", "الطبيب في إجازة في هذا اليوم.");
        if (!plan.Works || !plan.Grid.Contains(start)) throw ApiException.BadRequest("slot_invalid", "هذا الوقت خارج دوام الطبيب أو غير متاح للحجز.");
        if (!plan.Available.Contains(start)) throw ApiException.Conflict("slot_taken", "هذا الموعد حُجز للتو. اختر وقتًا آخر.");
    }

    private async Task EnsurePatientFreeAsync(int patientId, int? familyMemberId, DateOnly date, int start, int duration, int? ignoreId, CancellationToken ct)
    {
        var busy = await db.Appointments.AnyAsync(a => a.PatientId == patientId && a.FamilyMemberId == familyMemberId && a.ActiveSlotKey != null && a.Date == date && a.Id != ignoreId
                                                       && a.StartMinutes < start + duration && start < a.StartMinutes + a.DurationMinutes, ct);
        if (busy) throw ApiException.Conflict("patient_busy", "لديك موعد آخر في الوقت نفسه.");
    }

    /// <summary>حفظ مع تحويل تعارض الفهرس الفريد (حجز متزامن) إلى 409.</summary>
    private async Task SaveSlotAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.GetBaseException() is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
        {
            throw ApiException.Conflict("slot_taken", "هذا الموعد حُجز للتو. اختر وقتًا آخر.");
        }
    }

    // ------------------------------------------------------------ القراءة

    public async Task<PagedResult<AppointmentDto>> ListAsync(AppointmentQuery q, CancellationToken ct)
    {
        var (page, size) = Paging.Clamp(q.Page, q.PageSize);
        var query = Scoped();
        var today = clock.Today;

        if (q.Doctor is { } did) query = query.Where(a => a.DoctorId == did);
        if (q.PatientFilter is { } pid) query = query.Where(a => a.PatientId == pid);
        if (q.Date is { } d) query = query.Where(a => a.Date == d);
        if (q.From is { } f) query = query.Where(a => a.Date >= f);
        if (q.To is { } t) query = query.Where(a => a.Date <= t);
        if (q.Status is { } st) query = query.Where(a => a.Status == st);

        switch (q.Scope)
        {
            case AppointmentScope.Upcoming:
                query = query.Where(a => (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Arrived || a.Status == AppointmentStatus.InProgress) && a.Date >= today).OrderBy(a => a.Date).ThenBy(a => a.StartMinutes);
                break;
            case AppointmentScope.Past:
                query = query.Where(a => a.Status == AppointmentStatus.Done || a.Status == AppointmentStatus.NoShow || ((a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Arrived || a.Status == AppointmentStatus.InProgress) && a.Date < today)).OrderByDescending(a => a.Date).ThenByDescending(a => a.StartMinutes);
                break;
            case AppointmentScope.Cancelled:
                query = query.Where(a => a.Status == AppointmentStatus.Cancelled).OrderByDescending(a => a.Date).ThenByDescending(a => a.StartMinutes);
                break;
            default:
                query = q.Date is not null ? query.OrderBy(a => a.StartMinutes) : query.OrderByDescending(a => a.Date).ThenBy(a => a.StartMinutes);
                break;
        }

        var total = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<AppointmentDto>(await ToDtosAsync(rows, ct), page, size, total);
    }

    public async Task<AppointmentDto> GetAsync(int id, CancellationToken ct) => (await ToDtosAsync([await FindAsync(id, ct)], ct))[0];

    public async Task<AppointmentAgendaDto> MyAgendaAsync(DateOnly? date, CancellationToken ct)
    {
        var d = date ?? clock.Today;
        var rows = await db.Appointments.AsNoTracking().Where(a => a.DoctorId == me.Id && a.Date == d).OrderBy(a => a.StartMinutes).ToListAsync(ct);
        int Count(AppointmentStatus s) => rows.Count(a => a.Status == s);
        return new AppointmentAgendaDto(d, rows.Count(a => a.Status != AppointmentStatus.Cancelled), Count(AppointmentStatus.Pending), Count(AppointmentStatus.Confirmed), Count(AppointmentStatus.Arrived),
            Count(AppointmentStatus.InProgress), Count(AppointmentStatus.Done), Count(AppointmentStatus.Cancelled), rows.Count(a => a.Type == VisitType.Video && a.Status != AppointmentStatus.Cancelled),
            await ToDtosAsync(rows, ct));
    }

    // ------------------------------------------------------------ التغييرات

    public async Task<CancelResultDto> CancelAsync(int id, string reason, CancellationToken ct)
    {
        var a = await FindForActionAsync(id, AppointmentAction.Cancel, ct);
        var hours = clock.HoursUntil(a.Date, a.StartMinutes);
        if (me.Role == UserRole.Patient && hours <= 0) throw ApiException.Conflict("appointment_started", "بدأ وقت الموعد ولا يمكن إلغاؤه.");

        // الرسوم تنطبق عند إلغاء المريض نفسه قبل أقل من المهلة المجانية (تتحول الفاتورة إلى رسوم إلغاء)
        var late = me.Role == UserRole.Patient && hours < O.FreeCancellationHours;
        var fee = late ? PricingCalculator.LateCancellationFee(a.Price - a.InsuranceCovered, O.LateCancellationFeePercent) : 0;

        a.Status = AppointmentStatus.Cancelled;
        a.ActiveSlotKey = null;
        a.CancelReason = reason.Trim();
        a.CancelledByUserId = me.Id;
        a.CancelledAt = UtcNow;

        await billing.SettleCancellationAsync(a, fee, ct);
        await NotifyOtherPartyAsync(a, "أُلغي موعد", reason.Trim(), ct);
        audit.Add("appointment.cancel", me.Id, nameof(Appointment), a.Id, reason.Trim(), me.Ip);
        await db.SaveChangesAsync(ct);
        return new CancelResultDto((await ToDtosAsync([a], ct))[0], late, fee);
    }

    public async Task<AppointmentDto> RescheduleAsync(int id, DateOnly date, int start, CancellationToken ct)
    {
        var a = await FindForActionAsync(id, AppointmentAction.Reschedule, ct);
        if (me.Role == UserRole.Patient && clock.HoursUntil(a.Date, a.StartMinutes) <= 0) throw ApiException.Conflict("appointment_started", "بدأ وقت الموعد ولا يمكن تغييره.");
        if (a.Date == date && a.StartMinutes == start) throw ApiException.BadRequest("same_slot", "اختر وقتًا مختلفًا عن الموعد الحالي.");

        var doctor = await db.DoctorProfiles.AsNoTracking().Include(d => d.User).FirstAsync(d => d.UserId == a.DoctorId, ct);
        await EnsureSlotAsync(doctor, date, start, a.Id, ct);
        await EnsurePatientFreeAsync(a.PatientId, a.FamilyMemberId, date, start, a.DurationMinutes, a.Id, ct);

        a.Date = date;
        a.StartMinutes = start;
        a.ActiveSlotKey = Appointment.SlotKey(a.DoctorId, date, start);
        if (me.Role == UserRole.Doctor && a.Status == AppointmentStatus.Pending) a.Status = AppointmentStatus.Confirmed; // الطبيب حين ينقل الموعد يؤكده

        await NotifyOtherPartyAsync(a, "تغيّر موعدك", $"{date.Iso()} {FormatTime(start)}", ct);
        audit.Add("appointment.reschedule", me.Id, nameof(Appointment), a.Id, $"{date.Iso()} {start}", me.Ip);
        await SaveSlotAsync(ct);
        return (await ToDtosAsync([a], ct))[0];
    }

    public async Task<AppointmentDto> TransitionAsync(int id, AppointmentAction action, CancellationToken ct)
    {
        var a = await FindForActionAsync(id, action, ct);
        var now = UtcNow;
        switch (action)
        {
            case AppointmentAction.Confirm:
                a.Status = AppointmentStatus.Confirmed;
                notifications.Add(a.PatientId, NotificationType.Appointment, "تم تأكيد موعدك", $"{a.Date.Iso()} {FormatTime(a.StartMinutes)}", nameof(Appointment), a.Id);
                break;
            case AppointmentAction.Arrive:
                RequireToday(a);
                a.Status = AppointmentStatus.Arrived;
                a.CheckedInAt = now;
                notifications.Add(a.DoctorId, NotificationType.Appointment, "وصل المريض", $"بانتظارك · {FormatTime(a.StartMinutes)}", nameof(Appointment), a.Id);
                break;
            case AppointmentAction.Start:
                RequireToday(a);
                a.Status = AppointmentStatus.InProgress;
                a.StartedAt = now;
                break;
            case AppointmentAction.Complete:
                a.Status = AppointmentStatus.Done;
                a.CompletedAt = now;
                notifications.Add(a.PatientId, NotificationType.Appointment, "انتهى كشفك", "يمكنك تقييم الزيارة", nameof(Appointment), a.Id);
                break;
            case AppointmentAction.NoShow:
                if (clock.HoursUntil(a.Date, a.StartMinutes) > 0) throw ApiException.Conflict("appointment_not_started", "لم يحن وقت الموعد بعد.");
                a.Status = AppointmentStatus.NoShow;
                a.ActiveSlotKey = null;
                break;
            default:
                throw ApiException.BadRequest("unsupported_action", "إجراء غير مدعوم.");
        }
        audit.Add($"appointment.{action.ToString().ToLowerInvariant()}", me.Id, nameof(Appointment), a.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([a], ct))[0];
    }

    public async Task<ReviewDto> ReviewAsync(int id, int rating, string? comment, CancellationToken ct)
    {
        var a = await FindForActionAsync(id, AppointmentAction.Review, ct);
        var doctor = await db.DoctorProfiles.FirstAsync(d => d.UserId == a.DoctorId, ct);

        var review = new DoctorReview { DoctorId = a.DoctorId, PatientId = a.PatientId, AppointmentId = a.Id, Rating = rating, Comment = comment?.Trim() };
        db.DoctorReviews.Add(review);
        // متوسط جارٍ يحافظ على التقييمات التاريخية الموجودة في الملف
        doctor.Rating = Math.Round((doctor.Rating * doctor.ReviewsCount + rating) / (doctor.ReviewsCount + 1), 2, MidpointRounding.AwayFromZero);
        doctor.ReviewsCount++;
        notifications.Add(a.DoctorId, NotificationType.Appointment, "تقييم جديد", $"{rating} من 5", nameof(DoctorReview), null);
        await db.SaveChangesAsync(ct);
        var patient = await db.Users.AsNoTracking().FirstAsync(u => u.Id == a.PatientId, ct);
        return new ReviewDto(review.Id, DoctorCatalogService.ShortName(patient.FullName), review.Rating, review.Comment, review.CreatedAt);
    }

    // ------------------------------------------------------------ مساعدات

    private IQueryable<Appointment> Scoped()
    {
        var q = db.Appointments.AsNoTracking();
        return me.Role switch
        {
            UserRole.Patient => q.Where(a => a.PatientId == me.Id),
            UserRole.Doctor => q.Where(a => a.DoctorId == me.Id),
            UserRole.Reception or UserRole.Nurse or UserRole.Branch => me.BranchId is { } b ? q.Where(a => a.BranchId == b) : q.Where(_ => false),
            UserRole.Admin or UserRole.Hospital => q,
            _ => throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية عرض المواعيد."),
        };
    }

    private async Task<Appointment> FindAsync(int id, CancellationToken ct)
    {
        var scoped = Scoped();
        var exists = await scoped.AnyAsync(a => a.Id == id, ct);
        // خارج النطاق = غير موجود (لا نكشف وجود مواعيد الآخرين)
        if (!exists) throw ApiException.NotFound("appointment_not_found", "الموعد غير موجود.");
        return await db.Appointments.FirstAsync(a => a.Id == id, ct);
    }

    /// <summary>يحمّل الموعد (مُتتبَّعًا) ويتحقق من أن للمستخدم هذا الإجراء في حالته الحالية.</summary>
    private async Task<Appointment> FindForActionAsync(int id, AppointmentAction action, CancellationToken ct)
    {
        var a = await FindAsync(id, ct);
        var own = (a.PatientId == me.Id, a.DoctorId == me.Id);
        var reviewed = action == AppointmentAction.Review && await db.DoctorReviews.AnyAsync(r => r.AppointmentId == a.Id, ct);
        if (AppointmentWorkflow.Allowed(me.Role, own.Item1, own.Item2, a.Status, a.Type, reviewed).Contains(action)) return a;

        if (!AppointmentWorkflow.RoleMayEverDo(me.Role, own.Item1, own.Item2, action))
            throw ApiException.Forbidden("action_not_allowed", "ليست لديك صلاحية هذا الإجراء.");
        throw ApiException.Conflict(reviewed ? "already_reviewed" : "invalid_state", reviewed ? "قيّمت هذه الزيارة سابقًا." : $"لا يمكن تنفيذ هذا الإجراء والموعد في حالة «{StatusAr(a.Status)}».");
    }

    private void RequireToday(Appointment a)
    {
        if (a.Date != clock.Today) throw ApiException.Conflict("not_today", "هذا الإجراء متاح يوم الموعد فقط.");
    }

    private async Task NotifyOtherPartyAsync(Appointment a, string title, string text, CancellationToken ct)
    {
        // يُبلَّغ الطرف الآخر؛ وإن كان المنفِّذ هو الاستقبال يُبلَّغ الاثنان
        var targets = new List<int>();
        if (me.Id != a.PatientId) targets.Add(a.PatientId);
        if (me.Id != a.DoctorId) targets.Add(a.DoctorId);
        foreach (var t in targets) notifications.Add(t, NotificationType.Appointment, title, text, nameof(Appointment), a.Id);
        await Task.CompletedTask;
    }

    private async Task<List<AppointmentDto>> ToDtosAsync(List<Appointment> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var userIds = rows.SelectMany(a => new[] { a.PatientId, a.DoctorId }).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var profiles = await db.DoctorProfiles.AsNoTracking().Include(d => d.Specialty).Where(d => userIds.Contains(d.UserId)).ToDictionaryAsync(d => d.UserId, ct);
        var famIds = rows.Where(a => a.FamilyMemberId != null).Select(a => a.FamilyMemberId!.Value).Distinct().ToList();
        var family = await db.FamilyMembers.AsNoTracking().Where(f => famIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name, ct);
        var apptIds = rows.Select(a => a.Id).ToList();
        var reviewed = (await db.DoctorReviews.AsNoTracking().Where(r => apptIds.Contains(r.AppointmentId)).Select(r => r.AppointmentId).ToListAsync(ct)).ToHashSet();
        var year = clock.Today.Year;

        return rows.Select(a =>
        {
            var p = users[a.PatientId];
            var d = users[a.DoctorId];
            var isReviewed = reviewed.Contains(a.Id);
            var actions = me.IsAuthenticated ? AppointmentWorkflow.Allowed(me.Role, a.PatientId == me.Id, a.DoctorId == me.Id, a.Status, a.Type, isReviewed) : [];
            return new AppointmentDto(
                a.Id, new PersonRefDto(p.Id, p.FullName, p.AvatarKey, p.BirthYear is { } by ? year - by : null),
                a.FamilyMemberId is { } f ? family.GetValueOrDefault(f) : null,
                new PersonRefDto(d.Id, d.FullName, d.AvatarKey), profiles.TryGetValue(d.Id, out var prof) ? prof.Specialty.NameAr : "",
                a.BranchId, a.Date, a.StartMinutes, a.DurationMinutes, a.Type, a.Status, a.Reason, a.Notes,
                a.Price, a.InsuranceCovered, a.Price - a.InsuranceCovered, a.PaymentMethod, a.Paid, a.CancelReason, a.CreatedAt, actions, isReviewed);
        }).ToList();
    }

    public static string FormatTime(int minutes)
    {
        var h = minutes / 60;
        var h12 = h % 12 == 0 ? 12 : h % 12;
        return $"{h12:D2}:{minutes % 60:D2} {(h < 12 ? "ص" : "م")}";
    }

    private static string StatusAr(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Pending => "بانتظار التأكيد",
        AppointmentStatus.Confirmed => "مؤكد",
        AppointmentStatus.Arrived => "وصل المريض",
        AppointmentStatus.InProgress => "قيد الكشف",
        AppointmentStatus.Done => "منتهي",
        AppointmentStatus.Cancelled => "ملغى",
        _ => "لم يحضر",
    };
}

public enum AppointmentScope { All, Upcoming, Past, Cancelled }

public sealed record AppointmentQuery(AppointmentScope Scope, DateOnly? Date, DateOnly? From, DateOnly? To, AppointmentStatus? Status, int? Doctor, int? PatientFilter, int? Page, int? PageSize);
