// مستويات الفرز (1 الأخطر). الانتظار الأقصى بالدقائق
export const LEVELS = {
  1: { label: 'حرج', wait: 'فوري', tone: 'red' },
  2: { label: 'طارئ', wait: 'خلال 10 د', tone: 'orange' },
  3: { label: 'عاجل', wait: 'خلال 30 د', tone: 'amber' },
  4: { label: 'أقل إلحاحًا', wait: 'خلال ساعة', tone: 'green' },
  5: { label: 'غير عاجل', wait: 'خلال ساعتين', tone: 'blue' },
}
export const levelText = (l) => `${l} – ${LEVELS[l].label}`

export const STATUS = ['بانتظار الفحص', 'قيد المعاينة', 'تحويل للتنويم', 'خرج']

export const erPatients = [
  { id: 1, name: 'محمد علي', age: 58, complaint: 'ألم في الصدر', level: 1, arrived: 'قبل 6 د', zone: 'الإنعاش', status: 'قيد المعاينة', avatar: 'avatars/patient-3.jpg' },
  { id: 2, name: 'سارة أحمد', age: 34, complaint: 'ضيق تنفس', level: 2, arrived: 'قبل 12 د', zone: 'الحالات الحرجة', status: 'بانتظار الفحص', avatar: 'avatars/patient-2.jpg' },
  { id: 3, name: 'يوسف خالد', age: 9, complaint: 'كسر في الذراع', level: 3, arrived: 'قبل 25 د', zone: 'العظام', status: 'بانتظار الفحص', avatar: 'avatars/patient-1.jpg' },
  { id: 4, name: 'نورة سعد', age: 41, complaint: 'ألم في البطن', level: 3, arrived: 'قبل 40 د', zone: 'الباطنة', status: 'قيد المعاينة', avatar: 'avatars/patient-2.jpg' },
  { id: 5, name: 'عبدالله ناصر', age: 27, complaint: 'جرح سطحي', level: 4, arrived: 'قبل 55 د', zone: 'الجروح', status: 'بانتظار الفحص', avatar: 'avatars/patient-4.jpg' },
  { id: 6, name: 'هند ماجد', age: 22, complaint: 'سعال وحرارة خفيفة', level: 5, arrived: 'قبل ساعة', zone: 'العيادة السريعة', status: 'بانتظار الفحص', avatar: 'avatars/patient-2.jpg' },
]

export const ambulances = [
  { id: 1, unit: 'AMB-12', patient: 'رجل 62 سنة', condition: 'اشتباه جلطة دماغية', eta: '4 دقائق', level: 1, status: 'في الطريق' },
  { id: 2, unit: 'AMB-07', patient: 'امرأة 30 سنة', condition: 'حادث سير – إصابات متعددة', eta: '9 دقائق', level: 2, status: 'في الطريق' },
  { id: 3, unit: 'AMB-03', patient: 'طفل 5 سنوات', condition: 'حمى وتشنج', eta: 'وصلت', level: 2, status: 'وصلت' },
]

export const FLAGS = [
  { key: 'chest', label: 'ألم صدر مع تعرّق أو انتشار للذراع' },
  { key: 'bleed', label: 'نزيف شديد غير متحكَّم به' },
  { key: 'stroke', label: 'ضعف مفاجئ بجهة واحدة أو صعوبة كلام' },
  { key: 'airway', label: 'انسداد أو صعوبة شديدة في التنفس' },
  { key: 'preg', label: 'حامل مع ألم أو نزيف' },
]
export const COMPLAINTS = ['ألم في الصدر', 'ضيق تنفس', 'ألم في البطن', 'إصابة / كسر', 'حمى', 'صداع', 'جرح', 'أخرى']

export const arrivalsHourly = [
  { label: '8', value: 6 }, { label: '10', value: 11 }, { label: '12', value: 14 }, { label: '14', value: 9 },
  { label: '16', value: 12 }, { label: '18', value: 17 }, { label: '20', value: 13 },
]
export const levelsToday = [
  { label: '1', value: 3 }, { label: '2', value: 9 }, { label: '3', value: 21 }, { label: '4', value: 18 }, { label: '5', value: 7 },
]

// يعيد { level, why[] } وفق قواعد مبسّطة (واجهة تجريبية، ليست أداة طبية)
export function computeTriage({ consciousness, hr, sbp, spo2, rr, temp, flags, pain }) {
  const why = []
  let level = 5
  const raise = (l, w) => { if (l < level) level = l; why.push(w) }
  if (consciousness === 'غير مستجيب') raise(1, 'غير مستجيب')
  if (consciousness === 'يستجيب للألم فقط') raise(1, 'استجابة للألم فقط')
  if (spo2 !== null && spo2 < 90) raise(1, `تشبع الأكسجين ${spo2}%`)
  if (sbp !== null && sbp < 90) raise(1, `ضغط انقباضي منخفض ${sbp}`)
  if (flags.airway) raise(1, 'صعوبة شديدة في التنفس')
  if (flags.bleed) raise(2, 'نزيف شديد')
  if (flags.chest) raise(2, 'ألم صدر مع أعراض إنذار')
  if (flags.stroke) raise(2, 'أعراض جلطة دماغية محتملة')
  if (consciousness === 'يستجيب للصوت') raise(2, 'استجابة للصوت فقط')
  if (spo2 !== null && spo2 >= 90 && spo2 < 94) raise(2, `تشبع الأكسجين ${spo2}%`)
  if (hr !== null && (hr > 130 || hr < 45)) raise(2, `نبض ${hr}`)
  if (flags.preg) raise(3, 'حامل مع أعراض')
  if (sbp !== null && sbp >= 180) raise(3, `ضغط مرتفع ${sbp}`)
  if (hr !== null && hr > 110) raise(3, `نبض مرتفع ${hr}`)
  if (rr !== null && (rr > 24 || rr < 10)) raise(3, `معدل تنفس ${rr}`)
  if (temp !== null && temp >= 39) raise(3, `حرارة ${temp}`)
  if (pain >= 7) raise(3, `ألم شديد (${pain}/10)`)
  if (temp !== null && temp >= 38) raise(4, `حرارة ${temp}`)
  if (pain >= 4) raise(4, `ألم متوسط (${pain}/10)`)
  return { level, why }
}
