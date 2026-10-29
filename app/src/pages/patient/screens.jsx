import RoleRouter from '../RoleRouter'
import { PatientAppointments, PatientDashboard, PatientInvoices, PatientLab, PatientMedications, PatientRadiology, PatientRecords } from './PatientPages'

// شاشات دور «patient»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <PatientDashboard />,
  appointments: <PatientAppointments />,
  records: <PatientRecords />,
  lab: <PatientLab />,
  radiology: <PatientRadiology />,
  medications: <PatientMedications />,
  invoices: <PatientInvoices />,
}

export default function PatientScreens() {
  return <RoleRouter screens={screens} />
}
