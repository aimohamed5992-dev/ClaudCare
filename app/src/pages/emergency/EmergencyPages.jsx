import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, Checkbox, Icon, Input, Select } from '../../components/ui'
import { COMPLAINTS, FLAGS, LEVELS, ambulances, arrivalsHourly, computeTriage, erPatients, levelText, levelsToday } from '../../data/emergency'
import { cx } from '../../utils/cx'
import './emergency.css'
import '../reception/reception.css'

const Level = ({ level }) => <span className={cx('lvl', `lvl--${LEVELS[level].tone}`)}>{levelText(level)}</span>
const STATUS_TONES = { 'بانتظار الفحص': 'orange', 'قيد المعاينة': 'blue', 'تحويل للتنويم': 'green', خرج: 'gray' }
const AMB_TONES = { 'في الطريق': 'orange', وصلت: 'green' }

export function EmergencyDashboard() {
  const navigate = useNavigate()
  const waiting = erPatients.filter((p) => p.status === 'بانتظار الفحص').sort((a, b) => a.level - b.level)
  return (
    <DashboardTemplate
      banner={{ title: 'قسم الطوارئ', text: 'حالة حرجة قيد المعاينة، وإسعافان في الطريق.', cta: 'فرز مريض جديد', to: '/emergency/triage', image: 'role-emergency.png' }}
      stats={[
        { icon: 'users', label: 'مرضى حاليًا', value: String(erPatients.length) },
        { icon: 'alert', label: 'حالات حرجة (1–2)', value: '2', tone: 'red' },
        { icon: 'clock', label: 'متوسط الانتظار', value: '18 د', tone: 'orange' },
        { icon: 'bed', label: 'أسرّة متاحة', value: '5 من 18', tone: 'green' },
      ]}
      table={{
        title: 'بانتظار الفحص (حسب الأولوية)',
        columns: [
          { key: 'name', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'complaint' },
          { key: 'level', header: 'الفرز', render: (r) => <Level level={r.level} /> },
          { key: 'arrived', header: 'الوصول' },
          { key: 'zone', header: 'المنطقة' },
        ],
        rows: waiting,
      }}
      side={{
        title: 'إسعاف وارد',
        items: ambulances.map((a) => ({ mark: String(a.level), title: `${a.unit} · ${a.condition}`, subtitle: `${a.patient} · ${a.eta}`, tone: LEVELS[a.level].tone === 'red' ? 'red' : 'orange', onClick: () => navigate('/emergency/ambulances') })),
      }}
      charts={[
        { title: 'الوصول حسب الساعة (اليوم)', data: arrivalsHourly },
        { title: 'الحالات حسب مستوى الفرز (اليوم)', data: levelsToday },
      ]}
    />
  )
}

/* ---------- لوحة الطوارئ ---------- */
const NEXT = { 'بانتظار الفحص': 'بدء المعاينة', 'قيد المعاينة': 'تحويل للتنويم' }
const NEXT_STATUS = { 'بانتظار الفحص': 'قيد المعاينة', 'قيد المعاينة': 'تحويل للتنويم' }

