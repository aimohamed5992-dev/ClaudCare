import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Avatar, Badge, Button, Card, DataTable, EmptyState, Input, Modal, SearchBox, Select, StatCard, Tabs, Textarea } from '../ui'
import { toColumns } from './cells'
import './patterns.css'

const norm = (v) => String(v ?? '').toLowerCase()

/* لوحة التفاصيل الجانبية للصف المحدد */
function DetailCard({ row, config }) {
  const navigate = useNavigate()
  return (
    <Card title={config.title ?? 'التفاصيل'} className="detail">
      <div className="detail__head">
        <Avatar src={config.avatar ? row[config.avatar] : undefined} name={String(row[config.titleKey])} size={52} />
        <div>
          <b>{row[config.titleKey]}</b>
          {config.subKey && <small>{row[config.subKey]}</small>}
        </div>
        {config.badgeKey && <Badge tone={config.tones?.[row[config.badgeKey]] ?? 'gray'} dot>{row[config.badgeKey]}</Badge>}
      </div>
      <dl className="detail__list">
        {config.fields.map((f) => (
          <div key={f.key}>
            <dt>{f.label}</dt>
            <dd>{row[f.key] ?? '—'}</dd>
          </div>
        ))}
      </dl>
      {config.actions && (
        <div className="detail__actions">
          {config.actions.map((a) => (
            <Button key={a.label} variant={a.variant ?? 'soft'} icon={a.icon} full onClick={a.to ? () => navigate(a.to) : undefined}>
              {a.label}
            </Button>
          ))}
        </div>
      )}
    </Card>
  )
}

/* نموذج الإضافة */
function AddForm({ form, onClose, onSubmit }) {
  const initial = Object.fromEntries(form.fields.map((f) => [f.name, f.options?.[0] ?? '']))
  const [values, setValues] = useState(initial)
  const [errors, setErrors] = useState({})
  const set = (name) => (e) => setValues((v) => ({ ...v, [name]: e.target.value }))

  const submit = () => {
    const next = {}
    form.fields.forEach((f) => {
      if (f.required && !String(values[f.name]).trim()) next[f.name] = 'هذا الحقل مطلوب'
    })
    setErrors(next)
    if (Object.keys(next).length === 0) onSubmit(values)
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={form.title}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>إلغاء</Button>
          <Button onClick={submit}>{form.submit ?? 'حفظ'}</Button>
        </>
      }
    >
      {form.fields.map((f) => {
        const common = { label: f.label, value: values[f.name], onChange: set(f.name), error: errors[f.name], placeholder: f.placeholder }
        if (f.options) return <Select key={f.name} {...common} options={f.options} />
        if (f.type === 'textarea') return <Textarea key={f.name} {...common} />
        return <Input key={f.name} {...common} type={f.type ?? 'text'} />
      })}
    </Modal>
  )
}

/**
 * قالب صفحة القائمة: إحصاءات اختيارية + تبويبات + بحث + جدول + تفاصيل + نموذج إضافة.
 * columns: انظر cells.jsx · tabs: [{ key, label, match(row) }] · searchKeys: مفاتيح البحث
 * form: { title, fields:[{name,label,options?,required?}], defaults } · detail: انظر DetailCard
 */
export default function ListPage({ stats, tabs, columns, rows: initialRows, searchKeys = [], addLabel, form, detail, emptyImage = 'empty-search' }) {
  const [rows, setRows] = useState(initialRows)
  const [tab, setTab] = useState(tabs?.[0]?.key)
  const [query, setQuery] = useState('')
  const [selectedId, setSelectedId] = useState(null)
  const [adding, setAdding] = useState(false)

  const tabItems = useMemo(() => tabs?.map((t) => ({ key: t.key, label: t.label, count: rows.filter(t.match).length })), [tabs, rows])

  const filtered = useMemo(() => {
    const active = tabs?.find((t) => t.key === tab)
    const q = norm(query).trim()
    return rows.filter((r) => (!active || active.match(r)) && (!q || searchKeys.some((k) => norm(r[k]).includes(q))))
  }, [rows, tabs, tab, query, searchKeys])

  const selected = filtered.find((r) => r.id === selectedId) ?? filtered[0]

  const add = (values) => {
    const row = { id: Date.now(), ...form.defaults, ...values }
    setRows((r) => [row, ...r])
    setSelectedId(row.id)
    setAdding(false)
    setQuery('')
    if (tabs) setTab(tabs[0].key)
  }

  return (
    <div className="list">
      {stats && (
        <div className="dash__stats">
          {stats.map((s) => <StatCard key={s.label} {...s} />)}
        </div>
      )}

      <div className="list__bar">
        {tabItems && <Tabs items={tabItems} value={tab} onChange={setTab} />}
        <span className="list__spacer" />
        <SearchBox className="list__search" value={query} onChange={(e) => setQuery(e.target.value)} />
        {addLabel && <Button icon="plus" onClick={() => setAdding(true)}>{addLabel}</Button>}
      </div>

      <div className={detail ? 'list__body list__body--detail' : 'list__body'}>
        <Card padded={false}>
          {filtered.length === 0 ? (
            <EmptyState image={emptyImage} title="لا توجد نتائج" text="جرّب تغيير البحث أو التصفية." />
          ) : (
            <DataTable columns={toColumns(columns)} rows={filtered} selectedId={detail ? selected?.id : undefined} onRowClick={detail ? (r) => setSelectedId(r.id) : undefined} />
          )}
        </Card>
        {detail && selected && <DetailCard row={selected} config={detail} />}
      </div>

      {adding && form && <AddForm form={form} onClose={() => setAdding(false)} onSubmit={add} />}
    </div>
  )
}
