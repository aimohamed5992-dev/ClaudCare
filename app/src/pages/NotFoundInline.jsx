import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/ui'
import useAuth from '../hooks/useAuth'

// صفحة 404 داخل هيكل التطبيق (القائمة الجانبية ظاهرة)
export default function NotFoundInline() {
  const { role } = useAuth()
  const navigate = useNavigate()
  return (
    <EmptyState
      image="error-404"
      title="الصفحة غير موجودة"
      text="الرابط الذي طلبته غير صحيح أو تم نقله."
      action={{ label: 'العودة للرئيسية', onClick: () => navigate(`/${role.key}`) }}
    />
  )
}
