import { useId, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Icon } from '../ui'
import useAuth from '../../hooks/useAuth'
import { COMMON_PAGES, SUBPAGE_TITLES } from '../../data/roles'

// بحث سريع عن صفحات النظام المتاحة للدور الحالي (انتقال مباشر)
export default function GlobalSearch() {
  const { role } = useAuth()
  const navigate = useNavigate()
  const listId = useId()
  const [q, setQ] = useState('')
  const [active, setActive] = useState(0)

  const pages = useMemo(
    () => [
      ...role.nav.map((n) => ({ label: n.label, icon: n.icon, to: n.path ? `/${role.key}/${n.path}` : `/${role.key}` })),
      ...Object.entries(COMMON_PAGES).map(([p, label]) => ({ label, icon: p === 'profile' ? 'user' : p === 'settings' ? 'settings' : 'bell', to: `/${role.key}/${p}` })),
      ...Object.entries(SUBPAGE_TITLES)
        .filter(([p]) => ['consultation', 'entry', 'viewer', 'dispense', 'review'].includes(p) && roleOwns(role.key, p))
        .map(([p, label]) => ({ label, icon: 'file', to: `/${role.key}/${p}` })),
    ],
    [role],
  )
  const query = q.trim()
  const results = query ? pages.filter((p) => p.label.includes(query)) : []

  const go = (r) => {
    setQ('')
    navigate(r.to)
  }
  const onKeyDown = (e) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setActive((i) => Math.min(i + 1, results.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((i) => Math.max(i - 1, 0)) }
    else if (e.key === 'Enter' && results[active]) { e.preventDefault(); go(results[active]) }
    else if (e.key === 'Escape') setQ('')
  }

  return (
    <div className="gsearch">
      <label className="searchbox topbar__search">
        <Icon name="search" size={18} />
        <input
          type="search"
          role="combobox"
          aria-expanded={!!query}
          aria-controls={listId}
          aria-label="بحث في صفحات النظام"
          placeholder="انتقل إلى صفحة..."
          value={q}
          onChange={(e) => { setQ(e.target.value); setActive(0) }}
          onKeyDown={onKeyDown}
        />
      </label>
      {query && (
        <ul id={listId} className="gsearch__list" role="listbox">
          {results.length === 0 && <li className="gsearch__empty" role="status">لا توجد صفحة بهذا الاسم</li>}
          {results.map((r, i) => (
            <li key={r.to} role="option" aria-selected={i === active}>
              <button className={i === active ? 'is-on' : undefined} onMouseDown={(e) => e.preventDefault()} onClick={() => go(r)}>
                <Icon name={r.icon} size={18} />
                {r.label}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// الصفحات الفرعية الخاصة بأدوار محددة
function roleOwns(roleKey, page) {
  return { consultation: 'doctor', entry: 'lab', viewer: 'radiology', dispense: 'pharmacy', review: 'insurance', collect: 'finance', admit: 'admissions' }[page] === roleKey
}
