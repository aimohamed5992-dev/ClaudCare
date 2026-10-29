import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, DataTable, EmptyState, Icon, Input, Progress, Select, Tabs, Textarea } from '../../components/ui'
import {
  INSURERS, REJECT_REASONS, approvals, approvalsWeekly, claims, claimsByStatus, claimsMonthly, companies, policies, reviewCase,
} from '../../data/insurance'
import { cx } from '../../utils/cx'
import './insurance.css'
import '../reception/reception.css'

const PA_TONES = { 'قيد المراجعة': 'orange', 'معلومات ناقصة': 'gray', موافق: 'green', مرفوض: 'red' }
const PRIORITY_TONES = { عاجل: 'red', عادي: 'gray' }
const CLAIM_TONES = { مسودة: 'gray', مُرسلة: 'blue', مدفوعة: 'green', 'مدفوعة جزئيًا': 'orange', مرفوضة: 'red' }
const COMPANY_TONES = { نشط: 'green', 'قيد التجديد': 'orange' }
const money = (n) => `${Number(n).toLocaleString('en-US')} ر.س`

export function InsuranceDashboard() {
  const navigate = useNavigate()
  return (
    <DashboardTemplate
      banner={{ title: 'مرحبًا عبدالعزيز', text: 'يوجد طلبان قيد المراجعة، أحدهما عاجل.', cta: 'مراجعة الطلب العاجل', to: '/insurance/review', image: 'role-insurance.png' }}
      stats={[
        { icon: 'shield', label: 'موافقات بانتظار القرار', value: '2', tone: 'orange' },
        { icon: 'file', label: 'مطالبات هذا الشهر', value: '262', delta: '+7%' },
        { icon: 'wallet', label: 'المستحق من الشركات', value: '1.24 مليون', tone: 'green' },
        { icon: 'alert', label: 'نسبة الرفض', value: '6.5%', delta: '-0.8%', tone: 'red' },
      ]}
      table={{
        title: 'أحدث طلبات الموافقة',
        columns: [
          { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'no' },
          { key: 'procedure', header: 'الإجراء' },
          { key: 'insurer', header: 'الشركة' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: PA_TONES },
        ],
        rows: approvals,
      }}
      side={{
        title: 'تنبيهات',
        items: [
          { mark: 'عاجل', title: 'PA-7721 بانتظار القرار', subtitle: 'رنين مغناطيسي – سارة أحمد', tone: 'red', onClick: () => navigate('/insurance/review') },
          { mark: '30+', title: 'مطالبة CL-30188 متأخرة', subtitle: 'شركة ملاذ', tone: 'orange', onClick: () => navigate('/insurance/claims') },
          { mark: '!', title: 'عقد ميدغلف ينتهي قريبًا', subtitle: 'ديسمبر 2026', tone: 'orange', onClick: () => navigate('/insurance/companies') },
        ],
      }}
      charts={[{ title: 'طلبات الموافقة خلال الأسبوع', data: approvalsWeekly }]}
    />
  )
}

