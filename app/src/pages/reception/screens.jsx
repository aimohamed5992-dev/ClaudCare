import RoleRouter from '../RoleRouter'
import { ReceptionBookings, ReceptionDashboard, ReceptionInvoices, ReceptionQueue, ReceptionRegister } from './ReceptionPages'

// شاشات دور «reception»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <ReceptionDashboard />,
  register: <ReceptionRegister />,
  bookings: <ReceptionBookings />,
  queue: <ReceptionQueue />,
  invoices: <ReceptionInvoices />,
}

export default function ReceptionScreens() {
  return <RoleRouter screens={screens} />
}
