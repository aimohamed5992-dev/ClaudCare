import { DashboardTemplate, ListPage, SettingsTemplate } from '../../components/patterns'
import { growth, hospitals, revenue, subscriptions, users } from '../../data/admin'

const HOSPITAL_TONES = { نشط: 'green', 'قيد المراجعة': 'orange', موقوف: 'red' }
const USER_TONES = { نشط: 'green', 'معلّق': 'orange', 'غير مفعّل': 'gray' }
const SUB_TONES = { مدفوع: 'green', 'قريب التجديد': 'orange', متأخر: 'red' }
const PLANS = ['أساسية', 'احترافية', 'مؤسسات']

export function AdminDashboard() {
  return (
    <DashboardTemplate
      banner={{ title: 'نظرة شاملة على المنصة', text: '7 مستشفيات مسجلة و 2 من طلبات الانضمام بانتظار المراجعة.', cta: 'مراجعة الطلبات', to: '/admin/hospitals', image: 'role-admin.png' }}
      stats={[
        { icon: 'building', label: 'المستشفيات', value: '12', delta: '+2' },
        { icon: 'users', label: 'المستخدمون', value: '4,820', delta: '+8%', tone: 'orange' },
        { icon: 'wallet', label: 'إيرادات الشهر', value: '318 ألف', delta: '+12%' },
        { icon: 'bell', label: 'تذاكر الدعم', value: '7', delta: '-3', tone: 'orange' },
      ]}
      table={{
        title: 'أحدث المستشفيات',
        columns: [
          { key: 'name', header: 'المستشفى', type: 'person', sub: 'city' },
          { key: 'branches', header: 'الفروع' },
          { key: 'plan', header: 'الباقة' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: HOSPITAL_TONES },
        ],
        rows: hospitals.slice(0, 5),
      }}
      side={{
        title: 'نشاط النظام',
        items: [
          { mark: 'جديد', title: 'تسجيل مستشفى جديد', subtitle: 'قبل 10 دقائق', tone: 'blue' },
          { mark: 'ترقية', title: 'ترقية باقة مستشفى الأمل', subtitle: 'قبل ساعة', tone: 'orange' },
          { mark: 'تنبيه', title: 'تأخر دفع مستشفى السلام', subtitle: 'اليوم 08:00', tone: 'red' },
          { mark: 'تم', title: 'نسخة احتياطية ناجحة', subtitle: 'اليوم 03:00', tone: 'green' },
        ],
      }}
      charts={[{ title: 'نمو المستخدمين (بالمئات)', data: growth }]}
    />
  )
}

export function AdminHospitals() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'active', label: 'نشط', match: (r) => r.status === 'نشط' },
        { key: 'review', label: 'قيد المراجعة', match: (r) => r.status === 'قيد المراجعة' },
        { key: 'stopped', label: 'موقوف', match: (r) => r.status === 'موقوف' },
      ]}
      columns={[
        { key: 'name', header: 'المستشفى', type: 'person', sub: 'city' },
        { key: 'manager', header: 'المدير' },
        { key: 'branches', header: 'الفروع' },
        { key: 'plan', header: 'الباقة' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: HOSPITAL_TONES },
      ]}
      rows={hospitals}
      searchKeys={['name', 'city', 'manager']}
      addLabel="إضافة مستشفى"
      form={{
        title: 'إضافة مستشفى جديد',
        fields: [
          { name: 'name', label: 'اسم المستشفى', required: true },
          { name: 'city', label: 'المدينة', required: true },
          { name: 'manager', label: 'اسم المدير', required: true },
          { name: 'plan', label: 'الباقة', options: PLANS },
        ],
        defaults: { branches: 1, status: 'قيد المراجعة', since: '2026-10-18' },
      }}
      detail={{
        titleKey: 'name',
        subKey: 'city',
        badgeKey: 'status',
        tones: HOSPITAL_TONES,
        fields: [
          { label: 'المدير', key: 'manager' },
          { label: 'عدد الفروع', key: 'branches' },
          { label: 'الباقة', key: 'plan' },
          { label: 'تاريخ الانضمام', key: 'since' },
        ],
        actions: [{ label: 'فتح لوحة المستشفى', variant: 'blue', icon: 'building' }, { label: 'تعليق الحساب', variant: 'danger' }],
      }}
    />
  )
}

