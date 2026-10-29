import RoleRouter from '../RoleRouter'
import { EmergencyAmbulances, EmergencyBoard, EmergencyDashboard, EmergencyTriage } from './EmergencyPages'

// شاشات دور «emergency»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <EmergencyDashboard />,
  board: <EmergencyBoard />,
  triage: <EmergencyTriage />,
  ambulances: <EmergencyAmbulances />,
}

export default function EmergencyScreens() {
  return <RoleRouter screens={screens} />
}
