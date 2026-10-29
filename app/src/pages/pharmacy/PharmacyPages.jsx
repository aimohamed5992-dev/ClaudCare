import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, Checkbox, Icon, Select } from '../../components/ui'
import { dispenseRx, inventory, purchaseOrders, rxQueue, sales, salesMonthly, salesWeekly, topCategories } from '../../data/pharmacy'
import { cx } from '../../utils/cx'
import './pharmacy.css'
import '../reception/reception.css'

const RX_TONES = { 'بانتظار الصرف': 'orange', 'قيد التجهيز': 'blue', 'تم الصرف': 'green', مرفوضة: 'red' }
const PRIORITY_TONES = { عاجل: 'red', عادي: 'gray' }
const STOCK_TONES = { متوفر: 'green', منخفض: 'orange', نفد: 'red', 'قارب الانتهاء': 'orange' }
const PO_TONES = { مسودة: 'gray', مُرسل: 'blue', 'مستلم جزئيًا': 'orange', مستلم: 'green' }
const SALE_TONES = { مدفوعة: 'green', مرتجع: 'red' }
// المخزون: الأقل هو الأخطر (عكس الإشغال)
const stockTone = (v) => (v <= 25 ? 'red' : v <= 50 ? 'orange' : 'blue')

export function PharmacyDashboard() {
  const navigate = useNavigate()
  return (
    <DashboardTemplate
      banner={{ title: 'صيدلية الفرع الرئيسي', text: '3 وصفات بانتظار الصرف، وصنف واحد نفد من المخزون.', cta: 'صرف وصفة', to: '/pharmacy/prescriptions', image: 'role-pharmacy.png' }}
      stats={[
        { icon: 'file', label: 'وصفات اليوم', value: '86', delta: '+9%' },
        { icon: 'clock', label: 'بانتظار الصرف', value: '3', tone: 'orange' },
        { icon: 'alert', label: 'أصناف منخفضة', value: '4', tone: 'red' },
        { icon: 'wallet', label: 'مبيعات اليوم', value: '6,240 ر.س', delta: '+6%', tone: 'green' },
      ]}
      table={{
        title: 'وصفات بانتظار الصرف',
        columns: [
          { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'no' },
          { key: 'doctor', header: 'الطبيب' },
          { key: 'time', header: 'الوصول' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: RX_TONES },
        ],
        rows: rxQueue.filter((r) => r.status === 'بانتظار الصرف' || r.status === 'قيد التجهيز'),
      }}
      side={{
        title: 'تنبيهات المخزون',
        items: [
          { mark: '0', title: 'أوميبرازول 20 مجم', subtitle: 'نفد من المخزون', tone: 'red', onClick: () => navigate('/pharmacy/inventory') },
          { mark: '6', title: 'أنسولين لانتوس', subtitle: 'أقل من الحد الأدنى (30)', tone: 'orange', onClick: () => navigate('/pharmacy/inventory') },
          { mark: '!', title: 'سالبيوتامول بخاخ', subtitle: 'ينتهي في 2026-11', tone: 'orange', onClick: () => navigate('/pharmacy/inventory') },
        ],
      }}
      charts={[{ title: 'عدد الوصفات المصروفة خلال الأسبوع', data: salesWeekly }]}
    />
  )
}

