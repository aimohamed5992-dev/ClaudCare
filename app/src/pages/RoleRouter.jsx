import { Route, Routes } from 'react-router-dom'
import useAuth from '../hooks/useAuth'
import { COMMON_PAGES } from '../data/roles'
import { NotificationsPage, ProfilePage, SettingsPage } from './common/CommonPages'
import NotFoundInline from './NotFoundInline'
import PageStub from './PageStub'

// صفحات مشتركة لكل الأدوار
const common = {
  notifications: <NotificationsPage />,
  profile: <ProfilePage />,
  settings: <SettingsPage />,
}

// يبني مسارات دور واحد من خريطة الشاشات (screens) + القائمة الجانبية + الصفحات المشتركة.
// أي مسار في القائمة بلا شاشة يظهر كصفحة مؤقتة (PageStub).
export default function RoleRouter({ screens }) {
  const { role } = useAuth()
  const paths = [...new Set([...role.nav.map((n) => n.path), ...Object.keys(screens)])].filter(Boolean)

  return (
    <Routes>
      <Route index element={screens[''] ?? <PageStub />} />
      {paths.map((path) => (
        <Route key={path} path={path} element={screens[path] ?? <PageStub />} />
      ))}
      {Object.keys(COMMON_PAGES).map((p) => (
        <Route key={p} path={p} element={common[p] ?? <PageStub />} />
      ))}
      <Route path="*" element={<NotFoundInline />} />
    </Routes>
  )
}
