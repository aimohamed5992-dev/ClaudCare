# ClaudCare – منصة إدارة المستشفيات

ثلاثة عملاء على API واحد وقاعدة بيانات MySQL/MariaDB:

```
ClaudCare/
├─ backend/   ASP.NET Core Web API (net10) + EF Core/Pomelo + MySQL (XAMPP)   ← الحقيقة الوحيدة للبيانات
├─ app/       واجهة الويب React 19 + Vite لـ 16 دورًا (عربي RTL)
└─ flutter/   تطبيق الطبيب وتطبيق المريض + حزمة واجهات مشتركة claudcare_ui
```

## التشغيل من الصفر (3 خطوات)
1. **قاعدة البيانات:** شغّل MySQL من XAMPP (المنفذ 3306، المستخدم `root` بلا كلمة مرور). لا حاجة لإنشاء شيء: الـ API ينشئ قاعدة `claudcare` ويطبّق الـ migrations ويزرع بيانات العرض.
2. **الـ API:** `cd backend/src/ClaudCare.Api && dotnet run` ← http://localhost:5080 (التوثيق التفاعلي `/scalar/v1`).
3. **العميل:**
   - الويب: `cd app && npm install && npm run dev` ← http://localhost:5173
   - Flutter: `cd flutter/patient_app` (أو `doctor_app`) ثم `flutter pub get && flutter run` على محاكي iOS.
     أندرويد/جهاز حقيقي: `--dart-define=API_URL=http://<عنوان-الجهاز-المضيف>:5080`.

الويب يقرأ العنوان من `VITE_API_URL` (الافتراضي `http://localhost:5080`).

## حسابات التجربة (كلمة المرور `Demo@1234`، رمز التحقق للطاقم `123456`)
| الدور | الدخول |
|---|---|
| مريض | `0551234500` أو الهوية `1098765432` |
| طبيب | `DR-1042` أو `mona@hospital.com` |
| بقية الأدوار | `<role>@claudcare.test` مثل `admin@` `hospital@` `branch@` `lab@` `radiology@` `pharmacy@` `nurse@` `reception@` `insurance@` `finance@` `hr@` `emergency@` `admissions@` `surgery@` |

الطاقم يمر بخطوتين (كلمة المرور ثم الرمز). دور الحساب يأتي من الخادم، فلا اختيار يدوي للدور في الويب.

## الاختبار والتحقق
| ماذا | الأمر | النتيجة المتوقعة |
|---|---|---|
| الباك اند (328 اختبار تكامل على `claudcare_test`) | `cd backend && dotnet test` | كلها ناجحة |
| الواجهات المشتركة / المريض / الطبيب | `cd flutter/<app> && flutter analyze && flutter test` | 12 / 33 / 28 |
| تكامل Flutter حي | `flutter test test/live_api_test.dart --dart-define=LIVE_API=http://localhost:5080` (الـ API شغّال) | ناجح |
| تكامل الويب حي (16 دورًا) | `cd app && node scripts/e2e-live.mjs` (الـ API شغّال؛ يستغرق دقيقتين لحد معدل الدخول) | كل الفحوص ✓ |
| بناء الويب | `cd app && npm run build && npm run lint` | بلا تحذيرات |

## ما هو حقيقي وما هو محاكى
**حقيقي:** المصادقة (JWT + OTP + تجديد + تدوير + كشف إعادة الاستخدام)، الأدوار والنطاق بالفرع، الحجز والتوفر، الفواتير والدفع (تسجيل دفعات)، الكشف والوصفات وفحص الحساسية، المختبر والأشعة، الصيدلية والمخزون، التنويم والأسرّة، الطوارئ، العمليات، الموارد البشرية، المالية، التأمين، المراسلة، الإشعارات.
**محاكى/غير منفّذ:** بوابة دفع فعلية، مزوّد SMS (الرموز تُسجَّل في سجل الخادم وثابتة `123456` في التطوير)، تخزين الصور والمرفقات، مكالمات الفيديو، التحديث اللحظي (WebSocket)، تغيير رقم الجوال. قائمة ما قبل الإنتاج في `backend/docs/DEMO-DATA.md`.

## التوثيق
- الباك اند: [backend/README.md](backend/README.md) وملفات `backend/docs/` (قاعدة البيانات، المصادقة، المواعيد، السريري، الفواتير والتواصل، أدوار التشغيل، بيانات العرض والتحصين).
- الويب: [app/README.md](app/README.md) · Flutter: [flutter/README.md](flutter/README.md).