export function PharmacyPrescriptions() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'new', label: 'بانتظار الصرف', match: (r) => r.status === 'بانتظار الصرف' },
        { key: 'prep', label: 'قيد التجهيز', match: (r) => r.status === 'قيد التجهيز' },
        { key: 'done', label: 'تم الصرف', match: (r) => r.status === 'تم الصرف' },
        { key: 'rej', label: 'مرفوضة', match: (r) => r.status === 'مرفوضة' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الوصفة' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'doctor' },
        { key: 'itemsText', header: 'الأدوية' },
        { key: 'priority', header: 'الأولوية', type: 'badge', tones: PRIORITY_TONES },
        { key: 'status', header: 'الحالة', type: 'badge', tones: RX_TONES },
      ]}
      rows={rxQueue}
      searchKeys={['no', 'patient', 'itemsText']}
      detail={{
        titleKey: 'patient',
        subKey: 'no',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: RX_TONES,
        fields: [
          { label: 'الطبيب', key: 'doctor' },
          { label: 'الأدوية', key: 'itemsText' },
          { label: 'الأولوية', key: 'priority' },
          { label: 'وصلت', key: 'time' },
        ],
        actions: [{ label: 'بدء الصرف', icon: 'pill', variant: 'blue', to: '/pharmacy/dispense' }, { label: 'طباعة الملصقات', icon: 'print', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- صرف الدواء ---------- */
const money = (n) => `${n.toFixed(2)} ر.س`

export function PharmacyDispense() {
  const navigate = useNavigate()
  const rx = dispenseRx
  const [state, setState] = useState(() =>
    Object.fromEntries(rx.items.map((i) => [i.id, { qty: String(i.qty), substituted: false, skipped: false }])),
  )
  const [doctorOk, setDoctorOk] = useState(false)
  const [counsel, setCounsel] = useState(false)
  const [payment, setPayment] = useState('بطاقة')
  const [receipt, setReceipt] = useState(null)

  const patch = (id, p) => setState((s) => ({ ...s, [id]: { ...s[id], ...p } }))

  // لكل صنف: العنصر الفعلي (الأصلي أو البديل) وحالته
  const rows = useMemo(
    () =>
      rx.items.map((item) => {
        const st = state[item.id]
        const eff = st.substituted ? { ...item.substitute, id: item.id } : item
        const qty = Number(st.qty)
        const conflict = !st.substituted && item.conflict && !doctorOk
        let status = 'جاهز'
        if (st.skipped) status = 'تم التخطي'
        else if (conflict) status = 'تعارض مع الحساسية'
        else if (!Number.isInteger(qty) || qty <= 0) status = 'كمية غير صحيحة'
        else if (qty > eff.stock) status = eff.stock === 0 ? 'نفد المخزون' : 'مخزون غير كافٍ'
        return { item, eff, st, qty, status }
      }),
    [rx.items, state, doctorOk],
  )

  const ready = rows.filter((r) => r.status === 'جاهز')
  const blocking = rows.filter((r) => r.status !== 'جاهز' && r.status !== 'تم التخطي')
  const total = ready.reduce((sum, r) => sum + r.qty * r.eff.price, 0)
  const hasConflict = rows.some((r) => r.status === 'تعارض مع الحساسية')
  const reasons = [
    blocking.length > 0 && `${blocking.length} أصناف تحتاج معالجة قبل الصرف`,
    ready.length === 0 && 'لا يوجد صنف جاهز للصرف',
    !counsel && 'أكّد شرح الجرعات للمريض',
  ].filter(Boolean)

  if (receipt) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>تم صرف الوصفة</h2>
        <p>
          {rx.patient} · <b dir="ltr">{rx.no}</b> · {receipt.count} أصناف · الإجمالي <b>{money(receipt.total)}</b>
        </p>
        {receipt.skipped > 0 && <Badge tone="orange" dot>{receipt.skipped} أصناف لم تُصرف</Badge>}
        <div className="reg__actions">
          <Button variant="blue" onClick={() => navigate('/pharmacy/prescriptions')}>العودة للوصفات</Button>
          <Button variant="ghost" icon="print">طباعة الإيصال</Button>
        </div>
      </Card>
    )
  }

  const confirm = () =>
    setReceipt({ count: ready.length, total, skipped: rows.filter((r) => r.status === 'تم التخطي').length })

  return (
    <div className="disp">
      <div className="disp__main">
        <Card className="disp__head">
          <Avatar src={rx.avatar} name={rx.patient} size={56} />
          <div>
            <h3>{rx.patient}</h3>
            <small>{rx.age} سنة · <span dir="ltr">{rx.no}</span> · {rx.doctor}</small>
          </div>
          <Badge tone="red" dot>عاجل</Badge>
        </Card>

        <div className="disp__allergy" role="alert">
          <Icon name="alert" size={20} />
          <span><b>حساسية المريض:</b> {rx.allergies.join('، ')}</span>
        </div>

        {rows.map(({ item, eff, st, status }) => {
          const tone = status === 'جاهز' ? 'green' : status === 'تم التخطي' ? 'gray' : 'red'
          return (
            <Card key={item.id} className={cx('disp__item', status !== 'جاهز' && status !== 'تم التخطي' && 'is-bad', st.skipped && 'is-skipped')}>
              <div className="disp__row">
                <span className="disp__icon"><Icon name="pill" size={22} /></span>
                <div className="disp__name">
                  <b>{eff.name}</b>
                  <small>{eff.form} · المتوفر {eff.stock} · {money(eff.price)} للوحدة</small>
                  {st.substituted && <Badge tone="blue">بديل لـ {item.name}</Badge>}
                </div>
                <label className="disp__qty">
                  <small>الكمية</small>
                  <input
                    inputMode="numeric"
                    value={st.qty}
                    disabled={st.skipped}
                    aria-label={`كمية ${eff.name}`}
                    onChange={(e) => patch(item.id, { qty: e.target.value })}
                  />
                </label>
                <Badge tone={tone} dot>{status}</Badge>
              </div>

              {status === 'تعارض مع الحساسية' && (
                <div className="disp__warn">
                  <span>يحتوي على مادة من عائلة <b>{item.conflict}</b> وهي من حساسية المريض.</span>
                  <Button size="sm" variant="blue" onClick={() => patch(item.id, { substituted: true, qty: String(item.substitute.qty) })}>
                    استبدال بـ {item.substitute.name}
                  </Button>
                </div>
              )}
              <div className="disp__actions">
                {st.skipped ? (
                  <Button size="sm" variant="ghost" onClick={() => patch(item.id, { skipped: false })}>تراجع عن التخطي</Button>
                ) : (
                  <Button size="sm" variant="danger" onClick={() => patch(item.id, { skipped: true })}>عدم صرف هذا الصنف</Button>
                )}
                {st.substituted && (
                  <Button size="sm" variant="ghost" onClick={() => patch(item.id, { substituted: false, qty: String(item.qty) })}>العودة للصنف الأصلي</Button>
                )}
              </div>
            </Card>
          )
        })}
      </div>

      <aside className="disp__side">
        <Card title="ملخص الصرف">
          <dl className="detail__list">
            <div><dt>أصناف جاهزة</dt><dd>{ready.length} من {rows.length}</dd></div>
            <div><dt>الإجمالي</dt><dd>{money(total)}</dd></div>
          </dl>
          <div className="disp__form">
            <Select label="طريقة الدفع" options={['بطاقة', 'نقدًا', 'تأمين']} value={payment} onChange={(e) => setPayment(e.target.value)} />
            {hasConflict && <Checkbox label="تم التواصل مع الطبيب وموافقته على الصرف رغم الحساسية" checked={doctorOk} onChange={(e) => setDoctorOk(e.target.checked)} />}
            {!hasConflict && doctorOk && <Checkbox label="موافقة الطبيب على الصرف مسجّلة" checked={doctorOk} onChange={(e) => setDoctorOk(e.target.checked)} />}
            <Checkbox label="تم شرح الجرعات وطريقة الاستخدام للمريض" checked={counsel} onChange={(e) => setCounsel(e.target.checked)} />
            {reasons.length > 0 && <ul className="disp__reasons" aria-live="polite">{reasons.map((r) => <li key={r}>{r}</li>)}</ul>}
            <Button icon="check" size="lg" full disabled={reasons.length > 0} onClick={confirm}>تأكيد الصرف</Button>
            <Button variant="ghost" full onClick={() => navigate('/pharmacy/prescriptions')}>إلغاء</Button>
          </div>
        </Card>
      </aside>
    </div>
  )
}

export function PharmacyInventory() {
  return (
    <ListPage
      stats={[
        { icon: 'box', label: 'إجمالي الأصناف', value: '412' },
        { icon: 'alert', label: 'منخفضة', value: '4', tone: 'orange' },
        { icon: 'close', label: 'نفدت', value: '1', tone: 'red' },
        { icon: 'clock', label: 'قاربت الانتهاء', value: '2', tone: 'orange' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'low', label: 'منخفض / نفد', match: (r) => r.status === 'منخفض' || r.status === 'نفد' },
        { key: 'exp', label: 'قارب الانتهاء', match: (r) => r.status === 'قارب الانتهاء' },
      ]}
      columns={[
        { key: 'name', header: 'الدواء' },
        { key: 'category', header: 'الفئة' },
        { key: 'qty', header: 'الكمية' },
        { key: 'level', header: 'مستوى المخزون', type: 'progress', tone: stockTone },
        { key: 'expiry', header: 'الانتهاء' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: STOCK_TONES },
      ]}
      rows={inventory}
      searchKeys={['name', 'category', 'supplier']}
      addLabel="إضافة صنف"
      form={{
        title: 'إضافة صنف للمخزون',
        fields: [
          { name: 'name', label: 'اسم الدواء والتركيز', required: true },
          { name: 'category', label: 'الفئة', options: ['مسكنات', 'مضادات حيوية', 'السكري', 'الضغط', 'الجهاز الهضمي', 'الجهاز التنفسي'] },
          { name: 'qty', label: 'الكمية', type: 'number', required: true },
          { name: 'min', label: 'الحد الأدنى', type: 'number', required: true },
          { name: 'expiry', label: 'تاريخ الانتهاء', type: 'month', required: true },
          { name: 'supplier', label: 'المورد', options: ['الدوائية المتحدة', 'نماء الطبية', 'الشرق للأدوية'] },
        ],
        defaults: { level: 60, status: 'متوفر' },
      }}
      detail={{
        titleKey: 'name',
        subKey: 'category',
        badgeKey: 'status',
        tones: STOCK_TONES,
        fields: [
          { label: 'الكمية الحالية', key: 'qty' },
          { label: 'الحد الأدنى', key: 'min' },
          { label: 'تاريخ الانتهاء', key: 'expiry' },
          { label: 'المورد', key: 'supplier' },
        ],
        actions: [{ label: 'إعادة الطلب', icon: 'truck', variant: 'blue', to: '/pharmacy/orders' }, { label: 'تعديل الكمية', icon: 'edit', variant: 'ghost' }],
      }}
    />
  )
}

export function PharmacyOrders() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'draft', label: 'مسودات', match: (r) => r.status === 'مسودة' },
        { key: 'sent', label: 'مُرسلة', match: (r) => r.status === 'مُرسل' },
        { key: 'recv', label: 'مستلمة', match: (r) => r.status === 'مستلم' || r.status === 'مستلم جزئيًا' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الطلب' },
        { key: 'supplier', header: 'المورد' },
        { key: 'items', header: 'الأصناف' },
        { key: 'total', header: 'الإجمالي' },
        { key: 'date', header: 'التاريخ' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: PO_TONES },
      ]}
      rows={purchaseOrders}
      searchKeys={['no', 'supplier']}
      addLabel="طلب توريد جديد"
      form={{
        title: 'طلب توريد جديد',
        fields: [
          { name: 'supplier', label: 'المورد', options: ['الدوائية المتحدة', 'نماء الطبية', 'الشرق للأدوية'] },
          { name: 'items', label: 'عدد الأصناف', type: 'number', required: true },
          { name: 'total', label: 'القيمة التقديرية (ر.س)', type: 'number', required: true },
        ],
        defaults: { no: 'PO-5122', date: '2026-10-18', status: 'مسودة' },
      }}
      detail={{
        titleKey: 'no',
        subKey: 'supplier',
        badgeKey: 'status',
        tones: PO_TONES,
        fields: [
          { label: 'الأصناف', key: 'items' },
          { label: 'الإجمالي', key: 'total' },
          { label: 'التاريخ', key: 'date' },
        ],
        actions: [{ label: 'تسجيل استلام', icon: 'check', variant: 'blue' }, { label: 'إرسال للمورد', icon: 'mail', variant: 'ghost' }],
      }}
    />
  )
}

