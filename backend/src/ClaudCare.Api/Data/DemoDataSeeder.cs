using ClaudCare.Api.Configuration;
using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Clinical;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaudCare.Api.Data;

/// <summary>
/// بيانات عرض كاملة تغطي كل الأدوار (تُزرع مرة واحدة فقط بعد <see cref="DevSeeder"/>؛ تُعلَّم بـ SystemSetting).
/// التواريخ نسبية لليوم فتبقى الشاشات حيّة. مفعّلة فقط مع <c>Seed:DemoAccounts</c> (ترفضها بيئة الإنتاج عند الإقلاع).
/// </summary>
public sealed class DemoDataSeeder(ClaudCareDbContext db, IPasswordHasher<User> hasher, IOptions<SeedOptions> options, IOptions<ClinicOptions> clinic, TimeProvider time, ILogger<DemoDataSeeder> logger)
{
    public const string Marker = "demo.data";
    public const string Version = "v1";

    private DateTime Now => time.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(Now.AddHours(clinic.Value.FallbackUtcOffsetHours));

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!options.Value.DemoAccounts) return;
        if (await db.SystemSettings.AnyAsync(s => s.Key == Marker && s.Value == Version, ct)) return;

        var branch = await db.Branches.OrderBy(b => b.Id).FirstOrDefaultAsync(ct);
        var reem = await db.Users.FirstOrDefaultAsync(u => u.Email == "reem@claudcare.test", ct);
        var mona = await db.Users.FirstOrDefaultAsync(u => u.Email == "mona@hospital.com", ct);
        if (branch is null || reem is null || mona is null) { logger.LogWarning("تخطّي بيانات العرض: الحسابات الأساسية غير موجودة."); return; }

        // حسابات الديمو الأساسية فقط (واحد لكل دور؛ الأطباء الإضافيون بدومين آخر)
        var users = (await db.Users.Where(u => u.Email != null && u.Email.EndsWith("@claudcare.test")).OrderBy(u => u.Id).ToListAsync(ct)).GroupBy(u => u.Role).ToDictionary(g => g.Key, g => g.First());
        var drugs = await db.Drugs.ToDictionaryAsync(d => d.Name, ct);
        var otherDoctor = await db.Users.Where(u => u.Role == UserRole.Doctor && u.Id != mona.Id).OrderBy(u => u.Id).FirstOrDefaultAsync(ct) ?? mona;

        var patients = await SeedPatientsAsync(branch, ct);
        var (ahmed, sara, khaled, noura) = (patients[0], patients[1], patients[2], patients[3]);

        await SeedPeopleAsync(branch, users, mona, ct);
        await SeedFacilitiesAsync(branch, ct);
        await SeedClinicalHistoryAsync(branch, reem, mona, drugs, ct);
        await SeedPharmacyAsync(branch, users, drugs, ahmed, mona, ct);
        await SeedOrdersAsync(branch, mona, ahmed, sara, ct);
        await SeedWardsAsync(branch, mona, ahmed, sara, noura, ct);
        await SeedOperationsAsync(branch, users, otherDoctor, ahmed, ct);
        await SeedFinanceAsync(branch, reem, ahmed, ct);
        await SeedCommunicationAsync(reem, mona, ct);

        db.SystemSettings.Add(new SystemSetting { Key = Marker, Value = Version, Category = "seed" });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("زُرعت بيانات العرض ({Version}).", Version);
    }

    // ------------------------------------------------------------ المرضى

    private async Task<List<User>> SeedPatientsAsync(Branch branch, CancellationToken ct)
    {
        (string Name, string Phone, string Nid, int Year, Gender G)[] rows =
        [
            ("أحمد الزهراني", "0551000001", "1011111111", 1985, Gender.Male), ("سارة القحطاني", "0551000002", "1022222222", 1992, Gender.Female),
            ("خالد العتيبي", "0551000003", "1033333333", Year(10), Gender.Male), ("نورة السبيعي", "0551000004", "1044444444", 1958, Gender.Female),
        ];
        var list = new List<User>();
        foreach (var r in rows)
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Phone == r.Phone, ct);
            if (u is null)
            {
                u = new User { FullName = r.Name, Phone = r.Phone, NationalId = r.Nid, Role = UserRole.Patient, Gender = r.G, BirthYear = r.Year, BranchId = branch.Id, PhoneConfirmed = true };
                u.PasswordHash = hasher.HashPassword(u, options.Value.DemoPassword);
                db.Users.Add(u);
                await db.SaveChangesAsync(ct);
                db.PatientProfiles.Add(new PatientProfile { UserId = u.Id, MedicalRecordNo = $"MRN-{u.Id:D6}", BloodType = "A+" });
                await db.SaveChangesAsync(ct);
            }
            list.Add(u);
        }
        return list;

        int Year(int age) => Today.Year - age;
    }

    // ------------------------------------------------------------ الموارد البشرية

    private async Task SeedPeopleAsync(Branch branch, Dictionary<UserRole, User> users, User mona, CancellationToken ct)
    {
        string[] depts = ["الإدارة", "التمريض", "المختبر", "الصيدلية", "الطوارئ", "الاستقبال"];
        foreach (var d in depts) if (!await db.Departments.AnyAsync(x => x.BranchId == branch.Id && x.Name == d, ct)) db.Departments.Add(new Department { BranchId = branch.Id, Name = d });
        await db.SaveChangesAsync(ct);
        var dep = await db.Departments.Where(d => d.BranchId == branch.Id).ToDictionaryAsync(d => d.Name, ct);

        (User U, string Title, decimal Salary, string Dept)[] staff =
        [
            (users[UserRole.Branch], "مدير الفرع", 24000, "الإدارة"), (users[UserRole.Lab], "فني مختبر", 9000, "المختبر"), (users[UserRole.Radiology], "أخصائي أشعة", 18000, "المختبر"),
            (users[UserRole.Pharmacy], "صيدلانية", 12000, "الصيدلية"), (mona, "استشارية قلب", 35000, "الإدارة"), (users[UserRole.Nurse], "ممرضة", 8500, "التمريض"),
            (users[UserRole.Reception], "موظفة استقبال", 6500, "الاستقبال"), (users[UserRole.Insurance], "أخصائي تأمين", 9500, "الإدارة"), (users[UserRole.Finance], "محاسبة", 11000, "الإدارة"),
            (users[UserRole.Hr], "موارد بشرية", 10500, "الإدارة"), (users[UserRole.Emergency], "طبيب طوارئ", 22000, "الطوارئ"), (users[UserRole.Admissions], "مسؤولة تنويم", 7500, "التمريض"),
            (users[UserRole.Surgery], "جراح", 40000, "الإدارة"),
        ];
        var employees = new List<Employee>();
        foreach (var s in staff)
        {
            var e = await db.Employees.FirstOrDefaultAsync(x => x.UserId == s.U.Id, ct);
            if (e is null)
            {
                e = new Employee { UserId = s.U.Id, BranchId = branch.Id, DepartmentId = dep[s.Dept].Id, FullName = s.U.FullName, JobTitle = s.Title, Phone = s.U.Phone, HireDate = Today.AddYears(-3), BaseSalary = s.Salary, LeaveBalanceDays = 21 };
                db.Employees.Add(e);
            }
            employees.Add(e);
        }
        await db.SaveChangesAsync(ct);

        var byUser = employees.ToDictionary(e => e.UserId!.Value);
        db.ProfessionalLicenses.AddRange(
            new ProfessionalLicense { EmployeeId = byUser[mona.Id].Id, Number = "SCFHS-38877", ExpiresAt = Today.AddMonths(14) },
            new ProfessionalLicense { EmployeeId = byUser[users[UserRole.Nurse].Id].Id, Number = "SCFHS-55120", ExpiresAt = Today.AddDays(28) },
            new ProfessionalLicense { EmployeeId = byUser[users[UserRole.Pharmacy].Id].Id, Number = "SCFHS-61007", ExpiresAt = Today.AddDays(75) },
            new ProfessionalLicense { EmployeeId = byUser[users[UserRole.Emergency].Id].Id, Number = "SCFHS-70231", ExpiresAt = Today.AddMonths(9) });

        foreach (var role in new[] { UserRole.Nurse, UserRole.Emergency, UserRole.Reception, UserRole.Lab })
            for (var i = 0; i < 7; i++)
            {
                var kind = role == UserRole.Emergency && i % 3 == 2 ? ShiftKind.Night : i % 2 == 0 ? ShiftKind.Morning : ShiftKind.Evening;
                var (st, en) = kind switch { ShiftKind.Morning => (420, 900), ShiftKind.Evening => (900, 1380), _ => (1380, 420) };
                db.Shifts.Add(new Shift { EmployeeId = byUser[users[role].Id].Id, Date = Today.AddDays(i), Kind = kind, StartMinutes = st, EndMinutes = en });
            }
        db.LeaveRequests.AddRange(
            new LeaveRequest { EmployeeId = byUser[users[UserRole.Nurse].Id].Id, Type = LeaveType.Annual, From = Today.AddDays(14), To = Today.AddDays(18), Days = 5, Reason = "إجازة عائلية" },
            new LeaveRequest { EmployeeId = byUser[users[UserRole.Reception].Id].Id, Type = LeaveType.Sick, From = Today.AddDays(2), To = Today.AddDays(3), Days = 2, Reason = "وعكة صحية" });
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ المرافق

    private async Task SeedFacilitiesAsync(Branch branch, CancellationToken ct)
    {
        foreach (var (ward, prefix, n) in new[] { ("الباطنة", "IM", 6), ("العناية المركزة", "ICU", 4), ("الجراحة", "SU", 6) })
            for (var i = 1; i <= n; i++)
            {
                var code = $"{prefix}-{i:D2}";
                if (!await db.Beds.AnyAsync(b => b.BranchId == branch.Id && b.Code == code, ct)) db.Beds.Add(new Bed { BranchId = branch.Id, Ward = ward, Code = code });
            }
        db.ImagingDevices.AddRange(
            new ImagingDevice { BranchId = branch.Id, Name = "أشعة سينية 1", Modality = "X-Ray" }, new ImagingDevice { BranchId = branch.Id, Name = "رنين مغناطيسي 3T", Modality = "MRI" },
            new ImagingDevice { BranchId = branch.Id, Name = "مقطعية 64", Modality = "CT" }, new ImagingDevice { BranchId = branch.Id, Name = "سونار 1", Modality = "Ultrasound", IsAvailable = false });
        foreach (var code in new[] { "OR-1", "OR-2", "OR-3" })
            if (!await db.OperatingRooms.AnyAsync(r => r.BranchId == branch.Id && r.Code == code, ct))
                db.OperatingRooms.Add(new OperatingRoom { BranchId = branch.Id, Code = code, State = code == "OR-3" ? OperatingRoomState.Maintenance : OperatingRoomState.Available, Note = code == "OR-3" ? "صيانة دورية" : null });
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ السجل السريري لريم

    private async Task SeedClinicalHistoryAsync(Branch branch, User reem, User mona, Dictionary<string, Drug> drugs, CancellationToken ct)
    {
        var profile = await db.PatientProfiles.Include(p => p.Allergies).Include(p => p.Conditions).FirstAsync(p => p.UserId == reem.Id, ct);
        if (profile.Allergies.Count == 0) profile.Allergies.Add(new PatientAllergy { Name = "البنسلين", Reaction = "طفح جلدي" });
        if (profile.Conditions.Count == 0) { profile.Conditions.Add(new PatientCondition { Name = "ارتفاع ضغط الدم", SinceYear = 2021 }); profile.Conditions.Add(new PatientCondition { Name = "نقص فيتامين د", SinceYear = 2024 }); }

        var past = Today.AddDays(-30);
        var appt = new Appointment { PatientId = reem.Id, DoctorId = mona.Id, BranchId = branch.Id, Date = past, StartMinutes = 600, DurationMinutes = 30, Type = VisitType.Clinic, Status = AppointmentStatus.Done,
            Reason = "متابعة ضغط", Price = 300, InsuranceCovered = 240, PaymentMethod = PaymentMethod.Card, Paid = true, CompletedAt = Now.AddDays(-30) };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync(ct);

        var inv = new Invoice { No = "INV-D0001", PatientId = reem.Id, BranchId = branch.Id, AppointmentId = appt.Id, Title = "كشف — د. منى القاسم", IssueDate = past, DueDate = past, Subtotal = 300, InsuranceCovered = 240, PaidAmount = 60,
            Status = InvoiceStatus.Paid, Items = [new InvoiceItem { Description = "كشف — د. منى القاسم", UnitPrice = 300 }],
            Payments = [new Payment { Amount = 60, Method = PaymentMethod.Card, PaidAt = Now.AddDays(-30), Reference = "SIM-DEMO0001" }] };
        db.Invoices.Add(inv);
        await db.SaveChangesAsync(ct);
        appt.InvoiceId = inv.Id;

        var visit = new Visit { AppointmentId = appt.Id, PatientId = reem.Id, DoctorId = mona.Id, BranchId = branch.Id, VisitDate = past, Complaint = "صداع وارتفاع ضغط", SystolicBp = 148, DiastolicBp = 92, HeartRate = 78, TemperatureC = 36.8m, Spo2 = 98,
            Diagnosis = "ارتفاع ضغط الدم الأساسي", Notes = "الالتزام بالدواء وتقليل الملح.", FollowUp = "بعد شهر" };
        db.Visits.Add(visit);
        await db.SaveChangesAsync(ct);

        var rx = new Prescription { No = "RX-D0001", PatientId = reem.Id, DoctorId = mona.Id, VisitId = visit.Id, BranchId = branch.Id, Status = PrescriptionStatus.Dispensed, DispensedAt = Now.AddDays(-30), DispensedByUserId = null };
        foreach (var (name, freq, times, days, instr) in new[] { ("ميتفورمين 500 مجم", "مرتين يوميًا", 2, 60, "بعد الأكل"), ("فيتامين د 50,000", "مرة أسبوعيًا", 1, 60, "بعد الغداء") })
        {
            if (!drugs.TryGetValue(name, out var d)) continue;
            rx.Items.Add(new PrescriptionItem { DrugId = d.Id, DrugName = d.Name, Dose = "قرص", Frequency = freq, TimesPerDay = times, DurationDays = days, Quantity = times * days, Instructions = instr, UnitPrice = d.UnitPrice, Dispensed = true });
        }
        db.Prescriptions.Add(rx);
        await db.SaveChangesAsync(ct);
        foreach (var item in rx.Items)
            db.PatientMedications.Add(new PatientMedication { PatientId = reem.Id, PrescriptionItemId = item.Id, TimesCsv = MedicationSchedule.ToCsv(MedicationSchedule.Times(item.TimesPerDay)), StartDate = past, EndDate = past.AddDays(item.DurationDays) });

        var lab = new LabOrder { No = "LAB-D0001", PatientId = reem.Id, DoctorId = mona.Id, VisitId = visit.Id, BranchId = branch.Id, TestName = "السكر التراكمي (HbA1c)", Category = "سكري", Status = LabOrderStatus.ResultReady,
            OrderedAt = Now.AddDays(-30), SampleCollectedAt = Now.AddDays(-30), ResultAt = Now.AddDays(-29) };
        lab.Items.Add(new LabResultItem { Name = "السكر التراكمي", Value = 5.9m, Unit = "%", RefMin = 4, RefMax = 5.6m, Flag = ResultFlag.High });
        db.LabOrders.Add(lab);
        db.ImagingOrders.Add(new ImagingOrder { No = "IMG-D0001", PatientId = reem.Id, DoctorId = mona.Id, BranchId = branch.Id, StudyName = "أشعة سينية للصدر", Modality = "X-Ray", Status = ImagingOrderStatus.Reported, OrderedAt = Now.AddDays(-30),
            Report = new ImagingReport { Findings = "الرئتان صافيتان وحجم القلب طبيعي.", Impression = "فحص طبيعي", Status = ReportStatus.Final, ReportedAt = Now.AddDays(-29) } });

        // موعد قادم وآخر اليوم لتظهر الشاشات
        db.Appointments.Add(new Appointment { PatientId = reem.Id, DoctorId = mona.Id, BranchId = branch.Id, Date = Today.AddDays(2), StartMinutes = 660, Type = VisitType.Clinic, Status = AppointmentStatus.Confirmed, Reason = "متابعة",
            Price = 300, InsuranceCovered = 240, PaymentMethod = PaymentMethod.PayAtClinic, ActiveSlotKey = Appointment.SlotKey(mona.Id, Today.AddDays(2), 660) });
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ الصيدلية

    private async Task SeedPharmacyAsync(Branch branch, Dictionary<UserRole, User> users, Dictionary<string, Drug> drugs, User ahmed, User mona, CancellationToken ct)
    {
        var i = 0;
        foreach (var d in drugs.Values.OrderBy(d => d.Id))
        {
            if (await db.InventoryItems.AnyAsync(x => x.BranchId == branch.Id && x.DrugId == d.Id, ct)) continue;
            var qty = i % 5 == 4 ? 6 : 60 + i * 23 % 400;   // كل خامس صنف منخفض
            db.InventoryItems.Add(new InventoryItem { BranchId = branch.Id, DrugId = d.Id, Quantity = qty, MinQuantity = 20, UnitCost = Math.Round(d.UnitPrice * 0.6m, 2), UnitPrice = d.UnitPrice,
                ExpiryDate = i == 2 ? Today.AddDays(45) : Today.AddMonths(10 + i % 8), Supplier = i % 2 == 0 ? "الدواء السعودي" : "مستودعات الخليج" });
            i++;
        }
        await db.SaveChangesAsync(ct);

        // وصفة بانتظار الصرف لأحمد
        var drug = drugs.Values.First(d => d.Group != "البنسلين");
        db.Prescriptions.Add(new Prescription { No = "RX-D0002", PatientId = ahmed.Id, DoctorId = mona.Id, BranchId = branch.Id, Status = PrescriptionStatus.Pending, Priority = OrderPriority.Urgent,
            Items = [new PrescriptionItem { DrugId = drug.Id, DrugName = drug.Name, Frequency = "مرتين يوميًا", TimesPerDay = 2, DurationDays = 5, Quantity = 10, UnitPrice = drug.UnitPrice }] });

        db.PharmacySales.Add(new PharmacySale { No = "SAL-D0001", BranchId = branch.Id, CustomerName = "عميل نقدي", Method = PaymentMethod.Cash, Total = drug.UnitPrice * 2, SoldByUserId = users[UserRole.Pharmacy].Id,
            Lines = [new PharmacySaleLine { DrugId = drug.Id, ItemName = drug.Name, Quantity = 2, UnitPrice = drug.UnitPrice }] });
        db.PurchaseOrders.Add(new PurchaseOrder { No = "PO-D0001", BranchId = branch.Id, Supplier = "الدواء السعودي", Status = PurchaseOrderStatus.Sent, OrderDate = Today.AddDays(-2), Total = drug.UnitPrice * 0.6m * 100, CreatedByUserId = users[UserRole.Pharmacy].Id,
            Lines = [new PurchaseOrderLine { DrugId = drug.Id, Quantity = 100, UnitCost = Math.Round(drug.UnitPrice * 0.6m, 2) }] });
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ مختبر وأشعة جارية

    private async Task SeedOrdersAsync(Branch branch, User mona, User ahmed, User sara, CancellationToken ct)
    {
        db.LabOrders.AddRange(
            new LabOrder { No = "LAB-D0002", PatientId = sara.Id, DoctorId = mona.Id, BranchId = branch.Id, TestName = "صورة دم كاملة (CBC)", Category = "دم", Priority = OrderPriority.Urgent, OrderedAt = Now.AddHours(-2) },
            new LabOrder { No = "LAB-D0003", PatientId = ahmed.Id, DoctorId = mona.Id, BranchId = branch.Id, TestName = "وظائف الكلى", Category = "كلى", Status = LabOrderStatus.SampleCollected, OrderedAt = Now.AddHours(-3), SampleCollectedAt = Now.AddHours(-1) });
        db.ImagingOrders.AddRange(
            new ImagingOrder { No = "IMG-D0002", PatientId = ahmed.Id, DoctorId = mona.Id, BranchId = branch.Id, StudyName = "رنين مغناطيسي للركبة", Modality = "MRI", OrderedAt = Now.AddHours(-5), ClinicalNotes = "ألم مزمن" },
            new ImagingOrder { No = "IMG-D0003", PatientId = sara.Id, DoctorId = mona.Id, BranchId = branch.Id, StudyName = "أشعة سينية للصدر", Modality = "X-Ray", OrderedAt = Now.AddHours(-1) });
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ التنويم والطوارئ والاستقبال

    private async Task SeedWardsAsync(Branch branch, User mona, User ahmed, User sara, User noura, CancellationToken ct)
    {
        var beds = await db.Beds.Where(b => b.BranchId == branch.Id).OrderBy(b => b.Id).ToListAsync(ct);
        var internalBed = beds.First(b => b.Ward == "الباطنة"); var icuBed = beds.First(b => b.Ward == "العناية المركزة");
        internalBed.Status = BedStatus.Occupied; icuBed.Status = BedStatus.Occupied;
        beds.Where(b => b.Ward == "الجراحة").ElementAt(0).Status = BedStatus.Cleaning;
        var reserved = beds.Where(b => b.Ward == "الجراحة").ElementAt(1); reserved.Status = BedStatus.Reserved; reserved.ReservedFor = "عملية مجدولة غدًا";

        var a1 = new Admission { No = "ADM-D0001", PatientId = noura.Id, BranchId = branch.Id, BedId = internalBed.Id, DoctorId = mona.Id, Type = AdmissionType.Planned, Reason = "متابعة قصور القلب", RequestedWard = "الباطنة", Status = AdmissionStatus.Admitted,
            RequestedAt = Now.AddDays(-2), AdmittedAt = Now.AddDays(-2), InsuranceApprovalVerified = true };
        var a2 = new Admission { No = "ADM-D0002", PatientId = ahmed.Id, BranchId = branch.Id, BedId = icuBed.Id, DoctorId = mona.Id, Type = AdmissionType.Emergency, Reason = "ألم صدر حاد", RequestedWard = "العناية المركزة", Status = AdmissionStatus.Admitted,
            RequestedAt = Now.AddDays(-1), AdmittedAt = Now.AddDays(-1) };
        var a3 = new Admission { No = "ADM-D0003", PatientId = sara.Id, BranchId = branch.Id, DoctorId = mona.Id, Type = AdmissionType.Planned, Reason = "عملية مجدولة", RequestedWard = "الجراحة", RequestedAt = Now.AddHours(-6), InsuranceApprovalVerified = true };
        db.Admissions.AddRange(a1, a2, a3);
        await db.SaveChangesAsync(ct);

        db.VitalSignRecords.AddRange(
            new VitalSignRecord { PatientId = noura.Id, AdmissionId = a1.Id, RecordedAt = Now.AddHours(-8), SystolicBp = 132, DiastolicBp = 84, HeartRate = 76, TemperatureC = 36.7m, Spo2 = 96, RespiratoryRate = 16 },
            new VitalSignRecord { PatientId = noura.Id, AdmissionId = a1.Id, RecordedAt = Now.AddHours(-2), SystolicBp = 128, DiastolicBp = 80, HeartRate = 72, TemperatureC = 36.6m, Spo2 = 97, RespiratoryRate = 15 },
            new VitalSignRecord { PatientId = ahmed.Id, AdmissionId = a2.Id, RecordedAt = Now.AddHours(-1), SystolicBp = 165, DiastolicBp = 98, HeartRate = 112, TemperatureC = 37.1m, Spo2 = 93, RespiratoryRate = 22, Notes = "ألم صدر مستمر" });
        db.MedicationAdministrations.AddRange(
            new MedicationAdministration { AdmissionId = a1.Id, DrugName = "فوروسيميد", Dose = "40 مجم", DueAt = Now.AddHours(-6), GivenAt = Now.AddHours(-6), Status = DoseStatus.Given },
            new MedicationAdministration { AdmissionId = a1.Id, DrugName = "فوروسيميد", Dose = "40 مجم", DueAt = Now.AddMinutes(-50), Status = DoseStatus.Due },
            new MedicationAdministration { AdmissionId = a1.Id, DrugName = "أسبرين", Dose = "100 مجم", DueAt = Now.AddHours(2), Status = DoseStatus.Due });

        db.EmergencyCases.AddRange(
            new EmergencyCase { BranchId = branch.Id, PatientName = "رجل 55 سنة", Age = 55, Complaint = "ألم صدر وضيق نفس", TriageLevel = 1, Zone = "الإنعاش", ArrivedAt = Now.AddMinutes(-9), SystolicBp = 88, HeartRate = 128, Spo2 = 89, RedFlags = "ألم صدر" },
            new EmergencyCase { BranchId = branch.Id, PatientName = "طفلة 6 سنوات", Age = 6, Complaint = "حمى عالية وتقيؤ", TriageLevel = 3, Zone = "الأصفر", ArrivedAt = Now.AddMinutes(-35), HeartRate = 120, TemperatureC = 39.4m },
            new EmergencyCase { BranchId = branch.Id, PatientName = "شاب 24 سنة", Age = 24, Complaint = "التواء كاحل", TriageLevel = 4, Zone = "الأخضر", ArrivedAt = Now.AddMinutes(-70), PainScore = 5 });
        db.Ambulances.Add(new Ambulance { BranchId = branch.Id, UnitNo = "A-12", PatientDescription = "امرأة 67 سنة", Condition = "اشتباه جلطة دماغية", TriageLevel = 1, EtaMinutes = 7 });

        var today = Today;
        for (var n = 1; n <= 4; n++)
            db.QueueTickets.Add(new QueueTicket { BranchId = branch.Id, Date = today, TicketNo = n, PatientName = new[] { "عبدالله", "مها", "فهد", "جواهر" }[n - 1], DoctorId = n % 2 == 0 ? mona.Id : null, CreatedAt = Now.AddMinutes(-40 + n * 8),
                Status = n == 1 ? QueueStatus.Done : n == 2 ? QueueStatus.Called : QueueStatus.Waiting, CalledAt = n <= 2 ? Now.AddMinutes(-20) : null });
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ العمليات

    private async Task SeedOperationsAsync(Branch branch, Dictionary<UserRole, User> users, User surgeon, User ahmed, CancellationToken ct)
    {
        var or1 = await db.OperatingRooms.FirstAsync(r => r.BranchId == branch.Id && r.Code == "OR-1", ct);
        var or2 = await db.OperatingRooms.FirstAsync(r => r.BranchId == branch.Id && r.Code == "OR-2", ct);
        var keys = SurgeryServiceKeys;
        db.SurgeryCases.AddRange(
            new SurgeryCase { No = "SRG-D0001", PatientId = ahmed.Id, ProcedureName = "قسطرة قلبية تشخيصية", RoomId = or1.Id, Date = Today.AddDays(1), StartMinutes = 540, DurationMinutes = 90, SurgeonId = surgeon.Id,
                PreOpAlert = "يتناول مميّعات الدم", Checklist = keys.Select((k, i) => new PreOpCheckItem { ItemKey = k, Done = i < 4, DoneAt = i < 4 ? Now : null }).ToList() },
            new SurgeryCase { No = "SRG-D0002", PatientId = ahmed.Id, ProcedureName = "استئصال الزائدة", RoomId = or2.Id, Date = Today, StartMinutes = 780, DurationMinutes = 60, SurgeonId = surgeon.Id,
                PreOpReady = true, Checklist = keys.Select(k => new PreOpCheckItem { ItemKey = k, Done = true, DoneAt = Now }).ToList() });
        await db.SaveChangesAsync(ct);
    }

    private static string[] SurgeryServiceKeys => ["consent", "identity", "site", "fasting", "labs", "imaging", "allergies", "anesthesia"];

    // ------------------------------------------------------------ المالية والتأمين

    private async Task SeedFinanceAsync(Branch branch, User reem, User ahmed, CancellationToken ct)
    {
        db.Expenses.AddRange(
            new Expense { No = "EXP-D0001", BranchId = branch.Id, Title = "صيانة جهاز الأشعة", Category = "صيانة", Amount = 3200, ExpenseDate = Today.AddDays(-1), RequestedBy = "مدير الفرع" },
            new Expense { No = "EXP-D0002", BranchId = branch.Id, Title = "شراء أجهزة مراقبة مريض", Category = "معدات", Amount = 18000, ExpenseDate = Today, RequestedBy = "مدير الفرع" },
            new Expense { No = "EXP-D0003", BranchId = branch.Id, Title = "مستلزمات مكتبية", Category = "إدارية", Amount = 650, ExpenseDate = Today.AddDays(-4), RequestedBy = "الاستقبال", Status = ExpenseStatus.Approved },
            new Expense { No = "EXP-D0004", BranchId = branch.Id, Title = "فاتورة الكهرباء", Category = "خدمات", Amount = 7400, ExpenseDate = Today.AddDays(-9), RequestedBy = "مدير الفرع", Status = ExpenseStatus.Paid });

        var period = $"{Today.Year:D4}-{Today.Month:D2}";
        foreach (var e in await db.Employees.Where(e => e.BranchId == branch.Id).ToListAsync(ct))
            if (!await db.PayrollRecords.AnyAsync(p => p.EmployeeId == e.Id && p.Period == period, ct)) db.PayrollRecords.Add(new PayrollRecord { EmployeeId = e.Id, Period = period, Base = e.BaseSalary });

        // ديون ومطالبات
        var past = Today.AddDays(-3);
        db.Invoices.Add(new Invoice { No = "INV-D0002", PatientId = ahmed.Id, BranchId = branch.Id, Title = "كشف طوارئ", IssueDate = past, DueDate = past, Subtotal = 450, Status = InvoiceStatus.Unpaid, Items = [new InvoiceItem { Description = "كشف طوارئ", UnitPrice = 450 }] });

        var tawuniya = await db.InsuranceCompanies.FirstAsync(c => c.Name == "التعاونية", ct);
        var bupa = await db.InsuranceCompanies.FirstAsync(c => c.Name == "بوبا", ct);
        var reemPolicy = await db.PatientInsurances.FirstAsync(p => p.PatientId == reem.Id, ct);
        var ahmedPolicy = new PatientInsurance { PatientId = ahmed.Id, CompanyId = bupa.Id, PolicyNo = "POL-BUPA-7731", Tier = "الفضية", CopayRate = 0.30m, AnnualLimit = 60_000, UsedAmount = 12_000, ExpiresAt = Today.AddMonths(8) };
        db.PatientInsurances.Add(ahmedPolicy);
        await db.SaveChangesAsync(ct);

        var invoice = await db.Invoices.FirstAsync(i => i.No == "INV-D0001", ct);
        db.InsuranceClaims.Add(new InsuranceClaim { No = "CLM-D0001", PatientId = reem.Id, CompanyId = tawuniya.Id, InvoiceId = invoice.Id, Amount = 240, Status = ClaimStatus.Submitted, SubmittedAt = Now.AddDays(-28) });
        db.PreAuthorizations.AddRange(
            new PreAuthorization { No = "PA-D0001", PatientId = reem.Id, CompanyId = tawuniya.Id, PolicyId = reemPolicy.Id, ProcedureName = "رنين مغناطيسي للدماغ", Diagnosis = "صداع مزمن", Amount = 2500, Priority = OrderPriority.Normal, SubmittedAt = Now.AddHours(-20),
                Documents = [new PreAuthDocument { Name = "تقرير الطبيب", Received = true }, new PreAuthDocument { Name = "صورة الهوية" }] },
            new PreAuthorization { No = "PA-D0002", PatientId = ahmed.Id, CompanyId = bupa.Id, PolicyId = ahmedPolicy.Id, ProcedureName = "قسطرة قلبية", Diagnosis = "ألم صدر", Amount = 18500, Priority = OrderPriority.Urgent, SubmittedAt = Now.AddHours(-3),
                Documents = [new PreAuthDocument { Name = "تقرير تخطيط القلب", Received = true }, new PreAuthDocument { Name = "نتائج التحاليل", Received = true }] });
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------ التواصل

    private async Task SeedCommunicationAsync(User reem, User mona, CancellationToken ct)
    {
        var t = Now;
        var direct = new Conversation { Kind = ConversationKind.Direct, CreatedAt = t.AddDays(-2), LastMessageAt = t.AddMinutes(-30),
            Participants = [new ConversationParticipant { UserId = reem.Id, UnreadCount = 1 }, new ConversationParticipant { UserId = mona.Id }] };
        var support = new Conversation { Kind = ConversationKind.Support, Title = "الدعم", CreatedAt = t.AddDays(-1), LastMessageAt = t.AddHours(-5), Participants = [new ConversationParticipant { UserId = reem.Id }] };
        db.Conversations.AddRange(direct, support);
        await db.SaveChangesAsync(ct);
        db.Messages.AddRange(
            new Message { ConversationId = direct.Id, SenderId = reem.Id, Text = "دكتورة، هل أستمر على نفس جرعة الدواء؟", SentAt = t.AddDays(-2) },
            new Message { ConversationId = direct.Id, SenderId = mona.Id, Text = "نعم، وقيسي الضغط صباحًا ومساءً لأسبوع.", SentAt = t.AddDays(-2).AddMinutes(25) },
            new Message { ConversationId = direct.Id, SenderId = reem.Id, Text = "تمام، شكرًا لك.", SentAt = t.AddDays(-1) },
            new Message { ConversationId = direct.Id, SenderId = mona.Id, Text = "بانتظار قراءات الضغط في موعدنا القادم.", SentAt = t.AddMinutes(-30) },
            new Message { ConversationId = support.Id, SenderId = reem.Id, Text = "أريد نسخة من فاتورتي الأخيرة.", SentAt = t.AddHours(-5) });
        db.Notifications.AddRange(
            new Notification { UserId = reem.Id, Type = NotificationType.Appointment, Title = "تذكير بموعدك", Text = "موعدك مع د. منى القاسم بعد يومين", CreatedAt = t.AddHours(-3) },
            new Notification { UserId = reem.Id, Type = NotificationType.Result, Title = "نتيجة تحليل جاهزة", Text = "السكر التراكمي (HbA1c)", CreatedAt = t.AddDays(-29), ReadAt = t.AddDays(-29) },
            new Notification { UserId = reem.Id, Type = NotificationType.Message, Title = "رسالة من د. منى القاسم", Text = "بانتظار قراءات الضغط في موعدنا القادم.", CreatedAt = t.AddMinutes(-30) },
            new Notification { UserId = mona.Id, Type = NotificationType.Result, Title = "نتيجة تحليل جاهزة", Text = "وظائف الكلى", CreatedAt = t.AddHours(-1) },
            new Notification { UserId = mona.Id, Type = NotificationType.Alert, Title = "حالة حرجة في الطوارئ", Text = "ألم صدر وضيق نفس", CreatedAt = t.AddMinutes(-9) });
        await db.SaveChangesAsync(ct);
    }
}
