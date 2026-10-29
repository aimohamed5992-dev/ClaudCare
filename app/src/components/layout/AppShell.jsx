import { useEffect, useState } from 'react'
import { useLocation } from 'react-router-dom'
import NotificationsProvider from '../../context/NotificationsProvider'
import Sidebar from './Sidebar'
import Topbar from './Topbar'
import useAuth from '../../hooks/useAuth'
import { COMMON_PAGES, SUBPAGE_TITLES } from '../../data/roles'
import { hydrate } from '../../api/hydrate'
import './layout.css'

export default function AppShell({ children }) {
  const { role } = useAuth()
  const { pathname } = useLocation()
  const [open, setOpen] = useState(false)
  // رقم يتغير حين تتغير بيانات الدور على الخادم فتُعاد الصفحة الحالية ببيانات محدّثة
  const [dataVersion, setDataVersion] = useState(0)

  const segment = pathname.split('/')[2] ?? ''
  const title = role.nav.find((n) => n.path === segment)?.label ?? COMMON_PAGES[segment] ?? SUBPAGE_TITLES[segment] ?? role.label

  // تحديث البيانات عند كل تنقل (وكل دقيقة)؛ لا نعيد البناء إلا إن تغيّر شيء فعلًا
  useEffect(() => {
    let live = true
    const refresh = () => hydrate(role.key).then((r) => live && r.changed && setDataVersion((v) => v + 1)).catch(() => {})
    refresh()
    const t = setInterval(refresh, 60_000)
    return () => {
      live = false
      clearInterval(t)
    }
  }, [role.key, pathname])

  useEffect(() => {
    document.title = `${title} – ClaudCare`
  }, [title])

  return (
    <NotificationsProvider key={role.key} roleKey={role.key}>
      <div className="shell">
        <a href="#main" className="skip-link">تخطَّ إلى المحتوى</a>
        <Sidebar open={open} onNavigate={() => setOpen(false)} />
        {open && <div className="shell__scrim" onClick={() => setOpen(false)} />}
        <div className="shell__main">
          <Topbar title={title} onMenu={() => setOpen(true)} />
          <main id="main" tabIndex={-1} className="shell__content" key={dataVersion}>{children}</main>
        </div>
      </div>
    </NotificationsProvider>
  )
}
