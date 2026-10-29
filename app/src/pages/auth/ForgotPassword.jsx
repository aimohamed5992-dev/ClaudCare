import { useState } from 'react'
import { Link } from 'react-router-dom'
import AuthLayout from './AuthLayout'
import { Button, Icon, Input } from '../../components/ui'
import { authApi } from '../../api/auth'
import { asset } from '../../utils/assets'
import { passwordStrength, validateIdentity, validatePassword } from '../../utils/validators'

const LABELS = ['', 'ضعيفة', 'مقبولة', 'جيدة', 'قوية']

// استعادة كلمة المرور على الخادم: الحساب ← رمز التحقق ← كلمة مرور جديدة ← تم
export default function ForgotPassword() {
  const [step, setStep] = useState('identity')
  const [identity, setIdentity] = useState('')
  const [code, setCode] = useState('')
  const [pw, setPw] = useState('')
  const [confirm, setConfirm] = useState('')
  const [challenge, setChallenge] = useState(null)
  const [resetToken, setResetToken] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const strength = pw ? Math.max(1, passwordStrength(pw)) : 0

  const run = (fn) => async (e) => {
    e?.preventDefault()
    setError('')
    setLoading(true)
    try {
      await fn()
    } catch (err) {
      setError(err.userMessage ?? err.message)
    } finally {
      setLoading(false)
    }
  }

  const sendCode = run(async () => {
    const msg = validateIdentity(identity)
    if (msg) throw new Error(msg)
    setChallenge(await authApi.forgot(identity))
    setStep('code')
  })
  const verify = run(async () => {
    setResetToken(await authApi.verifyReset(challenge.challengeToken, code.trim()))
    setStep('password')
  })
  const save = run(async () => {
    const msg = validatePassword(pw) || (confirm !== pw ? 'كلمتا المرور غير متطابقتين' : '')
    if (msg) throw new Error(msg)
    await authApi.reset(resetToken, pw)
    setStep('done')
  })
  const resend = run(async () => setChallenge(await authApi.resend(challenge.challengeToken)))

  const back = (
    <Link to="/login">
      <Icon name="chevronRight" size={16} style={{ display: 'inline', verticalAlign: 'middle' }} /> العودة لتسجيل الدخول
    </Link>
  )
  const alert = error && <div className="auth__note auth__note--error" role="alert"><Icon name="alert" size={18} />{error}</div>

  if (step === 'done') {
    return (
      <AuthLayout title="تم تغيير كلمة المرور" footer={back}>
        <div className="auth__done">
          <img src={asset('empty/success-done.png')} alt="" />
          <p>يمكنك الآن تسجيل الدخول بكلمة المرور الجديدة.</p>
          <Link to="/login"><Button variant="blue" size="lg">الذهاب لتسجيل الدخول</Button></Link>
        </div>
      </AuthLayout>
    )
  }

  if (step === 'password') {
    return (
      <AuthLayout title="كلمة مرور جديدة" subtitle="اختر كلمة مرور قوية: 8 أحرف على الأقل وتحتوي على حروف وأرقام." footer={back}>
        <form onSubmit={save} noValidate>
          {alert}
          <Input label="كلمة المرور الجديدة" type="password" icon="lock" autoComplete="new-password" value={pw} onChange={(e) => setPw(e.target.value)} />
          <div className="strength" aria-live="polite">
            {[1, 2, 3, 4].map((i) => <i key={i} className={strength >= i ? `on-${strength}` : ''} />)}
            <small>{LABELS[strength]}</small>
          </div>
          <Input label="تأكيد كلمة المرور" type="password" icon="lock" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
          <Button type="submit" size="lg" full loading={loading}>حفظ كلمة المرور</Button>
        </form>
      </AuthLayout>
    )
  }

  if (step === 'code') {
    return (
      <AuthLayout title="رمز التحقق" subtitle={`أرسلنا رمزًا إلى ${challenge.destination}`} footer={back}>
        <form onSubmit={verify} noValidate>
          {alert}
          <Input label="رمز التحقق" icon="lock" inputMode="numeric" autoComplete="one-time-code" dir="ltr" value={code} onChange={(e) => setCode(e.target.value)} />
          <Button type="submit" size="lg" full loading={loading}>تحقق</Button>
          <Button type="button" variant="ghost" full onClick={resend}>إعادة إرسال الرمز</Button>
        </form>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="نسيت كلمة المرور؟" subtitle="أدخل بريدك أو جوالك أو رقم الموظف وسنرسل لك رمز تحقق." footer={back}>
      <form onSubmit={sendCode} noValidate>
        {alert}
        <Input label="البريد الإلكتروني أو الجوال أو رقم الموظف" icon="mail" placeholder="name@hospital.com" autoComplete="username" value={identity} onChange={(e) => setIdentity(e.target.value)} />
        <Button type="submit" size="lg" full loading={loading}>إرسال الرمز</Button>
      </form>
    </AuthLayout>
  )
}
