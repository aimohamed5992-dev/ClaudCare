// تحقق حيّ من ربط React بالـ API: دخول كل الأدوار (مع OTP للطاقم) وجلب بيانات كل دور وتجديد الرمز والخروج.
// الاستخدام: شغّل الـ API ثم `node scripts/e2e-live.mjs` (يستخدم vite لتحميل وحدات src كما يراها المتصفح).
import { createServer } from 'vite'

const mem = new Map()
globalThis.localStorage = { getItem: (k) => mem.get(k) ?? null, setItem: (k, v) => mem.set(k, String(v)), removeItem: (k) => mem.delete(k) }

const vite = await createServer({ server: { middlewareMode: true }, appType: 'custom', logLevel: 'error' })
const load = (p) => vite.ssrLoadModule(p)
const { api, tokens, ApiError } = await load('/src/api/client.js')
const { authApi } = await load('/src/api/auth.js')
const { hydrate, clearHydrated } = await load('/src/api/hydrate.js')
const { getRole } = await load('/src/data/roles.js')

let failed = 0
// حد الدخول 30 طلبًا/دقيقة لكل IP: نتوقف دقيقة قبل تجاوزه (وهو أيضًا يمنع إعادة OTP لنفس الحساب قبل 30 ثانية)
let authCalls = 0
async function budget(n) {
  if (authCalls + n > 26) {
    console.log('… انتظار دقيقة لحد معدل الدخول')
    await new Promise((r) => setTimeout(r, 62_000))
    authCalls = 0
  }
  authCalls += n
}
const check = (ok, msg) => {
  console.log(`${ok ? '✓' : '✗'} ${msg}`)
  if (!ok) failed++
}
const wait = (ms) => new Promise((r) => setTimeout(r, ms))

const ACCOUNTS = [
  ['admin', 'admin@claudcare.test'], ['hospital', 'hospital@claudcare.test'], ['branch', 'branch@claudcare.test'], ['lab', 'lab@claudcare.test'], ['radiology', 'radiology@claudcare.test'],
  ['pharmacy', 'pharmacy@claudcare.test'], ['doctor', 'DR-1042'], ['nurse', 'nurse@claudcare.test'], ['reception', 'reception@claudcare.test'], ['insurance', 'insurance@claudcare.test'],
  ['finance', 'finance@claudcare.test'], ['hr', 'hr@claudcare.test'], ['emergency', 'emergency@claudcare.test'], ['admissions', 'admissions@claudcare.test'],
  ['surgery', 'surgery@claudcare.test'], ['patient', '0551234500'],
]

