import { useLocation } from 'react-router-dom'
import { EmptyState } from '../components/ui'
import useAuth from '../hooks/useAuth'

// مكان مؤقت لأي صفحة لم تُبنَ بعد – يُستبدل تدريجيًا في الخطوات 4–9
export default function PageStub() {
  const { role } = useAuth()
  const { pathname } = useLocation()
  return (
    <div className="stub">
      <EmptyState
        image="empty-results"
        title="هذه الصفحة قيد البناء"
        text={`${role.label} · ${pathname}`}
      />
    </div>
  )
}