export function InsuranceApprovals() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'review', label: 'قيد المراجعة', match: (r) => r.status === 'قيد المراجعة' },
        { key: 'info', label: 'معلومات ناقصة', match: (r) => r.status === 'معلومات ناقصة' },
        { key: 'ok', label: 'موافق', match: (r) => r.status === 'موافق' },
        { key: 'no', label: 'مرفوض', match: (r) => r.status === 'مرفوض' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الطلب' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'insurer' },
        { key: 'procedure', header: 'الإجراء' },
        { key: 'amount', header: 'القيمة' },
        { key: 'priority', header: 'الأولوية', type: 'badge', tones: PRIORITY_TONES },
        { key: 'status', header: 'الحالة', type: 'badge', tones: PA_TONES },
      ]}
      rows={approvals}
      searchKeys={['no', 'patient', 'procedure', 'insurer']}
      detail={{
        titleKey: 'patient',
        subKey: 'no',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: PA_TONES,
        fields: [
          { label: 'الإجراء', key: 'procedure' },
          { label: 'شركة التأمين', key: 'insurer' },
          { label: 'القيمة', key: 'amount' },
          { label: 'الطبيب الطالب', key: 'doctor' },
          { label: 'وصل', key: 'submitted' },
        ],
        actions: [{ label: 'مراجعة الطلب', icon: 'shield', variant: 'blue', to: '/insurance/review' }, { label: 'تواصل مع الطبيب', icon: 'mail', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- مراجعة طلب الموافقة ---------- */
const DECISIONS = [
  { key: 'approve', label: 'موافقة' },
  { key: 'info', label: 'طلب معلومات' },
  { key: 'reject', label: 'رفض' },
]

export function InsuranceReview() {
  const navigate = useNavigate()
  const c = reviewCase
  const [docs, setDocs] = useState(c.docs)
  const [decision, setDecision] = useState('approve')
  const [amount, setAmount] = useState(null) // null = المبلغ المقترح
  const [reason, setReason] = useState(REJECT_REASONS[0])
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [result, setResult] = useState(null)

  const total = c.items.reduce((s, i) => s + i.amount, 0)
  const eligible = c.items.filter((i) => i.covered).reduce((s, i) => s + i.amount, 0)
  const suggested = Math.round(eligible * c.coverage)
  const remaining = c.limit - c.used
  const approved = amount === null ? suggested : Number(amount)

  // فحوصات تلقائية تُحسب من بيانات الحالة
  const checks = useMemo(
    () => [
      { label: 'الوثيقة سارية', ok: true, note: `تنتهي ${c.expiry}` },
      { label: 'الإجراء ضمن التغطية', ok: true, note: `نسبة التغطية ${c.coverage * 100}%` },
      { label: 'ضمن الحد السنوي المتبقي', ok: remaining >= eligible, note: `المتبقي ${money(remaining)}` },
      { label: 'فترة الانتظار منتهية', ok: true, note: 'مضى أكثر من 90 يومًا' },
      { label: 'المرفقات الطبية مكتملة', ok: docs.every((d) => d.ok), note: docs.every((d) => d.ok) ? 'كل المستندات متوفرة' : 'التقرير الطبي غير مرفق' },
    ],
    [c, docs, remaining, eligible],
  )
  const allOk = checks.every((k) => k.ok)

  const submit = () => {
    setError('')
    if (decision === 'approve') {
      if (!allOk) return setError('لا يمكن الموافقة قبل استيفاء كل الفحوصات. اطلب المعلومات الناقصة أو استلم المستند.')
      if (!Number.isFinite(approved) || approved <= 0) return setError('أدخل مبلغًا صحيحًا')
      if (approved > eligible) return setError(`المبلغ لا يمكن أن يتجاوز القيمة المشمولة (${money(eligible)})`)
      setResult({ kind: 'approve', text: `تمت الموافقة بمبلغ ${money(approved)}. حصة المريض ${money(total - approved)}.` })
    } else if (decision === 'reject') {
      if (reason === REJECT_REASONS[0]) return setError('اختر سبب الرفض')
      setResult({ kind: 'reject', text: `سبب الرفض: ${reason}` })
    } else {
      if (!note.trim()) return setError('اكتب المعلومات المطلوبة من الطبيب')
      setResult({ kind: 'info', text: 'أُرسل الطلب إلى الطبيب لاستكمال المعلومات.' })
    }
  }

  if (result) {
    const title = { approve: 'تمت الموافقة على الطلب', reject: 'تم رفض الطلب', info: 'تم طلب معلومات إضافية' }[result.kind]
    return (
      <Card className="reg__done">
        <span className={cx('reg__check', result.kind === 'reject' && 'ins__bad')}>
          <Icon name={result.kind === 'reject' ? 'close' : 'check'} size={32} strokeWidth={2.6} />
        </span>
        <h2>{title}</h2>
        <p><b dir="ltr">{c.no}</b> · {c.patient}</p>
        <p>{result.text}</p>
        <div className="reg__actions">
          <Button variant="blue" onClick={() => navigate('/insurance/approvals')}>العودة للطلبات</Button>
        </div>
      </Card>
    )
  }

  return (
    <div className="rev">
      <div className="rev__main">
        <Card className="rev__head">
          <Avatar src={c.avatar} name={c.patient} size={56} />
          <div>
            <h3>{c.patient}</h3>
            <small>{c.age} سنة · <span dir="ltr">{c.no}</span> · طلب {c.doctor}</small>
          </div>
          <Badge tone="red" dot>عاجل</Badge>
        </Card>

        <Card title="بيانات الوثيقة">
          <dl className="detail__list">
            <div><dt>شركة التأمين</dt><dd>{c.insurer} – {c.tier}</dd></div>
            <div><dt>رقم الوثيقة</dt><dd dir="ltr">{c.policy}</dd></div>
            <div><dt>نسبة التغطية</dt><dd>{c.coverage * 100}%</dd></div>
            <div><dt>الحد السنوي</dt><dd><Progress value={Math.round((c.used / c.limit) * 100)} /> {money(remaining)} متبقٍ</dd></div>
          </dl>
        </Card>

        <Card title={`الإجراء: ${c.procedure}`} padded={false}>
          <p className="rev__dx">التشخيص: {c.diagnosis}</p>
          <DataTable
            columns={[
              { key: 'name', header: 'البند' },
              { key: 'amount', header: 'المبلغ', render: (r) => money(r.amount) },
              { key: 'covered', header: 'التغطية', render: (r) => <Badge tone={r.covered ? 'green' : 'gray'} dot>{r.covered ? 'مشمول' : 'غير مشمول'}</Badge> },
            ]}
            rows={c.items}
          />
          <dl className="rev__totals">
            <div><dt>الإجمالي</dt><dd>{money(total)}</dd></div>
            <div><dt>المشمول</dt><dd>{money(eligible)}</dd></div>
            <div><dt>المقترح ({c.coverage * 100}%)</dt><dd>{money(suggested)}</dd></div>
          </dl>
        </Card>

        <Card title="المستندات المرفقة">
          <div className="dash__list">
            {docs.map((d) => (
              <div key={d.id} className="rev__doc">
                <Icon name="file" size={20} />
                <span>{d.name}</span>
                {d.ok ? <Badge tone="green" dot>متوفر</Badge> : (
                  <>
                    <Badge tone="red" dot>غير مرفق</Badge>
                    <Button size="sm" variant="soft" onClick={() => setDocs((l) => l.map((x) => (x.id === d.id ? { ...x, ok: true } : x)))}>تم الاستلام</Button>
                  </>
                )}
              </div>
            ))}
          </div>
        </Card>
      </div>

      <aside className="rev__side">
        <Card title="الفحوصات التلقائية">
          <ul className="rev__checks">
            {checks.map((k) => (
              <li key={k.label} className={k.ok ? 'is-ok' : 'is-bad'}>
                <Icon name={k.ok ? 'check' : 'close'} size={18} strokeWidth={2.4} />
                <span><b>{k.label}</b><small>{k.note}</small></span>
              </li>
            ))}
          </ul>
        </Card>

        <Card title="القرار">
          <Tabs items={DECISIONS} value={decision} onChange={(k) => { setDecision(k); setError('') }} />
          <div className="rev__form">
            {decision === 'approve' && (
              <Input label="المبلغ المعتمد (ر.س)" inputMode="numeric" value={amount ?? String(suggested)} onChange={(e) => setAmount(e.target.value)} hint={`الحد الأقصى ${money(eligible)}`} />
            )}
            {decision === 'reject' && <Select label="سبب الرفض" options={REJECT_REASONS} value={reason} onChange={(e) => setReason(e.target.value)} />}
            {decision === 'info' && <Textarea label="المعلومات المطلوبة" rows={4} value={note} onChange={(e) => setNote(e.target.value)} placeholder="مثال: يرجى إرفاق تقرير الأشعة السابق" />}
            {error && <p className="rev__error" role="alert">{error}</p>}
            <Button variant={decision === 'reject' ? 'danger' : 'primary'} size="lg" full icon={decision === 'reject' ? 'close' : 'check'} onClick={submit}>
              {{ approve: 'اعتماد الموافقة', reject: 'تأكيد الرفض', info: 'إرسال الطلب للطبيب' }[decision]}
            </Button>
          </div>
        </Card>
      </aside>
    </div>
  )
}

export function InsuranceClaims() {
  return (
    <ListPage
      stats={[
        { icon: 'file', label: 'مطالبات الشهر', value: '262' },
        { icon: 'check', label: 'مدفوعة', value: '148', tone: 'green' },
        { icon: 'clock', label: 'قيد السداد', value: '85', tone: 'orange' },
        { icon: 'close', label: 'مرفوضة', value: '17', tone: 'red' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'draft', label: 'مسودات', match: (r) => r.status === 'مسودة' },
        { key: 'sent', label: 'مُرسلة', match: (r) => r.status === 'مُرسلة' },
        { key: 'paid', label: 'مدفوعة', match: (r) => r.status === 'مدفوعة' || r.status === 'مدفوعة جزئيًا' },
        { key: 'rej', label: 'مرفوضة', match: (r) => r.status === 'مرفوضة' },
      ]}
      columns={[
        { key: 'no', header: 'رقم المطالبة' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'insurer' },
        { key: 'amount', header: 'المبلغ' },
        { key: 'date', header: 'التاريخ' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: CLAIM_TONES },
      ]}
      rows={claims}
      searchKeys={['no', 'patient', 'insurer']}
      addLabel="مطالبة جديدة"
      form={{
        title: 'إنشاء مطالبة',
        fields: [
          { name: 'patient', label: 'اسم المريض', required: true },
          { name: 'insurer', label: 'شركة التأمين', options: INSURERS },
          { name: 'amount', label: 'المبلغ (ر.س)', type: 'number', required: true },
          { name: 'invoice', label: 'رقم الفاتورة', required: true },
        ],
        defaults: { no: 'CL-30216', date: '2026-10-18', status: 'مسودة' },
      }}
      detail={{
        titleKey: 'no',
        subKey: 'patient',
        badgeKey: 'status',
        tones: CLAIM_TONES,
        fields: [
          { label: 'شركة التأمين', key: 'insurer' },
          { label: 'المبلغ', key: 'amount' },
          { label: 'التاريخ', key: 'date' },
          { label: 'رقم الفاتورة', key: 'invoice' },
        ],
        actions: [{ label: 'إرسال للشركة', icon: 'mail', variant: 'blue' }, { label: 'إعادة تقديم', icon: 'edit', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- التحقق من الوثيقة ---------- */
export function InsuranceEligibility() {
  const [insurer, setInsurer] = useState(INSURERS[0])
  const [id, setId] = useState('')
  const [error, setError] = useState('')
  const [result, setResult] = useState(null)
  const [history, setHistory] = useState([])

  const check = (e) => {
    e.preventDefault()
    const key = id.trim()
    if (!/^\d{6,12}$/.test(key)) return setError('أدخل رقم هوية أو وثيقة من 6 إلى 12 رقمًا')
    setError('')
    const p = policies[key]
    const found = p && p.insurer === insurer ? p : null
    const out = found
      ? { status: found.active ? 'سارية' : 'منتهية', tone: found.active ? 'green' : 'red', p: found }
      : { status: 'غير موجودة', tone: 'gray', p: null }
    setResult({ ...out, key, insurer })
    setHistory((h) => [{ id: Date.now(), key, insurer, status: out.status, tone: out.tone, name: found?.name ?? '—' }, ...h].slice(0, 5))
  }

  return (
    <div className="elig">
      <Card title="التحقق من التغطية">
        <form className="elig__form" onSubmit={check} noValidate>
          <Select label="شركة التأمين" options={INSURERS} value={insurer} onChange={(e) => setInsurer(e.target.value)} />
          <Input label="رقم الهوية أو الوثيقة" icon="search" inputMode="numeric" value={id} onChange={(e) => setId(e.target.value)} error={error} hint="جرّب: 1234567890 (بوبا) · 1098765432 (التعاونية) · 1122334455 (ميدغلف)" />
          <Button type="submit" size="lg" icon="shield">تحقق</Button>
        </form>
      </Card>

      <div className="elig__result">
        {!result && <Card><EmptyState image="empty-search" title="ابدأ بالتحقق" text="أدخل رقم الهوية أو الوثيقة لعرض حالة التغطية." /></Card>}
        {result && (
          <Card title="نتيجة التحقق" action={<Badge tone={result.tone} dot>{result.status}</Badge>}>
            {result.p ? (
              <>
                <div className="rev__head elig__who">
                  <Avatar name={result.p.name} size={48} />
                  <div><b>{result.p.name}</b><small dir="ltr">{result.p.policy}</small></div>
                </div>
                <dl className="detail__list">
                  <div><dt>شركة التأمين</dt><dd>{result.p.insurer} – {result.p.tier}</dd></div>
                  <div><dt>نسبة التحمل</dt><dd>{result.p.copay}</dd></div>
                  <div><dt>تاريخ الانتهاء</dt><dd>{result.p.expiry}</dd></div>
                  <div><dt>الاستهلاك السنوي</dt><dd><Progress value={Math.round((result.p.used / result.p.limit) * 100)} /> متبقٍ {money(result.p.limit - result.p.used)}</dd></div>
                </dl>
                {!result.p.active && <p className="rev__error" role="alert">الوثيقة منتهية، لا يمكن تقديم موافقات أو مطالبات عليها.</p>}
                <Button variant="soft" icon="print">طباعة إثبات التغطية</Button>
              </>
            ) : (
              <p className="elig__none">لا توجد وثيقة بالرقم <b dir="ltr">{result.key}</b> لدى شركة <b>{result.insurer}</b>. تأكد من الرقم أو اختر شركة أخرى.</p>
            )}
          </Card>
        )}

        {history.length > 0 && (
          <Card title="آخر عمليات التحقق">
            <div className="dash__list">
              {history.map((h) => (
                <div key={h.id} className="rev__doc">
                  <Icon name="search" size={18} />
                  <span><span dir="ltr">{h.key}</span> · {h.insurer} · {h.name}</span>
                  <Badge tone={h.tone} dot>{h.status}</Badge>
                </div>
              ))}
            </div>
          </Card>
        )}
      </div>
    </div>
  )
}

export function InsuranceCompanies() {
  return (
    <ListPage
      columns={[
        { key: 'name', header: 'الشركة', type: 'person', sub: 'contract' },
        { key: 'discount', header: 'خصم العقد' },
        { key: 'pending', header: 'مطالبات معلقة' },
        { key: 'days', header: 'متوسط السداد (يوم)' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: COMPANY_TONES },
      ]}
      rows={companies}
      searchKeys={['name']}
      addLabel="إضافة شركة"
      form={{
        title: 'إضافة شركة تأمين',
        fields: [
          { name: 'name', label: 'اسم الشركة', required: true },
          { name: 'discount', label: 'نسبة خصم العقد', placeholder: 'مثال: 10%', required: true },
          { name: 'contact', label: 'بريد المطالبات', type: 'email' },
        ],
        defaults: { contract: 'عقد جديد', pending: 0, days: 0, status: 'نشط' },
      }}
      detail={{
        titleKey: 'name',
        subKey: 'contract',
        badgeKey: 'status',
        tones: COMPANY_TONES,
        fields: [
          { label: 'خصم العقد', key: 'discount' },
          { label: 'مطالبات معلقة', key: 'pending' },
          { label: 'متوسط السداد', key: 'days' },
          { label: 'التواصل', key: 'contact' },
        ],
        actions: [{ label: 'عرض مطالباتها', icon: 'file', variant: 'blue', to: '/insurance/claims' }, { label: 'تجديد العقد', icon: 'edit', variant: 'ghost' }],
      }}
    />
  )
}

export function InsuranceReports() {
  return (
    <DashboardTemplate
      stats={[
        { icon: 'file', label: 'مطالبات الشهر', value: '262', delta: '+7%' },
        { icon: 'wallet', label: 'المحصّل هذا الشهر', value: '980 ألف', delta: '+11%', tone: 'green' },
        { icon: 'clock', label: 'متوسط السداد', value: '41 يوم', delta: '-3', tone: 'orange' },
        { icon: 'alert', label: 'نسبة الرفض', value: '6.5%', delta: '-0.8%', tone: 'red' },
      ]}
      charts={[
        { title: 'المطالبات حسب الحالة', data: claimsByStatus },
        { title: 'عدد المطالبات الشهرية', data: claimsMonthly },
      ]}
    />
  )
}
