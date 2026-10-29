import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, DataTable, EmptyState, Input, Select, StatCard, Tabs, Textarea } from '../../components/ui'
import { DAYS, doses as initialDoses, notes as initialNotes, shifts, vitalsHistory, wardChart, wardPatients } from '../../data/nurse'
import './nurse.css'

const STATUS_TONES = { مستقر: 'green', مراقبة: 'orange', حرج: 'red' }
const DOSE_TONES = { قادمة: 'blue', 'تم الإعطاء': 'green', متأخرة: 'red' }
const NOTE_TONES = { 'حالة حرجة': 'red', 'ملاحظة عامة': 'blue', تثقيف: 'green', دواء: 'orange' }
const SHIFT_TONES = { صباحي: 'blue', مسائي: 'orange', ليلي: 'gray', إجازة: 'green' }

export function NurseDashboard() {
  const navigate = useNavigate()
  return (
    <DashboardTemplate
      banner={{ title: 'مناوبة الصباح', text: '3 مرضى تحتاج علاماتهم الحيوية تسجيلًا خلال ساعة.', cta: 'تسجيل علامات', to: '/nurse/vitals', image: 'role-nurse.png' }}
      stats={[
        { icon: 'bed', label: 'مرضى القسم', value: '18' },
        { icon: 'pulse', label: 'علامات مطلوبة', value: '3', tone: 'orange' },
        { icon: 'pill', label: 'جرعات قادمة', value: '4', tone: 'green' },
        { icon: 'bell', label: 'نداءات', value: '2', tone: 'red' },
      ]}
      table={{
        title: 'مرضى القسم',
        columns: [
          { key: 'name', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'condition' },
          { key: 'room', header: 'الغرفة' },
          { key: 'last', header: 'آخر قياس' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: STATUS_TONES },
        ],
        rows: wardPatients,
      }}
      side={{
        title: 'جرعات قادمة',
        items: [
          { mark: '08:30', title: 'أنسولين – غرفة 204', subtitle: 'متأخرة', tone: 'red', onClick: () => navigate('/nurse/medications') },
          { mark: '09:00', title: 'مضاد حيوي – غرفة 206', subtitle: 'بعد 20 دقيقة', tone: 'blue', onClick: () => navigate('/nurse/medications') },
          { mark: '09:15', title: 'مسكن – غرفة 210', subtitle: 'بعد 35 دقيقة', tone: 'orange', onClick: () => navigate('/nurse/medications') },
        ],
      }}
      charts={[{ title: 'القياسات المسجّلة حسب الساعة', data: wardChart }]}
    />
  )
}

export function NurseWard() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'crit', label: 'حرج', match: (r) => r.status === 'حرج' },
        { key: 'watch', label: 'مراقبة', match: (r) => r.status === 'مراقبة' },
        { key: 'ok', label: 'مستقر', match: (r) => r.status === 'مستقر' },
      ]}
      columns={[
        { key: 'name', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'condition' },
        { key: 'room', header: 'الغرفة' },
        { key: 'doctor', header: 'الطبيب' },
        { key: 'last', header: 'آخر قياس' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: STATUS_TONES },
      ]}
      rows={wardPatients}
      searchKeys={['name', 'room', 'condition']}
      detail={{
        titleKey: 'name',
        subKey: 'condition',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: STATUS_TONES,
        fields: [
          { label: 'العمر', key: 'age' },
          { label: 'الغرفة', key: 'room' },
          { label: 'الطبيب المعالج', key: 'doctor' },
          { label: 'آخر قياس', key: 'last' },
        ],
        actions: [{ label: 'تسجيل علامات حيوية', icon: 'pulse', variant: 'blue', to: '/nurse/vitals' }, { label: 'إضافة ملاحظة', icon: 'edit', variant: 'ghost', to: '/nurse/notes' }],
      }}
    />
  )
}

/* ---------- العلامات الحيوية ---------- */
// يرجع قائمة بالقيم الخارجة عن المعدل
const assess = ({ sys, dia, pulse, temp, spo2 }) => {
  const f = []
  if (sys > 140 || dia > 90) f.push('ضغط مرتفع')
  if (sys < 90 || dia < 60) f.push('ضغط منخفض')
  if (pulse > 100) f.push('نبض مرتفع')
  if (pulse < 60) f.push('نبض منخفض')
  if (temp > 37.8) f.push('حرارة مرتفعة')
  if (spo2 < 94) f.push('أكسجين منخفض')
  return f
}

