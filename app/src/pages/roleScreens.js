import { lazy } from 'react'

// تقسيم الكود: كل دور يُحمَّل في حزمة مستقلة عند أول دخول له
export const roleScreens = {
  admin: lazy(() => import('./admin/screens')),
  hospital: lazy(() => import('./hospital/screens')),
  branch: lazy(() => import('./branch/screens')),
  lab: lazy(() => import('./lab/screens')),
  radiology: lazy(() => import('./radiology/screens')),
  pharmacy: lazy(() => import('./pharmacy/screens')),
  doctor: lazy(() => import('./doctor/screens')),
  nurse: lazy(() => import('./nurse/screens')),
  reception: lazy(() => import('./reception/screens')),
  insurance: lazy(() => import('./insurance/screens')),
  finance: lazy(() => import('./finance/screens')),
  hr: lazy(() => import('./hr/screens')),
  emergency: lazy(() => import('./emergency/screens')),
  admissions: lazy(() => import('./admissions/screens')),
  surgery: lazy(() => import('./surgery/screens')),
  patient: lazy(() => import('./patient/screens')),
}
