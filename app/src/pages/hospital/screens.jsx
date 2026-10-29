import RoleRouter from '../RoleRouter'
import { HospitalBeds, HospitalBranches, HospitalDashboard, HospitalDepartments, HospitalFinance, HospitalReports, HospitalStaff } from './HospitalPages'

// شاشات دور «hospital»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <HospitalDashboard />,
  branches: <HospitalBranches />,
  departments: <HospitalDepartments />,
  staff: <HospitalStaff />,
  beds: <HospitalBeds />,
  finance: <HospitalFinance />,
  reports: <HospitalReports />,
}

export default function HospitalScreens() {
  return <RoleRouter screens={screens} />
}
