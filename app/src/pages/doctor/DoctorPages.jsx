import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, LabReport, ListPage, Messenger, StudyGrid } from '../../components/patterns'
import { Avatar, Badge, Button, Card, Checkbox, DataTable, Icon, Input, Select, StatCard, Tabs, Textarea } from '../../components/ui'
import { LAB_TESTS, RAD_TESTS, conversations, doctorAppointments, doctorPatients, orders, prescriptions, weekVisits } from '../../data/doctor'
import { labReports, medications, records, studies } from '../../data/patient'
import './doctor.css'
import '../patient/patient.css'
import '../reception/reception.css'

const APPT_TONES = { بالانتظار: 'orange', حاضر: 'blue', مجدول: 'gray', مكتمل: 'green' }
const RX_TONES = { نشطة: 'green', مُصرفة: 'blue', منتهية: 'gray' }
const ORDER_TONES = { 'تم الطلب': 'gray', 'قيد التنفيذ': 'orange', 'النتيجة جاهزة': 'green' }

export function DoctorDashboard() {
  const navigate = useNavigate()
  return (
    <DashboardTemplate
      banner={{ title: 'صباح الخير د. منى', text: 'لديك 12 موعدًا اليوم، التالي بعد 15 دقيقة.', cta: 'بدء الكشف', to: '/doctor/consultation', image: 'role-doctor.png' }}
      stats={[
        { icon: 'calendar', label: 'مواعيد اليوم', value: '12' },
        { icon: 'users', label: 'مرضى جدد', value: '4', delta: '+1', tone: 'orange' },
        { icon: 'flask', label: 'نتائج جاهزة', value: '9', tone: 'green' },
        { icon: 'pill', label: 'وصفات هذا الأسبوع', value: '17', delta: '+3', tone: 'orange' },
      ]}
      table={{
        title: 'مواعيد اليوم',
        columns: [
          { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'reason' },
          { key: 'time', header: 'الوقت' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: APPT_TONES },
        ],
        rows: doctorAppointments.filter((a) => a.date === 'اليوم'),
      }}
      side={{
        title: 'نتائج بحاجة لمراجعة',
        items: [
          { mark: 'CBC', title: 'صورة دم – محمد علي', subtitle: 'قبل ساعة', tone: 'red', onClick: () => navigate('/doctor/orders') },
          { mark: 'XR', title: 'أشعة صدر – سارة أحمد', subtitle: 'قبل 3 ساعات', tone: 'orange', onClick: () => navigate('/doctor/orders') },
          { mark: 'LAB', title: 'سكر تراكمي – يوسف خالد', subtitle: 'أمس', tone: 'green', onClick: () => navigate('/doctor/orders') },
        ],
      }}
      charts={[{ title: 'عدد المرضى خلال الأسبوع', data: weekVisits }]}
    />
  )
}

export function DoctorAppointments() {
  return (
    <ListPage
      tabs={[
        { key: 'today', label: 'اليوم', match: (r) => r.date === 'اليوم' },
        { key: 'next', label: 'القادمة', match: (r) => r.date !== 'اليوم' },
        { key: 'done', label: 'المكتملة', match: (r) => r.status === 'مكتمل' },
      ]}
      columns={[
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'reason' },
        { key: 'age', header: 'العمر' },
        { key: 'time', header: 'الوقت' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: APPT_TONES },
      ]}
      rows={doctorAppointments}
      searchKeys={['patient', 'reason']}
      detail={{
        titleKey: 'patient',
        subKey: 'reason',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: APPT_TONES,
        fields: [
          { label: 'العمر', key: 'age' },
          { label: 'الوقت', key: 'time' },
          { label: 'اليوم', key: 'date' },
        ],
        actions: [{ label: 'بدء الكشف', icon: 'stethoscope', variant: 'blue', to: '/doctor/consultation' }, { label: 'فتح السجل الطبي', icon: 'file', variant: 'ghost', to: '/doctor/records' }],
      }}
    />
  )
}

