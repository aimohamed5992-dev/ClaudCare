import { useCallback, useEffect, useMemo, useState } from 'react'
import { NotificationsContext } from './notifications-context'
import { api } from '../api/client'
import { mapNotifications } from '../api/mappers'

// إشعارات المستخدم من الخادم: تتحدّث كل 30 ثانية، والتعديلات متفائلة مع إعادة الجلب عند الفشل.
export default function NotificationsProvider({ children }) {
  const [items, setItems] = useState([])

  const load = useCallback(() => api.get('/api/notifications', { pageSize: 50 }).then((j) => setItems(mapNotifications(j))).catch(() => {}), [])

  useEffect(() => {
    load()
    const t = setInterval(load, 30_000)
    return () => clearInterval(t)
  }, [load])

  const markRead = useCallback((id) => {
    setItems((l) => l.map((n) => (n.id === id ? { ...n, unread: false } : n)))
    api.post(`/api/notifications/${id}/read`).catch(load)
  }, [load])
  const markAllRead = useCallback(() => {
    setItems((l) => l.map((n) => ({ ...n, unread: false })))
    api.post('/api/notifications/read-all').catch(load)
  }, [load])
  const remove = useCallback((id) => {
    setItems((l) => l.filter((n) => n.id !== id))
    api.delete(`/api/notifications/${id}`).catch(load)
  }, [load])

  const value = useMemo(() => ({ items, unread: items.filter((n) => n.unread).length, markRead, markAllRead, remove }), [items, markRead, markAllRead, remove])
  return <NotificationsContext.Provider value={value}>{children}</NotificationsContext.Provider>
}
