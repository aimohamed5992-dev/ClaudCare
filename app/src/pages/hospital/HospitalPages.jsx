import { DashboardTemplate, ListPage } from '../../components/patterns'
import { beds, branches, departments, incomeByMonth, invoices, staff, weekly } from '../../data/hospital'

const BRANCH_TONES = { يعمل: 'green', 'ضغط عالٍ': 'red' }
const STAFF_TONES = { 'على رأس العمل': 'green', إجازة: 'orange' }
const INV_TONES = { مدفوعة: 'green', معلّقة: 'orange', متأخرة: 'red' }

export function HospitalDashboard() {
  return (
    <DashboardTemplate
      banner={{ title: 'مستشفى النور', text: 'نسبة إشغال الأسرّة اليوم 78% عبر جميع الفروع.', cta: 'عرض الفروع', to: '/hospital/branches', image: 'role-hospital.png' }}
      stats={[
        { icon: 'branch', label: 'الفروع', value: '4' },
        { icon: 'users', label: 'الكوادر', value: '236', delta: '+5', tone: 'orange' },
        { icon: 'bed', label: 'إشغال الأسرّة', value: '78%', delta: '+4%' },
        { icon: 'wallet', label: 'إيراد اليوم', value: '42 ألف', delta: '+9%', tone: 'orange' },
      ]}
      table={{
        title: 'أداء الفروع',
        columns: [
          { key: 'name', header: 'الفرع', type: 'person', avatar: 'avatar', sub: 'manager' },
          { key: 'patients', header: 'مرضى اليوم' },
          { key: 'occupancy', header: 'الإشغال', type: 'progress' },
        ],
        rows: branches,
      }}
      side={{
        title: 'تنبيهات',
        items: [
          { mark: '!', title: 'فرع الشرق قارب الامتلاء', subtitle: 'الآن', tone: 'red' },
          { mark: 'نقل', title: 'طلب نقل مريض بين الفروع', subtitle: 'قبل 20 د', tone: 'blue' },
          { mark: 'إجازة', title: '3 طلبات إجازة بانتظار الاعتماد', subtitle: 'قبل ساعة', tone: 'orange' },
        ],
      }}
      charts={[{ title: 'عدد المرضى خلال الأسبوع', data: weekly }]}
    />
  )
}

export function HospitalBranches() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'busy', label: 'ضغط عالٍ', match: (r) => r.status === 'ضغط عالٍ' },
      ]}
      columns={[
        { key: 'name', header: 'الفرع', type: 'person', avatar: 'avatar', sub: 'city' },
        { key: 'manager', header: 'المدير' },
        { key: 'patients', header: 'مرضى اليوم' },
        { key: 'occupancy', header: 'الإشغال', type: 'progress' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: BRANCH_TONES },
      ]}
      rows={branches}
      searchKeys={['name', 'manager', 'city']}
      addLabel="إضافة فرع"
      form={{
        title: 'إضافة فرع جديد',
        fields: [
          { name: 'name', label: 'اسم الفرع', required: true },
          { name: 'city', label: 'الموقع', required: true },
          { name: 'manager', label: 'مدير الفرع', required: true },
          { name: 'phone', label: 'رقم التواصل' },
        ],
        defaults: { patients: 0, occupancy: 0, status: 'يعمل' },
      }}
      detail={{
        titleKey: 'name',
        subKey: 'city',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: BRANCH_TONES,
        fields: [
          { label: 'المدير', key: 'manager' },
          { label: 'رقم التواصل', key: 'phone' },
          { label: 'مرضى اليوم', key: 'patients' },
        ],
        actions: [{ label: 'فتح لوحة الفرع', variant: 'blue', icon: 'branch' }],
      }}
    />
  )
}

export function HospitalDepartments() {
  return (
    <ListPage
      columns={[
        { key: 'name', header: 'القسم', type: 'person', avatar: 'avatar', sub: 'head' },
        { key: 'doctors', header: 'الأطباء' },
        { key: 'beds', header: 'الأسرّة' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: BRANCH_TONES },
      ]}
      rows={departments}
      searchKeys={['name', 'head']}
      addLabel="إضافة قسم"
      form={{
        title: 'إضافة قسم',
        fields: [
          { name: 'name', label: 'اسم القسم', required: true },
          { name: 'head', label: 'رئيس القسم', required: true },
        ],
        defaults: { doctors: 0, beds: 0, status: 'يعمل' },
      }}
    />
  )
}

