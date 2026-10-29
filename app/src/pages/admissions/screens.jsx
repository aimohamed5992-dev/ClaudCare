import RoleRouter from '../RoleRouter'
import { AdmissionsAdmit, AdmissionsBeds, AdmissionsDashboard, AdmissionsDischarge, AdmissionsTransfers } from './AdmissionsPages'

// شاشات دور «admissions»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <AdmissionsDashboard />,
  beds: <AdmissionsBeds />,
  admit: <AdmissionsAdmit />,
  transfers: <AdmissionsTransfers />,
  discharge: <AdmissionsDischarge />,
}

export default function AdmissionsScreens() {
  return <RoleRouter screens={screens} />
}
