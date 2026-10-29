import { useState } from 'react'
import { Avatar, Badge, Button, Card, Icon } from '../ui'
import { cx } from '../../utils/cx'
import './patterns.css'

// محادثات: conversations = [{ id, name, role, avatar, unread, messages:[{ from:'me'|'them', text, time }] }]
export default function Messenger({ conversations: initial }) {
  const [convs, setConvs] = useState(initial)
  const [activeId, setActiveId] = useState(initial[0]?.id)
  const [draft, setDraft] = useState('')
  const active = convs.find((c) => c.id === activeId)

  const open = (id) => {
    setActiveId(id)
    setConvs((list) => list.map((c) => (c.id === id ? { ...c, unread: 0 } : c)))
  }

  const send = (e) => {
    e.preventDefault()
    const text = draft.trim()
    if (!text) return
    setConvs((list) => list.map((c) => (c.id === activeId ? { ...c, messages: [...c.messages, { from: 'me', text, time: 'الآن' }] } : c)))
    setDraft('')
  }

  return (
    <div className="msg">
      <Card title="المحادثات" className="msg__list">
        {convs.map((c) => {
          const last = c.messages[c.messages.length - 1]
          return (
            <button key={c.id} className={cx('msg__conv', c.id === activeId && 'is-on')} onClick={() => open(c.id)}>
              <Avatar src={c.avatar} name={c.name} size={42} />
              <span className="msg__meta">
                <b>{c.name}</b>
                <small>{last?.text}</small>
              </span>
              {c.unread > 0 && <Badge tone="orange">{c.unread}</Badge>}
            </button>
          )
        })}
      </Card>

      {active && (
        <Card padded={false} className="msg__thread">
          <header className="msg__head">
            <Avatar src={active.avatar} name={active.name} size={40} />
            <span><b>{active.name}</b><small>{active.role}</small></span>
          </header>
          <div className="msg__body" aria-live="polite">
            {active.messages.map((m, i) => (
              <div key={i} className={cx('msg__bubble', m.from === 'me' && 'is-me')}>
                {m.text}
                <small>{m.time}</small>
              </div>
            ))}
          </div>
          <form className="msg__form" onSubmit={send}>
            <input value={draft} onChange={(e) => setDraft(e.target.value)} placeholder="اكتب رسالتك..." aria-label="نص الرسالة" />
            <Button type="submit" icon="chevronLeft" aria-label="إرسال">إرسال</Button>
          </form>
        </Card>
      )}
      {!active && <Icon name="mail" />}
    </div>
  )
}
