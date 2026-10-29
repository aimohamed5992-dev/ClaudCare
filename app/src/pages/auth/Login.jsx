import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import AuthLayout from './AuthLayout'
import { Button, Checkbox, Icon, Input } from '../../components/ui'
import useAuth from '../../hooks/useAuth'
import { authApi } from '../../api/auth'
import { validateIdentity, validatePassword } from '../../utils/validators'

const DEMO_PASSWORD = 'Demo@1234'
const DEMO_ACCOUNTS = [
  ['المريض', '0551234500'],
  ['الطبيب', 'DR-1042'],
  ['مدير النظام', 'admin@claudcare.test'],
  ['مدير المستشفى', 'hospital@claudcare.test'],
  ['مدير الفرع', 'branch@claudcare.test'],
  ['الاستقبال', 'reception@claudcare.test'],
  ['التمريض', 'nurse@claudcare.test'],
  ['المختبر', 'lab@claudcare.test'],
  ['الأشعة', 'radiology@claudcare.test'],
  ['الصيدلية', 'pharmacy@claudcare.test'],
  ['المالية', 'finance@claudcare.test'],
  ['الموارد البشرية', 'hr@claudcare.test'],
  ['التأمين', 'insurance@claudcare.test'],
  ['الطوارئ', 'emergency@claudcare.test'],
  ['التنويم', 'admissions@claudcare.test'],
  ['العمليات', 'surgery@claudcare.test'],
]

/** حسابات التجربة: تظهر في وضع التطوير فقط ولا تدخل بناء الإنتاج. */
function DemoAccounts({ onPick }) {
  return (
    <details className="auth__demo">
      <summary>حسابات التجربة</summary>
      <p>كلمة المرور <bdi dir="ltr">{DEMO_PASSWORD}</bdi>، ورمز التحقق للطاقم <bdi dir="ltr">123456</bdi>. اضغط على حساب لتعبئته.</p>
      <div className="auth__demo-list">
        {DEMO_ACCOUNTS.map(([label, id]) => (
          <button type="button" key={id} onClick={() => onPick(id, DEMO_PASSWORD)}>
            <strong>{label}</strong>
            <bdi dir="ltr">{id}</bdi>
          </button>
        ))}
      </div>
    </details>
  )
}

export default function Login() {
  const { login, verifyOtp } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ identity: '', password: '', remember: true })
  const [errors, setErrors] = useState({})
  const [loading, setLoading] = useState(false)
  const [serverError, setServerError] = useState('')
  const [challenge, setChallenge] = useState(null)
  const [code, setCode] = useState('')

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }))

  const submit = async (e) => {
    e.preventDefault()
    const next = { identity: validateIdentity(form.identity), password: validatePassword(form.password) }
    setErrors(next)
    setServerError('')
    if (next.identity || next.password) return
    setLoading(true)
    try {
      const out = await login(form.identity, form.password)
      if (out.challenge) setChallenge(out.challenge)
      else navigate(`/${out.roleKey}`, { replace: true })
    } catch (err) {
      setServerError(err.userMessage ?? err.message)
    } finally {
      setLoading(false)
    }
  }

  const verify = async (e) => {
    e.preventDefault()
    if (code.trim().length < 4) return setServerError('أدخل رمز التحقق')
    setLoading(true)
    setServerError('')
    try {
      const out = await verifyOtp(challenge.challengeToken, code.trim())
      navigate(`/${out.roleKey}`, { replace: true })
    } catch (err) {
      setServerError(err.userMessage ?? err.message)
    } finally {
      setLoading(false)
    }
  }

  const resend = async () => {
    try {
      setChallenge(await authApi.resend(challenge.challengeToken))
      setServerError('')
    } catch (err) {
      setServerError(err.userMessage ?? err.message)
    }
  }

  if (challenge) {
    return (
      <AuthLayout title="التحقق بخطوتين" subtitle={`أرسلنا رمز التحقق إلى ${challenge.destination}`} footer={<a href="/login" onClick={(e) => { e.preventDefault(); setChallenge(null); setCode('') }}>رجوع</a>}>
        <form onSubmit={verify} noValidate>
          {serverError && <div className="auth__note auth__note--error" role="alert"><Icon name="alert" size={18} />{serverError}</div>}
          <Input label="رمز التحقق" icon="lock" inputMode="numeric" autoComplete="one-time-code" dir="ltr" value={code} onChange={(e) => setCode(e.target.value)} />
          <Button type="submit" size="lg" full loading={loading}>تأكيد</Button>
          <Button type="button" variant="ghost" full onClick={resend}>إعادة إرسال الرمز</Button>
        </form>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="تسجيل الدخول" subtitle="مرحبًا بعودتك، أدخل بياناتك للمتابعة." footer="ليس لديك حساب؟ تواصل مع مدير المستشفى لإنشاء حساب لك.">
      <form onSubmit={submit} noValidate>
        {serverError && <div className="auth__note auth__note--error" role="alert"><Icon name="alert" size={18} />{serverError}</div>}
        <Input label="البريد الإلكتروني أو الجوال أو رقم الموظف" icon="mail" placeholder="name@hospital.com" autoComplete="username" value={form.identity} onChange={set('identity')} error={errors.identity} />
        <Input label="كلمة المرور" type="password" icon="lock" placeholder="••••••••" autoComplete="current-password" value={form.password} onChange={set('password')} error={errors.password} />
        <div className="auth__row">
          <Checkbox label="تذكرني" checked={form.remember} onChange={set('remember')} />
          <Link to="/forgot-password">نسيت كلمة المرور؟</Link>
        </div>
        <Button type="submit" size="lg" full loading={loading}>دخول</Button>
      </form>
      {import.meta.env.DEV && <DemoAccounts onPick={(identity, password) => setForm((f) => ({ ...f, identity, password }))} />}
    </AuthLayout>
  )
}
