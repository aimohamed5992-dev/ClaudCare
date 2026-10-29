import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, Checkbox, Icon, Select, Tabs } from '../../components/ui'
import { ADMISSION_TYPES, BED_TONES, DISCHARGE_STEPS, WARDS, admissionsByWard, beds as initialBeds, dischargeCandidates, occupancyWeekly, pendingAdmissions, transfers } from '../../data/admissions'
import { cx } from '../../utils/cx'
import './admissions.css'
import '../reception/reception.css'

const TR_TONES = { 'بانتظار سرير': 'orange', معتمد: 'blue', 'تم النقل': 'green' }

export function AdmissionsDashboard() {
  const navigate = useNavigate()
  const free = initialBeds.filter((b) => b.status === 'متاح').length
  return (
    <DashboardTemplate
      banner={{ title: 'الإدخال والأسرّة', text: `${pendingAdmissions.length} مرضى بانتظار سرير، و${free} أسرّة متاحة الآن.`, cta: 'خريطة الأسرّة', to: '/admissions/beds', image: 'role-admissions.png' }}
      stats={[
        { icon: 'bed', label: 'الأسرّة المتاحة', value: `${free} من ${initialBeds.length}`, tone: 'green' },
        { icon: 'users', label: 'بانتظار الإدخال', value: String(pendingAdmissions.length), tone: 'orange' },
        { icon: 'truck', label: 'طلبات نقل', value: '2', tone: 'blue' },
        { icon: 'chart', label: 'نسبة الإشغال', value: '84%', delta: '+2%' },
      ]}
      table={{
        title: 'بانتظار الإدخال',
        columns: [
          { key: 'name', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'reason' },
          { key: 'from', header: 'القادم من' },
          { key: 'ward', header: 'القسم' },
        ],
        rows: pendingAdmissions,
      }}
      side={{
        title: 'يحتاج انتباهك',
        items: [
          { mark: '2', title: 'أسرّة بانتظار التنظيف', subtitle: 'الباطنة والعناية المركزة', tone: 'orange', onClick: () => navigate('/admissions/beds') },
          { mark: '1', title: 'نقل بانتظار سرير', subtitle: 'نورة سعد ← العناية المركزة', tone: 'red', onClick: () => navigate('/admissions/transfers') },
          { mark: '3', title: 'مرشحون للخروج اليوم', subtitle: 'تحقق من خطوات الخروج', tone: 'blue', onClick: () => navigate('/admissions/discharge') },
        ],
      }}
      charts={[
        { title: 'نسبة الإشغال الأسبوعية (%)', data: occupancyWeekly },
        { title: 'الإدخالات حسب القسم (هذا الشهر)', data: admissionsByWard },
      ]}
    />
  )
}

/* ---------- خريطة الأسرّة ---------- */
export function AdmissionsBeds() {
  const navigate = useNavigate()
  const [beds, setBeds] = useState(initialBeds)
  const [ward, setWard] = useState('all')
  const [selId, setSelId] = useState(null)
  const sel = beds.find((b) => b.id === selId)
  const setStatus = (id, status) => setBeds((bs) => bs.map((b) => (b.id === id ? { id: b.id, ward: b.ward, status } : b)))
  const shown = WARDS.filter((w) => ward === 'all' || w === ward)
  const count = (s) => beds.filter((b) => b.status === s).length

  return (
    <div className="bm">
      <div className="bm__main">
        <Tabs
          items={[{ key: 'all', label: 'كل الأقسام' }, ...WARDS.map((w) => ({ key: w, label: w }))]}
          value={ward}
          onChange={setWard}
        />
        <div className="bm__legend" aria-live="polite">
          {Object.keys(BED_TONES).map((s) => <Badge key={s} tone={BED_TONES[s]} dot>{s}: {count(s)}</Badge>)}
        </div>
        {shown.map((w) => (
          <section key={w} className="bm__ward" aria-label={w}>
            <h3>{w}</h3>
            <div className="bm__grid">
              {beds.filter((b) => b.ward === w).map((b) => (
                <button key={b.id} type="button" className={cx('bed', `bed--${BED_TONES[b.status]}`, b.id === selId && 'is-sel')} onClick={() => setSelId(b.id)} aria-pressed={b.id === selId}>
                  <b dir="ltr">{b.id}</b>
                  <Badge tone={BED_TONES[b.status]} dot>{b.status}</Badge>
                  <small>{b.patient ?? b.reservedFor ?? '—'}</small>
                </button>
              ))}
            </div>
          </section>
        ))}
      </div>
      <aside className="bm__side">
        <Card title={sel ? `سرير ${sel.id}` : 'تفاصيل السرير'}>
          {!sel && <p className="bm__empty">اختر سريرًا من الخريطة.</p>}
          {sel && (
            <>
              <dl className="detail__list">
                <div><dt>القسم</dt><dd>{sel.ward}</dd></div>
                <div><dt>الحالة</dt><dd><Badge tone={BED_TONES[sel.status]} dot>{sel.status}</Badge></dd></div>
                {sel.patient && <div><dt>المريض</dt><dd>{sel.patient}</dd></div>}
                {sel.since && <div><dt>منذ</dt><dd>{sel.since}</dd></div>}
                {sel.reservedFor && <div><dt>محجوز لـ</dt><dd>{sel.reservedFor}</dd></div>}
              </dl>
              <div className="bm__actions">
                {sel.status === 'متاح' && <Button variant="blue" icon="plus" onClick={() => navigate(`/admissions/admit?bed=${sel.id}`)}>إدخال مريض هنا</Button>}
                {sel.status === 'متاح' && <Button variant="ghost" onClick={() => setStatus(sel.id, 'محجوز')}>حجز السرير</Button>}
                {sel.status === 'تنظيف' && <Button variant="blue" icon="check" onClick={() => setStatus(sel.id, 'متاح')}>تم التنظيف – إتاحة السرير</Button>}
                {sel.status === 'محجوز' && <Button variant="ghost" onClick={() => setStatus(sel.id, 'متاح')}>إلغاء الحجز</Button>}
                {sel.status === 'مشغول' && <Button variant="blue" onClick={() => navigate('/admissions/discharge')}>بدء إجراءات الخروج</Button>}
              </div>
            </>
          )}
        </Card>
      </aside>
    </div>
  )
}

