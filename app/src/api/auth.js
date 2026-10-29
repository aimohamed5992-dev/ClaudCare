import { api, tokens } from './client'

const complete = (auth) => {
  tokens.save(auth.accessToken, auth.refreshToken)
  return auth.user
}

export const authApi = {
  /** يعيد { user } عند الدخول المباشر أو { challenge } عند طلب رمز التحقق (الطاقم). */
  async login(identifier, password) {
    const j = await api.post('/api/auth/login', { identifier: identifier.trim(), password, deviceName: 'ClaudCare Web' }, { auth: false })
    return j.status === 'authenticated' ? { user: complete(j.auth) } : { challenge: j.challenge }
  },
  async verifyLogin(challengeToken, code) {
    const j = await api.post('/api/auth/otp/verify', { challengeToken, code, deviceName: 'ClaudCare Web' }, { auth: false })
    return complete(j.auth)
  },
  async verifyReset(challengeToken, code) {
    const j = await api.post('/api/auth/otp/verify', { challengeToken, code }, { auth: false })
    return j.resetToken
  },
  resend: (challengeToken) => api.post('/api/auth/otp/resend', { challengeToken }, { auth: false }),
  forgot: (identifier) => api.post('/api/auth/password/forgot', { identifier: identifier.trim() }, { auth: false }),
  reset: (resetToken, newPassword) => api.post('/api/auth/password/reset', { resetToken, newPassword }, { auth: false }),
  me: () => api.get('/api/auth/me'),
  async logout() {
    const rt = tokens.refresh
    try {
      if (rt) await api.post('/api/auth/logout', { refreshToken: rt }, { auth: false })
    } catch {
      /* نمسح محليًا في كل الأحوال */
    }
    tokens.clear()
  },
  hasSession: () => Boolean(tokens.refresh),
}
