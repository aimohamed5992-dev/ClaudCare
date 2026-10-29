import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, Checkbox, EmptyState, Icon, Select } from '../../components/ui'
import { DEPTS, REJECT_REASONS, attendanceWeekly, employees, headcountByDept, leaveRequests, licenseStatus, licenses, shifts } from '../../data/hr'
import './hr.css'

const EMP_TONES = { 'على رأس العمل': 'green', إجازة: 'orange', 'تحت التجربة': 'blue' }
const SHIFT_TONES = { مؤكدة: 'green', 'بانتظار التبديل': 'orange' }
const LIC_TONES = { ساري: 'green', 'ينتهي قريبًا': 'orange', منتهي: 'red' }
const licRows = licenses.map((l) => ({ ...l, status: licenseStatus(l.days), left: l.days < 0 ? `منتهٍ منذ ${-l.days} أيام` : `${l.days} يومًا` }))

export function HrDashboard() {
  const navigate = useNavigate()
  return (
    <DashboardTemplate
      banner={{ title: 'الموارد البشرية', text: 'ترخيص منتهٍ، و4 طلبات إجازة بانتظار القرار.', cta: 'مراجعة الإجازات', to: '/hr/leaves', image: 'role-hr.png' }}
      stats={[
        { icon: 'users', label: 'إجمالي الموظفين', value: '312', delta: '+4' },
        { icon: 'calendar', label: 'طلبات إجازة', value: '4', tone: 'orange' },
        { icon: 'alert', label: 'تراخيص تحتاج تجديدًا', value: '2', tone: 'red' },
        { icon: 'check', label: 'نسبة الحضور اليوم', value: '96%', tone: 'green' },
      ]}
      table={{
        title: 'تراخيص المزاولة',
        columns: [
          { key: 'name', header: 'الموظف', type: 'person', avatar: 'avatar', sub: 'role' },
          { key: 'expiry', header: 'الانتهاء' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: LIC_TONES },
        ],
        rows: licRows.filter((l) => l.status !== 'ساري'),
      }}
      side={{
        title: 'يحتاج انتباهك',
        items: [
          { mark: '!', title: 'ترخيص د. عمر الشامي منتهٍ', subtitle: 'منذ 8 أيام', tone: 'red', onClick: () => navigate('/hr/licenses') },
          { mark: '4', title: 'طلبات إجازة معلقة', subtitle: 'أقدمها اليوم', tone: 'orange', onClick: () => navigate('/hr/leaves') },
          { mark: '1', title: 'تبديل مناوبة', subtitle: 'تمريض – الليلية', tone: 'orange', onClick: () => navigate('/hr/shifts') },
        ],
      }}
      charts={[
        { title: 'الموظفون حسب القسم', data: headcountByDept },
        { title: 'الحضور الأسبوعي (%)', data: attendanceWeekly },
      ]}
    />
  )
}