export function DoctorPatients() {
  return (
    <ListPage
      columns={[
        { key: 'name', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'phone' },
        { key: 'age', header: 'العمر' },
        { key: 'condition', header: 'الحالة المزمنة' },
        { key: 'last', header: 'آخر زيارة' },
      ]}
      rows={doctorPatients}
      searchKeys={['name', 'condition', 'phone']}
      detail={{
        titleKey: 'name',
        subKey: 'condition',
        avatar: 'avatar',
        fields: [
          { label: 'العمر', key: 'age' },
          { label: 'الجنس', key: 'gender' },
          { label: 'فصيلة الدم', key: 'blood' },
          { label: 'آخر زيارة', key: 'last' },
        ],
        actions: [{ label: 'فتح السجل الطبي', icon: 'file', variant: 'blue', to: '/doctor/records' }, { label: 'مراسلة', icon: 'mail', variant: 'ghost', to: '/doctor/messages' }],
      }}
    />
  )
}

/* ---------- السجلات الطبية ---------- */
export function DoctorRecords() {
  const [patientId, setPatientId] = useState(String(doctorPatients[0].id))
  const [tab, setTab] = useState('summary')
  const p = doctorPatients.find((x) => String(x.id) === patientId)

  return (
    <div className="rec">
      <Card className="rec__profile">
        <Avatar src={p.avatar} name={p.name} size={72} />
        <div className="rec__id">
          <h2>{p.name}</h2>
          <small>{p.age} سنة · {p.gender} · {p.condition}</small>
        </div>
        <Select
          className="doc__picker"
          aria-label="اختيار المريض"
          options={doctorPatients.map((x) => ({ value: String(x.id), label: x.name }))}
          value={patientId}
          onChange={(e) => setPatientId(e.target.value)}
        />
      </Card>

      <div className="dash__stats">
        <StatCard icon="heart" label="ضغط الدم" value="130/85" tone="orange" />
        <StatCard icon="pulse" label="النبض" value="74" />
        <StatCard icon="flask" label="سكر صائم" value="112" delta="+8" tone="red" />
        <StatCard icon="user" label="فصيلة الدم" value={p.blood} tone="green" />
      </div>

      <Tabs
        value={tab}
        onChange={setTab}
        items={[{ key: 'summary', label: 'الملخص' }, { key: 'labs', label: 'التحاليل' }, { key: 'imaging', label: 'الأشعة' }, { key: 'meds', label: 'الأدوية' }]}
      />

      {tab === 'summary' && (
        <div className="rec__two">
          <Card title="التشخيصات" padded={false}>
            <DataTable
              columns={[
                { key: 'name', header: 'التشخيص' },
                { key: 'since', header: 'منذ' },
                { key: 'status', header: 'الحالة', render: (r) => <Badge tone={r.status === 'مستقر' ? 'green' : 'orange'} dot>{r.status}</Badge> },
              ]}
              rows={records.conditions}
            />
          </Card>
          <Card title="الحساسية">
            <div className="rec__tags">{records.allergies.map((a) => <Badge key={a} tone="red" dot>{a}</Badge>)}</div>
          </Card>
        </div>
      )}
      {tab === 'labs' && <LabReport report={labReports[0]} showActions={false} />}
      {tab === 'imaging' && <StudyGrid studies={studies.slice(0, 3)} />}
      {tab === 'meds' && (
        <Card padded={false}>
          <DataTable
            columns={[
              { key: 'name', header: 'الدواء' },
              { key: 'dose', header: 'الجرعة' },
              { key: 'schedule', header: 'المواعيد' },
              { key: 'purpose', header: 'الغرض' },
            ]}
            rows={medications}
          />
        </Card>
      )}
    </div>
  )
}

