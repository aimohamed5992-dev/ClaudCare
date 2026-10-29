import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import AuthProvider from './context/AuthProvider'
import AppShell from './components/layout/AppShell'
import RequireRole from './components/layout/RequireRole'
import { GuestOnly, RequireAuth } from './components/layout/guards'
import ForgotPassword from './pages/auth/ForgotPassword'
import Login from './pages/auth/Login'
import NotFound from './pages/NotFound'
import RoleRoutes from './pages/RoleRoutes'
import ErrorBoundary from './components/ErrorBoundary'

// صفحة مراجعة المكونات متاحة في وضع التطوير فقط
const UiShowcase = import.meta.env.DEV ? lazy(() => import('./pages/UiShowcase')) : null

export default function App() {
  return (
    <BrowserRouter>
      <ErrorBoundary>
      <AuthProvider>
        <Routes>
          <Route path="/" element={<Navigate to="/login" replace />} />

          {/* ضيف */}
          <Route path="/login" element={<GuestOnly><Login /></GuestOnly>} />
          <Route path="/forgot-password" element={<GuestOnly><ForgotPassword /></GuestOnly>} />
          <Route path="/reset-password" element={<Navigate to="/forgot-password" replace />} />

          {/* بعد الدخول */}
          {/* الدور يأتي من الحساب مباشرة؛ المسار القديم يحوّل إلى لوحة الدور */}
          <Route path="/roles" element={<RequireAuth><Navigate to="/" replace /></RequireAuth>} />

          {UiShowcase && (
            <Route path="/ui" element={<Suspense fallback={null}><UiShowcase /></Suspense>} />
          )}

          <Route
            path="/:role/*"
            element={
              <RequireRole>
                <AppShell>
                  <RoleRoutes />
                </AppShell>
              </RequireRole>
            }
          />

          <Route path="*" element={<NotFound />} />
        </Routes>
      </AuthProvider>
      </ErrorBoundary>
    </BrowserRouter>
  )
}
