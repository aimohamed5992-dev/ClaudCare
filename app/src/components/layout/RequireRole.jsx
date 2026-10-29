import { Navigate, useParams } from 'react-router-dom'
import useAuth from '../../hooks/useAuth'
import { getRole } from '../../data/roles'
import NotFound from '../../pages/NotFound'

// يحمي مسارات /:role: يتطلب تسجيل دخول ثم دورًا مطابقًا للمسار (دور الحساب من الخادم)
export default function RequireRole({ children }) {
  const { signedIn, role, loading } = useAuth()
  const { role: param } = useParams()

  if (!getRole(param)) return <NotFound />
  if (loading) return null
  if (!signedIn) return <Navigate to="/login" replace />
  if (role.key !== param) return <Navigate to={`/${role.key}`} replace />
  return children
}
