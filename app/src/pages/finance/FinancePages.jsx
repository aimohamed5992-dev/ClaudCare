import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Avatar, Badge, Button, Card, Checkbox, Icon, Input, Select, Textarea } from '../../components/ui'
import { EXPENSE_CATEGORIES, closeExpected, expenses, fmtInvoice, invoices, money, payroll, revenueByDept, revenueMonthly } from '../../data/finance'
import { cx } from '../../utils/cx'
import './finance.css'
import '../reception/reception.css'

const INV_TONES = { 'غير مدفوعة': 'orange', 'مدفوعة جزئيًا': 'blue', مدفوعة: 'green', متأخرة: 'red' }
const EXP_TONES = { 'بانتظار الاعتماد': 'orange', معتمد: 'blue', مدفوع: 'green', مرفوض: 'red' }
const PAY_TONES = { مسودة: 'gray', 'قيد الاعتماد': 'orange', مصروف: 'green' }

export function FinanceDashboard() {
  const navigate = useNavigate()
  return (
    <DashboardTemplate
      banner={{ title: 'اللوحة المالية', text: 'فاتورة متأخرة واحدة، و12,500 ر.س مصروفات بانتظار الاعتماد.', cta: 'تحصيل فاتورة', to: '/finance/invoices', image: 'role-finance.png' }}
      stats={[
        { icon: 'wallet', label: 'إيرادات اليوم', value: '88,550 ر.س', delta: '+5%' },
        { icon: 'file', label: 'مستحقات غير محصّلة', value: '27,550 ر.س', tone: 'orange' },
        { icon: 'alert', label: 'فواتير متأخرة', value: '1', tone: 'red' },
        { icon: 'chart', label: 'صافي الشهر', value: '1.1 مليون', delta: '+8%', tone: 'green' },
      ]}
      table={{
        title: 'فواتير تحتاج تحصيلًا',
        columns: [
          { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'no' },
          { key: 'remainingText', header: 'المتبقي' },
          { key: 'due', header: 'الاستحقاق' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: INV_TONES },
        ],
        rows: invoices.filter((i) => i.status !== 'مدفوعة').map(fmtInvoice),
      }}
      side={{
        title: 'يحتاج انتباهك',
        items: [
          { mark: '!', title: 'INV-8801 متأخرة 18 يومًا', subtitle: 'يوسف خالد · 9,500 ر.س', tone: 'red', onClick: () => navigate('/finance/invoices') },
          { mark: '1', title: 'مصروف بانتظار الاعتماد', subtitle: 'صيانة جهاز الأشعة', tone: 'orange', onClick: () => navigate('/finance/expenses') },
          { mark: '3', title: 'رواتب لم تُصرف', subtitle: 'مسودة وقيد الاعتماد', tone: 'orange', onClick: () => navigate('/finance/payroll') },
        ],
      }}
      charts={[
        { title: 'الإيرادات الشهرية (ملايين الريالات)', data: revenueMonthly },
        { title: 'الإيرادات حسب القسم (آلاف الريالات)', data: revenueByDept },
      ]}
    />
  )
}

