import { useCallback, useEffect, useMemo, useState } from 'react'
import { AuthContext } from './auth-context'
import { getRole } from '../data/roles'
import { authApi } from '../api/auth'
import { setSessionExpiredHandler } from '../api/client'
import { clearHydrated, hydrate } from '../api/hydrate'

const toSession = (u) => {
  const role = getRole(u.role.toLowerCase())
  if (!role) return null
  return { role, user: { id: u.id, name: u.fullName, email: u.email, phone: u.phone, avatar: u.avatarKey ? `${u.avatarKey}.jpg` : role.user.avatar } }
}

// جلسة حقيقية: الدور يأتي من الخادم (لا اختيار يدوي)، والرموز تُحفظ وتُجدَّد تلقائيًا.
export default function AuthProvider({ children }) {
  const [session, setSession] = useState(null)
  const [loading, setLoading] = useState(() => authApi.hasSession())

  // استعادة الجلسة المحفوظة عند فتح الصفحة
  useEffect(() => {
    if (!authApi.hasSession()) return
    let live = true
    authApi
      .me()
      .then(async (u) => {
        const s = toSession(u)
        if (s) await hydrate(s.role.key).catch(() => {})
        if (live) setSession(s)
      })
      .catch(() => {})
      .finally(() => live && setLoading(false))
    return () => {
      live = false
    }
  }, [])

  useEffect(() => {
    setSessionExpiredHandler(() => setSession((s) => {
      if (s) clearHydrated(s.role.key)
      return null
    }))
    return () => setSessionExpiredHandler(null)
  }, [])

  // نملأ بيانات الدور قبل إظهار لوحته كي تُبنى الصفحات ببيانات حقيقية
  const accept = useCallback(async (u) => {
    const s = toSession(u)
    if (!s) throw new Error('دور غير مدعوم في هذه الواجهة')
    await hydrate(s.role.key).catch(() => {})
    setSession(s)
    return s.role.key
  }, [])

  /** يعيد { roleKey } عند النجاح أو { challenge } لطلب رمز التحقق. */
  const login = useCallback(
    async (identifier, password) => {
      const out = await authApi.login(identifier, password)
      return out.user ? { roleKey: await accept(out.user) } : { challenge: out.challenge }
    },
    [accept],
  )

  const verifyOtp = useCallback(async (challengeToken, code) => ({ roleKey: await accept(await authApi.verifyLogin(challengeToken, code)) }), [accept])

  const logout = useCallback(async () => {
    await authApi.logout()
    setSession((s) => {
      if (s) clearHydrated(s.role.key)
      return null
    })
  }, [])

  const value = useMemo(
    () => ({ signedIn: Boolean(session), loading, role: session?.role, user: session?.user ?? null, login, verifyOtp, logout }),
    [session, loading, login, verifyOtp, logout],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