export function HospitalStaff() {
  const types = ['أطباء', 'تمريض', 'فنيون', 'إداريون']
  return (
    <ListPage
      tabs={[{ key: 'all', label: 'الكل', match: () => true }, ...types.map((t) => ({ key: t, label: t, match: (r) => r.type === t }))]}
      columns={[
        { key: 'name', header: 'الاسم', type: 'person', avatar: 'avatar', sub: 'title' },
        { key: 'dept', header: 'القسم' },
        { key: 'branch', header: 'الفرع' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: STAFF_TONES },
      ]}
      rows={staff}
      searchKeys={['name', 'title', 'dept', 'branch']}
      addLabel="إضافة موظف"
      form={{
        title: 'إضافة موظف',
        fields: [
          { name: 'name', label: 'الاسم الكامل', required: true },
          { name: 'title', label: 'المسمى الوظيفي', required: true },
          { name: 'type', label: 'الفئة', options: types },
          { name: 'branch', label: 'الفرع', options: branches.map((b) => b.name) },
        ],
        defaults: { dept: 'غير محدد', status: 'على رأس العمل' },
      }}
      detail={{
        titleKey: 'name',
        subKey: 'title',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: STAFF_TONES,
        fields: [
          { label: 'الفئة', key: 'type' },
          { label: 'القسم', key: 'dept' },
          { label: 'الفرع', key: 'branch' },
        ],
        actions: [{ label: 'عرض الجدول', icon: 'calendar' }, { label: 'تعديل البيانات', icon: 'edit', variant: 'ghost' }],
      }}
    />
  )
}

export function HospitalBeds() {
  return (
    <ListPage
      stats={[
        { icon: 'bed', label: 'إجمالي الأسرّة', value: '174' },
        { icon: 'users', label: 'مشغولة', value: '139', tone: 'orange' },
        { icon: 'check', label: 'متاحة', value: '35', tone: 'green' },
        { icon: 'alert', label: 'أقسام ممتلئة تقريبًا', value: '2', tone: 'red' },
      ]}
      columns={[
        { key: 'branch', header: 'الفرع' },
        { key: 'dept', header: 'القسم' },
        { key: 'total', header: 'الإجمالي' },
        { key: 'used', header: 'مشغولة' },
        { key: 'free', header: 'متاحة' },
        { key: 'occupancy', header: 'الإشغال', type: 'progress' },
      ]}
      rows={beds}
      searchKeys={['branch', 'dept']}
    />
  )
}

export function HospitalFinance() {
  return (
    <ListPage
      stats={[
        { icon: 'wallet', label: 'الإيراد هذا الشهر', value: '1.2 مليون', delta: '+9%' },
        { icon: 'file', label: 'المصروفات', value: '740 ألف', tone: 'orange' },
        { icon: 'chart', label: 'صافي الربح', value: '460 ألف', delta: '+14%', tone: 'green' },
        { icon: 'clock', label: 'فواتير معلّقة', value: '38', tone: 'red' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'paid', label: 'مدفوعة', match: (r) => r.status === 'مدفوعة' },
        { key: 'pending', label: 'معلّقة', match: (r) => r.status === 'معلّقة' },
        { key: 'late', label: 'متأخرة', match: (r) => r.status === 'متأخرة' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الفاتورة' },
        { key: 'patient', header: 'المريض' },
        { key: 'item', header: 'البند' },
        { key: 'amount', header: 'المبلغ' },
        { key: 'date', header: 'التاريخ' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: INV_TONES },
      ]}
      rows={invoices}
      searchKeys={['no', 'patient', 'item']}
    />
  )
}

export function HospitalReports() {
  return (
    <DashboardTemplate
      stats={[
        { icon: 'users', label: 'مرضى الشهر', value: '3,720', delta: '+9%' },
        { icon: 'bed', label: 'متوسط الإشغال', value: '78%', tone: 'orange' },
        { icon: 'wallet', label: 'الإيراد', value: '1.2 مليون', delta: '+9%' },
        { icon: 'star', label: 'رضا المرضى', value: '4.6', delta: '+0.2', tone: 'green' },
      ]}
      charts={[
        { title: 'عدد المرضى خلال الأسبوع', data: weekly },
        { title: 'الإيراد الشهري (مئات الآلاف)', data: incomeByMonth },
      ]}
    />
  )
}
