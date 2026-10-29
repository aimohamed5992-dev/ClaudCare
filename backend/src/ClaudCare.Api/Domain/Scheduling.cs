using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

/// <summary>
/// موعد. التاريخ والوقت بتوقيت العيادة (DateOnly + دقائق من منتصف الليل) لتفادي أخطاء المناطق الزمنية.
/// </summary>
[Index(nameof(ActiveSlotKey), IsUnique = true)]
[Index(nameof(DoctorId), nameof(Date))]
[Index(nameof(PatientId), nameof(Date))]
public class Appointment : AuditedEntity
{
    public int PatientId { get; set; }
    [ForeignKey(nameof(PatientId))] public User Patient { get; set; } = null!;
    /// <summary>إن كان الموعد لأحد أفراد العائلة.</summary>
    public int? FamilyMemberId { get; set; }
    public FamilyMember? FamilyMember { get; set; }
    public int DoctorId { get; set; }
    [ForeignKey(nameof(DoctorId))] public User Doctor { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public DateOnly Date { get; set; }
    public int StartMinutes { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public VisitType Type { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    [MaxLength(500)] public string Reason { get; set; } = string.Empty;
    [MaxLength(500)] public string? Notes { get; set; }

    public decimal Price { get; set; }
    public decimal InsuranceCovered { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public bool Paid { get; set; }
    public int? InvoiceId { get; set; }

    [MaxLength(300)] public string? CancelReason { get; set; }
    public int? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// مفتاح فريد «طبيب|تاريخ|وقت» يُملأ فقط للمواعيد النشطة؛ يمنع الحجز المزدوج على مستوى قاعدة البيانات
    /// (MySQL يسمح بعدة NULL في الفهرس الفريد فيتحرر المفتاح عند الإلغاء).
    /// </summary>
    [MaxLength(40)] public string? ActiveSlotKey { get; set; }

    public static string SlotKey(int doctorId, DateOnly date, int startMinutes) => $"{doctorId}|{date.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)}|{startMinutes}";
}
