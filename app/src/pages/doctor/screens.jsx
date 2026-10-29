import RoleRouter from '../RoleRouter'
import { DoctorAppointments, DoctorConsultation, DoctorDashboard, DoctorMessages, DoctorOrders, DoctorPatients, DoctorPrescriptions, DoctorRecords } from './DoctorPages'

// شاشات دور «doctor»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <DoctorDashboard />,
  appointments: <DoctorAppointments />,
  patients: <DoctorPatients />,
  records: <DoctorRecords />,
  prescriptions: <DoctorPrescriptions />,
  orders: <DoctorOrders />,
  messages: <DoctorMessages />,
  consultation: <DoctorConsultation />,
}

export default function DoctorScreens() {
  return <RoleRouter screens={screens} />
}
