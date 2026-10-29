import RoleRouter from '../RoleRouter'
import { SurgeryChecklist, SurgeryDashboard, SurgeryRooms, SurgerySchedule, SurgeryTeam } from './SurgeryPages'

// شاشات دور «surgery»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <SurgeryDashboard />,
  schedule: <SurgerySchedule />,
  rooms: <SurgeryRooms />,
  team: <SurgeryTeam />,
  checklist: <SurgeryChecklist />,
}

export default function SurgeryScreens() {
  return <RoleRouter screens={screens} />
}
