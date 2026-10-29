import { useEffect, useState } from 'react'
import { SettingsTemplate } from '../../components/patterns'
import { Avatar, Badge, Button, Card, EmptyState, Icon, Input, Select, Switch, Tabs, Textarea } from '../../components/ui'
import { TYPE_META } from '../../data/notifications'
import useAuth from '../../hooks/useAuth'
import useNotifications from '../../hooks/useNotifications'
import { cx } from '../../utils/cx'
import { passwordStrength, validatePassword } from '../../utils/validators'
import './common.css'
import '../auth/auth.css'
import '../patient/patient.css'

/* ================= الإشعارات ================= */
const FILTERS = [
  { key: 'all', label: 'الكل' },
  { key: 'unread', label: 'غير المقروءة' },
  { key: 'alert', label: 'تنبيهات' },
  { key: 'appointment', label: 'مواعيد' },
  { key: 'result', label: 'نتائج' },
]
const GROUPS = ['اليوم', 'أمس', 'هذا الأسبوع']

export function NotificationsPage() {
  const { items, unread, markRead, markAllRead, remove } = useNotifications()
  const [filter, setFilter] = useState('all')

  const match = (n) => (filter === 'all' ? true : filter === 'unread' ? n.unread : n.type === filter)
  const shown = items.filter(match)
  const tabs = FILTERS.map((f) => ({ ...f, count: f.key === 'unread' ? unread : undefined }))

  return (
    <div className="notif">
      <div className="notif__bar">
        <Tabs items={tabs} value={filter} onChange={setFilter} />
        <span className="list__spacer" />
        <Button variant="soft" size="sm" icon="check" disabled={unread === 0} onClick={markAllRead}>
          تعليم الكل كمقروء
        </Button>
      </div>

      {shown.length === 0 ? (
        <Card>
          <EmptyState image="empty-notifications" title="لا توجد إشعارات" text="سنُعلمك هنا بكل جديد يخص عملك." />
        </Card>
      ) : (
        GROUPS.map((g) => {
          const list = shown.filter((n) => n.group === g)
          if (list.length === 0) return null
          return (
            <section key={g} className="notif__group">
              <h3>{g}</h3>
              <Card padded={false}>
                {list.map((n) => {
                  const meta = TYPE_META[n.type]
                  return (
                    <article key={n.id} className={cx('notif__item', n.unread && 'is-unread')}>
                      <button className="notif__main" onClick={() => markRead(n.id)} aria-label={`${n.title}${n.unread ? ' – غير مقروء' : ''}`}>
                        <span className={cx('notif__icon', `badge--${meta.tone}`)}><Icon name={meta.icon} size={20} /></span>
                        <span className="notif__text">
                          <b>{n.title}</b>
                          <span>{n.text}</span>
                          <small>{n.time}</small>
                        </span>
                        {n.unread && <i className="notif__dot" />}
                      </button>
                      <button className="notif__del" onClick={() => remove(n.id)} aria-label="حذف الإشعار">
                        <Icon name="trash" size={18} />
                      </button>
                    </article>
                  )
                })}
              </Card>
            </section>
          )
        })
      )}
    </div>
  )
}

/* ================= الملف الشخصي ================= */
const SESSIONS = [
  { id: 1, device: 'Chrome على macOS', place: 'الرياض', time: 'نشط الآن', current: true },
  { id: 2, device: 'تطبيق iPhone', place: 'جدة', time: 'قبل ساعتين' },
  { id: 3, device: 'Edge على Windows', place: 'الرياض', time: 'أمس' },
]
const ACTIVITY = [
  { id: 1, date: 'اليوم 09:12', title: 'تسجيل دخول', text: 'من متصفح Chrome في الرياض.' },
  { id: 2, date: 'أمس 17:40', title: 'تحديث بيانات الملف', text: 'تم تغيير رقم الجوال.' },
  { id: 3, date: '12 أكتوبر', title: 'تغيير كلمة المرور', text: 'تم تحديث كلمة المرور بنجاح.' },
]
const STRENGTH = ['', 'ضعيفة', 'مقبولة', 'جيدة', 'قوية']

