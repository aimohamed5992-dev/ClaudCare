import { useState } from 'react'
import { asset } from '../../utils/assets'
import { cx } from '../../utils/cx'
import Icon from './Icon'
import Button from './Button'

/* ---------- Card ---------- */
export function Card({ title, action, children, padded = true, className, ...rest }) {
  return (
    <section className={cx('card', !padded && 'card--flush', className)} {...rest}>
      {(title || action) && (
        <header className="card__head">
          {title && <h3>{title}</h3>}
          {action}
        </header>
      )}
      {children}
    </section>
  )
}

/* ---------- Badge ---------- */
// tone: green | orange | blue | red | gray
export function Badge({ tone = 'blue', children, dot = false }) {
  return (
    <span className={cx('badge', `badge--${tone}`)}>
      {dot && <i className="badge__dot" />}
      {children}
    </span>
  )
}

/* ---------- Avatar ---------- */
export function Avatar({ src, name = '', size = 40, className }) {
  const [failed, setFailed] = useState(false)
  const [prevSrc, setPrevSrc] = useState(src)
  if (src !== prevSrc) {
    setPrevSrc(src)
    setFailed(false)
  }
  const isAbsolute = src && /^(https?:|\/|blob:|data:)/.test(src)
  const url = src && !isAbsolute ? asset(src) : src
  return (
    <span className={cx('avatar', className)} style={{ width: size, height: size, fontSize: size * 0.4 }}>
      {url && !failed ? <img src={url} alt={name} onError={() => setFailed(true)} /> : name.trim().charAt(0)}
    </span>
  )
}

/* ---------- StatCard ---------- */
export function StatCard({ icon, label, value, delta, tone = 'blue' }) {
  const negative = typeof delta === 'string' && delta.startsWith('-')
  return (
    <div className="stat">
      <div className="stat__top">
        <span className={cx('stat__icon', `stat__icon--${tone}`)}>
          <Icon name={icon} size={20} />
        </span>
        {delta && <em className={negative ? 'is-down' : 'is-up'}>{delta}</em>}
      </div>
      <b>{value}</b>
      <span>{label}</span>
    </div>
  )
}

/* ---------- ListItem ---------- */
export function ListItem({ mark, title, subtitle, tone = 'blue', onClick }) {
  const Tag = onClick ? 'button' : 'div'
  return (
    <Tag className="list-item" onClick={onClick}>
      <span className={cx('list-item__mark', `badge--${tone}`)}>{mark}</span>
      <span className="list-item__text">
        <b>{title}</b>
        {subtitle && <small>{subtitle}</small>}
      </span>
    </Tag>
  )
}

/* ---------- Tabs ---------- */
// تنقل بالأسهم: في الواجهة العربية السهم الأيسر = التالي
export function Tabs({ items, value, onChange, className }) {
  const onKeyDown = (e) => {
    const i = items.findIndex((t) => t.key === value)
    const rtl = document.dir === 'rtl'
    const step = { ArrowRight: rtl ? -1 : 1, ArrowLeft: rtl ? 1 : -1 }[e.key]
    let next = step != null ? (i + step + items.length) % items.length : e.key === 'Home' ? 0 : e.key === 'End' ? items.length - 1 : null
    if (next == null) return
    e.preventDefault()
    onChange(items[next].key)
    e.currentTarget.querySelectorAll('[role="tab"]')[next]?.focus()
  }
  return (
    <div className={cx('tabs', className)} role="tablist" onKeyDown={onKeyDown}>
      {items.map((t) => (
        <button
          key={t.key}
          role="tab"
          aria-selected={value === t.key}
          tabIndex={value === t.key ? 0 : -1}
          className={cx('tabs__item', value === t.key && 'is-on')}
          onClick={() => onChange(t.key)}
        >
          {t.label}
          {t.count != null && <span>{t.count}</span>}
        </button>
      ))}
    </div>
  )
}

/* ---------- EmptyState ---------- */
// image: اسم ملف من assets/empty مثل empty-search
export function EmptyState({ image = 'empty-search', title, text, action }) {
  return (
    <div className="empty">
      <img src={asset(`empty/${image}.png`)} alt="" />
      <h3>{title}</h3>
      {text && <p>{text}</p>}
      {action && <Button onClick={action.onClick}>{action.label}</Button>}
    </div>
  )
}

/* ---------- BarChart ---------- */
export function BarChart({ data, height = 140 }) {
  const max = Math.max(...data.map((d) => d.value), 1)
  return (
    <div className="bars" style={{ height }}>
      {data.map((d) => (
        <div key={d.label} className="bars__col">
          <div className={cx('bars__bar', d.value === max && 'is-max')} style={{ height: `${(d.value / max) * 100}%` }} title={d.value} />
          <span>{d.label}</span>
        </div>
      ))}
    </div>
  )
}

/* ---------- SearchBox ---------- */
export function SearchBox({ placeholder = 'بحث...', className, ...rest }) {
  return (
    <label className={cx('searchbox', className)}>
      <Icon name="search" size={18} />
      <input type="search" placeholder={placeholder} aria-label={placeholder} {...rest} />
    </label>
  )
}

/* ---------- Progress ---------- */
// tone تلقائي: أحمر ≥ 90، برتقالي ≥ 75، أزرق غير ذلك
export function Progress({ value, tone, label = true }) {
  const v = Math.max(0, Math.min(100, value))
  const t = tone ?? (v >= 90 ? 'red' : v >= 75 ? 'orange' : 'blue')
  return (
    <span className="progress">
      <span className="progress__track">
        <i className={cx('progress__bar', `progress__bar--${t}`)} style={{ width: `${v}%` }} />
      </span>
      {label && <small>{v}%</small>}
    </span>
  )
}
