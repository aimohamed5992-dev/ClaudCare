import RoleRouter from '../RoleRouter'
import { BranchAppointments, BranchDashboard, BranchDiagnostics, BranchReports, BranchRooms, BranchStaff } from './BranchPages'

// شاشات دور «branch»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <BranchDashboard />,
  appointments: <BranchAppointments />,
  staff: <BranchStaff />,
  rooms: <BranchRooms />,
  diagnostics: <BranchDiagnostics />,
  reports: <BranchReports />,
}

export default function BranchScreens() {
  return <RoleRouter screens={screens} />
}