export function EmergencyBoard() {
  const [patients, setPatients] = useState(erPatients)
  const advance = (id) => setPatients((ps) => ps.map((p) => (p.id === id ? { ...p, status: NEXT_STATUS[p.status] } : p)))
  const discharge = (id) => setPatients((ps) => ps.map((p) => (p.id === id ? { ...p, status: 'خرج' } : p)))
  const active = patients.filter((p) => p.status !== 'خرج' && p.status !== 'تحويل للتنويم')
  return (
    <>
      <p className="erb__summary" aria-live="polite" style={{ margin: '0 0 var(--s-4)', color: 'var(--muted)' }}>
        {active.length} مريض نشط · {patients.filter((p) => p.status === 'تحويل للتنويم').length} تحويل للتنويم · {patients.filter((p) => p.status === 'خرج').length} خرج
      </p>
      <div className="erb" role="group" aria-label="لوحة الطوارئ حسب مستوى الفرز">
        {Object.entries(LEVELS).map(([lv, info]) => {
          const col = active.filter((p) => p.level === Number(lv))
          return (
            <section key={lv} className={cx('erb__col', `erb__col--${info.tone}`)} aria-label={levelText(lv)}>
              <div className="erb__head"><b>{levelText(lv)}</b><small>{info.wait} · {col.length}</small></div>
              {col.length === 0 && <div className="erb__none">لا حالات</div>}
              {col.map((p) => (
                <Card key={p.id} className="erb__card">
                  <div className="erb__who">
                    <Avatar src={p.avatar} name={p.name} size={40} />
                    <div><b>{p.name}</b><small>{p.age} سنة · {p.complaint}</small></div>
                  </div>
                  <div className="erb__meta"><span>{p.zone}</span><span>{p.arrived}</span></div>
                  <Badge tone={STATUS_TONES[p.status]} dot>{p.status}</Badge>
                  <Button size="sm" variant="blue" onClick={() => advance(p.id)}>{NEXT[p.status]}</Button>
                  {p.status === 'قيد المعاينة' && <Button size="sm" variant="ghost" onClick={() => discharge(p.id)}>خروج بعد المعاينة</Button>}
                </Card>
              ))}
            </section>
          )
        })}
      </div>
    </>
  )
}

/* ---------- الفرز السريع ---------- */
const num = (v) => (v === '' ? null : Number(v))

const INIT = { name: '', age: '', complaint: COMPLAINTS[0], consciousness: 'واعٍ', hr: '', sbp: '', spo2: '', rr: '', temp: '', pain: '0' }