const EMPTY_VITALS = { patient: wardPatients[0].name, sys: '', dia: '', pulse: '', temp: '', spo2: '' }

export function NurseVitals() {
  const [v, setV] = useState(EMPTY_VITALS)
  const [errors, setErrors] = useState({})
  const [history, setHistory] = useState(vitalsHistory)
  const [last, setLast] = useState(null)
  const set = (k) => (e) => setV((s) => ({ ...s, [k]: e.target.value }))

  const live = useMemo(() => {
    const n = Object.fromEntries(['sys', 'dia', 'pulse', 'temp', 'spo2'].map((k) => [k, Number(v[k])]))
    const filled = ['sys', 'dia', 'pulse', 'temp', 'spo2'].every((k) => v[k] !== '')
    return filled ? assess(n) : null
  }, [v])

  const save = (e) => {
    e.preventDefault()
    const next = {}
    const range = { sys: [50, 260], dia: [30, 160], pulse: [20, 220], temp: [30, 43], spo2: [50, 100] }
    for (const [k, [lo, hi]] of Object.entries(range)) {
      const n = Number(v[k])
      if (v[k] === '' || Number.isNaN(n)) next[k] = 'مطلوب'
      else if (n < lo || n > hi) next[k] = `القيمة بين ${lo} و ${hi}`
    }
    setErrors(next)
    if (Object.keys(next).length) return
    const flags = assess({ sys: +v.sys, dia: +v.dia, pulse: +v.pulse, temp: +v.temp, spo2: +v.spo2 })
    const row = { id: Date.now(), patient: v.patient, bp: `${v.sys}/${v.dia}`, pulse: +v.pulse, temp: +v.temp, spo2: +v.spo2, time: 'الآن', flags }
    setHistory((h) => [row, ...h])
    setLast(row)
    setV({ ...EMPTY_VITALS, patient: v.patient })
  }

  return (
    <div className="vitals">
      <Card title="تسجيل قياس جديد">
        <form className="vitals__form" onSubmit={save} noValidate>
          <Select label="المريض" options={wardPatients.map((p) => p.name)} value={v.patient} onChange={set('patient')} />
          <div className="vitals__grid">
            <Input label="الضغط الانقباضي" inputMode="numeric" placeholder="120" value={v.sys} onChange={set('sys')} error={errors.sys} />
            <Input label="الضغط الانبساطي" inputMode="numeric" placeholder="80" value={v.dia} onChange={set('dia')} error={errors.dia} />
            <Input label="النبض (نبضة/د)" inputMode="numeric" placeholder="75" value={v.pulse} onChange={set('pulse')} error={errors.pulse} />
            <Input label="الحرارة (°م)" inputMode="decimal" placeholder="36.7" value={v.temp} onChange={set('temp')} error={errors.temp} />
            <Input label="الأكسجين SpO₂ (%)" inputMode="numeric" placeholder="98" value={v.spo2} onChange={set('spo2')} error={errors.spo2} />
          </div>
          {live && (
            <div className={live.length ? 'vitals__alert is-bad' : 'vitals__alert is-ok'} role="status">
              {live.length ? `تنبيه: ${live.join('، ')}` : 'جميع القيم ضمن المعدل الطبيعي'}
            </div>
          )}
          <Button type="submit" icon="check" size="lg">حفظ القياس</Button>
        </form>
        {last && <p className="vitals__saved">تم حفظ قياس {last.patient} ✓</p>}
      </Card>

      <Card title="آخر القياسات" padded={false}>
        <DataTable
          columns={[
            { key: 'patient', header: 'المريض' },
            { key: 'bp', header: 'الضغط' },
            { key: 'pulse', header: 'النبض' },
            { key: 'temp', header: 'الحرارة' },
            { key: 'spo2', header: 'SpO₂' },
            { key: 'time', header: 'الوقت' },
            {
              key: 'flags',
              header: 'التقييم',
              render: (r) => (r.flags.length ? <span className="vitals__flags">{r.flags.map((f) => <Badge key={f} tone="red">{f}</Badge>)}</span> : <Badge tone="green" dot>طبيعي</Badge>),
            },
          ]}
          rows={history}
        />
      </Card>
    </div>
  )
}