export function FinanceInvoices() {
  return (
    <ListPage
      stats={[
        { icon: 'file', label: 'فواتير مفتوحة', value: '4', tone: 'orange' },
        { icon: 'alert', label: 'متأخرة', value: '1', tone: 'red' },
        { icon: 'wallet', label: 'المتبقي للتحصيل', value: '31,550 ر.س' },
        { icon: 'check', label: 'محصّل هذا الأسبوع', value: '11,000 ر.س', tone: 'green' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'open', label: 'غير مدفوعة', match: (r) => r.status === 'غير مدفوعة' || r.status === 'مدفوعة جزئيًا' },
        { key: 'late', label: 'متأخرة', match: (r) => r.status === 'متأخرة' },
        { key: 'paid', label: 'مدفوعة', match: (r) => r.status === 'مدفوعة' },
      ]}
      columns={[
        { key: 'no', header: 'رقم الفاتورة' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'service' },
        { key: 'totalText', header: 'الإجمالي' },
        { key: 'remainingText', header: 'المتبقي' },
        { key: 'due', header: 'الاستحقاق' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: INV_TONES },
      ]}
      rows={invoices.map(fmtInvoice)}
      searchKeys={['no', 'patient', 'service']}
      detail={{
        titleKey: 'patient',
        subKey: 'no',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: INV_TONES,
        fields: [
          { label: 'الخدمة', key: 'service' },
          { label: 'الإجمالي', key: 'totalText' },
          { label: 'المتبقي', key: 'remainingText' },
          { label: 'الاستحقاق', key: 'due' },
        ],
        actions: [{ label: 'تحصيل', icon: 'wallet', variant: 'blue', to: '/finance/collect' }, { label: 'طباعة الفاتورة', icon: 'print', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- تحصيل فاتورة ---------- */
const COLLECT_INV = invoices[1] // INV-8839: المتبقي 6,800
const MAX_DISCOUNT = 0.1

export function FinanceCollect() {
  const navigate = useNavigate()
  const inv = COLLECT_INV
  const remaining = inv.total - inv.paid
  const [amount, setAmount] = useState(null) // null = المتبقي كاملًا
  const [method, setMethod] = useState('بطاقة')
  const [discount, setDiscount] = useState('')
  const [reason, setReason] = useState('')
  const [cash, setCash] = useState('')
  const [sendCopy, setSendCopy] = useState(true)
  const [receipt, setReceipt] = useState(null)

  const disc = Number(discount) || 0
  const due = Math.max(0, remaining - disc)
  const pay = amount ?? String(due)
  const payN = Number(pay)
  const cashN = Number(cash)
  const change = method === 'نقدًا' && cashN >= payN ? cashN - payN : 0

  const reasons = [
    disc < 0 && 'الخصم لا يمكن أن يكون سالبًا',
    disc > remaining * MAX_DISCOUNT && `الخصم يتجاوز ${MAX_DISCOUNT * 100}% من المتبقي (يحتاج اعتماد المدير)`,
    disc > 0 && !reason.trim() && 'اكتب سبب الخصم',
    (!Number.isFinite(payN) || payN <= 0) && 'أدخل مبلغًا صحيحًا',
    payN > due && 'المبلغ أكبر من المستحق',
    method === 'نقدًا' && cashN < payN && 'المبلغ المستلم أقل من المطلوب',
  ].filter(Boolean)

  if (receipt) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>تم التحصيل</h2>
        <p>
          {inv.patient} · <b dir="ltr">{inv.no}</b> · المبلغ <b>{money(receipt.pay)}</b> ({receipt.method})
        </p>
        <Badge tone={receipt.left > 0 ? 'orange' : 'green'} dot>{receipt.left > 0 ? `المتبقي ${money(receipt.left)}` : 'الفاتورة مسدّدة بالكامل'}</Badge>
        {receipt.change > 0 && <p>الباقي للعميل: <b>{money(receipt.change)}</b></p>}
        <div className="reg__actions">
          <Button variant="blue" onClick={() => navigate('/finance/invoices')}>العودة للفواتير</Button>
          <Button variant="ghost" icon="print">طباعة الإيصال</Button>
        </div>
      </Card>
    )
  }

  const confirm = () => setReceipt({ pay: payN, method, change, left: Math.max(0, due - payN) })

  return (
    <div className="col">
      <div className="col__main">
        <Card className="col__head">
          <Avatar src={inv.avatar} name={inv.patient} size={56} />
          <div>
            <h3>{inv.patient}</h3>
            <small><span dir="ltr">{inv.no}</span> · {inv.service}</small>
          </div>
          <Badge tone={INV_TONES[inv.status]} dot>{inv.status}</Badge>
        </Card>
        <Card title="تفاصيل الدفع">
          <div className="col__form">
            <div className="col__row">
              <Select label="طريقة الدفع" options={['بطاقة', 'نقدًا', 'تحويل بنكي']} value={method} onChange={(e) => setMethod(e.target.value)} />
              <Input label="المبلغ المحصّل (ر.س)" inputMode="decimal" value={pay} onChange={(e) => setAmount(e.target.value)} />
            </div>
            {method === 'نقدًا' && <Input label="المبلغ المستلم نقدًا (ر.س)" inputMode="decimal" value={cash} onChange={(e) => setCash(e.target.value)} hint={change > 0 ? `الباقي: ${money(change)}` : undefined} />}
            <div className="col__row">
              <Input label="خصم (ر.س)" inputMode="decimal" value={discount} onChange={(e) => { setDiscount(e.target.value); setAmount(null) }} hint="حتى 10% من المتبقي" />
              <Input label="سبب الخصم" value={reason} onChange={(e) => setReason(e.target.value)} disabled={disc <= 0} />
            </div>
            <Checkbox label="إرسال نسخة الإيصال للمريض عبر الرسائل" checked={sendCopy} onChange={(e) => setSendCopy(e.target.checked)} />
          </div>
        </Card>
      </div>
      <aside className="col__side">
        <Card title="ملخص الفاتورة">
          <dl className="detail__list">
            <div><dt>الإجمالي</dt><dd>{money(inv.total)}</dd></div>
            <div><dt>المدفوع سابقًا</dt><dd>{money(inv.paid)}</dd></div>
            <div><dt>الخصم</dt><dd>{money(disc)}</dd></div>
            <div><dt>المستحق الآن</dt><dd className="col__big">{money(due)}</dd></div>
          </dl>
          <div className="col__form" style={{ marginTop: 16 }}>
            {reasons.length > 0 && <ul className="col__reasons" aria-live="polite">{reasons.map((r) => <li key={r}>{r}</li>)}</ul>}
            <Button icon="check" size="lg" full disabled={reasons.length > 0} onClick={confirm}>تأكيد التحصيل</Button>
            <Button variant="ghost" full onClick={() => navigate('/finance/invoices')}>إلغاء</Button>
          </div>
        </Card>
      </aside>
    </div>
  )
}

export function FinanceExpenses() {
  return (
    <ListPage
      stats={[
        { icon: 'wallet', label: 'مصروفات الشهر', value: '186 ألف ر.س' },
        { icon: 'clock', label: 'بانتظار الاعتماد', value: '1', tone: 'orange' },
        { icon: 'check', label: 'معتمدة', value: '1', tone: 'green' },
        { icon: 'close', label: 'مرفوضة', value: '1', tone: 'red' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'wait', label: 'بانتظار الاعتماد', match: (r) => r.status === 'بانتظار الاعتماد' },
        { key: 'ok', label: 'معتمدة', match: (r) => r.status === 'معتمد' || r.status === 'مدفوع' },
        { key: 'no', label: 'مرفوضة', match: (r) => r.status === 'مرفوض' },
      ]}
      columns={[
        { key: 'no', header: 'الرقم' },
        { key: 'title', header: 'البند' },
        { key: 'category', header: 'الفئة' },
        { key: 'amount', header: 'المبلغ' },
        { key: 'date', header: 'التاريخ' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: EXP_TONES },
      ]}
      rows={expenses}
      searchKeys={['no', 'title', 'category', 'by']}
      addLabel="تسجيل مصروف"
      form={{
        title: 'تسجيل مصروف',
        fields: [
          { name: 'title', label: 'البند', required: true },
          { name: 'category', label: 'الفئة', options: EXPENSE_CATEGORIES },
          { name: 'amount', label: 'المبلغ (ر.س)', type: 'number', required: true },
          { name: 'by', label: 'الجهة الطالبة', required: true },
        ],
        defaults: { no: 'EX-2211', date: '2026-10-18', status: 'بانتظار الاعتماد' },
      }}
      detail={{
        titleKey: 'title',
        subKey: 'no',
        badgeKey: 'status',
        tones: EXP_TONES,
        fields: [
          { label: 'الفئة', key: 'category' },
          { label: 'المبلغ', key: 'amount' },
          { label: 'الجهة الطالبة', key: 'by' },
          { label: 'التاريخ', key: 'date' },
        ],
        actions: [{ label: 'اعتماد', icon: 'check', variant: 'blue' }, { label: 'رفض', variant: 'danger' }],
      }}
    />
  )
}

export function FinancePayroll() {
  return (
    <ListPage
      stats={[
        { icon: 'users', label: 'موظفون', value: '312' },
        { icon: 'wallet', label: 'إجمالي الرواتب', value: '2.4 مليون ر.س' },
        { icon: 'clock', label: 'قيد الاعتماد', value: '2', tone: 'orange' },
        { icon: 'check', label: 'مصروفة', value: '2', tone: 'green' },
      ]}
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'draft', label: 'مسودة', match: (r) => r.status === 'مسودة' },
        { key: 'wait', label: 'قيد الاعتماد', match: (r) => r.status === 'قيد الاعتماد' },
        { key: 'paid', label: 'مصروفة', match: (r) => r.status === 'مصروف' },
      ]}
      columns={[
        { key: 'name', header: 'الموظف', type: 'person', avatar: 'avatar', sub: 'role' },
        { key: 'base', header: 'الأساسي' },
        { key: 'extras', header: 'إضافات' },
        { key: 'deductions', header: 'خصومات' },
        { key: 'net', header: 'الصافي' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: PAY_TONES },
      ]}
      rows={payroll}
      searchKeys={['name', 'role']}
      detail={{
        titleKey: 'name',
        subKey: 'role',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: PAY_TONES,
        fields: [
          { label: 'الراتب الأساسي', key: 'base' },
          { label: 'الإضافات', key: 'extras' },
          { label: 'الخصومات', key: 'deductions' },
          { label: 'الصافي', key: 'net' },
        ],
        actions: [{ label: 'اعتماد الصرف', icon: 'check', variant: 'blue' }, { label: 'كشف الراتب', icon: 'print', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- الإقفال اليومي ---------- */
export function FinanceClose() {
  const [counted, setCounted] = useState({ cash: '', card: '' })
  const [note, setNote] = useState('')
  const [reviewed, setReviewed] = useState(false)
  const [done, setDone] = useState(false)

  const lines = closeExpected.map((l) => {
    if (!l.counted) return { ...l, diff: 0, filled: true }
    const raw = counted[l.key]
    const n = Number(raw)
    const filled = raw !== '' && Number.isFinite(n) && n >= 0
    return { ...l, diff: filled ? n - l.expected : 0, filled }
  })
  const totalDiff = lines.reduce((s, l) => s + l.diff, 0)
  const hasDiff = lines.some((l) => l.filled && l.diff !== 0)
  const reasons = [
    lines.some((l) => !l.filled) && 'أدخل المبلغ الفعلي للنقد والبطاقات',
    hasDiff && !note.trim() && 'اكتب سبب الفرق قبل الإقفال',
    !reviewed && 'أكّد مراجعة الفواتير الملغاة والمرتجعات',
  ].filter(Boolean)

  if (done) {
    return (
      <Card className="reg__done">
        <span className="reg__check"><Icon name="check" size={32} strokeWidth={2.6} /></span>
        <h2>تم إقفال اليوم المالي</h2>
        <p>إجمالي المقبوضات المتوقعة <b>{money(closeExpected.reduce((s, l) => s + l.expected, 0))}</b>{hasDiff && <> · الفرق <b>{money(totalDiff)}</b></>}</p>
        <Button variant="ghost" icon="print">طباعة تقرير الإقفال</Button>
      </Card>
    )
  }

  return (
    <div className="close">
      <div className="close__main">
        {lines.map((l) => (
          <Card key={l.key}>
            <div className="close__line">
              <div><b>{l.label}</b><small>المتوقع من النظام: {money(l.expected)}</small></div>
              {l.counted ? (
                <Input label="المبلغ الفعلي (ر.س)" inputMode="decimal" value={counted[l.key]} onChange={(e) => setCounted((c) => ({ ...c, [l.key]: e.target.value }))} />
              ) : (
                <div><small>لا يُعدّ يدويًا</small><span>يُسوّى مع شركات التأمين</span></div>
              )}
              <div>
                <small>الفرق</small>
                <span className={cx('close__diff', l.filled && (l.diff === 0 ? 'is-ok' : 'is-bad'))}>
                  {l.counted && !l.filled ? '—' : l.diff === 0 ? 'مطابق' : money(l.diff)}
                </span>
              </div>
            </div>
          </Card>
        ))}
      </div>
      <aside className="close__side">
        <Card title="إقفال اليوم">
          <dl className="detail__list">
            <div><dt>التاريخ</dt><dd>2026-10-18</dd></div>
            <div><dt>إجمالي المتوقع</dt><dd>{money(closeExpected.reduce((s, l) => s + l.expected, 0))}</dd></div>
            <div><dt>إجمالي الفرق</dt><dd>{money(totalDiff)}</dd></div>
          </dl>
          <div className="close__form">
            {hasDiff && <Textarea label="سبب الفرق" rows={3} value={note} onChange={(e) => setNote(e.target.value)} />}
            <Checkbox label="تمت مراجعة الفواتير الملغاة والمرتجعات" checked={reviewed} onChange={(e) => setReviewed(e.target.checked)} />
            {reasons.length > 0 && <ul className="col__reasons" aria-live="polite">{reasons.map((r) => <li key={r}>{r}</li>)}</ul>}
            <Button icon="lock" size="lg" full disabled={reasons.length > 0} onClick={() => setDone(true)}>إقفال اليوم</Button>
          </div>
        </Card>
      </aside>
    </div>
  )
}
