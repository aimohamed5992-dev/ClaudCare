// عميل HTTP للـ API: رأس Bearer، تجديد الرمز تلقائيًا عند 401 (مرة واحدة دون تكرار متزامن)، وأخطاء موحدة.
export const API_URL = (import.meta.env.VITE_API_URL ?? 'http://localhost:5080').replace(/\/$/, '')

const KEY_ACCESS = 'claudcare.access'
const KEY_REFRESH = 'claudcare.refresh'

const store = {
  get: (k) => {
    try {
      return localStorage.getItem(k)
    } catch {
      return null
    }
  },
  set: (k, v) => {
    try {
      if (v == null) localStorage.removeItem(k)
      else localStorage.setItem(k, v)
    } catch {
      /* التخزين غير متاح */
    }
  },
}

export const tokens = {
  get access() {
    return store.get(KEY_ACCESS)
  },
  get refresh() {
    return store.get(KEY_REFRESH)
  },
  save(access, refresh) {
    store.set(KEY_ACCESS, access)
    store.set(KEY_REFRESH, refresh)
  },
  clear() {
    store.set(KEY_ACCESS, null)
    store.set(KEY_REFRESH, null)
  },
}

/** خطأ من الخادم (ProblemDetails) أو الشبكة (status = 0). */
export class ApiError extends Error {
  constructor(status, code, message, extra = {}) {
    super(message)
    this.status = status
    this.code = code
    this.errors = extra.errors ?? {}
    this.data = extra.data ?? {}
  }
  get userMessage() {
    const first = Object.values(this.errors).flat()[0]
    return first ?? this.message
  }
}

let onSessionExpired = null
export const setSessionExpiredHandler = (fn) => {
  onSessionExpired = fn
}

let refreshing = null
async function refreshTokens() {
  const rt = tokens.refresh
  if (!rt) return false
  try {
    const res = await fetch(`${API_URL}/api/auth/refresh`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ refreshToken: rt, deviceName: 'ClaudCare Web' }),
    })
    if (res.ok) {
      const j = await res.json()
      tokens.save(j.accessToken, j.refreshToken)
      return true
    }
    if (res.status === 400 || res.status === 401) tokens.clear()
  } catch {
    /* خلل شبكة مؤقت: لا نمسح الجلسة */
  }
  return false
}

async function parse(res) {
  const text = await res.text()
  let json = null
  try {
    json = text ? JSON.parse(text) : null
  } catch {
    json = null
  }
  if (res.ok) return json
  const body = json ?? {}
  const { title, detail, code, errors } = body
  const data = Object.fromEntries(Object.entries(body).filter(([k]) => !['type', 'title', 'status', 'detail', 'instance', 'traceId', 'code', 'errors'].includes(k)))
  throw new ApiError(res.status, code ?? `http_${res.status}`, title ?? detail ?? `حدث خطأ غير متوقع (${res.status})`, { errors, data })
}

async function send(method, path, { query, body, auth = true, retry = true } = {}) {
  const url = new URL(API_URL + path)
  Object.entries(query ?? {}).forEach(([k, v]) => v != null && url.searchParams.set(k, v))
  const headers = { accept: 'application/json' }
  if (body !== undefined) headers['content-type'] = 'application/json'
  if (auth && tokens.access) headers.authorization = `Bearer ${tokens.access}`
  let res
  try {
    res = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  } catch {
    throw new ApiError(0, 'network', 'تعذّر الاتصال بالخادم، تحقق من الشبكة.')
  }
  if (res.status === 401 && auth && retry && tokens.refresh) {
    if (await (refreshing ??= refreshTokens().finally(() => (refreshing = null)))) return send(method, path, { query, body, auth, retry: false })
    onSessionExpired?.()
  }
  return parse(res)
}

export const api = {
  get: (path, query) => send('GET', path, { query }),
  post: (path, body = {}, opts) => send('POST', path, { body, ...opts }),
  put: (path, body = {}) => send('PUT', path, { body }),
  patch: (path, body = {}) => send('PATCH', path, { body }),
  delete: (path) => send('DELETE', path),
}