try {
  // 1) رفض بيانات خاطئة برسالة مفهومة
  try {
    await authApi.login('0551234500', 'wrong-password')
    check(false, 'كلمة مرور خاطئة يجب أن تُرفض')
  } catch (e) {
    check(e instanceof ApiError && e.status === 401, `كلمة مرور خاطئة ⇒ 401 (${e.message})`)
  }

  // 2) كل دور: دخول ← دور الخادم يطابق دور الواجهة ← بيانات الدور تُجلب بالشكل الصحيح
  for (const [key, id] of ACCOUNTS) {
    tokens.clear()
    await budget(2)
    let out = await authApi.login(id, 'Demo@1234')
    let user = out.user
    if (out.challenge) user = await authApi.verifyLogin(out.challenge.challengeToken, '123456')
    check(getRole(user.role.toLowerCase())?.key === key, `${key}: دخول ودور الخادم = ${user.role}`)
    const { changed, errors } = await hydrate(key)
    check(errors.length === 0, `${key}: جلب البيانات بلا أخطاء ${errors.map((e) => e.message).join(' | ')}`)
    void changed
    await wait(200)
  }

  // 3) محتوى فعلي: أمثلة من بيانات الديمو
  async function login(id) {
    await budget(2)
    tokens.clear()
    let out
    try {
      out = await authApi.login(id, 'Demo@1234')
    } catch (e) {
      if (e.code !== 'otp_throttled') throw e
      await wait(31_000) // مهلة إعادة إرسال OTP لنفس الحساب
      out = await authApi.login(id, 'Demo@1234')
    }
    if (out.challenge) await authApi.verifyLogin(out.challenge.challengeToken, '123456')
  }
  const data = {
    pharmacy: await load('/src/data/pharmacy.js'), lab: await load('/src/data/lab.js'), hr: await load('/src/data/hr.js'), patient: await load('/src/data/patient.js'),
    finance: await load('/src/data/finance.js'), insurance: await load('/src/data/insurance.js'), emergency: await load('/src/data/emergency.js'), surgery: await load('/src/data/surgery.js'),
  }
  const noUndefined = (rows) => rows.every((r) => Object.values(r).every((v) => v !== undefined && v !== 'undefined'))

  await login('pharmacy@claudcare.test')
  await hydrate('pharmacy')
  check(data.pharmacy.rxQueue.length > 0 && noUndefined(data.pharmacy.rxQueue), `الصيدلية: ${data.pharmacy.rxQueue.length} وصفات بحالات عربية`)
  check(data.pharmacy.inventory.some((i) => i.status === 'منخفض'), 'الصيدلية: أصناف منخفضة ظاهرة')
  check(data.pharmacy.purchaseOrders.length > 0 && data.pharmacy.sales.length > 0, 'الصيدلية: أوامر شراء ومبيعات')

  await login('hr@claudcare.test')
  await hydrate('hr')
  check(data.hr.employees.length >= 13, `الموارد البشرية: ${data.hr.employees.length} موظفًا`)
  check(data.hr.leaveRequests.length >= 2 && data.hr.leaveRequests.every((l) => l.balance > 0), 'الموارد البشرية: إجازات معلّقة بأرصدتها')

  await login('0551234500')
  await hydrate('patient')
  check(data.patient.myAppointments.length > 0 && noUndefined(data.patient.myAppointments), `المريض: ${data.patient.myAppointments.length} مواعيد`)
  check(data.patient.patientInvoices.length > 0, 'المريض: فواتير')

  await login('emergency@claudcare.test')
  await hydrate('emergency')
  check(data.emergency.erPatients.length >= 3 && data.emergency.erPatients[0].level === 1, 'الطوارئ: الأخطر أولًا')
  await login('surgery@claudcare.test')
  await hydrate('surgery')
  check(data.surgery.cases.length >= 2, 'العمليات: حالات مجدولة')
  await login('insurance@claudcare.test')
  await hydrate('insurance')
  check(data.insurance.approvals.length >= 2 && data.insurance.claims.length >= 1, 'التأمين: موافقات ومطالبات')
  await login('finance@claudcare.test')
  await hydrate('finance')
  check(data.finance.expenses.length >= 4 && data.finance.invoices.length > 0, 'المالية: مصروفات وفواتير')

  // 4) تجديد الرمز تلقائيًا: نفسد رمز الوصول فيُجدَّد بصمت ويكمل الطلب
  await login('0551234500')
  const oldRefresh = tokens.refresh
  tokens.save('invalid.access.token', oldRefresh)
  const me = await api.get('/api/auth/me')
  check(me.role === 'Patient' && tokens.access !== 'invalid.access.token' && tokens.refresh !== oldRefresh, 'تجديد الرمز تلقائيًا بعد 401 وتدوير رمز التجديد')

  // 5) طلبات متزامنة بالرمز الفاسد تجدّد مرة واحدة فقط
  const rt = tokens.refresh
  tokens.save('bad', rt)
  const results = await Promise.all([1, 2, 3, 4].map(() => api.get('/api/auth/me')))
  check(results.every((r) => r.role === 'Patient'), 'أربعة طلبات متزامنة بعد انتهاء الرمز نجحت كلها (تجديد واحد)')

  // 6) الإشعارات
  const n = await api.get('/api/notifications', { pageSize: 5 })
  check(n.page.items.length > 0, 'الإشعارات من الخادم')

  // 7) الخروج يبطل الجلسة
  const refreshBefore = tokens.refresh
  await authApi.logout()
  check(tokens.access === null && tokens.refresh === null, 'الخروج يمسح الرموز')
  tokens.save('x', refreshBefore)
  try {
    await api.get('/api/auth/me')
    check(false, 'رمز التجديد بعد الخروج يجب أن يُرفض')
  } catch (e) {
    check(e.status === 401, 'رمز التجديد بعد الخروج مُلغى (401)')
  }
  clearHydrated('patient')
} finally {
  await vite.close()
}
console.log(failed ? `\n${failed} فحص فشل` : '\nكل الفحوص نجحت')
process.exit(failed ? 1 : 0)
