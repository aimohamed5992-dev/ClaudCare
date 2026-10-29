import { Navigate } from 'react-router-dom'
import useAuth from '../../hooks/useAuth'

// صفحات الضيف (الدخول…): من سجّل دخوله يُحوَّل إلى لوحة دوره
export function GuestOnly({ children }) {
  const { signedIn, role, loading } = useAuth()
  if (loading) return null
  if (signedIn) return <Navigate to={`/${role.key}`} replace />
  return children
}

// صفحات تتطلب تسجيل دخول فقط
export function RequireAuth({ children }) {
  const { signedIn, loading } = useAuth()
  if (loading) return null
  if (!signedIn) return <Navigate to="/login" replace />
  return children
}
