using ClaudCare.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClaudCare.Api.Data;

/// <summary>سياق قاعدة بيانات ClaudCare (MySQL/MariaDB عبر Pomelo).</summary>
public class ClaudCareDbContext(DbContextOptions<ClaudCareDbContext> options, TimeProvider clock) : DbContext(options)
{
    // الهوية والمؤسسة
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // الأشخاص
    public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();
    public DbSet<DoctorWorkingHour> DoctorWorkingHours => Set<DoctorWorkingHour>();
    public DbSet<FavoriteDoctor> FavoriteDoctors => Set<FavoriteDoctor>();
    public DbSet<DoctorReview> DoctorReviews => Set<DoctorReview>();
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();
    public DbSet<PatientCondition> PatientConditions => Set<PatientCondition>();
    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ProfessionalLicense> ProfessionalLicenses => Set<ProfessionalLicense>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<Shift> Shifts => Set<Shift>();

    // المواعيد والسريري
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<Drug> Drugs => Set<Drug>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<PatientMedication> PatientMedications => Set<PatientMedication>();
    public DbSet<DoseLog> DoseLogs => Set<DoseLog>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<LabResultItem> LabResultItems => Set<LabResultItem>();
    public DbSet<ImagingDevice> ImagingDevices => Set<ImagingDevice>();
    public DbSet<ImagingOrder> ImagingOrders => Set<ImagingOrder>();
    public DbSet<ImagingReport> ImagingReports => Set<ImagingReport>();

    // الصيدلية
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PharmacySale> PharmacySales => Set<PharmacySale>();
    public DbSet<PharmacySaleLine> PharmacySaleLines => Set<PharmacySaleLine>();

    // المرافق
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<Admission> Admissions => Set<Admission>();
    public DbSet<BedTransfer> BedTransfers => Set<BedTransfer>();
    public DbSet<VitalSignRecord> VitalSignRecords => Set<VitalSignRecord>();
    public DbSet<NursingNote> NursingNotes => Set<NursingNote>();
    public DbSet<MedicationAdministration> MedicationAdministrations => Set<MedicationAdministration>();
    public DbSet<QueueTicket> QueueTickets => Set<QueueTicket>();
    public DbSet<EmergencyCase> EmergencyCases => Set<EmergencyCase>();
    public DbSet<Ambulance> Ambulances => Set<Ambulance>();
    public DbSet<OperatingRoom> OperatingRooms => Set<OperatingRoom>();
    public DbSet<SurgeryCase> SurgeryCases => Set<SurgeryCase>();
    public DbSet<PreOpCheckItem> PreOpCheckItems => Set<PreOpCheckItem>();
    public DbSet<SurgeryTeamMember> SurgeryTeamMembers => Set<SurgeryTeamMember>();

    // المالية والتأمين
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<PayrollRecord> PayrollRecords => Set<PayrollRecord>();
    public DbSet<DailyClose> DailyCloses => Set<DailyClose>();
    public DbSet<InsuranceCompany> InsuranceCompanies => Set<InsuranceCompany>();
    public DbSet<PatientInsurance> PatientInsurances => Set<PatientInsurance>();
    public DbSet<PreAuthorization> PreAuthorizations => Set<PreAuthorization>();
    public DbSet<PreAuthDocument> PreAuthDocuments => Set<PreAuthDocument>();
    public DbSet<InsuranceClaim> InsuranceClaims => Set<InsuranceClaim>();

