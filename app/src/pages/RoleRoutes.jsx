import { Suspense } from 'react'
import useAuth from '../hooks/useAuth'
import PageSkeleton from '../components/patterns/PageSkeleton'
import { roleScreens } from './roleScreens'

// يحمّل شاشات الدور الحالي عند الحاجة ويعرض هيكل تحميل أثناء الانتظار
export default function RoleRoutes() {
  const { role } = useAuth()
  const Screens = roleScreens[role.key]
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Screens />
    </Suspense>
  )
}
