export const radRequests = [
  { id: 1, order: 'RAD-1182', patient: 'سارة أحمد', study: 'أشعة صدر', modality: 'X-Ray', doctor: 'د. فهد عمر', priority: 'عادي', status: 'بانتظار التقرير', time: 'اليوم 08:40', avatar: 'avatars/patient-2.jpg' },
  { id: 2, order: 'RAD-1183', patient: 'ليلى كريم', study: 'رنين مغناطيسي للمخ', modality: 'MRI', doctor: 'د. منى القاسم', priority: 'عادي', status: 'قيد التصوير', time: 'اليوم 09:00', avatar: 'avatars/patient-2.jpg' },
  { id: 3, order: 'RAD-1184', patient: 'سعيد ناصر', study: 'مقطعية للبطن', modality: 'CT', doctor: 'د. زياد النجار', priority: 'عاجل', status: 'بانتظار التصوير', time: 'اليوم 09:10', avatar: 'avatars/patient-3.jpg' },
  { id: 4, order: 'RAD-1185', patient: 'هند ماجد', study: 'سونار حوض', modality: 'US', doctor: 'د. ريم العتيبي', priority: 'عاجل', status: 'بانتظار التصوير', time: 'اليوم 09:20', avatar: 'avatars/patient-2.jpg' },
  { id: 5, order: 'RAD-1181', patient: 'علي حسن', study: 'أشعة يد', modality: 'X-Ray', doctor: 'د. عمر الشامي', priority: 'عادي', status: 'مكتمل', time: 'أمس 16:20', avatar: 'avatars/patient-4.jpg' },
]

export const radReports = [
  { id: 1, study: 'أشعة صدر', patient: 'سارة أحمد', date: 'اليوم', radiologist: 'د. عمر الخطيب', status: 'مسودة', impression: 'لا توجد ارتشاحات رئوية. حجم القلب طبيعي.' },
  { id: 2, study: 'أشعة يد', patient: 'علي حسن', date: 'أمس', radiologist: 'د. عمر الخطيب', status: 'معتمد', impression: 'العظام سليمة دون كسور أو خلع.' },
  { id: 3, study: 'رنين مغناطيسي للمخ', patient: 'ريم العلي', date: '18 سبتمبر', radiologist: 'د. عمر الخطيب', status: 'معتمد', impression: 'لا آفات بؤرية، البنية الدماغية طبيعية.' },
  { id: 4, study: 'مقطعية للبطن', patient: 'خالد منصور', date: '3 سبتمبر', radiologist: 'د. نهى الفارس', status: 'معتمد', impression: 'لا كتل أو تجمعات سائلة، الأعضاء طبيعية.' },
]

// سلسلة الصور في العارض
export const series = [
  { file: 'xray-chest.png', label: 'صدر' },
  { file: 'xray-hand.png', label: 'يد' },
  { file: 'mri-brain.png', label: 'مخ' },
  { file: 'ct-abdomen.png', label: 'بطن' },
  { file: 'ultrasound.png', label: 'سونار' },
]

export const HOURS = ['08:00', '09:00', '10:00', '11:00', '12:00', '13:00', '14:00', '15:00']

// جدول الأجهزة: كل جهاز ← الساعة ← اسم المريض
export const scheduleDevices = [
  { id: 'xr1', name: 'X-Ray 1', booked: { '08:00': 'سارة أحمد', '10:00': 'علي حسن', '13:00': 'منى القاسم' } },
  { id: 'xr2', name: 'X-Ray 2', booked: { '09:00': 'خالد منصور', '11:00': 'ريم عادل' } },
  { id: 'mri', name: 'MRI', booked: { '09:00': 'ليلى كريم', '11:00': 'فؤاد سالم', '14:00': 'ياسر علي' } },
  { id: 'ct', name: 'CT', booked: { '09:00': 'سعيد ناصر', '12:00': 'هدى سالم' } },
  { id: 'us', name: 'سونار', booked: { '09:00': 'هند ماجد', '10:00': 'مريم يوسف', '15:00': 'دانة خالد' } },
]

export const examsByType = [
  { label: 'X-Ray', value: 120 }, { label: 'CT', value: 48 }, { label: 'MRI', value: 36 },
  { label: 'سونار', value: 74 }, { label: 'أخرى', value: 15 },
]
export const radMonthly = [
  { label: 'ماي', value: 240 }, { label: 'يون', value: 262 }, { label: 'يول', value: 275 },
  { label: 'أغس', value: 301 }, { label: 'سبت', value: 318 }, { label: 'أكت', value: 345 },
]
export const radHourly = [
  { label: '8', value: 35 }, { label: '10', value: 50 }, { label: '12', value: 70 }, { label: '14', value: 80 },
  { label: '16', value: 60 }, { label: '18', value: 45 }, { label: '20', value: 30 },
]
