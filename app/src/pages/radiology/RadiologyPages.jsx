import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, Icon, Select, Textarea } from '../../components/ui'
import { HOURS, examsByType, radHourly, radMonthly, radReports, radRequests, scheduleDevices, series } from '../../data/radiology'
import { asset } from '../../utils/assets'
import { cx } from '../../utils/cx'
import './radiology.css'
import '../reception/reception.css'

const REQ_TONES = { 'بانتظار التصوير': 'gray', 'قيد التصوير': 'blue', 'بانتظار التقرير': 'orange', مكتمل: 'green' }
const PRIORITY_TONES = { عاجل: 'red', عادي: 'gray' }
const REPORT_TONES = { مسودة: 'orange', معتمد: 'green' }

export function RadiologyDashboard() {
  const navigate = useNavigate()
  return (
    <DashboardTemplate
      banner={{ title: 'قسم الأشعة', text: 'يوجد فحص واحد بانتظار كتابة التقرير وفحصان عاجلان للتصوير.', cta: 'كتابة تقرير', to: '/radiology/viewer', image: 'role-radiology.png' }}
      stats={[
        { icon: 'scan', label: 'فحوصات اليوم', value: '64', delta: '+7%' },
        { icon: 'file', label: 'تقارير معلقة', value: '21', tone: 'orange' },
        { icon: 'pulse', label: 'رنين مغناطيسي', value: '12' },
        { icon: 'alert', label: 'عاجل', value: '2', tone: 'red' },
      ]}
      table={{
        title: 'قائمة الفحوصات',
        columns: [
          { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'order' },
          { key: 'study', header: 'الفحص' },
          { key: 'modality', header: 'الجهاز' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: REQ_TONES },
        ],
        rows: radRequests,
      }}
      side={{
        title: 'الأجهزة',
        items: [
          { mark: 'MRI', title: 'رنين مغناطيسي', subtitle: 'قيد الاستخدام', tone: 'orange', onClick: () => navigate('/radiology/schedule') },
          { mark: 'CT', title: 'مقطعية', subtitle: 'متاح', tone: 'green', onClick: () => navigate('/radiology/schedule') },
          { mark: 'XR', title: 'X-Ray 1', subtitle: 'متاح', tone: 'green', onClick: () => navigate('/radiology/schedule') },
        ],
      }}
      charts={[{ title: 'الفحوصات حسب الساعة', data: radHourly }]}
    />
  )
}

export function RadiologyRequests() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'urgent', label: 'عاجل', match: (r) => r.priority === 'عاجل' },
        { key: 'todo', label: 'بانتظار التصوير', match: (r) => r.status === 'بانتظار التصوير' },
        { key: 'report', label: 'بانتظار التقرير', match: (r) => r.status === 'بانتظار التقرير' },
        { key: 'done', label: 'مكتمل', match: (r) => r.status === 'مكتمل' },
      ]}
      columns={[
        { key: 'order', header: 'رقم الطلب' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'doctor' },
        { key: 'study', header: 'الفحص' },
        { key: 'priority', header: 'الأولوية', type: 'badge', tones: PRIORITY_TONES },
        { key: 'status', header: 'الحالة', type: 'badge', tones: REQ_TONES },
      ]}
      rows={radRequests}
      searchKeys={['order', 'patient', 'study']}
      detail={{
        titleKey: 'patient',
        subKey: 'order',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: REQ_TONES,
        fields: [
          { label: 'الفحص', key: 'study' },
          { label: 'الجهاز', key: 'modality' },
          { label: 'الطبيب المحيل', key: 'doctor' },
          { label: 'وقت الطلب', key: 'time' },
        ],
        actions: [{ label: 'فتح في العارض', icon: 'scan', variant: 'blue', to: '/radiology/viewer' }, { label: 'جدولة الفحص', icon: 'calendar', variant: 'ghost', to: '/radiology/schedule' }],
      }}
    />
  )
}

