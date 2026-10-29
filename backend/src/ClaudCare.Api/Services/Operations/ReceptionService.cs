using ClaudCare.Api.Auth;
using ClaudCare.Api.Contracts;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using ClaudCare.Api.Services.Scheduling;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace ClaudCare.Api.Services.Operations;

/// <summary>الاستقبال: تذاكر الطابور اليومية (ترقيم لكل فرع/يوم)، تسجيل مريض زائر، ولوحة اليوم.</summary>
public sealed class ReceptionService(ClaudCareDbContext db, StaffScope scope, ICurrentUser me, IClinicClock clock, IPasswordHasher<User> hasher, IAuditService audit, TimeProvider time)
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
    private static readonly UserRole[] Desk = [UserRole.Reception, UserRole.Branch];

    public async Task<TicketDto> IssueAsync(IssueTicketRequest r, CancellationToken ct)
    {
        scope.Require(Desk);
        var branch = scope.BranchForNew();
        string name;
        if (r.PatientId is { } pid)
            name = (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == pid && u.Role == UserRole.Patient, ct))?.FullName ?? throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");
        else name = string.IsNullOrWhiteSpace(r.PatientName) ? throw ApiException.Validation(nameof(r.PatientName), "حدّد المريض أو اسمه") : r.PatientName.Trim();
        if (r.DoctorId is { } d && !await db.Users.AnyAsync(u => u.Id == d && u.Role == UserRole.Doctor, ct)) throw ApiException.NotFound("doctor_not_found", "الطبيب غير موجود.");

        var today = clock.Today;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var next = (await db.QueueTickets.Where(t => t.BranchId == branch && t.Date == today).MaxAsync(t => (int?)t.TicketNo, ct) ?? 0) + 1;
            var t = new QueueTicket { BranchId = branch, Date = today, TicketNo = next, PatientId = r.PatientId, PatientName = name, DoctorId = r.DoctorId, CreatedAt = UtcNow };
            db.QueueTickets.Add(t);
            try { await db.SaveChangesAsync(ct); return ToDto(t); }
            catch (DbUpdateException ex) when (ex.GetBaseException() is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
            {
                db.Entry(t).State = EntityState.Detached; // تذكرة متزامنة أخذت الرقم؛ أعد المحاولة بعد تأخير عشوائي قصير
                await Task.Delay(Random.Shared.Next(5, 40), ct);
            }
        }
        throw ApiException.Conflict("queue_busy", "تعذّر إصدار التذكرة، حاول مجددًا.");
    }

    public async Task<IReadOnlyList<TicketDto>> QueueAsync(DateOnly? date, QueueStatus? status, CancellationToken ct)
    {
        scope.Require(Desk.Append(UserRole.Nurse).Append(UserRole.Doctor).ToArray());
        var b = scope.BranchFilter;
        var d = date ?? clock.Today;
        var q = db.QueueTickets.AsNoTracking().Where(t => (b == null || t.BranchId == b) && t.Date == d);
        if (me.Role == UserRole.Doctor) q = q.Where(t => t.DoctorId == me.Id);
        if (status is { } s) q = q.Where(t => t.Status == s);
        return (await q.OrderBy(t => t.TicketNo).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<TicketDto> TransitionAsync(int id, QueueStatus target, CancellationToken ct)
    {
        scope.Require(Desk.Append(UserRole.Doctor).ToArray());
        var t = await db.QueueTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        var visible = t is not null && scope.InBranch(t.BranchId) && (me.Role != UserRole.Doctor || t.DoctorId == me.Id);
        if (!visible) throw ApiException.NotFound("ticket_not_found", "التذكرة غير موجودة.");
        var ok = (t!.Status, target) switch
        {
            (QueueStatus.Waiting, QueueStatus.Called) => true,
            (QueueStatus.Called, QueueStatus.Done) => true,
            (QueueStatus.Waiting or QueueStatus.Called, QueueStatus.Cancelled) => me.Role != UserRole.Doctor,
            _ => false,
        };
        if (!ok) throw ApiException.Conflict("invalid_state", $"لا يمكن الانتقال من «{t.Status}» إلى «{target}».");
        t.Status = target;
        if (target == QueueStatus.Called) t.CalledAt = UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    /// <summary>تسجيل مريض زائر. لا كلمة مرور قابلة للاستخدام: يفعّل حسابه لاحقًا بـ«نسيت كلمة المرور» على جواله.</summary>
    public async Task<PersonRefDto> RegisterPatientAsync(RegisterPatientRequest r, CancellationToken ct)
    {
        scope.Require(Desk);
        var phone = PhoneNormalizer.TryNormalize(r.Phone) ?? throw ApiException.Validation(nameof(r.Phone), "رقم الجوال غير صالح");
        string? nid = null;
        if (!string.IsNullOrWhiteSpace(r.NationalId)) nid = PhoneNormalizer.TryNationalId(r.NationalId) ?? throw ApiException.Validation(nameof(r.NationalId), "رقم الهوية غير صالح");
        if (await db.Users.AnyAsync(u => u.Phone == phone, ct)) throw ApiException.Conflict("phone_taken", "رقم الجوال مسجّل مسبقًا.");
        if (nid is not null && await db.Users.AnyAsync(u => u.NationalId == nid, ct)) throw ApiException.Conflict("national_id_taken", "رقم الهوية مسجّل مسبقًا.");

        var u = new User { FullName = r.FullName.Trim(), Phone = phone, NationalId = nid, Role = UserRole.Patient, BirthYear = r.BirthYear, Gender = r.Gender, BranchId = scope.BranchForNew(), PhoneConfirmed = false };
        u.PasswordHash = hasher.HashPassword(u, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)) + "aA1!");
        db.Users.Add(u);
        await db.SaveChangesAsync(ct);
        db.PatientProfiles.Add(new PatientProfile { UserId = u.Id, MedicalRecordNo = $"MRN-{u.Id:D6}" });
        audit.Add("reception.register_patient", me.Id, nameof(User), u.Id, ip: me.Ip);
        await db.SaveChangesAsync(ct);
        return new PersonRefDto(u.Id, u.FullName, u.AvatarKey);
    }

    public async Task<ReceptionTodayDto> TodayAsync(CancellationToken ct)
    {
        scope.Require(Desk);
        var b = scope.BranchFilter;
        var today = clock.Today;
        var appts = await db.Appointments.AsNoTracking().Where(a => (b == null || a.BranchId == b) && a.Date == today).GroupBy(a => a.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var queue = await db.QueueTickets.AsNoTracking().Where(t => (b == null || t.BranchId == b) && t.Date == today).GroupBy(t => t.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        int A(AppointmentStatus s) => appts.FirstOrDefault(x => x.Key == s)?.N ?? 0;
        int Q(QueueStatus s) => queue.FirstOrDefault(x => x.Key == s)?.N ?? 0;
        return new ReceptionTodayDto(today, appts.Sum(x => x.N), A(AppointmentStatus.Pending), A(AppointmentStatus.Confirmed), A(AppointmentStatus.Arrived), A(AppointmentStatus.InProgress), A(AppointmentStatus.Done), A(AppointmentStatus.Cancelled),
            Q(QueueStatus.Waiting), Q(QueueStatus.Called), Q(QueueStatus.Done));
    }

    private TicketDto ToDto(QueueTicket t) => new(t.Id, t.TicketNo, t.PatientName ?? "", t.PatientId, t.DoctorId, t.Status, t.CreatedAt, t.CalledAt,
        t.Status == QueueStatus.Waiting ? (int)Math.Max(0, (UtcNow - t.CreatedAt).TotalMinutes) : 0);
}
