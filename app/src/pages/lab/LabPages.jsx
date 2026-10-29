import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, LabReport, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, Icon, Input, Progress } from '../../components/ui'
import { CBC_PARAMS, devices as initialDevices, labHourly, labRequests, labResults, qcRuns, qcWeekly, samples } from '../../data/lab'
import './lab.css'
import '../patient/patient.css'
import '../reception/reception.css'

const REQ_TONES = { 'بانتظار العينة': 'gray', 'قيد التحليل': 'orange', جاهز: 'green' }
const PRIORITY_TONES = { عاجل: 'red', عادي: 'gray' }
const SAMPLE_TONES = { 'تم الاستلام': 'green', 'بانتظار الاستلام': 'orange', مرفوضة: 'red' }
const DEVICE_TONES = { يعمل: 'green', معايرة: 'orange', عطل: 'red' }
const QC_TONES = { مقبول: 'green', تحذير: 'orange', مرفوض: 'red' }

export function LabDashboard() {
  const navigate = useNavigate()
  return (
    <DashboardTemplate
      banner={{ title: 'مختبر الفرع الرئيسي', text: '5 طلبات تحليل بانتظار الإدخال، منها طلبان عاجلان.', cta: 'بدء الإدخال', to: '/lab/entry', image: 'role-lab.png' }}
      stats={[
        { icon: 'flask', label: 'طلبات اليوم', value: '126', delta: '+10%' },
        { icon: 'pill', label: 'عينات مستلمة', value: '98', tone: 'orange' },
        { icon: 'file', label: 'نتائج جاهزة', value: '74', delta: '+14', tone: 'green' },
        { icon: 'alert', label: 'طلبات عاجلة', value: '2', tone: 'red' },
      ]}
      table={{
        title: 'طلبات التحاليل',
        columns: [
          { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'order' },
          { key: 'test', header: 'التحليل' },
          { key: 'priority', header: 'الأولوية', type: 'badge', tones: PRIORITY_TONES },
          { key: 'status', header: 'الحالة', type: 'badge', tones: REQ_TONES },
        ],
        rows: labRequests,
      }}
      side={{
        title: 'حالة الأجهزة',
        items: initialDevices.map((d) => ({ mark: d.status === 'يعمل' ? 'OK' : '!', title: d.name, subtitle: d.status, tone: DEVICE_TONES[d.status], onClick: () => navigate('/lab/devices') })),
      }}
      charts={[{ title: 'الطلبات حسب الساعة', data: labHourly }]}
    />
  )
}

