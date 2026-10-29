# نموذج البيانات (66 جدولًا)

| المجموعة | الجداول |
|---|---|
| الهوية والأمان | `Users` · `RefreshTokens` · `OtpCodes` · `UserSettings` · `AuditLogs` |
| المؤسسة | `Hospitals` · `Branches` · `Departments` · `Specialties` (15 مبذورة) · `SystemSettings` |
| الأشخاص | `DoctorProfiles` · `DoctorWorkingHours` · `DoctorReviews` · `PatientProfiles` · `PatientAllergies` · `PatientConditions` · `FamilyMembers` |
| الموارد البشرية | `Employees` · `ProfessionalLicenses` · `LeaveRequests` · `Shifts` · `PayrollRecords` |
| المواعيد | `Appointments` (مفتاح فريد يمنع الحجز المزدوج) |
| السريري | `Visits` · `Drugs` · `Prescriptions` · `PrescriptionItems` · `PatientMedications` · `DoseLogs` |
| المختبر والأشعة | `LabOrders` · `LabResultItems` · `ImagingDevices` · `ImagingOrders` · `ImagingReports` |
| الصيدلية | `InventoryItems` · `PurchaseOrders` · `PurchaseOrderLines` · `PharmacySales` · `PharmacySaleLines` |
| التنويم والتمريض | `Beds` · `Admissions` · `BedTransfers` · `VitalSignRecords` · `NursingNotes` · `MedicationAdministrations` |
| الاستقبال والطوارئ | `QueueTickets` · `EmergencyCases` · `Ambulances` |
| العمليات | `OperatingRooms` · `SurgeryCases` · `PreOpCheckItems` · `SurgeryTeamMembers` |
| المالية | `Invoices` · `InvoiceItems` · `Payments` · `Expenses` · `DailyCloses` |
| التأمين | `InsuranceCompanies` · `PatientInsurances` · `PreAuthorizations` · `PreAuthDocuments` · `InsuranceClaims` |
| التواصل | `Conversations` · `ConversationParticipants` · `Messages` · `Notifications` |

## قرارات التصميم
- **التعدادات نصوص** (`Role = 'Pharmacy'`) لسهولة القراءة والتقارير.
- **التاريخ والوقت السريري**: `DateOnly` + دقائق من منتصف الليل (توقيت العيادة) فلا أخطاء مناطق زمنية. أما الأحداث (إنشاء، رسائل، دفع) فبـ UTC.
- **منع الحذف** (`Restrict`) افتراضيًا حفاظًا على السجلات الطبية والمالية؛ التوابع فقط (بنود فاتورة، أدوية وصفة، نتائج تحليل…) تُحذف مع أصلها.
- **منع الحجز المزدوج**: العمود `ActiveSlotKey` فريد ويُفرَّغ (NULL) عند الإلغاء فتتحرر الخانة.
- **الأموال** `decimal(12,2)`؛ نتائج التحاليل `decimal(12,3)`.
- **MariaDB 10.4**: لا أعمدة JSON ولا فهارس مفلترة، لذا الحقول القائمية تُخزَّن CSV.
- **كل جدول به `CreatedAt/UpdatedAt`** (UTC) يُختم تلقائيًا عند الحفظ.

## الـ migrations
```bash
cd backend/src/ClaudCare.Api
dotnet ef migrations add <Name> -o Data/Migrations
dotnet ef database update          # أو تلقائيًا عند التشغيل (Database:MigrateOnStartup)
```
اختبار `HasPendingModelChanges` يفشل إن غيّرت النموذج دون إضافة migration.