/* ---------- إدخال مريض ---------- */
export function AdmissionsAdmit() {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const presetBed = params.get('bed')
  const presetWard = initialBeds.find((b) => b.id === presetBed && b.status === 'متاح')?.ward
  const [patientId, setPatientId] = useState(String(pendingAdmissions[0].id))
  const [ward, setWard] = useState(presetWard ?? pendingAdmissions[0].ward)
  const [bed, setBed] = useState(presetWard ? presetBed : '')
  const [type, setType] = useState(ADMISSION_TYPES[0])
  const [consent, setConsent] = useState(false)
  const [approval, setApproval] = useState(false)
  const [done, setDone] = useState(null)

  const p = pendingAdmissions.find((x) => String(x.id) === patientId)
  const free = initialBeds.filter((b) => b.ward === ward && b.status === 'متاح')
  const bedValid = free.some((b) => b.id === bed)
  const minor = p.age < 18
  const reasons = [
    !bedValid && 'اختر سريرًا متاحًا',
    ward !== p.ward && `القسم المطلوب طبيًا هو ${p.ward}، أكّد الاختلاف مع الطبيب`,
    minor && !consent && 'موافقة ولي الأمر مطلوبة للقاصر',
    p.insured && !approval && 'تحقق من الموافقة المسبقة للتأمين',
  ].filter(Boolean)

  if (done) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>تم إدخال المريض</h2>
        <p>{done.name} · السرير <b dir="ltr">{done.bed}</b> · {done.ward} · {done.type}</p>
        <div className="reg__actions">
          <Button variant="blue" onClick={() => navigate('/admissions/beds')}>خريطة الأسرّة</Button>
          <Button variant="ghost" icon="print">طباعة سوار المريض</Button>
        </div>
      </Card>
    )
  }

  const changePatient = (id) => {
    const np = pendingAdmissions.find((x) => String(x.id) === id)
    setPatientId(id); setWard(np.ward); setBed(''); setConsent(false); setApproval(false)
  }

  return (
    <div className="adm">
      <div className="adm__main">
        <Card title="المريض">
          <div className="adm__who">
            <Avatar src={p.avatar} name={p.name} size={56} />
            <div><b>{p.name}</b><small>{p.age} سنة · من {p.from} · {p.reason}</small></div>
            {p.insured ? <Badge tone="blue" dot>{p.approval}</Badge> : <Badge tone="gray">بدون تأمين</Badge>}
          </div>
        </Card>
        <Card title="بيانات الإدخال">
          <div className="adm__grid">
            <Select label="طلب الإدخال" options={pendingAdmissions.map((x) => ({ value: String(x.id), label: x.name }))} value={patientId} onChange={(e) => changePatient(e.target.value)} />
            <Select label="نوع الإدخال" options={ADMISSION_TYPES} value={type} onChange={(e) => setType(e.target.value)} />
            <Select label="القسم" options={WARDS} value={ward} onChange={(e) => { setWard(e.target.value); setBed('') }} />
            <Select label="السرير" options={[{ value: '', label: free.length ? 'اختر السرير…' : 'لا أسرّة متاحة' }, ...free.map((b) => ({ value: b.id, label: b.id }))]} value={bed} onChange={(e) => setBed(e.target.value)} />
          </div>
        </Card>
      </div>
      <aside className="adm__side">
        <Card title="التحقق قبل الإدخال">
          <div className="adm__form">
            {minor && <Checkbox label="تم الحصول على موافقة ولي الأمر" checked={consent} onChange={(e) => setConsent(e.target.checked)} />}
            {p.insured && <Checkbox label="تم التحقق من الموافقة المسبقة للتأمين" checked={approval} onChange={(e) => setApproval(e.target.checked)} />}
            {!minor && !p.insured && <small style={{ color: 'var(--muted)' }}>لا شروط إضافية لهذا المريض.</small>}
            {reasons.length > 0 && <ul className="adm__reasons" aria-live="polite">{reasons.map((r) => <li key={r}>{r}</li>)}</ul>}
            <Button icon="check" size="lg" full disabled={reasons.length > 0} onClick={() => setDone({ name: p.name, bed, ward, type })}>تأكيد الإدخال</Button>
          </div>
        </Card>
      </aside>
    </div>
  )
}

