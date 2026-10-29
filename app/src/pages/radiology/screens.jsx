import RoleRouter from '../RoleRouter'
import { RadiologyDashboard, RadiologyReports, RadiologyRequests, RadiologySchedule, RadiologyStats, RadiologyViewer } from './RadiologyPages'

// شاشات دور «radiology»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <RadiologyDashboard />,
  requests: <RadiologyRequests />,
  reports: <RadiologyReports />,
  schedule: <RadiologySchedule />,
  stats: <RadiologyStats />,
  viewer: <RadiologyViewer />,
}

export default function RadiologyScreens() {
  return <RoleRouter screens={screens} />
}
