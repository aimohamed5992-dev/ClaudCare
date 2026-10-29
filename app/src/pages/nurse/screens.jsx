import RoleRouter from '../RoleRouter'
import { NurseDashboard, NurseMedications, NurseNotes, NurseShifts, NurseVitals, NurseWard } from './NursePages'

// شاشات دور «nurse»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <NurseDashboard />,
  ward: <NurseWard />,
  vitals: <NurseVitals />,
  medications: <NurseMedications />,
  notes: <NurseNotes />,
  shifts: <NurseShifts />,
}

export default function NurseScreens() {
  return <RoleRouter screens={screens} />
}
