import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, EmptyState, Icon, Input, Select, Textarea } from '../../components/ui'
import { bookings, dailyVisits, queue as initialQueue, receptionInvoices } from '../../data/reception'
import './reception.css'

const BOOKING_TONES = { مؤكد: 'blue', 'تم الحضور': 'green', 'بانتظار التأكيد': 'orange', ملغي: 'red' }
const INVOICE_TONES = { مدفوعة: 'green', 'غير مدفوعة': 'red', جزئية: 'orange' }
const DEPTS = ['الباطنة', 'القلب', 'الأطفال', 'العظام', 'الجلدية', 'الطوارئ']
const DOCTORS = ['د. منى القاسم', 'د. فهد عمر', 'د. ريم العتيبي', 'د. زياد النجار', 'د. عمر الشامي']

export function ReceptionDashboard() {
  const navigate = useNavigate()
  const waiting = initialQueue.filter((q) => q.status !== 'مكتمل')
  return (
    <DashboardTemplate
      banner={{ title: 'مرحبًا هبة', text: 'يوجد 3 مرضى في قائمة الانتظار الآن.', cta: 'تسجيل مريض جديد', to: '/reception/register', image: 'role-reception.png' }}
      stats={[
        { icon: 'users', label: 'زيارات اليوم', value: '184', delta: '+5%' },
        { icon: 'calendar', label: 'حجوزات قادمة', value: '63', tone: 'orange' },
        { icon: 'clock', label: 'بالانتظار', value: '27', tone: 'green' },
        { icon: 'wallet', label: 'تحصيل اليوم', value: '18 ألف', delta: '+8%', tone: 'orange' },
      ]}
      table={{
        title: 'قائمة الانتظار',
        columns: [
          { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'ticket' },
          { key: 'clinic', header: 'العيادة' },
          { key: 'arrived', header: 'الوصول' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: { بالانتظار: 'orange', 'داخل العيادة': 'blue' } },
        ],
        rows: waiting,
      }}
      side={{
        title: 'إجراءات سريعة',
        items: [
          { mark: '+', title: 'حجز موعد جديد', subtitle: 'اختيار الطبيب والوقت', tone: 'orange', onClick: () => navigate('/reception/bookings') },
          { mark: '✓', title: 'قائمة الانتظار', subtitle: 'استدعاء المرضى', tone: 'green', onClick: () => navigate('/reception/queue') },
          { mark: '$', title: 'الفواتير والتحصيل', subtitle: 'إصدار وتحصيل', tone: 'blue', onClick: () => navigate('/reception/invoices') },
        ],
      }}
      charts={[{ title: 'الزيارات حسب الساعة', data: dailyVisits }]}
    />
  )
}

/* ---------- تسجيل مريض جديد ---------- */
const EMPTY = {
  first: '', last: '', nid: '', birth: '', gender: 'ذكر', blood: 'غير معروف', phone: '', email: '', city: '',
  insurer: 'بدون تأمين', policy: '', emName: '', emPhone: '', notes: '',
}

const validate = (v) => {
  const e = {}
  if (!v.first.trim()) e.first = 'الاسم الأول مطلوب'
  if (!v.last.trim()) e.last = 'اسم العائلة مطلوب'
  if (!/^\d{10}$/.test(v.nid)) e.nid = 'رقم الهوية 10 أرقام'
  if (!v.birth) e.birth = 'تاريخ الميلاد مطلوب'
  if (!/^05\d{8}$/.test(v.phone)) e.phone = 'رقم جوال صحيح مثل 0551234567'
  if (v.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.email)) e.email = 'بريد إلكتروني غير صحيح'
  if (v.insurer !== 'بدون تأمين' && !v.policy.trim()) e.policy = 'رقم الوثيقة مطلوب'
  return e
}

