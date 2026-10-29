import { useState } from 'react'
import { Button, Card, Input, Select, Switch } from '../ui'
import './patterns.css'

// قالب الإعدادات: أقسام بداخلها عناصر switch | input | select
export default function SettingsTemplate({ sections }) {
  const initial = Object.fromEntries(sections.flatMap((s) => s.items.map((i) => [i.name, i.value])))
  const [values, setValues] = useState(initial)
  const [saved, setSaved] = useState(false)

  const set = (name, value) => {
    setSaved(false)
    setValues((v) => ({ ...v, [name]: value }))
  }

  return (
    <div className="settings">
      {sections.map((s) => (
        <Card key={s.title} title={s.title}>
          {s.desc && <p className="settings__desc">{s.desc}</p>}
          <div className="settings__items">
            {s.items.map((i) =>
              i.type === 'switch' ? (
                <div key={i.name} className="settings__switch">
                  <span>
                    <b>{i.label}</b>
                    {i.hint && <small>{i.hint}</small>}
                  </span>
                  <Switch checked={values[i.name]} onChange={(e) => set(i.name, e.target.checked)} aria-label={i.label} />
                </div>
              ) : i.type === 'select' ? (
                <Select key={i.name} label={i.label} options={i.options} value={values[i.name]} onChange={(e) => set(i.name, e.target.value)} />
              ) : (
                <Input key={i.name} label={i.label} value={values[i.name]} onChange={(e) => set(i.name, e.target.value)} />
              ),
            )}
          </div>
        </Card>
      ))}
      <div className="settings__save">
        {saved && <span className="settings__ok">تم حفظ التغييرات ✓</span>}
        <Button onClick={() => setSaved(true)}>حفظ التغييرات</Button>
      </div>
    </div>
  )
}
