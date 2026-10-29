using ClaudCare.Api.Auth;
using ClaudCare.Api.Data;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Services.Clinical;

public enum AccessLevel { Demographics, Clinical }

/// <summary>
/// من يستطيع الاطلاع على ملف أي مريض؟
/// المريض: نفسه فقط. الطبيب: مرضاه (من له معهم موعد غير ملغى أو كشف). التمريض/الطوارئ/التنويم/العمليات: سريريًا لكل المرضى.
/// الاستقبال والإدارة: بيانات التعريف فقط. المختبر والأشعة والصيدلية: لا شيء من هنا (لهم قوائم عملهم).
/// مريض خارج النطاق ⇒ 404 (لا نكشف وجوده).
/// </summary>
public sealed class PatientAccessService(ClaudCareDbContext db, ICurrentUser me)
{
    private static readonly UserRole[] ClinicalAnyPatient = [UserRole.Nurse, UserRole.Emergency, UserRole.Admissions, UserRole.Surgery];
    private static readonly UserRole[] DemographicsOnly = [UserRole.Reception, UserRole.Branch, UserRole.Hospital, UserRole.Admin];

    public bool CanSeeClinicalInGeneral => me.Role is UserRole.Doctor or UserRole.Patient || ClinicalAnyPatient.Contains(me.Role);

    public async Task<User> RequireAsync(int patientId, AccessLevel level, CancellationToken ct)
    {
        var patient = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == patientId && u.Role == UserRole.Patient, ct)
                      ?? throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");

        var allowed = me.Role switch
        {
            UserRole.Patient => me.Id == patientId,
            UserRole.Doctor => await db.Appointments.AnyAsync(a => a.DoctorId == me.Id && a.PatientId == patientId && a.Status != AppointmentStatus.Cancelled, ct)
                               || await db.Visits.AnyAsync(v => v.DoctorId == me.Id && v.PatientId == patientId, ct),
            var r when ClinicalAnyPatient.Contains(r) => true,
            var r when DemographicsOnly.Contains(r) => level == AccessLevel.Demographics,
            _ => false,
        };
        if (!allowed) throw ApiException.NotFound("patient_not_found", "المريض غير موجود.");
        return patient;
    }

    /// <summary>الأدوار التي تكتب في الملف الطبي (الحساسية والأمراض المزمنة).</summary>
    public static bool CanWriteClinical(UserRole role) => role is UserRole.Doctor or UserRole.Nurse or UserRole.Emergency;
}
