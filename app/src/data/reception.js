export const queue = [
  { id: 1, ticket: 'A-021', patient: 'عبدالله ناصر', clinic: 'الباطنة', doctor: 'د. منى القاسم', arrived: 'منذ 25 د', status: 'بالانتظار', avatar: 'avatars/patient-4.jpg' },
  { id: 2, ticket: 'A-022', patient: 'مريم يوسف', clinic: 'الأطفال', doctor: 'د. ريم العتيبي', arrived: 'منذ 18 د', status: 'داخل العيادة', avatar: 'avatars/patient-1.jpg' },
  { id: 3, ticket: 'A-023', patient: 'صالح أحمد', clinic: 'العظام', doctor: 'د. زياد النجار', arrived: 'منذ 14 د', status: 'بالانتظار', avatar: 'avatars/patient-3.jpg' },
  { id: 4, ticket: 'A-024', patient: 'دانة خالد', clinic: 'الجلدية', doctor: 'د. هدى سالم', arrived: 'منذ 9 د', status: 'بالانتظار', avatar: 'avatars/patient-2.jpg' },
  { id: 5, ticket: 'A-020', patient: 'فهد العتيبي', clinic: 'القلب', doctor: 'د. عمر الشامي', arrived: 'منذ 50 د', status: 'مكتمل', avatar: 'avatars/patient-3.jpg' },
]

export const bookings = [
  { id: 1, patient: 'محمد علي', phone: '0551234567', doctor: 'د. منى القاسم', dept: 'الباطنة', date: '2026-10-18', time: '09:00', status: 'مؤكد', avatar: 'avatars/patient-3.jpg' },
  { id: 2, patient: 'سارة أحمد', phone: '0557654321', doctor: 'د. فهد عمر', dept: 'القلب', date: '2026-10-18', time: '09:30', status: 'تم الحضور', avatar: 'avatars/patient-2.jpg' },
  { id: 3, patient: 'يوسف خالد', phone: '0560011223', doctor: 'د. ريم العتيبي', dept: 'الأطفال', date: '2026-10-19', time: '10:00', status: 'مؤكد', avatar: 'avatars/patient-1.jpg' },
  { id: 4, patient: 'نورة سعد', phone: '0503344556', doctor: 'د. عمر الشامي', dept: 'القلب', date: '2026-10-19', time: '12:00', status: 'بانتظار التأكيد', avatar: 'avatars/patient-2.jpg' },
  { id: 5, patient: 'عبدالله ناصر', phone: '0598877665', doctor: 'د. زياد النجار', dept: 'العظام', date: '2026-10-20', time: '11:30', status: 'ملغي', avatar: 'avatars/patient-4.jpg' },
]

export const receptionInvoices = [
  { id: 1, no: 'INV-10482', patient: 'محمد علي', item: 'كشف وتحاليل', amount: '620 ر.س', method: 'بطاقة', status: 'مدفوعة', avatar: 'avatars/patient-3.jpg' },
  { id: 2, no: 'INV-10481', patient: 'سارة أحمد', item: 'أشعة صدر', amount: '350 ر.س', method: '—', status: 'غير مدفوعة', avatar: 'avatars/patient-2.jpg' },
  { id: 3, no: 'INV-10480', patient: 'يوسف خالد', item: 'كشف أطفال', amount: '200 ر.س', method: 'نقدًا', status: 'مدفوعة', avatar: 'avatars/patient-1.jpg' },
  { id: 4, no: 'INV-10479', patient: 'نورة سعد', item: 'تخطيط قلب', amount: '300 ر.س', method: '—', status: 'جزئية', avatar: 'avatars/patient-2.jpg' },
]

export const dailyVisits = [
  { label: '8', value: 30 }, { label: '10', value: 70 }, { label: '12', value: 85 }, { label: '14', value: 60 },
  { label: '16', value: 66 }, { label: '18', value: 48 }, { label: '20', value: 28 },
]