export function ReceptionRegister() {
  const navigate = useNavigate()
  const [v, setV] = useState(EMPTY)
  const [errors, setErrors] = useState({})
  const [created, setCreated] = useState(null)
  const set = (k) => (e) => setV((s) => ({ ...s, [k]: e.target.value }))

  const submit = (e) => {
    e.preventDefault()
    const next = validate(v)
    setErrors(next)
    if (Object.keys(next).length === 0) setCreated({ name: `${v.first} ${v.last}`, mrn: `P-${10500 + Math.floor(Math.random() * 400)}` })
  }

  if (created) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>تم تسجيل المريض بنجاح</h2>
        <p><b>{created.name}</b> · رقم الملف <b dir="ltr">{created.mrn}</b></p>
        <div className="reg__actions">
          <Button variant="blue" icon="calendar" onClick={() => navigate('/reception/bookings')}>حجز موعد</Button>
          <Button variant="ghost" icon="plus" onClick={() => { setV(EMPTY); setCreated(null) }}>تسجيل مريض آخر</Button>
        </div>
      </Card>
    )
  }

  return (
    <form className="reg" onSubmit={submit} noValidate>
      <Card title="البيانات الشخصية">
        <div className="reg__grid">
          <Input label="الاسم الأول" value={v.first} onChange={set('first')} error={errors.first} />
          <Input label="اسم العائلة" value={v.last} onChange={set('last')} error={errors.last} />
          <Input label="رقم الهوية" inputMode="numeric" maxLength={10} value={v.nid} onChange={set('nid')} error={errors.nid} />
          <Input label="تاريخ الميلاد" type="date" value={v.birth} onChange={set('birth')} error={errors.birth} />
          <Select label="الجنس" options={['ذكر', 'أنثى']} value={v.gender} onChange={set('gender')} />
          <Select label="فصيلة الدم" options={['غير معروف', 'A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-']} value={v.blood} onChange={set('blood')} />
        </div>
      </Card>

      <Card title="بيانات التواصل">
        <div className="reg__grid">
          <Input label="رقم الجوال" icon="phone" inputMode="tel" placeholder="05xxxxxxxx" value={v.phone} onChange={set('phone')} error={errors.phone} />
          <Input label="البريد الإلكتروني (اختياري)" icon="mail" value={v.email} onChange={set('email')} error={errors.email} />
          <Input label="المدينة" value={v.city} onChange={set('city')} />
        </div>
      </Card>

      <Card title="التأمين وجهة الطوارئ">
        <div className="reg__grid">
          <Select label="شركة التأمين" options={['بدون تأمين', 'التعاونية', 'بوبا', 'ميدغلف', 'ملاذ']} value={v.insurer} onChange={set('insurer')} />
          <Input label="رقم الوثيقة" disabled={v.insurer === 'بدون تأمين'} value={v.policy} onChange={set('policy')} error={errors.policy} />
          <span />
          <Input label="اسم جهة اتصال الطوارئ" value={v.emName} onChange={set('emName')} />
          <Input label="جوال جهة الطوارئ" value={v.emPhone} onChange={set('emPhone')} />
        </div>
        <Textarea label="ملاحظات طبية (حساسية، أمراض مزمنة)" rows={3} value={v.notes} onChange={set('notes')} className="reg__notes" />
      </Card>

      <div className="reg__actions reg__actions--end">
        <Button variant="ghost" type="button" onClick={() => { setV(EMPTY); setErrors({}) }}>مسح الحقول</Button>
        <Button type="submit" size="lg" icon="check">تسجيل المريض</Button>
      </div>
    </form>
  )
}

export function ReceptionBookings() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'today', label: 'اليوم', match: (r) => r.date === '2026-10-18' },
        { key: 'pending', label: 'بانتظار التأكيد', match: (r) => r.status === 'بانتظار التأكيد' },
        { key: 'cancel', label: 'ملغي', match: (r) => r.status === 'ملغي' },
      ]}
      columns={[
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'phone' },
        { key: 'doctor', header: 'الطبيب' },
        { key: 'dept', header: 'القسم' },
        { key: 'date', header: 'التاريخ' },
        { key: 'time', header: 'الوقت' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: BOOKING_TONES },
      ]}
      rows={bookings}
      searchKeys={['patient', 'phone', 'doctor']}
      addLabel="حجز موعد"
      form={{
        title: 'حجز موعد جديد',
        fields: [
          { name: 'patient', label: 'اسم المريض', required: true },
          { name: 'phone', label: 'رقم الجوال', required: true },
          { name: 'dept', label: 'القسم', options: DEPTS },
          { name: 'doctor', label: 'الطبيب', options: DOCTORS },
          { name: 'date', label: 'التاريخ', type: 'date', required: true },
          { name: 'time', label: 'الوقت', type: 'time', required: true },
        ],
        defaults: { status: 'مؤكد' },
      }}
      detail={{
        titleKey: 'patient',
        subKey: 'phone',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: BOOKING_TONES,
        fields: [
          { label: 'الطبيب', key: 'doctor' },
          { label: 'القسم', key: 'dept' },
          { label: 'التاريخ', key: 'date' },
          { label: 'الوقت', key: 'time' },
        ],
        actions: [{ label: 'تسجيل الحضور', icon: 'check', variant: 'blue' }, { label: 'تعديل الموعد', icon: 'edit', variant: 'ghost' }, { label: 'إلغاء الحجز', variant: 'danger' }],
      }}
    />
  )
}