export function PharmacySales() {
  return (
    <ListPage
      stats={[
        { icon: 'wallet', label: 'مبيعات اليوم', value: '6,240 ر.س', delta: '+6%' },
        { icon: 'file', label: 'فواتير', value: '86', tone: 'orange' },
        { icon: 'users', label: 'عملاء', value: '71', tone: 'green' },
        { icon: 'close', label: 'مرتجعات', value: '2', tone: 'red' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'paid', label: 'مدفوعة', match: (r) => r.status === 'مدفوعة' },
        { key: 'ret', label: 'مرتجعات', match: (r) => r.status === 'مرتجع' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الفاتورة' },
        { key: 'patient', header: 'العميل' },
        { key: 'items', header: 'الأصناف' },
        { key: 'amount', header: 'المبلغ' },
        { key: 'method', header: 'الدفع' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: SALE_TONES },
      ]}
      rows={sales}
      searchKeys={['no', 'patient', 'items']}
      detail={{
        titleKey: 'no',
        subKey: 'patient',
        badgeKey: 'status',
        tones: SALE_TONES,
        fields: [
          { label: 'الأصناف', key: 'items' },
          { label: 'المبلغ', key: 'amount' },
          { label: 'طريقة الدفع', key: 'method' },
          { label: 'الوقت', key: 'time' },
        ],
        actions: [{ label: 'طباعة الفاتورة', icon: 'print', variant: 'blue' }, { label: 'إرجاع', variant: 'danger' }],
      }}
    />
  )
}

export function PharmacyReports() {
  return (
    <DashboardTemplate
      stats={[
        { icon: 'wallet', label: 'مبيعات الشهر', value: '163 ألف', delta: '+7%' },
        { icon: 'file', label: 'وصفات مصروفة', value: '1,842', tone: 'orange' },
        { icon: 'box', label: 'قيمة المخزون', value: '480 ألف', tone: 'green' },
        { icon: 'alert', label: 'أدوية منتهية', value: '0', tone: 'green' },
      ]}
      charts={[
        { title: 'المبيعات الشهرية (آلاف الريالات)', data: salesMonthly },
        { title: 'الصرف حسب الفئة العلاجية', data: topCategories },
      ]}
    />
  )
}