export function HrEmployees() {
  return (
    <ListPage
      stats={[
        { icon: 'users', label: 'الموظفون', value: '312' },
        { icon: 'check', label: 'على رأس العمل', value: '287', tone: 'green' },
        { icon: 'calendar', label: 'في إجازة', value: '19', tone: 'orange' },
        { icon: 'user', label: 'تحت التجربة', value: '6', tone: 'blue' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'on', label: 'على رأس العمل', match: (r) => r.status === 'على رأس العمل' },
        { key: 'leave', label: 'إجازة', match: (r) => r.status === 'إجازة' },
        { key: 'trial', label: 'تحت التجربة', match: (r) => r.status === 'تحت التجربة' },
      ]}
      columns={[
        { key: 'name', header: 'الموظف', type: 'person', avatar: 'avatar', sub: 'role' },
        { key: 'dept', header: 'القسم' },
        { key: 'hired', header: 'تاريخ التعيين' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: EMP_TONES },
      ]}
      rows={employees}
      searchKeys={['name', 'role', 'dept']}
      addLabel="إضافة موظف"
      form={{
        title: 'إضافة موظف',
        fields: [
          { name: 'name', label: 'الاسم الكامل', required: true },
          { name: 'role', label: 'المسمى الوظيفي', required: true },
          { name: 'dept', label: 'القسم', options: DEPTS },
          { name: 'phone', label: 'رقم الجوال', type: 'tel', required: true },
          { name: 'hired', label: 'تاريخ التعيين', type: 'date', required: true },
        ],
        defaults: { status: 'تحت التجربة' },
      }}
      detail={{
        titleKey: 'name',
        subKey: 'role',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: EMP_TONES,
        fields: [
          { label: 'القسم', key: 'dept' },
          { label: 'تاريخ التعيين', key: 'hired' },
          { label: 'الجوال', key: 'phone' },
        ],
        actions: [{ label: 'تعديل البيانات', icon: 'edit', variant: 'blue' }, { label: 'ملف الموظف', icon: 'file', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- طلبات الإجازة ---------- */
function LeaveCard({ req, decision, onDecide }) {
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState(REJECT_REASONS[0])
  const [exception, setException] = useState(false)
  const [error, setError] = useState('')

  const overBalance = req.days > req.balance
  const coverage = req.onLeave + 1 > req.maxOff
  const blocked = overBalance || (coverage && !exception)

  if (decision) {
    return (
      <Card className="lv__card">
        <div className="lv__row">
          <Avatar src={req.avatar} name={req.name} size={44} />
          <div className="lv__who"><b>{req.name}</b><small>{req.dept} · {req.type} · {req.days} أيام</small></div>
          <Badge tone={decision.kind === 'approved' ? 'green' : 'red'} dot>{decision.kind === 'approved' ? 'تمت الموافقة' : `مرفوض: ${decision.reason}`}</Badge>
          <Button size="sm" variant="ghost" onClick={() => onDecide(null)}>تراجع</Button>
        </div>
      </Card>
    )
  }

  const reject = () => {
    if (reason === REJECT_REASONS[0]) return setError('اختر سبب الرفض')
    onDecide({ kind: 'rejected', reason })
  }

  return (
    <Card className="lv__card">
      <div className="lv__row">
        <Avatar src={req.avatar} name={req.name} size={44} />
        <div className="lv__who"><b>{req.name}</b><small>{req.dept} · إجازة {req.type}</small></div>
        <dl className="lv__facts">
          <div><dt>الفترة</dt><dd>{req.from} ← {req.to}</dd></div>
          <div><dt>الأيام</dt><dd>{req.days}</dd></div>
          <div><dt>الرصيد</dt><dd>{req.balance}</dd></div>
        </dl>
      </div>
      {overBalance && <div className="lv__warn is-bad" role="alert"><Icon name="alert" size={18} />الأيام المطلوبة ({req.days}) تتجاوز الرصيد المتاح ({req.balance}).</div>}
      {coverage && <div className="lv__warn" role="alert"><Icon name="alert" size={18} />يوجد {req.onLeave} زميل في إجازة بنفس الفترة، والحد المسموح {req.maxOff}. الموافقة تُضعف تغطية القسم.</div>}
      {coverage && !overBalance && <Checkbox label="موافقة استثنائية: تم ترتيب بديل لتغطية القسم" checked={exception} onChange={(e) => setException(e.target.checked)} />}
      {rejecting ? (
        <div className="lv__reject">
          <Select label="سبب الرفض" options={REJECT_REASONS} value={reason} onChange={(e) => { setReason(e.target.value); setError('') }} />
          <Button variant="danger" onClick={reject}>تأكيد الرفض</Button>
          <Button variant="ghost" onClick={() => { setRejecting(false); setError('') }}>إلغاء</Button>
        </div>
      ) : (
        <div className="lv__actions">
          <Button icon="check" variant="blue" disabled={blocked} onClick={() => onDecide({ kind: 'approved' })}>موافقة</Button>
          <Button variant="danger" onClick={() => setRejecting(true)}>رفض</Button>
        </div>
      )}
      {error && <p className="lv__error" role="alert">{error}</p>}
    </Card>
  )
}

export function HrLeaves() {
  const [decisions, setDecisions] = useState({})
  const pending = leaveRequests.filter((r) => !decisions[r.id]).length
  return (
    <div className="lv">
      <Badge tone={pending ? 'orange' : 'green'} dot>{pending ? `${pending} طلبات بانتظار القرار` : 'لا توجد طلبات معلقة'}</Badge>
      {pending === 0 && <EmptyState title="تمت معالجة كل الطلبات" text="يمكنك التراجع عن أي قرار أدناه." />}
      {leaveRequests.map((r) => (
        <LeaveCard key={r.id} req={r} decision={decisions[r.id]} onDecide={(d) => setDecisions((s) => ({ ...s, [r.id]: d }))} />
      ))}
    </div>
  )
}

export function HrShifts() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'am', label: 'صباحية', match: (r) => r.shift === 'صباحية' },
        { key: 'pm', label: 'مسائية', match: (r) => r.shift === 'مسائية' },
        { key: 'night', label: 'ليلية', match: (r) => r.shift === 'ليلية' },
      ]}
      columns={[
        { key: 'name', header: 'الموظف', type: 'person', avatar: 'avatar', sub: 'dept' },
        { key: 'day', header: 'اليوم' },
        { key: 'shift', header: 'المناوبة' },
        { key: 'time', header: 'الوقت' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: SHIFT_TONES },
      ]}
      rows={shifts}
      searchKeys={['name', 'dept', 'shift']}
      addLabel="إضافة مناوبة"
      form={{
        title: 'إضافة مناوبة',
        fields: [
          { name: 'name', label: 'الموظف', required: true },
          { name: 'dept', label: 'القسم', options: DEPTS },
          { name: 'day', label: 'اليوم', options: ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس'] },
          { name: 'shift', label: 'المناوبة', options: ['صباحية', 'مسائية', 'ليلية'] },
        ],
        defaults: { time: '—', status: 'مؤكدة' },
      }}
      detail={{
        titleKey: 'name',
        subKey: 'dept',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: SHIFT_TONES,
        fields: [
          { label: 'اليوم', key: 'day' },
          { label: 'المناوبة', key: 'shift' },
          { label: 'الوقت', key: 'time' },
        ],
        actions: [{ label: 'اعتماد التبديل', icon: 'check', variant: 'blue' }, { label: 'تعديل المناوبة', icon: 'edit', variant: 'ghost' }],
      }}
    />
  )
}

export function HrLicenses() {
  return (
    <ListPage
      stats={[
        { icon: 'shield', label: 'تراخيص مسجلة', value: '4' },
        { icon: 'alert', label: 'منتهية', value: '1', tone: 'red' },
        { icon: 'clock', label: 'تنتهي خلال 60 يومًا', value: '1', tone: 'orange' },
        { icon: 'check', label: 'سارية', value: '2', tone: 'green' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'exp', label: 'منتهية', match: (r) => r.status === 'منتهي' },
        { key: 'soon', label: 'تنتهي قريبًا', match: (r) => r.status === 'ينتهي قريبًا' },
        { key: 'ok', label: 'سارية', match: (r) => r.status === 'ساري' },
      ]}
      columns={[
        { key: 'name', header: 'الموظف', type: 'person', avatar: 'avatar', sub: 'role' },
        { key: 'no', header: 'رقم الترخيص' },
        { key: 'expiry', header: 'الانتهاء' },
        { key: 'left', header: 'المتبقي' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: LIC_TONES },
      ]}
      rows={licRows}
      searchKeys={['name', 'no', 'role']}
      detail={{
        titleKey: 'name',
        subKey: 'no',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: LIC_TONES,
        fields: [
          { label: 'المهنة', key: 'role' },
          { label: 'تاريخ الانتهاء', key: 'expiry' },
          { label: 'المتبقي', key: 'left' },
        ],
        actions: [{ label: 'إرسال تذكير بالتجديد', icon: 'mail', variant: 'blue' }, { label: 'رفع الترخيص الجديد', icon: 'download', variant: 'ghost' }],
      }}
    />
  )
}