function ProfileInfo({ role, user }) {
  const [photo, setPhoto] = useState(user.avatar)
  const [v, setV] = useState({ name: user.name, email: `${role.key}@claudcare.com`, phone: '0551234567', lang: 'العربية', bio: '' })
  const [errors, setErrors] = useState({})
  const [saved, setSaved] = useState(false)
  const set = (k) => (e) => { setSaved(false); setV((s) => ({ ...s, [k]: e.target.value })) }

  useEffect(() => () => { if (photo?.startsWith('blob:')) URL.revokeObjectURL(photo) }, [photo])

  const onPhoto = (e) => {
    const file = e.target.files?.[0]
    if (file && file.type.startsWith('image/')) setPhoto(URL.createObjectURL(file))
  }
  const save = (e) => {
    e.preventDefault()
    const next = {}
    if (!v.name.trim()) next.name = 'الاسم مطلوب'
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.email)) next.email = 'بريد إلكتروني غير صحيح'
    if (!/^05\d{8}$/.test(v.phone)) next.phone = 'رقم جوال صحيح مثل 0551234567'
    setErrors(next)
    setSaved(Object.keys(next).length === 0)
  }

  return (
    <form className="prof__form" onSubmit={save} noValidate>
      <Card title="الصورة الشخصية">
        <div className="prof__photo">
          <Avatar src={photo} name={v.name} size={84} />
          <label className="btn btn--soft btn--md">
            تغيير الصورة
            <input type="file" accept="image/*" onChange={onPhoto} className="visually-hidden" />
          </label>
          <Button type="button" variant="ghost" onClick={() => setPhoto(undefined)}>إزالة</Button>
        </div>
      </Card>
      <Card title="البيانات الأساسية">
        <div className="prof__grid">
          <Input label="الاسم الكامل" value={v.name} onChange={set('name')} error={errors.name} />
          <Input label="البريد الإلكتروني" icon="mail" value={v.email} onChange={set('email')} error={errors.email} />
          <Input label="رقم الجوال" icon="phone" value={v.phone} onChange={set('phone')} error={errors.phone} />
          <Input label="نوع الحساب" value={role.label} disabled readOnly />
          <Select label="اللغة" options={['العربية', 'English']} value={v.lang} onChange={set('lang')} />
        </div>
        <Textarea label="نبذة (اختياري)" rows={3} value={v.bio} onChange={set('bio')} className="prof__bio" />
      </Card>
      <div className="settings__save">
        {saved && <span className="settings__ok">تم حفظ التغييرات ✓</span>}
        <Button type="submit">حفظ التغييرات</Button>
      </div>
    </form>
  )
}

function ProfileSecurity() {
  const [pw, setPw] = useState({ current: '', next: '', confirm: '' })
  const [errors, setErrors] = useState({})
  const [done, setDone] = useState(false)
  const [twofa, setTwofa] = useState(true)
  const [sessions, setSessions] = useState(SESSIONS)
  const strength = pw.next ? Math.max(1, passwordStrength(pw.next)) : 0
  const set = (k) => (e) => { setDone(false); setPw((s) => ({ ...s, [k]: e.target.value })) }

  const change = (e) => {
    e.preventDefault()
    const next = {
      current: pw.current ? '' : 'كلمة المرور الحالية مطلوبة',
      next: validatePassword(pw.next),
      confirm: pw.confirm !== pw.next ? 'كلمتا المرور غير متطابقتين' : '',
    }
    setErrors(next)
    if (!next.current && !next.next && !next.confirm) {
      setDone(true)
      setPw({ current: '', next: '', confirm: '' })
    }
  }

  return (
    <div className="prof__form">
      <Card title="تغيير كلمة المرور">
        <form className="prof__pw" onSubmit={change} noValidate>
          <Input label="كلمة المرور الحالية" type="password" value={pw.current} onChange={set('current')} error={errors.current} />
          <Input label="كلمة المرور الجديدة" type="password" value={pw.next} onChange={set('next')} error={errors.next} />
          <div className="strength" aria-live="polite">
            {[1, 2, 3, 4].map((i) => <i key={i} className={strength >= i ? `on-${strength}` : ''} />)}
            <small>{STRENGTH[strength]}</small>
          </div>
          <Input label="تأكيد كلمة المرور" type="password" value={pw.confirm} onChange={set('confirm')} error={errors.confirm} />
          <div className="prof__row">
            <Button type="submit">تحديث كلمة المرور</Button>
            {done && <span className="settings__ok">تم تحديث كلمة المرور ✓</span>}
          </div>
        </form>
      </Card>

      <Card title="المصادقة الثنائية">
        <div className="settings__switch">
          <span><b>تفعيل المصادقة الثنائية</b><small>رمز تحقق إضافي عند تسجيل الدخول</small></span>
          <Switch checked={twofa} onChange={(e) => setTwofa(e.target.checked)} aria-label="المصادقة الثنائية" />
        </div>
      </Card>

      <Card title="الجلسات النشطة">
        <div className="dash__list">
          {sessions.map((s) => (
            <div key={s.id} className="prof__session">
              <span className="pick__icon"><Icon name="globe" size={20} /></span>
              <span className="pick__text"><b>{s.device}</b><small>{s.place} · {s.time}</small></span>
              {s.current ? <Badge tone="green" dot>هذا الجهاز</Badge> : <Button size="sm" variant="danger" onClick={() => setSessions((l) => l.filter((x) => x.id !== s.id))}>إنهاء</Button>}
            </div>
          ))}
        </div>
      </Card>
    </div>
  )
}