export function DoctorPrescriptions() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'active', label: 'نشطة', match: (r) => r.status === 'نشطة' },
        { key: 'done', label: 'مُصرفة / منتهية', match: (r) => r.status !== 'نشطة' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الوصفة' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar' },
        { key: 'meds', header: 'الأدوية' },
        { key: 'date', header: 'التاريخ' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: RX_TONES },
      ]}
      rows={prescriptions}
      searchKeys={['no', 'patient', 'meds']}
      addLabel="وصفة جديدة"
      form={{
        title: 'وصفة طبية جديدة',
        fields: [
          { name: 'patient', label: 'المريض', options: doctorPatients.map((p) => p.name) },
          { name: 'meds', label: 'الدواء والجرعة', placeholder: 'مثال: أموكسيسيلين 500 مجم', required: true },
          { name: 'duration', label: 'المدة', options: ['7 أيام', '14 يومًا', '30 يومًا', '60 يومًا'] },
        ],
        defaults: { no: 'RX-3302', date: 'اليوم', status: 'نشطة', avatar: 'avatars/patient-3.jpg' },
      }}
      detail={{
        titleKey: 'no',
        subKey: 'patient',
        badgeKey: 'status',
        tones: RX_TONES,
        fields: [
          { label: 'الأدوية', key: 'meds' },
          { label: 'المدة', key: 'duration' },
          { label: 'التاريخ', key: 'date' },
        ],
        actions: [{ label: 'طباعة الوصفة', icon: 'print', variant: 'blue' }, { label: 'تجديد', icon: 'plus', variant: 'ghost' }],
      }}
    />
  )
}

