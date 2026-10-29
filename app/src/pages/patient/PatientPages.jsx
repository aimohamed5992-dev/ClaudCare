import { useState } from 'react'
import { DashboardTemplate, LabReport, ListPage, StudyGrid } from '../../components/patterns'
import { Avatar, Badge, Button, Card, DataTable, EmptyState, Icon, Progress, StatCard, Switch, Tabs } from '../../components/ui'
import { labReports, medications, myAppointments, patientInvoices, records, studies } from '../../data/patient'
import './patient.css'

const APPT_TONES = { قادم: 'blue', مكتمل: 'green', ملغي: 'red' }
const INVOICE_TONES = { مدفوعة: 'green', مستحقة: 'orange' }

export function PatientDashboard() {
  return (
    <DashboardTemplate
      banner={{ title: 'أهلًا ريم', text: 'موعدك القادم مع د. منى القاسم غدًا الساعة 10:30 صباحًا.', cta: 'عرض مواعيدي', to: '/patient/appointments', image: 'role-patient.png' }}
      stats={[
        { icon: 'heart', label: 'ضغط الدم', value: '120/80', tone: 'green' },
        { icon: 'pulse', label: 'النبض', value: '72', tone: 'blue' },
        { icon: 'flask', label: 'سكر صائم (mg/dL)', value: '98', tone: 'orange' },
        { icon: 'user', label: 'الوزن (كجم)', value: '64', delta: '-1', tone: 'blue' },
      ]}
      table={{
        title: 'زياراتي الأخيرة',
        columns: [
          { key: 'doctor', header: 'الطبيب', type: 'person', avatar: 'avatar', sub: 'dept' },
          { key: 'date', header: 'التاريخ' },
          { key: 'reason', header: 'السبب' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: APPT_TONES },
        ],
        rows: myAppointments.filter((a) => a.status !== 'قادم'),
      }}
      side={{
        title: 'أدوية اليوم',
        items: [
          { mark: '08:00', title: 'ميتفورمين 500 مجم', subtitle: 'بعد الفطور', tone: 'green' },
          { mark: '08:00', title: 'أوميبرازول 20 مجم', subtitle: 'قبل الفطور', tone: 'green' },
          { mark: '20:00', title: 'ميتفورمين 500 مجم', subtitle: 'بعد العشاء', tone: 'orange' },
        ],
      }}
    />
  )
}

export function PatientAppointments() {
  return (
    <ListPage
      tabs={[
        { key: 'up', label: 'القادمة', match: (r) => r.status === 'قادم' },
        { key: 'past', label: 'السابقة', match: (r) => r.status !== 'قادم' },
      ]}
      columns={[
        { key: 'doctor', header: 'الطبيب', type: 'person', avatar: 'avatar', sub: 'dept' },
        { key: 'date', header: 'التاريخ' },
        { key: 'time', header: 'الوقت' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: APPT_TONES },
      ]}
      rows={myAppointments}
      searchKeys={['doctor', 'dept', 'reason']}
      addLabel="حجز موعد جديد"
      form={{
        title: 'حجز موعد جديد',
        fields: [
          { name: 'dept', label: 'القسم', options: ['الباطنة', 'القلب', 'الأطفال', 'العظام', 'الجلدية'] },
          { name: 'doctor', label: 'الطبيب', options: ['د. منى القاسم', 'د. فهد عمر', 'د. عمر الشامي'] },
          { name: 'date', label: 'التاريخ', type: 'date', required: true },
          { name: 'time', label: 'الوقت', type: 'time', required: true },
          { name: 'reason', label: 'سبب الزيارة' },
        ],
        defaults: { status: 'قادم', place: 'الفرع الرئيسي', avatar: 'avatars/doc-f1.jpg' },
      }}
      detail={{
        titleKey: 'doctor',
        subKey: 'dept',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: APPT_TONES,
        fields: [
          { label: 'التاريخ', key: 'date' },
          { label: 'الوقت', key: 'time' },
          { label: 'المكان', key: 'place' },
          { label: 'السبب', key: 'reason' },
        ],
        actions: [{ label: 'تغيير الموعد', icon: 'calendar', variant: 'blue' }, { label: 'إلغاء الموعد', variant: 'danger' }],
      }}
    />
  )
}

