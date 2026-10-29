import { DashboardTemplate, ListPage } from '../../components/patterns'
import { appointments, branchStaff, diagnostics, hourly, monthlyVisits, rooms, todayDepartments } from '../../data/branch'

const WAIT_TONES = { جيد: 'green', متوسط: 'orange', مزدحم: 'red' }
const APPT_TONES = { بالانتظار: 'orange', حاضر: 'blue', مجدول: 'gray', مكتمل: 'green', ملغي: 'red' }
const STAFF_TONES = { 'على رأس العمل': 'green', 'خارج الدوام': 'gray' }
const ROOM_TONES = { مشغولة: 'red', متاحة: 'green', 'قيد التنظيف': 'orange' }
const DIAG_TONES = { 'قيد التحليل': 'orange', 'بانتظار التقرير': 'blue', جاهز: 'green' }
const DEPTS = ['الباطنة', 'القلب', 'الأطفال', 'العظام', 'الطوارئ']

export function BranchDashboard() {
  return (
    <DashboardTemplate
      banner={{ title: 'الفرع الرئيسي', text: 'لديك 312 زيارة اليوم و 14 حالة طارئة.', cta: 'عرض الجدول', to: '/branch/appointments', image: 'role-branch.png' }}
      stats={[
        { icon: 'calendar', label: 'مواعيد اليوم', value: '312', delta: '+6%' },
        { icon: 'users', label: 'الطاقم الحاضر', value: '58', tone: 'orange' },
        { icon: 'bed', label: 'غرف شاغرة', value: '23', delta: '-4', tone: 'green' },
        { icon: 'pulse', label: 'حالات طارئة', value: '14', delta: '+3', tone: 'red' },
      ]}
      table={{
        title: 'الأقسام اليوم',
        columns: [
          { key: 'name', header: 'القسم' },
          { key: 'on', header: 'المناوب' },
          { key: 'patients', header: 'المرضى' },
          { key: 'wait', header: 'الانتظار' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: WAIT_TONES },
        ],
        rows: todayDepartments,
      }}
      side={{
        title: 'المهام',
        items: [
          { mark: '1', title: 'اعتماد جدول المناوبات', subtitle: 'اليوم', tone: 'blue' },
          { mark: '2', title: 'طلب صيانة جهاز أشعة', subtitle: 'أمس', tone: 'orange' },
          { mark: '3', title: 'مراجعة شكوى مريض', subtitle: 'أمس', tone: 'red' },
        ],
      }}
      charts={[{ title: 'الزيارات حسب الساعة', data: hourly }]}
    />
  )
}

export function BranchAppointments() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'today', label: 'اليوم', match: (r) => r.date === 'اليوم' },
        { key: 'wait', label: 'بالانتظار', match: (r) => r.status === 'بالانتظار' },
        { key: 'done', label: 'مكتمل', match: (r) => r.status === 'مكتمل' },
      ]}
      columns={[
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'reason' },
        { key: 'doctor', header: 'الطبيب' },
        { key: 'dept', header: 'القسم' },
        { key: 'time', header: 'الوقت' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: APPT_TONES },
      ]}
      rows={appointments}
      searchKeys={['patient', 'doctor', 'dept']}
      addLabel="حجز موعد"
      form={{
        title: 'حجز موعد جديد',
        fields: [
          { name: 'patient', label: 'اسم المريض', required: true },
          { name: 'dept', label: 'القسم', options: DEPTS },
          { name: 'doctor', label: 'الطبيب', options: ['د. منى القاسم', 'د. فهد عمر', 'د. ريم العتيبي'] },
          { name: 'time', label: 'الوقت', type: 'time', required: true },
          { name: 'reason', label: 'سبب الزيارة' },
        ],
        defaults: { date: 'اليوم', status: 'مجدول' },
      }}
      detail={{
        titleKey: 'patient',
        subKey: 'reason',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: APPT_TONES,
        fields: [
          { label: 'الطبيب', key: 'doctor' },
          { label: 'القسم', key: 'dept' },
          { label: 'الموعد', key: 'time' },
          { label: 'اليوم', key: 'date' },
        ],
        actions: [{ label: 'تسجيل الحضور', icon: 'check', variant: 'blue' }, { label: 'إلغاء الموعد', variant: 'danger' }],
      }}
    />
  )
}

export function BranchStaff() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'on', label: 'على رأس العمل', match: (r) => r.status === 'على رأس العمل' },
        { key: 'off', label: 'خارج الدوام', match: (r) => r.status !== 'على رأس العمل' },
      ]}
      columns={[
        { key: 'name', header: 'الاسم', type: 'person', avatar: 'avatar', sub: 'title' },
        { key: 'dept', header: 'القسم' },
        { key: 'shift', header: 'الشفت' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: STAFF_TONES },
      ]}
      rows={branchStaff}
      searchKeys={['name', 'title', 'dept']}
      detail={{
        titleKey: 'name',
        subKey: 'title',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: STAFF_TONES,
        fields: [
          { label: 'القسم', key: 'dept' },
          { label: 'الشفت', key: 'shift' },
        ],
        actions: [{ label: 'عرض الجدول', icon: 'calendar' }],
      }}
    />
  )
}

export function BranchRooms() {
  return (
    <ListPage
      stats={[
        { icon: 'bed', label: 'إجمالي الغرف', value: '48' },
        { icon: 'users', label: 'مشغولة', value: '25', tone: 'red' },
        { icon: 'check', label: 'متاحة', value: '23', tone: 'green' },
        { icon: 'clock', label: 'قيد التنظيف', value: '4', tone: 'orange' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'free', label: 'متاحة', match: (r) => r.status === 'متاحة' },
        { key: 'busy', label: 'مشغولة', match: (r) => r.status === 'مشغولة' },
      ]}
      columns={[
        { key: 'room', header: 'الغرفة' },
        { key: 'type', header: 'النوع' },
        { key: 'floor', header: 'الطابق' },
        { key: 'patient', header: 'المريض' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: ROOM_TONES },
      ]}
      rows={rooms}
      searchKeys={['room', 'type', 'patient']}
    />
  )
}

export function BranchDiagnostics() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'lab', label: 'مختبر', match: (r) => r.kind === 'مختبر' },
        { key: 'rad', label: 'أشعة', match: (r) => r.kind === 'أشعة' },
      ]}
      columns={[
        { key: 'order', header: 'رقم الطلب' },
        { key: 'kind', header: 'النوع' },
        { key: 'test', header: 'الفحص' },
        { key: 'patient', header: 'المريض' },
        { key: 'date', header: 'التاريخ' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: DIAG_TONES },
      ]}
      rows={diagnostics}
      searchKeys={['order', 'test', 'patient']}
    />
  )
}

export function BranchReports() {
  return (
    <DashboardTemplate
      stats={[
        { icon: 'users', label: 'زيارات الشهر', value: '3,720', delta: '+9%' },
        { icon: 'clock', label: 'متوسط الانتظار', value: '22 د', delta: '-4 د', tone: 'green' },
        { icon: 'pulse', label: 'حالات طارئة', value: '212', tone: 'red' },
        { icon: 'star', label: 'رضا المرضى', value: '4.5', delta: '+0.1', tone: 'orange' },
      ]}
      charts={[
        { title: 'الزيارات حسب الساعة', data: hourly },
        { title: 'الزيارات الشهرية', data: monthlyVisits },
      ]}
    />
  )
}