    // التواصل
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>الكيانات التابعة التي تُحذف مع أصلها؛ كل ما عداها يمنع الحذف (Restrict) حفاظًا على السجلات الطبية والمالية.</summary>
    private static readonly HashSet<Type> CascadeChildren =
    [
        typeof(RefreshToken), typeof(UserSetting), typeof(Notification), typeof(DoctorProfile), typeof(PatientProfile),
        typeof(PatientAllergy), typeof(PatientCondition), typeof(ProfessionalLicense), typeof(Shift),
        typeof(PrescriptionItem), typeof(LabResultItem), typeof(ImagingReport), typeof(DoseLog), typeof(FavoriteDoctor),
        typeof(InvoiceItem), typeof(Payment), typeof(PreAuthDocument), typeof(PurchaseOrderLine), typeof(PharmacySaleLine),
        typeof(PreOpCheckItem), typeof(SurgeryTeamMember), typeof(Message), typeof(ConversationParticipant),
    ];

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        // كل التعدادات تُخزَّن نصوصًا مقروءة
        foreach (var e in typeof(UserRole).Assembly.GetTypes().Where(t => t.IsEnum && t.Namespace == typeof(UserRole).Namespace))
            cb.Properties(e).HaveConversion(typeof(EnumToStringConverter<>).MakeGenericType(e)).HaveMaxLength(30);
        cb.Properties<DayOfWeek>().HaveConversion<string>().HaveMaxLength(12);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.UseCollation("utf8mb4_unicode_ci");

        // افتراضيات للخصائص التي لم تُضبط صراحةً (الخصائص ذات [MaxLength]/[Precision]/TypeName تبقى كما هي)
        foreach (var prop in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (prop.ClrType == typeof(string) && prop.GetMaxLength() is null && prop.GetColumnType() is null && prop.GetValueConverter() is null)
                prop.SetMaxLength(250);
            else if ((prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?)) && prop.GetPrecision() is null)
            {
                prop.SetPrecision(12);
                prop.SetScale(2);
            }
        }

        foreach (var fk in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = CascadeChildren.Contains(fk.DeclaringEntityType.ClrType) ? DeleteBehavior.Cascade : DeleteBehavior.Restrict;

        // بيانات مرجعية: التخصصات (نفس مفاتيح تطبيق المريض)
        b.Entity<Specialty>().HasData(
            new Specialty { Id = 1, Key = "cardiology", NameAr = "القلب", NameEn = "Cardiology" },
            new Specialty { Id = 2, Key = "pediatrics", NameAr = "الأطفال", NameEn = "Pediatrics" },
            new Specialty { Id = 3, Key = "dermatology", NameAr = "الجلدية", NameEn = "Dermatology" },
            new Specialty { Id = 4, Key = "orthopedics", NameAr = "العظام", NameEn = "Orthopedics" },
            new Specialty { Id = 5, Key = "neurology", NameAr = "الأعصاب", NameEn = "Neurology" },
            new Specialty { Id = 6, Key = "obgyn", NameAr = "النساء والولادة", NameEn = "Obstetrics & Gynecology" },
            new Specialty { Id = 7, Key = "ophthalmology", NameAr = "العيون", NameEn = "Ophthalmology" },
            new Specialty { Id = 8, Key = "ent", NameAr = "أنف وأذن وحنجرة", NameEn = "ENT" },
            new Specialty { Id = 9, Key = "dentistry", NameAr = "الأسنان", NameEn = "Dentistry" },
            new Specialty { Id = 10, Key = "nutrition", NameAr = "التغذية", NameEn = "Nutrition" },
            new Specialty { Id = 11, Key = "internal", NameAr = "الباطنة", NameEn = "Internal Medicine" },
            new Specialty { Id = 12, Key = "surgery", NameAr = "الجراحة", NameEn = "Surgery" },
            new Specialty { Id = 13, Key = "anesthesia", NameAr = "التخدير", NameEn = "Anesthesia" },
            new Specialty { Id = 14, Key = "radiology", NameAr = "الأشعة", NameEn = "Radiology" },
            new Specialty { Id = 15, Key = "emergency", NameAr = "الطوارئ", NameEn = "Emergency Medicine" });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        Stamp();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        Stamp();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    /// <summary>يختم CreatedAt/UpdatedAt (UTC) تلقائيًا.</summary>
    private void Stamp()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var e in ChangeTracker.Entries<IAuditable>())
        {
            if (e.State == EntityState.Added && e.Entity.CreatedAt == default) e.Entity.CreatedAt = now;
            else if (e.State == EntityState.Modified) e.Entity.UpdatedAt = now;
        }
    }
}