export function EmergencyTriage() {
  const navigate = useNavigate()
  const [f, setF] = useState(INIT)
  const [flags, setFlags] = useState({})
  const [done, setDone] = useState(null)
  const set = (k) => (e) => setF((s) => ({ ...s, [k]: e.target.value }))

  const vitals = ['hr', 'sbp', 'spo2', 'rr', 'temp']
  const bad = (k, min, max) => f[k] !== '' && !(Number(f[k]) >= min && Number(f[k]) <= max)
  const invalid = bad('hr', 20, 250) || bad('sbp', 40, 300) || bad('spo2', 50, 100) || bad('rr', 4, 80) || bad('temp', 30, 44)
  const filled = vitals.filter((k) => f[k] !== '').length
  const age = Number(f.age)
  const reasons = [
    !f.name.trim() && 'اكتب اسم المريض',
    !(age > 0 && age < 120) && 'أدخل عمرًا صحيحًا',
    filled < 3 && 'أدخل 3 علامات حيوية على الأقل',
    invalid && 'علامة حيوية خارج النطاق المعقول',
  ].filter(Boolean)

  const result = computeTriage({
    consciousness: f.consciousness, hr: num(f.hr), sbp: num(f.sbp), spo2: num(f.spo2), rr: num(f.rr), temp: num(f.temp), flags, pain: Number(f.pain),
  })
  const info = LEVELS[result.level]

  if (done) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>أُضيف إلى لوحة الطوارئ</h2>
        <p>{done.name} · <Level level={done.level} /></p>
        <p>الانتظار الأقصى: <b>{LEVELS[done.level].wait}</b></p>
        <div className="reg__actions">
          <Button variant="blue" onClick={() => navigate('/emergency/board')}>فتح اللوحة</Button>
          <Button variant="ghost" onClick={() => { setDone(null); setF(INIT); setFlags({}) }}>فرز مريض آخر</Button>
        </div>
      </Card>
    )
  }

  return (
    <div className="tri">
      <div className="tri__main">
        <Card title="بيانات المريض">
          <div className="tri__grid tri__grid--2">
            <Input label="اسم المريض" value={f.name} onChange={set('name')} />
            <Input label="العمر" inputMode="numeric" value={f.age} onChange={set('age')} />
            <Select label="الشكوى الرئيسية" options={COMPLAINTS} value={f.complaint} onChange={set('complaint')} />
            <Select label="مستوى الوعي" options={['واعٍ', 'يستجيب للصوت', 'يستجيب للألم فقط', 'غير مستجيب']} value={f.consciousness} onChange={set('consciousness')} />
          </div>
        </Card>
        <Card title="العلامات الحيوية">
          <div className="tri__grid">
            <Input label="النبض (/د)" inputMode="numeric" value={f.hr} onChange={set('hr')} error={bad('hr', 20, 250) ? 'قيمة غير معقولة' : undefined} />
            <Input label="الضغط الانقباضي" inputMode="numeric" value={f.sbp} onChange={set('sbp')} error={bad('sbp', 40, 300) ? 'قيمة غير معقولة' : undefined} />
            <Input label="تشبع الأكسجين %" inputMode="numeric" value={f.spo2} onChange={set('spo2')} error={bad('spo2', 50, 100) ? 'قيمة غير معقولة' : undefined} />
            <Input label="معدل التنفس (/د)" inputMode="numeric" value={f.rr} onChange={set('rr')} error={bad('rr', 4, 80) ? 'قيمة غير معقولة' : undefined} />
            <Input label="الحرارة °م" inputMode="decimal" value={f.temp} onChange={set('temp')} error={bad('temp', 30, 44) ? 'قيمة غير معقولة' : undefined} />
            <Select label="شدة الألم (0–10)" options={Array.from({ length: 11 }, (_, i) => String(i))} value={f.pain} onChange={set('pain')} />
          </div>
        </Card>
        <Card title="علامات الخطر">
          <div className="tri__flags">
            {FLAGS.map((x) => (
              <Checkbox key={x.key} label={x.label} checked={!!flags[x.key]} onChange={(e) => setFlags((s) => ({ ...s, [x.key]: e.target.checked }))} />
            ))}
          </div>
        </Card>
      </div>
      <aside className="tri__side">
        <Card title="نتيجة الفرز المقترحة">
          <div className={cx('tri__result', `tri__result--${info.tone}`)} aria-live="polite">
            <b>{result.level}</b>
            <span>{info.label}</span>
            <small>الانتظار الأقصى: {info.wait}</small>
          </div>
          {result.why.length > 0 && <ul className="tri__why">{result.why.map((w) => <li key={w}>{w}</li>)}</ul>}
          <div className="tri__form">
            {reasons.length > 0 && <ul className="tri__reasons" aria-live="polite">{reasons.map((r) => <li key={r}>{r}</li>)}</ul>}
            <Button icon="check" size="lg" full disabled={reasons.length > 0} onClick={() => setDone({ name: f.name.trim(), level: result.level })}>تأكيد وإضافة للوحة</Button>
            <small style={{ color: 'var(--muted)' }}>النتيجة مقترحة وللممرض تعديلها بحكمه السريري.</small>
          </div>
        </Card>
      </aside>
    </div>
  )
}

export function EmergencyAmbulances() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'way', label: 'في الطريق', match: (r) => r.status === 'في الطريق' },
        { key: 'here', label: 'وصلت', match: (r) => r.status === 'وصلت' },
      ]}
      columns={[
        { key: 'unit', header: 'الوحدة' },
        { key: 'patient', header: 'المريض' },
        { key: 'condition', header: 'الحالة' },
        { key: 'level', header: 'الأولوية', render: (r) => <Level level={r.level} /> },
        { key: 'eta', header: 'الوصول' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: AMB_TONES },
      ]}
      rows={ambulances}
      searchKeys={['unit', 'patient', 'condition']}
      detail={{
        titleKey: 'unit',
        subKey: 'patient',
        badgeKey: 'status',
        tones: AMB_TONES,
        fields: [
          { label: 'الحالة', key: 'condition' },
          { label: 'الوصول المتوقع', key: 'eta' },
        ],
        actions: [{ label: 'استقبال وفرز', icon: 'stethoscope', variant: 'blue', to: '/emergency/triage' }, { label: 'تجهيز غرفة الإنعاش', icon: 'bed', variant: 'ghost' }],
      }}
    />
  )
}