/* ---------- سجل إعطاء الأدوية ---------- */
export function NurseMedications() {
  const [list, setList] = useState(initialDoses)
  const [tab, setTab] = useState('todo')
  const give = (id) => setList((l) => l.map((d) => (d.id === id ? { ...d, status: 'تم الإعطاء' } : d)))
  const count = (fn) => list.filter(fn).length
  const shown = list.filter((d) => (tab === 'todo' ? d.status !== 'تم الإعطاء' : d.status === 'تم الإعطاء'))

  return (
    <div className="meds-nurse">
      <div className="dash__stats">
        <StatCard icon="clock" label="قادمة" value={count((d) => d.status === 'قادمة')} />
        <StatCard icon="alert" label="متأخرة" value={count((d) => d.status === 'متأخرة')} tone="red" />
        <StatCard icon="check" label="تم إعطاؤها" value={count((d) => d.status === 'تم الإعطاء')} tone="green" />
      </div>
      <Tabs
        value={tab}
        onChange={setTab}
        items={[{ key: 'todo', label: 'المطلوبة', count: count((d) => d.status !== 'تم الإعطاء') }, { key: 'done', label: 'تم إعطاؤها', count: count((d) => d.status === 'تم الإعطاء') }]}
      />
      <Card padded={false}>
        {shown.length === 0 ? (
          <EmptyState image="success-done" title="لا توجد جرعات هنا" text="أحسنت! تمت كل الجرعات المطلوبة." />
        ) : (
          <DataTable
            columns={[
              { key: 'time', header: 'الوقت' },
              { key: 'patient', header: 'المريض' },
              { key: 'room', header: 'الغرفة' },
              { key: 'drug', header: 'الدواء' },
              { key: 'dose', header: 'الجرعة' },
              { key: 'status', header: 'الحالة', render: (r) => <Badge tone={DOSE_TONES[r.status]} dot>{r.status}</Badge> },
              { key: 'act', header: '', render: (r) => (r.status !== 'تم الإعطاء' ? <Button size="sm" variant="blue" icon="check" onClick={() => give(r.id)}>تم الإعطاء</Button> : null) },
            ]}
            rows={shown}
          />
        )}
      </Card>
    </div>
  )
}

/* ---------- ملاحظات التمريض ---------- */
export function NurseNotes() {
  const [list, setList] = useState(initialNotes)
  const [form, setForm] = useState({ patient: wardPatients[0].name, category: 'ملاحظة عامة', text: '' })
  const [error, setError] = useState('')

  const add = (e) => {
    e.preventDefault()
    if (!form.text.trim()) return setError('نص الملاحظة مطلوب')
    setError('')
    setList((l) => [{ id: Date.now(), ...form, time: 'الآن', author: 'نادين يوسف' }, ...l])
    setForm({ ...form, text: '' })
  }

  return (
    <div className="notes">
      <Card title="ملاحظة جديدة">
        <form className="notes__form" onSubmit={add} noValidate>
          <Select label="المريض" options={wardPatients.map((p) => p.name)} value={form.patient} onChange={(e) => setForm({ ...form, patient: e.target.value })} />
          <Select label="التصنيف" options={Object.keys(NOTE_TONES)} value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })} />
          <Textarea label="الملاحظة" rows={5} value={form.text} error={error} onChange={(e) => setForm({ ...form, text: e.target.value })} />
          <Button type="submit" icon="plus">إضافة الملاحظة</Button>
        </form>
      </Card>
      <div className="notes__list">
        {list.map((n) => (
          <Card key={n.id} className="note">
            <header>
              <Avatar name={n.author} size={36} />
              <span><b>{n.patient}</b><small>{n.author} · {n.time}</small></span>
              <Badge tone={NOTE_TONES[n.category] ?? 'gray'} dot>{n.category}</Badge>
            </header>
            <p>{n.text}</p>
          </Card>
        ))}
      </div>
    </div>
  )
}

/* ---------- المناوبات ---------- */
export function NurseShifts() {
  return (
    <div className="shifts">
      <div className="shifts__legend">
        {Object.keys(SHIFT_TONES).map((s) => <Badge key={s} tone={SHIFT_TONES[s]} dot>{s}</Badge>)}
      </div>
      <Card padded={false}>
        <div className="table-wrap">
          <table className="table shifts__table">
            <thead>
              <tr>
                <th>الممرض/ة</th>
                {DAYS.map((d) => <th key={d}>{d}</th>)}
              </tr>
            </thead>
            <tbody>
              {shifts.map((s) => (
                <tr key={s.id}>
                  <td><span className="cell-person"><Avatar src={s.avatar} name={s.name} size={36} /><span><b>{s.name}</b></span></span></td>
                  {s.days.map((d, i) => <td key={i}><Badge tone={SHIFT_TONES[d]}>{d}</Badge></td>)}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  )
}
