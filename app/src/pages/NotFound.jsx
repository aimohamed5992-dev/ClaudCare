import { useNavigate } from 'react-router-dom'
import { EmptyState } from '../components/ui'

export default function NotFound() {
  const navigate = useNavigate()
  return (
    <div className="fullpage">
      <EmptyState
        image="error-404"
        title="الصفحة غير موجودة"
        text="الرابط الذي طلبته غير صحيح أو تم نقله."
        action={{ label: 'العودة للرئيسية', onClick: () => navigate('/') }}
      />
    </div>
  )
}
