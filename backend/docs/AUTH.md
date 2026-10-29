# المصادقة والصلاحيات

كل نقطة نهاية **محمية افتراضيًا** (Fallback policy). المفتوحة صراحةً: `/api/health*` وكل `/api/auth/*` ما عدا `me` و`logout-all` و`password/change`.
الرمز في كل طلب: `Authorization: Bearer <accessToken>`.

## التدفقات

| التدفق | الخطوات |
|---|---|
| **دخول المريض** | `POST /api/auth/login {identifier, password}` ← `status:"authenticated"` + `auth` (جلسة مباشرة). المعرّف: جوال (05…، +966…، أرقام عربية) أو هوية. إن لم يكن جواله مؤكدًا يأتي `otp_required` (Register). |
| **دخول الطاقم** | `login` (بريد أو رقم موظف أو جوال) ← `status:"otp_required"` + `challenge` ← `POST /api/auth/otp/verify {challengeToken, code}` ← `auth`. |
| **تسجيل مريض** | `POST /api/auth/register {fullName, phone, nationalId, password}` ← 201 + challenge ← `otp/verify` ← `auth`. إعادة التسجيل قبل تأكيد الجوال تحدّث البيانات. |
| **استعادة كلمة المرور** | `password/forgot {identifier}` ← challenge (دائمًا، حتى لحساب غير موجود) ← `otp/verify` ← `resetToken` ← `password/reset {resetToken, newPassword}` (204، وتنتهي كل الجلسات). |
| **تجديد الجلسة** | `POST /api/auth/refresh {refreshToken}` ← `auth` جديد؛ الرمز القديم يموت (تدوير). |
| **الخروج** | `logout {refreshToken}` (204) · `logout-all` (مصادَق). |
| **تغيير كلمة المرور** | `POST /api/auth/password/change {currentPassword,newPassword}` ← `auth` جديد، وتنتهي الجلسات الأخرى. |
| **بياناتي** | `GET /api/auth/me`. |

**إعادة الإرسال:** `POST /api/auth/otp/resend {challengeToken}` (بعد 30 ثانية، بحد 6 رموز/ساعة لكل هدف) ← challenge جديد (القديم يبطل).

## رموز الخطأ (`code` في ProblemDetails)
`invalid_credentials` 401 · `account_locked` 423 (+`retryAfterSeconds`) · `account_disabled` 403 · `otp_invalid` · `otp_expired` · `otp_attempts_exceeded` 400 ·
`otp_throttled` / `otp_limit` / `rate_limited` 429 (+`Retry-After`) · `challenge_invalid` · `reset_token_invalid` 400 · `invalid_refresh_token` / `token_reused` 401 ·
`phone_taken` / `national_id_taken` 409 · `validation_failed` 400 (+`errors` لكل حقل).

## الأمان
- كلمات المرور: PBKDF2 (ASP.NET Identity PasswordHasher) · سياسة: 8+ أحرف، حروف وأرقام، ليست شائعة.
- إقفال الحساب 15 دقيقة بعد 5 إخفاقات؛ لا فرق بين «حساب غير موجود» و«كلمة مرور خاطئة».
- JWT بثلاثة جماهير: `claudcare-app` (وصول، 30 دقيقة) · `…:challenge` (10 دقائق) · `…:reset` (10 دقائق، يبطل بتغيّر كلمة المرور). لا يُقبل رمز تحدٍّ أو استعادة كرمز وصول.
- رموز التجديد عشوائية 256 بت، تُخزَّن مُجزّأة، تُدوَّر عند كل استخدام، وإعادة استخدام رمز قديم تُنهي كل جلسات المستخدم؛ حد أقصى 10 جلسات.
- رموز OTP مُجزّأة بـ HMAC، 5 دقائق، 5 محاولات، تُستهلك مرة واحدة.
- تقييد معدل نقاط المصادقة: 30 طلبًا/دقيقة لكل IP (`RateLimit:AuthPermitPerMinute`).
- يرفض التطبيق الإقلاع خارج Development إذا: مفتاح التوقيع < 32 بايت أو مفتاح التطوير، أو `Auth:FixedOtpCode`، أو `Seed:DemoAccounts`.
- كل عمليات الدخول والإخفاق والإقفال وتغيير كلمة المرور تُسجَّل في `AuditLogs`.

## حسابات التجربة (Development فقط) — كلمة المرور `Demo@1234`، ورمز التحقق `123456`
| الدور | الدخول |
|---|---|
| المريض (ريم العلي) | `0551234500` أو `1098765432` |
| الطبيب (د. منى القاسم) | `mona@hospital.com` أو `DR-1042` |
| مدير النظام | `admin@claudcare.test` / `AD-0001` |
| باقي الأدوار | `<role>@claudcare.test` (hospital, branch, lab, radiology, pharmacy, nurse, reception, insurance, finance, hr, emergency, admissions, surgery) |

## الصلاحيات في الكود
`[Authorize(Policy = Policies.Doctor)]` وما شابه: `Patient` · `Doctor` · `Staff` (أي غير مريض) · `Admin` · `Management` (مدير نظام/مستشفى/فرع) · `Clinical`.
`ICurrentUser` يعطي `Id` و`Role` و`BranchId` من الرمز للتحقق من ملكية البيانات في الخطوات القادمة.

## في الإنتاج
مرّر `Jwt__SigningKey` (32+ بايت عشوائية) وضبط `ConnectionStrings__Default` و`Database__MigrateOnStartup=false`، واستبدل `LoggingOtpSender` بمزوّد SMS حقيقي (واجهة `IOtpSender`).
