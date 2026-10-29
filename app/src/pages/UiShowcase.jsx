import { useState } from 'react'
import {
  Avatar, Badge, BarChart, Button, Card, Checkbox, DataTable, EmptyState, Icon, Input,
  ListItem, Modal, SearchBox, Select, StatCard, Switch, Tabs, Textarea, iconNames,
} from '../components/ui'
import { logo } from '../utils/assets'
import './UiShowcase.css'

const patients = [
  { id: 1, name: 'محمد علي', clinic: 'الباطنة', time: '09:00', status: ['بالانتظار', 'orange'], avatar: 'avatars/patient-3.jpg' },
  { id: 2, name: 'سارة أحمد', clinic: 'القلب', time: '09:30', status: ['حاضر', 'green'], avatar: 'avatars/patient-2.jpg' },
  { id: 3, name: 'يوسف خالد', clinic: 'الأطفال', time: '10:00', status: ['عاجل', 'red'], avatar: 'avatars/patient-1.jpg' },
]

const columns = [
  { key: 'name', header: 'المريض', render: (r) => <span className="show__person"><Avatar src={r.avatar} name={r.name} size={34} />{r.name}</span> },
  { key: 'clinic', header: 'العيادة' },
  { key: 'time', header: 'الوقت' },
  { key: 'status', header: 'الحالة', render: (r) => <Badge tone={r.status[1]} dot>{r.status[0]}</Badge> },
]

const Section = ({ title, children }) => (
  <Card title={title} className="show__section">
    {children}
  </Card>
)

// صفحة عرض المكونات – للمراجعة فقط (/ui)
export default function UiShowcase() {
  const [tab, setTab] = useState('all')
  const [open, setOpen] = useState(false)
  const [sel, setSel] = useState(1)

  return (
    <main className="show">
      <header className="show__head">
        <img src={logo} alt="ClaudCare" />
        <h1>مكتبة المكونات</h1>
      </header>

      <Section title="الأزرار">
        <div className="show__row">
          <Button>رئيسي</Button>
          <Button variant="blue">أزرق</Button>
          <Button variant="ghost">محايد</Button>
          <Button variant="soft" icon="plus">خفيف</Button>
          <Button variant="danger" icon="trash">حذف</Button>
          <Button loading>جارٍ الحفظ</Button>
          <Button disabled>معطّل</Button>
          <Button size="sm">صغير</Button>
          <Button size="lg">كبير</Button>
        </div>
      </Section>

      <Section title="الحقول">
        <div className="show__grid">
          <Input label="البريد الإلكتروني" icon="mail" placeholder="name@hospital.com" />
          <Input label="كلمة المرور" type="password" icon="lock" defaultValue="12345678" />
          <Input label="رقم الجوال" icon="phone" error="رقم غير صحيح" defaultValue="05" />
          <Select label="القسم" options={['الباطنة', 'القلب', 'الأطفال', 'العظام']} />
          <Textarea label="ملاحظات" placeholder="سبب الزيارة" />
          <div className="show__col">
            <Checkbox label="تذكرني" defaultChecked />
            <Switch label="تفعيل الإشعارات" defaultChecked />
            <SearchBox placeholder="ابحث عن مريض..." />
          </div>
        </div>
      </Section>

      <Section title="بطاقات الإحصاءات">
        <div className="show__stats">
          <StatCard icon="users" label="المرضى" value="1,284" delta="+8%" />
          <StatCard icon="calendar" label="مواعيد اليوم" value="312" delta="+6%" tone="orange" />
          <StatCard icon="bed" label="غرف شاغرة" value="23" delta="-4" tone="green" />
          <StatCard icon="alert" label="حالات عاجلة" value="14" tone="red" />
        </div>
      </Section>

      <div className="show__two">
        <Section title="الجدول والتبويبات">
          <Tabs
            value={tab}
            onChange={setTab}
            items={[{ key: 'all', label: 'الكل', count: 3 }, { key: 'today', label: 'اليوم', count: 2 }, { key: 'done', label: 'مكتمل' }]}
            className="show__tabs"
          />
          <DataTable columns={columns} rows={patients} selectedId={sel} onRowClick={(r) => setSel(r.id)} />
        </Section>

        <Section title="قائمة ومخطط">
          <div className="show__col">
            <ListItem mark="CBC" title="صورة دم – محمد علي" subtitle="قبل ساعة" tone="red" />
            <ListItem mark="XR" title="أشعة صدر – سارة" subtitle="قبل 3 ساعات" tone="orange" />
            <BarChart data={[40, 55, 48, 70, 62, 85, 74].map((value, i) => ({ value, label: 'سحنثرخج'[i] }))} />
          </div>
        </Section>
      </div>

      <Section title="الشارات والصور الشخصية">
        <div className="show__row">
          <Badge tone="green">نشط</Badge><Badge tone="orange">قيد المراجعة</Badge><Badge tone="red">موقوف</Badge>
          <Badge tone="blue">جديد</Badge><Badge tone="gray">مؤرشف</Badge>
          <Avatar src="avatars/doc-f1.jpg" name="منى" size={48} />
          <Avatar src="avatars/doc-m1.jpg" name="خالد" size={48} />
          <Avatar name="سلمى" size={48} />
        </div>
      </Section>

      <Section title="نافذة وحالة فارغة">
        <div className="show__row">
          <Button variant="blue" onClick={() => setOpen(true)}>فتح النافذة</Button>
        </div>
        <EmptyState image="empty-appointments" title="لا توجد مواعيد" text="لم يتم حجز أي موعد بعد." action={{ label: 'حجز موعد', onClick: () => setOpen(true) }} />
      </Section>

      <Section title={`الأيقونات (${iconNames.length})`}>
        <div className="show__icons">
          {iconNames.map((n) => (
            <span key={n} title={n}><Icon name={n} size={22} /><small>{n}</small></span>
          ))}
        </div>
      </Section>

      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title="حجز موعد جديد"
        footer={<><Button variant="ghost" onClick={() => setOpen(false)}>إلغاء</Button><Button onClick={() => setOpen(false)}>تأكيد الحجز</Button></>}
      >
        <Input label="اسم المريض" placeholder="ابحث أو أدخل الاسم" />
        <Select label="الطبيب" options={['د. منى القاسم', 'د. فهد عمر']} />
        <Input label="التاريخ" type="date" />
      </Modal>
    </main>
  )
}