/* ---------- عارض الصور وكتابة التقرير ---------- */
const DEFAULTS = { zoom: 1, brightness: 100, contrast: 100, rotate: 0, invert: false }
const TEMPLATES = {
  'تقرير فارغ': '',
  'صدر – طبيعي': 'الرئتان سليمتان وخاليتان من الارتشاحات أو الكتل. حجم القلب ضمن الحدود الطبيعية. الزوايا الضلعية الحجابية واضحة.',
  'عظام – بدون كسور': 'العظام سليمة، لا توجد كسور أو خلع، والمسافات المفصلية محفوظة.',
}

export function RadiologyViewer() {
  const navigate = useNavigate()
  const [current, setCurrent] = useState(series[0].file)
  const [view, setView] = useState(DEFAULTS)
  const [template, setTemplate] = useState('تقرير فارغ')
  const [findings, setFindings] = useState('')
  const [error, setError] = useState('')
  const [done, setDone] = useState(false)

  const set = (patch) => setView((v) => ({ ...v, ...patch }))
  const clamp = (n, lo, hi) => Math.min(hi, Math.max(lo, n))

  const pickTemplate = (name) => {
    setTemplate(name)
    setFindings(TEMPLATES[name])
  }
  const approve = () => {
    if (!findings.trim()) return setError('اكتب نتائج التقرير قبل الاعتماد')
    setError('')
    setDone(true)
  }

  if (done) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>تم اعتماد التقرير</h2>
        <p>أشعة صدر · سارة أحمد · أُرسل إلى الطبيب المحيل د. فهد عمر</p>
        <div className="reg__actions">
          <Button variant="blue" onClick={() => navigate('/radiology/reports')}>عرض التقارير</Button>
          <Button variant="ghost" onClick={() => navigate('/radiology/requests')}>العودة للطلبات</Button>
        </div>
      </Card>
    )
  }

  const imgStyle = {
    transform: `scale(${view.zoom}) rotate(${view.rotate}deg)`,
    filter: `brightness(${view.brightness}%) contrast(${view.contrast}%) invert(${view.invert ? 1 : 0})`,
  }

  return (
    <div className="viewer">
      <section className="viewer__main">
        <div className="viewer__bar" role="toolbar" aria-label="أدوات العارض">
          <Button size="sm" variant="ghost" onClick={() => set({ zoom: clamp(+(view.zoom + 0.25).toFixed(2), 0.5, 3) })}>تكبير +</Button>
          <Button size="sm" variant="ghost" onClick={() => set({ zoom: clamp(+(view.zoom - 0.25).toFixed(2), 0.5, 3) })}>تصغير −</Button>
          <Button size="sm" variant="ghost" onClick={() => set({ rotate: (view.rotate + 90) % 360 })}>تدوير</Button>
          <Button size="sm" variant={view.invert ? 'blue' : 'ghost'} onClick={() => set({ invert: !view.invert })}>عكس الألوان</Button>
          <Button size="sm" variant="soft" onClick={() => setView(DEFAULTS)}>إعادة ضبط</Button>
        </div>

        <div className="viewer__stage">
          <img src={asset(`medical/${current}`)} alt="صورة الفحص" style={imgStyle} />
          <span className="viewer__tag">زوم {Math.round(view.zoom * 100)}%</span>
        </div>

        <div className="viewer__sliders">
          <label>السطوع <b>{view.brightness}%</b>
            <input type="range" min="40" max="200" value={view.brightness} onChange={(e) => set({ brightness: +e.target.value })} />
          </label>
          <label>التباين <b>{view.contrast}%</b>
            <input type="range" min="40" max="250" value={view.contrast} onChange={(e) => set({ contrast: +e.target.value })} />
          </label>
        </div>

        <div className="viewer__thumbs">
          {series.map((s) => (
            <button key={s.file} className={cx('viewer__thumb', s.file === current && 'is-on')} onClick={() => { setCurrent(s.file); setView(DEFAULTS) }} aria-label={s.label}>
              <img src={asset(`medical/${s.file}`)} alt="" />
              <small>{s.label}</small>
            </button>
          ))}
        </div>
      </section>

      <aside className="viewer__side">
        <Card>
          <div className="viewer__patient">
            <Avatar src="avatars/patient-2.jpg" name="سارة أحمد" size={48} />
            <div><b>سارة أحمد</b><small>RAD-1182 · 34 سنة</small></div>
            <Badge tone="orange" dot>بانتظار التقرير</Badge>
          </div>
          <dl className="detail__list">
            <div><dt>الفحص</dt><dd>أشعة صدر</dd></div>
            <div><dt>الطبيب المحيل</dt><dd>د. فهد عمر</dd></div>
            <div><dt>سبب الفحص</dt><dd>ألم في الصدر</dd></div>
          </dl>
        </Card>
        <Card title="التقرير">
          <div className="viewer__form">
            <Select label="قالب جاهز" options={Object.keys(TEMPLATES)} value={template} onChange={(e) => pickTemplate(e.target.value)} />
            <Textarea label="النتائج والانطباع" rows={7} value={findings} error={error} onChange={(e) => setFindings(e.target.value)} />
            <Button icon="check" onClick={approve}>اعتماد التقرير</Button>
            <Button variant="ghost" onClick={() => navigate('/radiology/requests')}>حفظ كمسودة</Button>
          </div>
        </Card>
      </aside>
    </div>
  )
}

