import RoleRouter from '../RoleRouter'
import { HrDashboard, HrEmployees, HrLeaves, HrLicenses, HrShifts } from './HrPages'

// شاشات دور «hr»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <HrDashboard />,
  employees: <HrEmployees />,
  leaves: <HrLeaves />,
  shifts: <HrShifts />,
  licenses: <HrLicenses />,
}

export default function HrScreens() {
  return <RoleRouter screens={screens} />
}
