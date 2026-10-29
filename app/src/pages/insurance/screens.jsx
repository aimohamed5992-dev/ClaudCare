import RoleRouter from '../RoleRouter'
import {
  InsuranceApprovals, InsuranceClaims, InsuranceCompanies, InsuranceDashboard, InsuranceEligibility, InsuranceReports, InsuranceReview,
} from './InsurancePages'

// شاشات دور «insurance»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <InsuranceDashboard />,
  approvals: <InsuranceApprovals />,
  claims: <InsuranceClaims />,
  eligibility: <InsuranceEligibility />,
  companies: <InsuranceCompanies />,
  reports: <InsuranceReports />,
  review: <InsuranceReview />,
}

export default function InsuranceScreens() {
  return <RoleRouter screens={screens} />
}
