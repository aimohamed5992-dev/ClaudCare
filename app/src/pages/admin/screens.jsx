import RoleRouter from '../RoleRouter'
import { AdminDashboard, AdminHospitals, AdminReports, AdminSubscriptions, AdminSystem, AdminUsers } from './AdminPages'

// شاشات دور «admin»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <AdminDashboard />,
  hospitals: <AdminHospitals />,
  users: <AdminUsers />,
  subscriptions: <AdminSubscriptions />,
  reports: <AdminReports />,
  system: <AdminSystem />,
}

export default function AdminScreens() {
  return <RoleRouter screens={screens} />
}