export function DoctorOrders() {
  return (
    <ListPage
      stats={[
        { icon: 'flask', label: 'طلبات مختبر', value: '3' },
        { icon: 'scan', label: 'طلبات أشعة', value: '2', tone: 'orange' },
        { icon: 'check', label: 'نتائج جاهزة', value: '2', tone: 'green' },
        { icon: 'alert', label: 'عاجلة', value: '2', tone: 'red' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'lab', label: 'مختبر', match: (r) => r.kind === 'مختبر' },
        { key: 'rad', label: 'أشعة', match: (r) => r.kind === 'أشعة' },
        { key: 'ready', label: 'النتيجة جاهزة', match: (r) => r.status === 'النتيجة جاهزة' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الطلب' },
        { key: 'test', header: 'الفحص' },
        { key: 'patient', header: 'المريض' },
        { key: 'priority', header: 'الأولوية', type: 'badge', tones: { عاجل: 'red', عادي: 'gray' } },
        { key: 'status', header: 'الحالة', type: 'badge', tones: ORDER_TONES },
      ]}
      rows={orders}
      searchKeys={['no', 'test', 'patient']}
      addLabel="طلب فحص"
      form={{
        title: 'طلب فحص جديد',
        fields: [
          { name: 'patient', label: 'المريض', options: doctorPatients.map((p) => p.name) },
          { name: 'test', label: 'الفحص', options: [...LAB_TESTS, ...RAD_TESTS] },
          { name: 'priority', label: 'الأولوية', options: ['عادي', 'عاجل'] },
        ],
        defaults: { no: 'LAB-2211', kind: 'مختبر', date: 'الآن', status: 'تم الطلب' },
      }}
      detail={{
        titleKey: 'test',
        subKey: 'no',
        badgeKey: 'status',
        tones: ORDER_TONES,
        fields: [
          { label: 'النوع', key: 'kind' },
          { label: 'المريض', key: 'patient' },
          { label: 'التاريخ', key: 'date' },
        ],
        actions: [{ label: 'عرض النتيجة', icon: 'eye', variant: 'blue' }],
      }}
    />
  )
}

export function DoctorMessages() {
  return <Messenger conversations={conversations} />
}

/* ---------- شاشة الكشف الطبي ---------- */
export function DoctorConsultation() {
  const navigate = useNavigate()
  const patient = doctorPatients[1]
  const [tab, setTab] = useState('notes')
  const [notes, setNotes] = useState({ complaint: '', exam: '', diagnosis: '' })
  const [rx, setRx] = useState([])
  const [draft, setDraft] = useState({ name: '', dose: '', days: '7 أيام' })
  const [ordered, setOrdered] = useState([])
  const [done, setDone] = useState(false)

  const addRx = () => {
    if (!draft.name.trim()) return
    setRx((l) => [...l, { ...draft, id: Date.now() }])
    setDraft({ name: '', dose: '', days: '7 أيام' })
  }
  const toggleTest = (t) => setOrdered((l) => (l.includes(t) ? l.filter((x) => x !== t) : [...l, t]))

  if (done) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>تم حفظ الكشف</h2>
        <p>{patient.name} · {rx.length} أدوية · {ordered.length} فحوصات مطلوبة</p>
        <div className="reg__actions">
          <Button variant="blue" onClick={() => navigate('/doctor/appointments')}>العودة للمواعيد</Button>
          <Button variant="ghost" onClick={() => navigate('/doctor/prescriptions')}>عرض الوصفات</Button>
        </div>
      </Card>
    )
  }

  return (
    <div className="consult">
      <Card className="consult__side">
        <div className="consult__who">
          <Avatar src={patient.avatar} name={patient.name} size={64} />
          <b>{patient.name}</b>
          <small>{patient.age} سنة · {patient.gender} · {patient.blood}</small>
        </div>
        <dl className="detail__list">
          <div><dt>الشكوى</dt><dd>ألم في الصدر</dd></div>
          <div><dt>الضغط</dt><dd>130/85</dd></div>
          <div><dt>النبض</dt><dd>74</dd></div>
          <div><dt>الحرارة</dt><dd>36.8</dd></div>
        </dl>
        <div className="consult__allergy">
          <small>الحساسية</small>
          <div className="rec__tags">{records.allergies.map((a) => <Badge key={a} tone="red" dot>{a}</Badge>)}</div>
        </div>
      </Card>

      <Card className="consult__main">
        <Tabs
          value={tab}
          onChange={setTab}
          items={[
            { key: 'notes', label: 'الكشف والتشخيص' },
            { key: 'rx', label: 'الوصفة', count: rx.length },
            { key: 'orders', label: 'طلب فحوصات', count: ordered.length },
          ]}
        />

        {tab === 'notes' && (
          <div className="consult__form">
            <Textarea label="الشكوى الحالية" value={notes.complaint} onChange={(e) => setNotes({ ...notes, complaint: e.target.value })} />
            <Textarea label="الفحص السريري" value={notes.exam} onChange={(e) => setNotes({ ...notes, exam: e.target.value })} />
            <Input label="التشخيص" value={notes.diagnosis} onChange={(e) => setNotes({ ...notes, diagnosis: e.target.value })} />
          </div>
        )}

        {tab === 'rx' && (
          <div className="consult__form">
            <div className="consult__rx">
              <Input label="الدواء" value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
              <Input label="الجرعة" value={draft.dose} onChange={(e) => setDraft({ ...draft, dose: e.target.value })} />
              <Select label="المدة" options={['7 أيام', '14 يومًا', '30 يومًا']} value={draft.days} onChange={(e) => setDraft({ ...draft, days: e.target.value })} />
              <Button variant="soft" icon="plus" onClick={addRx}>إضافة</Button>
            </div>
            {rx.length === 0 && <p className="consult__hint">لم تتم إضافة أدوية بعد.</p>}
            {rx.map((r) => (
              <div key={r.id} className="consult__item">
                <span><b>{r.name}</b><small>{r.dose} · {r.days}</small></span>
                <Button size="sm" variant="danger" icon="trash" aria-label="حذف" onClick={() => setRx((l) => l.filter((x) => x.id !== r.id))} />
              </div>
            ))}
          </div>
        )}

        {tab === 'orders' && (
          <div className="consult__form">
            <h4>تحاليل</h4>
            <div className="consult__checks">{LAB_TESTS.map((t) => <Checkbox key={t} label={t} checked={ordered.includes(t)} onChange={() => toggleTest(t)} />)}</div>
            <h4>أشعة</h4>
            <div className="consult__checks">{RAD_TESTS.map((t) => <Checkbox key={t} label={t} checked={ordered.includes(t)} onChange={() => toggleTest(t)} />)}</div>
          </div>
        )}

        <div className="consult__foot">
          <Button variant="ghost" onClick={() => navigate('/doctor/appointments')}>إلغاء</Button>
          <Button icon="check" onClick={() => setDone(true)}>إنهاء وحفظ الكشف</Button>
        </div>
      </Card>
    </div>
  )
}