export function ProfilePage() {
  const { role, user } = useAuth()
  const [tab, setTab] = useState('info')
  return (
    <div className="prof">
      <Card className="rec__profile">
        <Avatar src={user.avatar} name={user.name} size={72} />
        <div className="rec__id">
          <h2>{user.name}</h2>
          <small>{role.label} · {role.key}@claudcare.com</small>
        </div>
        <Badge tone="green" dot>حساب مفعّل</Badge>
      </Card>
      <Tabs value={tab} onChange={setTab} items={[{ key: 'info', label: 'بياناتي' }, { key: 'security', label: 'الأمان' }, { key: 'activity', label: 'النشاط' }]} />
      {tab === 'info' && <ProfileInfo key={role.key} role={role} user={user} />}
      {tab === 'security' && <ProfileSecurity />}
      {tab === 'activity' && (
        <Card>
          <ol className="timeline">
            {ACTIVITY.map((a) => (
              <li key={a.id}>
                <i />
                <div><small>{a.date}</small><b>{a.title}</b><p>{a.text}</p></div>
              </li>
            ))}
          </ol>
        </Card>
      )}
    </div>
  )
}

/* ================= الإعدادات ================= */
const EXTRA = {
  doctor: { title: 'تفضيلات الكشف', items: [{ type: 'switch', name: 'autoRx', label: 'اقتراح الأدوية الشائعة', hint: 'عند كتابة الوصفة', value: true }, { type: 'switch', name: 'sign', label: 'توقيع الوصفات إلكترونيًا', value: true }] },
  nurse: { title: 'تفضيلات التمريض', items: [{ type: 'switch', name: 'doseAlert', label: 'تنبيه قبل موعد الجرعة بـ 15 دقيقة', value: true }, { type: 'switch', name: 'critAlert', label: 'تنبيه صوتي للحالات الحرجة', value: true }] },
  lab: { title: 'تفضيلات المختبر', items: [{ type: 'switch', name: 'critical', label: 'إشعار فوري بالقيم الحرجة', value: true }, { type: 'select', name: 'unit', label: 'وحدة القياس الافتراضية', options: ['SI', 'تقليدية'], value: 'SI' }] },
  radiology: { title: 'تفضيلات الأشعة', items: [{ type: 'switch', name: 'tpl', label: 'تفعيل قوالب التقارير', value: true }, { type: 'select', name: 'viewer', label: 'تباين العارض الافتراضي', options: ['عادي', 'مرتفع'], value: 'عادي' }] },
  pharmacy: { title: 'تفضيلات الصيدلية', items: [{ type: 'switch', name: 'lowStock', label: 'تنبيه عند انخفاض المخزون', hint: 'عند الوصول للحد الأدنى', value: true }, { type: 'switch', name: 'interaction', label: 'تحذير التداخلات والحساسية عند الصرف', value: true }, { type: 'select', name: 'expiry', label: 'التنبيه قبل انتهاء الصلاحية', options: ['30 يومًا', '60 يومًا', '90 يومًا'], value: '60 يومًا' }] },
  finance: { title: 'تفضيلات المحاسبة', items: [{ type: 'switch', name: 'overdue', label: 'تنبيه الفواتير المتأخرة', value: true }, { type: 'select', name: 'discountCap', label: 'سقف الخصم دون اعتماد المدير', options: ['5%', '10%', '15%'], value: '10%' }, { type: 'switch', name: 'closeReminder', label: 'تذكير بالإقفال اليومي', hint: 'قبل نهاية الدوام بساعة', value: true }] },
  hr: { title: 'تفضيلات الموارد البشرية', items: [{ type: 'select', name: 'licenseAlert', label: 'التنبيه قبل انتهاء الترخيص', options: ['30 يومًا', '60 يومًا', '90 يومًا'], value: '60 يومًا' }, { type: 'switch', name: 'leaveNotify', label: 'إشعار عند طلب إجازة جديد', value: true }, { type: 'switch', name: 'coverage', label: 'تحذير نقص تغطية القسم', value: true }] },
  emergency: { title: 'تفضيلات الطوارئ', items: [{ type: 'switch', name: 'critical', label: 'تنبيه صوتي للحالات الحرجة', value: true }, { type: 'switch', name: 'ambulance', label: 'إشعار عند اقتراب الإسعاف', value: true }, { type: 'select', name: 'waitAlert', label: 'تنبيه تجاوز الانتظار', options: ['حسب المستوى', 'دائمًا بعد 30 دقيقة'], value: 'حسب المستوى' }] },
  admissions: { title: 'تفضيلات الإدخال', items: [{ type: 'switch', name: 'bedFree', label: 'إشعار عند إتاحة سرير', value: true }, { type: 'switch', name: 'cleanAlert', label: 'تنبيه الأسرّة المتأخرة عن التنظيف', value: true }, { type: 'select', name: 'occAlert', label: 'تنبيه الإشغال عند', options: ['80%', '90%', '95%'], value: '90%' }] },
  surgery: { title: 'تفضيلات غرفة العمليات', items: [{ type: 'switch', name: 'preopAlert', label: 'تنبيه قائمة ما قبل العملية غير المكتملة', value: true }, { type: 'select', name: 'turnover', label: 'مدة التعقيم بين العمليات', options: ['20 دقيقة', '30 دقيقة', '45 دقيقة'], value: '30 دقيقة' }, { type: 'switch', name: 'startNotify', label: 'إشعار قبل بدء العملية بساعة', value: true }] },
  insurance: { title: 'تفضيلات التأمين', items: [{ type: 'select', name: 'sla', label: 'مهلة مراجعة الموافقات (SLA)', options: ['24 ساعة', '48 ساعة', '72 ساعة'], value: '48 ساعة' }, { type: 'switch', name: 'urgent', label: 'تنبيه فوري للموافقات العاجلة', value: true }, { type: 'switch', name: 'autoCheck', label: 'تحقق تلقائي من الوثيقة عند استلام الطلب', value: true }] },
  reception: { title: 'تفضيلات الاستقبال', items: [{ type: 'switch', name: 'sms', label: 'إرسال تذكير SMS للمرضى', value: true }, { type: 'switch', name: 'print', label: 'طباعة تذكرة الانتظار تلقائيًا', value: false }] },
  patient: { title: 'التذكيرات الصحية', items: [{ type: 'switch', name: 'meds', label: 'تذكير بمواعيد الأدوية', value: true }, { type: 'switch', name: 'appt', label: 'تذكير قبل الموعد بـ 24 ساعة', value: true }] },
}

