import RoleRouter from '../RoleRouter'
import { LabDashboard, LabDevices, LabEntry, LabQuality, LabRequests, LabResults, LabSamples } from './LabPages'

// شاشات دور «lab»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <LabDashboard />,
  requests: <LabRequests />,
  results: <LabResults />,
  samples: <LabSamples />,
  quality: <LabQuality />,
  devices: <LabDevices />,
  entry: <LabEntry />,
}

export default function LabScreens() {
  return <RoleRouter screens={screens} />
}