/* ---------- قائمة الانتظار (لوحة أعمدة) ---------- */
const COLUMNS = [
  { key: 'بالانتظار', title: 'بالانتظار', tone: 'orange', next: 'داخل العيادة', action: 'استدعاء' },
  { key: 'داخل العيادة', title: 'داخل العيادة', tone: 'blue', next: 'مكتمل', action: 'إنهاء الزيارة' },
  { key: 'مكتمل', title: 'مكتمل', tone: 'green' },
]

export function ReceptionQueue() {
  const [items, setItems] = useState(initialQueue)
  const move = (id, status) => setItems((list) => list.map((i) => (i.id === id ? { ...i, status } : i)))

  return (
    <div className="queue">
      {COLUMNS.map((col) => {
        const list = items.filter((i) => i.status === col.key)
        return (
          <section key={col.key} className="queue__col">
            <header>
              <h3>{col.title}</h3>
              <Badge tone={col.tone}>{list.length}</Badge>
            </header>
            {list.length === 0 && <EmptyState image="empty-patients" title="لا يوجد مرضى" />}
            {list.map((i) => (
              <article key={i.id} className="queue__card">
                <div className="queue__top">
                  <Avatar src={i.avatar} name={i.patient} size={40} />
                  <div>
                    <b>{i.patient}</b>
                    <small>{i.clinic} · {i.doctor}</small>
                  </div>
                  <span className="queue__ticket" dir="ltr">{i.ticket}</span>
                </div>
                <div className="queue__foot">
                  <small><Icon name="clock" size={14} /> {i.arrived}</small>
                  {col.next && (
                    <Button size="sm" variant={col.key === 'بالانتظار' ? 'primary' : 'blue'} onClick={() => move(i.id, col.next)}>
                      {col.action}
                    </Button>
                  )}
                </div>
              </article>
            ))}
          </section>
        )
      })}
    </div>
  )
}

export function ReceptionInvoices() {
  return (
    <ListPage
      stats={[
        { icon: 'wallet', label: 'تحصيل اليوم', value: '18,420 ر.س', delta: '+8%' },
        { icon: 'check', label: 'فواتير مدفوعة', value: '41', tone: 'green' },
        { icon: 'clock', label: 'غير مدفوعة', value: '7', tone: 'red' },
        { icon: 'alert', label: 'سداد جزئي', value: '3', tone: 'orange' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'paid', label: 'مدفوعة', match: (r) => r.status === 'مدفوعة' },
        { key: 'unpaid', label: 'غير مدفوعة', match: (r) => r.status === 'غير مدفوعة' },
        { key: 'part', label: 'جزئية', match: (r) => r.status === 'جزئية' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الفاتورة' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar' },
        { key: 'item', header: 'البند' },
        { key: 'amount', header: 'المبلغ' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: INVOICE_TONES },
      ]}
      rows={receptionInvoices}
      searchKeys={['no', 'patient', 'item']}
      addLabel="فاتورة جديدة"
      form={{
        title: 'إصدار فاتورة',
        fields: [
          { name: 'patient', label: 'اسم المريض', required: true },
          { name: 'item', label: 'البند', required: true },
          { name: 'amount', label: 'المبلغ (ر.س)', type: 'number', required: true },
          { name: 'method', label: 'طريقة الدفع', options: ['نقدًا', 'بطاقة', 'تحويل'] },
        ],
        defaults: { no: 'INV-10483', status: 'غير مدفوعة' },
      }}
      detail={{
        titleKey: 'patient',
        subKey: 'no',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: INVOICE_TONES,
        fields: [
          { label: 'البند', key: 'item' },
          { label: 'المبلغ', key: 'amount' },
          { label: 'طريقة الدفع', key: 'method' },
        ],
        actions: [{ label: 'تحصيل المبلغ', icon: 'wallet', variant: 'blue' }, { label: 'طباعة الفاتورة', icon: 'print', variant: 'ghost' }],
      }}
    />
  )
}