export function LabRequests() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'urgent', label: 'عاجل', match: (r) => r.priority === 'عاجل' },
        { key: 'wait', label: 'بانتظار العينة', match: (r) => r.status === 'بانتظار العينة' },
        { key: 'run', label: 'قيد التحليل', match: (r) => r.status === 'قيد التحليل' },
        { key: 'ready', label: 'جاهز', match: (r) => r.status === 'جاهز' },
      ]}
      columns={[
        { key: 'order', header: 'رقم الطلب' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'doctor' },
        { key: 'test', header: 'التحليل' },
        { key: 'priority', header: 'الأولوية', type: 'badge', tones: PRIORITY_TONES },
        { key: 'status', header: 'الحالة', type: 'badge', tones: REQ_TONES },
      ]}
      rows={labRequests}
      searchKeys={['order', 'patient', 'test']}
      detail={{
        titleKey: 'patient',
        subKey: 'order',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: REQ_TONES,
        fields: [
          { label: 'التحليل', key: 'test' },
          { label: 'الطبيب', key: 'doctor' },
          { label: 'الأولوية', key: 'priority' },
          { label: 'وقت الطلب', key: 'time' },
        ],
        actions: [{ label: 'إدخال النتيجة', icon: 'edit', variant: 'blue', to: '/lab/entry' }, { label: 'طباعة الباركود', icon: 'print', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- إدخال النتيجة ---------- */
const flagOf = (value, [lo, hi]) => (value < lo ? 'منخفض' : value > hi ? 'مرتفع' : 'طبيعي')
const FLAG_TONES = { طبيعي: 'green', مرتفع: 'red', منخفض: 'orange' }

export function LabEntry() {
  const navigate = useNavigate()
  const [values, setValues] = useState({})
  const [errors, setErrors] = useState({})
  const [done, setDone] = useState(false)

  const flags = useMemo(
    () => Object.fromEntries(CBC_PARAMS.map((p) => [p.key, values[p.key] !== undefined && values[p.key] !== '' && !Number.isNaN(Number(values[p.key])) ? flagOf(Number(values[p.key]), p.range) : null])),
    [values],
  )
  const abnormal = Object.values(flags).filter((f) => f && f !== 'طبيعي').length

  const submit = () => {
    const next = {}
    CBC_PARAMS.forEach((p) => {
      const v = values[p.key]
      if (v === undefined || v === '') next[p.key] = 'مطلوب'
      else if (Number.isNaN(Number(v)) || Number(v) < 0) next[p.key] = 'قيمة غير صحيحة'
    })
    setErrors(next)
    if (Object.keys(next).length === 0) setDone(true)
  }

  if (done) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>تم اعتماد النتيجة وإرسالها</h2>
        <p>صورة دم كاملة · محمد علي · {abnormal ? `${abnormal} قيم خارج المعدل` : 'كل القيم طبيعية'}</p>
        <div className="reg__actions">
          <Button variant="blue" onClick={() => navigate('/lab/requests')}>العودة للطلبات</Button>
          <Button variant="ghost" onClick={() => navigate('/lab/results')}>عرض النتائج</Button>
        </div>
      </Card>
    )
  }

  return (
    <div className="entry">
      <Card className="entry__head">
        <Avatar src="avatars/patient-3.jpg" name="محمد علي" size={56} />
        <div>
          <h3>محمد علي</h3>
          <small>LAB-2210 · صورة دم كاملة (CBC) · طلب د. منى القاسم</small>
        </div>
        <Badge tone="red" dot>عاجل</Badge>
      </Card>

      <Card padded={false} title="القيم المخبرية">
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr><th>التحليل</th><th>النتيجة</th><th>الوحدة</th><th>المعدل الطبيعي</th><th>التقييم</th></tr>
            </thead>
            <tbody>
              {CBC_PARAMS.map((p) => (
                <tr key={p.key}>
                  <td><b>{p.name}</b></td>
                  <td className="entry__input">
                    <Input
                      aria-label={p.name}
                      inputMode="decimal"
                      value={values[p.key] ?? ''}
                      error={errors[p.key]}
                      onChange={(e) => setValues((v) => ({ ...v, [p.key]: e.target.value }))}
                    />
                  </td>
                  <td>{p.unit}</td>
                  <td>{p.range[0]} – {p.range[1]}</td>
                  <td>{flags[p.key] ? <Badge tone={FLAG_TONES[flags[p.key]]} dot>{flags[p.key]}</Badge> : <span className="entry__dash">—</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="entry__foot">
          <span className={abnormal ? 'entry__sum is-bad' : 'entry__sum'}>{abnormal ? `${abnormal} قيم خارج المعدل` : 'أدخل القيم ليظهر التقييم'}</span>
          <Button variant="ghost" onClick={() => navigate('/lab/requests')}>إلغاء</Button>
          <Button icon="check" onClick={submit}>اعتماد وإرسال</Button>
        </div>
      </Card>
    </div>
  )
}

export function LabResults() {
  const [selected, setSelected] = useState(labResults[0].id)
  const report = labResults.find((r) => r.id === selected)
  return (
    <div className="split">
      <Card title="النتائج المعتمدة" className="split__list">
        <div className="dash__list">
          {labResults.map((r) => (
            <button key={r.id} className={selected === r.id ? 'pick is-on' : 'pick'} onClick={() => setSelected(r.id)}>
              <span className="pick__icon"><Icon name="file" size={20} /></span>
              <span className="pick__text"><b>{r.title}</b><small>{r.date}</small></span>
            </button>
          ))}
        </div>
      </Card>
      <LabReport report={report} />
    </div>
  )
}

export function LabSamples() {
  return (
    <ListPage
      stats={[
        { icon: 'pill', label: 'عينات اليوم', value: '98' },
        { icon: 'check', label: 'تم الاستلام', value: '71', tone: 'green' },
        { icon: 'clock', label: 'بانتظار الاستلام', value: '24', tone: 'orange' },
        { icon: 'alert', label: 'مرفوضة', value: '3', tone: 'red' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'wait', label: 'بانتظار الاستلام', match: (r) => r.status === 'بانتظار الاستلام' },
        { key: 'ok', label: 'تم الاستلام', match: (r) => r.status === 'تم الاستلام' },
        { key: 'bad', label: 'مرفوضة', match: (r) => r.status === 'مرفوضة' },
      ]}
      columns={[
        { key: 'code', header: 'الباركود' },
        { key: 'patient', header: 'المريض' },
        { key: 'type', header: 'النوع' },
        { key: 'tube', header: 'الأنبوب' },
        { key: 'collected', header: 'وقت السحب' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: SAMPLE_TONES },
      ]}
      rows={samples}
      searchKeys={['code', 'patient']}
      addLabel="تسجيل عينة"
      form={{
        title: 'تسجيل عينة جديدة',
        fields: [
          { name: 'patient', label: 'اسم المريض', required: true },
          { name: 'type', label: 'نوع العينة', options: ['دم', 'بول', 'مسحة', 'براز'] },
          { name: 'tube', label: 'الأنبوب', options: ['أنبوب بنفسجي', 'أنبوب ذهبي', 'أنبوب أخضر', 'عبوة بول'] },
        ],
        defaults: { code: 'S-88425', collected: 'الآن', status: 'تم الاستلام' },
      }}
    />
  )
}

export function LabQuality() {
  return (
    <DashboardTemplate
      stats={[
        { icon: 'check', label: 'نسبة القبول الأسبوعية', value: '97%', delta: '+1%', tone: 'green' },
        { icon: 'flask', label: 'تشغيلات الجودة اليوم', value: '24' },
        { icon: 'alert', label: 'تحذيرات', value: '1', tone: 'orange' },
        { icon: 'close', label: 'مرفوضة', value: '1', tone: 'red' },
      ]}
      table={{
        title: 'تشغيلات ضبط الجودة',
        columns: [
          { key: 'device', header: 'الجهاز' },
          { key: 'test', header: 'الفحص' },
          { key: 'level', header: 'المستوى' },
          { key: 'result', header: 'الانحراف' },
          { key: 'status', header: 'النتيجة', type: 'badge', tones: QC_TONES },
        ],
        rows: qcRuns,
      }}
      charts={[{ title: 'نسبة قبول الجودة خلال الأسبوع (%)', data: qcWeekly }]}
    />
  )
}

export function LabDevices() {
  const [list, setList] = useState(initialDevices)
  const requestFix = (id) => setList((l) => l.map((d) => (d.id === id ? { ...d, requested: true } : d)))
  return (
    <div className="devices">
      {list.map((d) => (
        <Card key={d.id} className="device">
          <header>
            <span className="device__icon"><Icon name="settings" size={22} /></span>
            <div><b>{d.name}</b><small>{d.model}</small></div>
            <Badge tone={DEVICE_TONES[d.status]} dot>{d.status}</Badge>
          </header>
          <dl className="detail__list">
            <div><dt>الحمل الحالي</dt><dd><Progress value={d.load} /></dd></div>
            <div><dt>آخر معايرة</dt><dd>{d.calibrated}</dd></div>
          </dl>
          {d.status !== 'يعمل' && (
            <Button size="sm" variant={d.requested ? 'ghost' : 'danger'} disabled={d.requested} onClick={() => requestFix(d.id)}>
              {d.requested ? 'تم طلب الصيانة ✓' : 'طلب صيانة'}
            </Button>
          )}
        </Card>
      ))}
    </div>
  )
}