export function AdmissionsTransfers() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'wait', label: 'بانتظار سرير', match: (r) => r.status === 'بانتظار سرير' },
        { key: 'ok', label: 'معتمدة', match: (r) => r.status === 'معتمد' },
        { key: 'done', label: 'تم النقل', match: (r) => r.status === 'تم النقل' },
      ]}
      columns={[
        { key: 'no', header: 'الرقم' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'reason' },
        { key: 'from', header: 'من' },
        { key: 'to', header: 'إلى' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: TR_TONES },
      ]}
      rows={transfers}
      searchKeys={['no', 'patient', 'from', 'to']}
      addLabel="طلب نقل"
      form={{
        title: 'طلب نقل مريض',
        fields: [
          { name: 'patient', label: 'اسم المريض', required: true },
          { name: 'from', label: 'من قسم', options: WARDS },
          { name: 'to', label: 'إلى قسم', options: [...WARDS].reverse() },
          { name: 'reason', label: 'سبب النقل', required: true },
        ],
        defaults: { no: 'TR-411', status: 'بانتظار سرير' },
      }}
      detail={{
        titleKey: 'patient',
        subKey: 'no',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: TR_TONES,
        fields: [
          { label: 'من', key: 'from' },
          { label: 'إلى', key: 'to' },
          { label: 'السبب', key: 'reason' },
        ],
        actions: [{ label: 'اختيار سرير', icon: 'bed', variant: 'blue', to: '/admissions/beds' }, { label: 'اعتماد النقل', icon: 'check', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- الخروج ---------- */
export function AdmissionsDischarge() {
  const [id, setId] = useState(String(dischargeCandidates[0].id))
  const [checked, setChecked] = useState({})
  const [out, setOut] = useState([])
  const left = dischargeCandidates.filter((c) => !out.includes(c.id))
  const p = left.find((c) => String(c.id) === id) ?? left[0]

  if (!p) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>لا مرشحين للخروج</h2>
        <p>خرج {out.length} مرضى وأُرسلت أسرّتهم للتنظيف.</p>
      </Card>
    )
  }
  const mine = checked[p.id] ?? {}
  const remaining = DISCHARGE_STEPS.filter((_, i) => !mine[i])
  const confirm = () => { setOut((o) => [...o, p.id]); setId('') }

  return (
    <div className="adm">
      <div className="adm__main">
        <Card title="المريض">
          <div className="adm__who">
            <Avatar src={p.avatar} name={p.name} size={56} />
            <div><b>{p.name}</b><small>السرير <span dir="ltr">{p.bed}</span> · {p.days} أيام إقامة</small></div>
          </div>
          <div className="adm__form">
            <Select label="المريض" options={left.map((c) => ({ value: String(c.id), label: c.name }))} value={String(p.id)} onChange={(e) => setId(e.target.value)} />
          </div>
        </Card>
        <Card title="خطوات الخروج">
          <div className="adm__steps">
            {DISCHARGE_STEPS.map((s, i) => (
              <Checkbox key={s} label={s} checked={!!mine[i]} onChange={(e) => setChecked((c) => ({ ...c, [p.id]: { ...c[p.id], [i]: e.target.checked } }))} />
            ))}
          </div>
        </Card>
      </div>
      <aside className="adm__side">
        <Card title="تأكيد الخروج">
          <div className="adm__form">
            <Badge tone={remaining.length ? 'orange' : 'green'} dot>{remaining.length ? `${remaining.length} خطوات متبقية` : 'كل الخطوات مكتملة'}</Badge>
            <Button icon="check" size="lg" full disabled={remaining.length > 0} onClick={confirm}>تأكيد الخروج</Button>
            <small style={{ color: 'var(--muted)' }}>بعد التأكيد يتحول السرير إلى «تنظيف».</small>
          </div>
        </Card>
      </aside>
    </div>
  )
}