export function SettingsPage() {
  const { role } = useAuth()
  const extra = EXTRA[role.key]
  const sections = [
    {
      title: 'العرض واللغة',
      items: [
        { type: 'select', name: 'lang', label: 'لغة الواجهة', options: ['العربية', 'English'], value: 'العربية' },
        { type: 'select', name: 'date', label: 'تنسيق التاريخ', options: ['يوم/شهر/سنة', 'سنة-شهر-يوم'], value: 'يوم/شهر/سنة' },
        { type: 'select', name: 'tz', label: 'المنطقة الزمنية', options: ['الرياض (GMT+3)', 'القاهرة (GMT+2)', 'دبي (GMT+4)'], value: 'الرياض (GMT+3)' },
      ],
    },
    {
      title: 'الإشعارات',
      items: [
        { type: 'switch', name: 'inapp', label: 'إشعارات داخل النظام', value: true },
        { type: 'switch', name: 'email', label: 'إشعارات البريد الإلكتروني', value: true },
        { type: 'switch', name: 'digest', label: 'ملخص يومي في الصباح', hint: 'يصلك الساعة 8:00 صباحًا', value: false },
      ],
    },
    {
      title: 'الخصوصية',
      items: [
        { type: 'switch', name: 'online', label: 'إظهار حالة الاتصال لزملائي', value: true },
        { type: 'switch', name: 'analytics', label: 'المساهمة في تحسين النظام بإحصاءات مجهولة', value: false },
      ],
    },
    ...(extra ? [extra] : []),
  ]
  return <SettingsTemplate key={role.key} sections={sections} />
}