export function AdminUsers() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'active', label: 'نشط', match: (r) => r.status === 'نشط' },
        { key: 'other', label: 'معلّق / غير مفعّل', match: (r) => r.status !== 'نشط' },
      ]}
      columns={[
        { key: 'name', header: 'المستخدم', type: 'person', avatar: 'avatar', sub: 'email' },
        { key: 'role', header: 'الدور' },
        { key: 'hospital', header: 'المستشفى' },
        { key: 'last', header: 'آخر دخول' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: USER_TONES },
      ]}
      rows={users}
      searchKeys={['name', 'email', 'hospital', 'role']}
      addLabel="إضافة مستخدم"
      form={{
        title: 'إضافة مستخدم',
        fields: [
          { name: 'name', label: 'الاسم الكامل', required: true },
          { name: 'email', label: 'البريد الإلكتروني', type: 'email', required: true },
          { name: 'role', label: 'الدور', options: ['طبيب', 'ممرضة', 'فني مختبر', 'أخصائي أشعة', 'استقبال', 'مدير فرع'] },
          { name: 'hospital', label: 'المستشفى', options: hospitals.map((h) => h.name) },
        ],
        defaults: { last: 'لم يدخل بعد', status: 'غير مفعّل' },
      }}
      detail={{
        titleKey: 'name',
        subKey: 'email',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: USER_TONES,
        fields: [
          { label: 'الدور', key: 'role' },
          { label: 'المستشفى', key: 'hospital' },
          { label: 'آخر دخول', key: 'last' },
        ],
        actions: [{ label: 'إعادة تعيين كلمة المرور', icon: 'lock' }, { label: 'تعليق الحساب', variant: 'danger' }],
      }}
    />
  )
}

export function AdminSubscriptions() {
  return (
    <ListPage
      stats={[
        { icon: 'wallet', label: 'إيراد الاشتراكات السنوي', value: '190 ألف', delta: '+9%' },
        { icon: 'check', label: 'اشتراكات مدفوعة', value: '3', tone: 'green' },
        { icon: 'clock', label: 'قريبة التجديد', value: '2', tone: 'orange' },
        { icon: 'alert', label: 'متأخرة', value: '1', tone: 'red' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'paid', label: 'مدفوع', match: (r) => r.status === 'مدفوع' },
        { key: 'soon', label: 'قريب التجديد', match: (r) => r.status === 'قريب التجديد' },
        { key: 'late', label: 'متأخر', match: (r) => r.status === 'متأخر' },
      ]}
      columns={[
        { key: 'hospital', header: 'المستشفى', type: 'person' },
        { key: 'plan', header: 'الباقة' },
        { key: 'renewal', header: 'تاريخ التجديد' },
        { key: 'amount', header: 'المبلغ' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: SUB_TONES },
      ]}
      rows={subscriptions}
      searchKeys={['hospital', 'plan']}
    />
  )
}

export function AdminReports() {
  return (
    <DashboardTemplate
      stats={[
        { icon: 'users', label: 'إجمالي المستخدمين', value: '4,820', delta: '+8%' },
        { icon: 'calendar', label: 'مواعيد هذا الشهر', value: '36,400', delta: '+11%', tone: 'orange' },
        { icon: 'wallet', label: 'الإيراد الشهري', value: '318 ألف', delta: '+12%' },
        { icon: 'building', label: 'مستشفيات نشطة', value: '10', tone: 'green' },
      ]}
      charts={[
        { title: 'نمو المستخدمين (بالمئات)', data: growth },
        { title: 'الإيراد الشهري (آلاف الريالات)', data: revenue },
      ]}
    />
  )
}

export function AdminSystem() {
  return (
    <SettingsTemplate
      sections={[
        {
          title: 'إعدادات عامة',
          desc: 'تنطبق على جميع المستشفيات في المنصة.',
          items: [
            { type: 'input', name: 'platform', label: 'اسم المنصة', value: 'ClaudCare' },
            { type: 'input', name: 'support', label: 'بريد الدعم', value: 'support@claudcare.com' },
            { type: 'select', name: 'lang', label: 'اللغة الافتراضية', options: ['العربية', 'English'], value: 'العربية' },
            { type: 'select', name: 'tz', label: 'المنطقة الزمنية', options: ['الرياض (GMT+3)', 'القاهرة (GMT+2)', 'دبي (GMT+4)'], value: 'الرياض (GMT+3)' },
          ],
        },
        {
          title: 'الأمان',
          items: [
            { type: 'switch', name: 'twofa', label: 'المصادقة الثنائية', hint: 'إلزام جميع المستخدمين بتفعيلها', value: true },
            { type: 'switch', name: 'session', label: 'انتهاء الجلسة تلقائيًا', hint: 'بعد 30 دقيقة من الخمول', value: true },
            { type: 'switch', name: 'audit', label: 'سجل التدقيق', hint: 'تسجيل كل العمليات الحساسة', value: true },
          ],
        },
        {
          title: 'الإشعارات',
          items: [
            { type: 'switch', name: 'mail', label: 'إشعارات البريد الإلكتروني', value: true },
            { type: 'switch', name: 'sms', label: 'رسائل SMS للمواعيد', value: false },
          ],
        },
      ]}
    />
  )
}
