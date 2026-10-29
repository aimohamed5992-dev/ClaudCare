import RoleRouter from '../RoleRouter'
import {
  PharmacyDashboard, PharmacyDispense, PharmacyInventory, PharmacyOrders, PharmacyPrescriptions, PharmacyReports, PharmacySales,
} from './PharmacyPages'

// شاشات دور «pharmacy»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <PharmacyDashboard />,
  prescriptions: <PharmacyPrescriptions />,
  inventory: <PharmacyInventory />,
  orders: <PharmacyOrders />,
  sales: <PharmacySales />,
  reports: <PharmacyReports />,
  dispense: <PharmacyDispense />,
}

export default function PharmacyScreens() {
  return <RoleRouter screens={screens} />
}