export function RadiologyReports() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'draft', label: 'مسودات', match: (r) => r.status === 'مسودة' },
        { key: 'ok', label: 'معتمدة', match: (r) => r.status === 'معتمد' },
      ]}
      columns={[
        { key: 'study', header: 'الفحص' },
        { key: 'patient', header: 'المريض' },
        { key: 'date', header: 'التاريخ' },
        { key: 'radiologist', header: 'الأخصائي' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: REPORT_TONES },
      ]}
      rows={radReports}
      searchKeys={['study', 'patient']}
      detail={{
        titleKey: 'study',
        subKey: 'patient',
        badgeKey: 'status',
        tones: REPORT_TONES,
        fields: [
          { label: 'التاريخ', key: 'date' },
          { label: 'الأخصائي', key: 'radiologist' },
          { label: 'الانطباع', key: 'impression' },
        ],
        actions: [{ label: 'فتح في العارض', icon: 'scan', variant: 'blue', to: '/radiology/viewer' }, { label: 'طباعة التقرير', icon: 'print', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- جدول الأجهزة ---------- */
export function RadiologySchedule() {
  const [devices, setDevices] = useState(scheduleDevices)
  const book = (id, hour) =>
    setDevices((list) => list.map((d) => (d.id === id ? { ...d, booked: { ...d.booked, [hour]: 'حجز جديد' } } : d)))
  const cancel = (id, hour) =>
    setDevices((list) =>
      list.map((d) => {
        if (d.id !== id) return d
        const { [hour]: _removed, ...rest } = d.booked
        return { ...d, booked: rest }
      }),
    )

  return (
    <div className="sched">
      <div className="sched__legend">
        <Badge tone="blue" dot>محجوز</Badge>
        <Badge tone="gray" dot>متاح (اضغط للحجز)</Badge>
      </div>
      <Card padded={false}>
        <div className="table-wrap">
          <table className="table sched__table">
            <thead>
              <tr>
                <th>الجهاز</th>
                {HOURS.map((h) => <th key={h}>{h}</th>)}
              </tr>
            </thead>
            <tbody>
              {devices.map((d) => (
                <tr key={d.id}>
                  <td><b>{d.name}</b></td>
                  {HOURS.map((h) => (
                    <td key={h}>
                      {d.booked[h] ? (
                        <button className="sched__slot is-booked" onClick={() => cancel(d.id, h)} title="اضغط لإلغاء الحجز">{d.booked[h]}</button>
                      ) : (
                        <button className="sched__slot" onClick={() => book(d.id, h)}>+</button>
                      )}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  )
}

export function RadiologyStats() {
  return (
    <DashboardTemplate
      stats={[
        { icon: 'scan', label: 'فحوصات الشهر', value: '345', delta: '+8%' },
        { icon: 'clock', label: 'متوسط زمن التقرير', value: '3.2 س', delta: '-0.4', tone: 'green' },
        { icon: 'file', label: 'تقارير معتمدة', value: '324', tone: 'orange' },
        { icon: 'alert', label: 'فحوصات عاجلة', value: '27', tone: 'red' },
      ]}
      charts={[
        { title: 'الفحوصات حسب النوع', data: examsByType },
        { title: 'الفحوصات الشهرية', data: radMonthly },
      ]}
    />
  )
}
