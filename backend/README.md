# ClaudCare – الباك اند (ASP.NET Core Web API + MySQL/MariaDB)

```
backend/
├─ src/ClaudCare.Api/           الـ API (net10.0 · Controllers · EF Core 9 + Pomelo)
└─ tests/ClaudCare.Api.Tests/   اختبارات تكامل (xUnit + WebApplicationFactory) على قاعدة claudcare_test
```

## المتطلبات
- .NET SDK 10 · XAMPP مع تشغيل MySQL (MariaDB 10.4) على المنفذ 3306 (المستخدم `root` بدون كلمة مرور افتراضيًا).

## التشغيل
```bash
cd backend/src/ClaudCare.Api
dotnet run            # http://localhost:5080
```
- الوثائق التفاعلية: http://localhost:5080/scalar/v1 · مواصفة OpenAPI: `/openapi/v1.json`
- الفحص: `GET /api/health` (يُرجع 503 إن تعذّر الاتصال بقاعدة البيانات).
- تُنشأ قاعدة `claudcare` تلقائيًا عند أول تشغيل ولا يُلمس أي قاعدة أخرى على الخادم.

## الإعدادات (`appsettings.json` أو متغيرات بيئة)
| المفتاح | الافتراضي |
|---|---|
| `ConnectionStrings__Default` | `Server=127.0.0.1;Port=3306;Database=claudcare;User=root;Password=;` |
| `Database__ServerType` / `Database__ServerVersion` | `MariaDb` / `10.4.28` |
| `Database__CreateIfMissing` | `true` |
| `Cors__AllowedOrigins` / `Cors__AllowAnyLocalhost` | React على 5173 + أي منفذ localhost |

## قاعدة البيانات
انظر [docs/DATABASE.md](docs/DATABASE.md): 66 جدولًا تغطي كل أدوار المنصة، مع migration `InitialCreate` يُطبَّق تلقائيًا عند التشغيل في التطوير.

## المصادقة
JWT + OTP + تجديد الجلسات وتقييد المعدل وسياسات الأدوار: [docs/AUTH.md](docs/AUTH.md) (ومعه حسابات التجربة).

## الأطباء والمواعيد
البحث والخانات والحجز والإلغاء وإعادة الجدولة وسير الزيارة والتقييم: [docs/API-SCHEDULING.md](docs/API-SCHEDULING.md).

## السريري
الكشف والوصفات (فحص الحساسية) والتحاليل والأشعة والسجل الطبي وأدوية المريض والتذكير وإعادة الصرف: [docs/API-CLINICAL.md](docs/API-CLINICAL.md).

الفواتير والدفع والتأمين والإشعارات والمراسلة والملف الشخصي والإعدادات: [docs/API-BILLING-COMMS.md](docs/API-BILLING-COMMS.md).

أدوار التشغيل (المختبر، الأشعة، الصيدلية، التمريض، التنويم، الطوارئ، الاستقبال، العمليات، الموارد البشرية، المالية، التأمين، اللوحات): [docs/API-OPERATIONS.md](docs/API-OPERATIONS.md).

بيانات العرض الكاملة والتحصين الأمني وقائمة ما قبل الإنتاج: [docs/DEMO-DATA.md](docs/DEMO-DATA.md).

## الاختبارات
```bash
cd backend && dotnet test      # تنشئ claudcare_test وتحذفها بعد الانتهاء
```