/* ---------- السجل الصحي ---------- */
export function PatientRecords() {
  const [tab, setTab] = useState('overview')
  const p = records.profile
  return (
    <div className="rec">
      <Card className="rec__profile">
        <Avatar src={p.avatar} name={p.name} size={72} />
        <div className="rec__id">
          <h2>{p.name}</h2>
          <small>رقم الملف <b dir="ltr">{p.mrn}</b> · {p.age} · {p.insurance}</small>
        </div>
        <dl className="rec__vitals">
          <div><dt>فصيلة الدم</dt><dd>{p.blood}</dd></div>
          <div><dt>الطول</dt><dd>{p.height}</dd></div>
          <div><dt>الوزن</dt><dd>{p.weight}</dd></div>
        </dl>
      </Card>

      <Tabs
        value={tab}
        onChange={setTab}
        items={[{ key: 'overview', label: 'نظرة عامة' }, { key: 'conditions', label: 'التشخيصات' }, { key: 'history', label: 'سجل الزيارات' }]}
      />

      {tab === 'overview' && (
        <div className="rec__two">
          <Card title="الحساسية">
            <div className="rec__tags">
              {records.allergies.map((a) => <Badge key={a} tone="red" dot>{a}</Badge>)}
            </div>
          </Card>
          <Card title="التطعيمات">
            <div className="dash__list">
              {records.vaccines.map((v) => (
                <div key={v.name} className="rec__row"><span>{v.name}</span><small>{v.date}</small></div>
              ))}
            </div>
          </Card>
        </div>
      )}

      {tab === 'conditions' && (
        <Card padded={false}>
          <DataTable
            columns={[
              { key: 'name', header: 'التشخيص' },
              { key: 'since', header: 'منذ' },
              { key: 'doctor', header: 'الطبيب' },
              { key: 'status', header: 'الحالة', render: (r) => <Badge tone={r.status === 'مستقر' ? 'green' : 'orange'} dot>{r.status}</Badge> },
            ]}
            rows={records.conditions}
          />
        </Card>
      )}

      {tab === 'history' && (
        <Card>
          <ol className="timeline">
            {records.timeline.map((t) => (
              <li key={t.id}>
                <i />
                <div>
                  <small>{t.date}</small>
                  <b>{t.title}</b>
                  <p>{t.text}</p>
                </div>
              </li>
            ))}
          </ol>
        </Card>
      )}
    </div>
  )
}

/* ---------- نتائج التحاليل ---------- */
export function PatientLab() {
  const [selected, setSelected] = useState(labReports[0].id)
  const report = labReports.find((r) => r.id === selected)
  return (
    <div className="split">
      <Card title="تقاريري" className="split__list">
        <div className="dash__list">
          {labReports.map((r) => {
            const abnormal = r.rows.filter((x) => x.flag !== 'طبيعي').length
            return (
              <button key={r.id} className={selected === r.id ? 'pick is-on' : 'pick'} onClick={() => setSelected(r.id)}>
                <span className="pick__icon"><Icon name="flask" size={20} /></span>
                <span className="pick__text"><b>{r.title}</b><small>{r.date}</small></span>
                {abnormal > 0 && <Badge tone="orange">{abnormal}</Badge>}
              </button>
            )
          })}
        </div>
      </Card>
      <LabReport report={report} />
    </div>
  )
}

export function PatientRadiology() {
  return <StudyGrid studies={studies} />
}

/* ---------- الأدوية ---------- */
export function PatientMedications() {
  const [meds, setMeds] = useState(medications)
  const toggle = (id) => setMeds((m) => m.map((x) => (x.id === id ? { ...x, remind: !x.remind } : x)))

  if (meds.length === 0) return <EmptyState image="empty-results" title="لا توجد أدوية حالية" />
  return (
    <div className="meds">
      <div className="dash__stats">
        <StatCard icon="pill" label="أدوية حالية" value={meds.length} />
        <StatCard icon="clock" label="جرعات اليوم" value="5" tone="orange" />
        <StatCard icon="alert" label="تنتهي قريبًا" value={meds.filter((m) => m.left <= 7).length} tone="red" />
      </div>
      <div className="meds__grid">
        {meds.map((m) => (
          <Card key={m.id} className="med">
            <div className="med__head">
              <span className="med__icon"><Icon name="pill" size={22} /></span>
              <div>
                <b>{m.name}</b>
                <small>{m.purpose}</small>
              </div>
            </div>
            <dl className="detail__list">
              <div><dt>الجرعة</dt><dd>{m.dose}</dd></div>
              <div><dt>المواعيد</dt><dd>{m.schedule}</dd></div>
              <div><dt>المتبقي</dt><dd><Progress value={Math.min(100, m.left * 3)} tone={m.left <= 7 ? 'red' : 'blue'} label={false} /> {m.left} يوم</dd></div>
            </dl>
            <div className="med__foot">
              <Switch label="تذكير بالجرعات" checked={m.remind} onChange={() => toggle(m.id)} />
              <Button size="sm" variant="soft">طلب تجديد</Button>
            </div>
          </Card>
        ))}
      </div>
    </div>
  )
}

export function PatientInvoices() {
  return (
    <ListPage
      stats={[
        { icon: 'wallet', label: 'إجمالي الفواتير', value: '4,170 ر.س' },
        { icon: 'check', label: 'مدفوعة', value: '2,370 ر.س', tone: 'green' },
        { icon: 'alert', label: 'مستحقة', value: '1,800 ر.س', tone: 'orange' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'due', label: 'مستحقة', match: (r) => r.status === 'مستحقة' },
        { key: 'paid', label: 'مدفوعة', match: (r) => r.status === 'مدفوعة' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الفاتورة' },
        { key: 'item', header: 'البند' },
        { key: 'date', header: 'التاريخ' },
        { key: 'amount', header: 'المبلغ' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: INVOICE_TONES },
      ]}
      rows={patientInvoices}
      searchKeys={['no', 'item']}
      detail={{
        titleKey: 'no',
        subKey: 'item',
        badgeKey: 'status',
        tones: INVOICE_TONES,
        fields: [
          { label: 'التاريخ', key: 'date' },
          { label: 'المبلغ', key: 'amount' },
          { label: 'التأمين', key: 'insurance' },
        ],
        actions: [{ label: 'ادفع الآن', icon: 'wallet', variant: 'primary' }, { label: 'تحميل الفاتورة', icon: 'download', variant: 'ghost' }],
      }}
    />
  )
}
