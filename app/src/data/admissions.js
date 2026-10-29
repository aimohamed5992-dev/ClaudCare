export const WARDS = ['الباطنة', 'الجراحة', 'العناية المركزة', 'الأطفال']

const P = (name, avatar) => ({ patient: name, avatar })
// حالات السرير: متاح · مشغول · تنظيف · محجوز
export const beds = [
  { id: 'M-101', ward: 'الباطنة', status: 'مشغول', ...P('محمد علي', 'avatars/patient-3.jpg'), since: '2026-10-15' },
  { id: 'M-102', ward: 'الباطنة', status: 'متاح' },
  { id: 'M-103', ward: 'الباطنة', status: 'تنظيف' },
  { id: 'M-104', ward: 'الباطنة', status: 'مشغول', ...P('عبدالله ناصر', 'avatars/patient-4.jpg'), since: '2026-10-16' },
  { id: 'S-201', ward: 'الجراحة', status: 'مشغول', ...P('يوسف خالد', 'avatars/patient-1.jpg'), since: '2026-10-17' },
  { id: 'S-202', ward: 'الجراحة', status: 'محجوز', reservedFor: 'سارة أحمد (بعد العملية)' },
  { id: 'S-203', ward: 'الجراحة', status: 'متاح' },
  { id: 'S-204', ward: 'الجراحة', status: 'متاح' },
  { id: 'I-301', ward: 'العناية المركزة', status: 'مشغول', ...P('نورة سعد', 'avatars/patient-2.jpg'), since: '2026-10-17' },
  { id: 'I-302', ward: 'العناية المركزة', status: 'مشغول', ...P('هند ماجد', 'avatars/patient-2.jpg'), since: '2026-10-18' },
  { id: 'I-303', ward: 'العناية المركزة', status: 'تنظيف' },
  { id: 'P-401', ward: 'الأطفال', status: 'متاح' },
  { id: 'P-402', ward: 'الأطفال', status: 'مشغول', ...P('طفل: زياد', 'avatars/patient-1.jpg'), since: '2026-10-16' },
  { id: 'P-403', ward: 'الأطفال', status: 'متاح' },
]
export const BED_TONES = { متاح: 'green', مشغول: 'blue', تنظيف: 'orange', محجوز: 'gray' }

// طلبات إدخال بانتظار سرير
export const pendingAdmissions = [
  { id: 1, name: 'سارة أحمد', age: 34, from: 'الطوارئ', reason: 'التهاب رئوي', ward: 'الباطنة', insured: true, approval: 'موافقة مسبقة PA-7721', avatar: 'avatars/patient-2.jpg' },
  { id: 2, name: 'زياد الحربي', age: 7, from: 'العيادات', reason: 'التهاب لوزتين', ward: 'الأطفال', insured: false, approval: '', avatar: 'avatars/patient-1.jpg' },
  { id: 3, name: 'فهد العتيبي', age: 52, from: 'العيادات', reason: 'عملية فتق', ward: 'الجراحة', insured: true, approval: 'موافقة مسبقة PA-7730', avatar: 'avatars/patient-4.jpg' },
]
export const ADMISSION_TYPES = ['طارئ', 'مخطط', 'تحويل من مستشفى']

export const transfers = [
  { id: 1, no: 'TR-410', patient: 'نورة سعد', from: 'الطوارئ', to: 'العناية المركزة', reason: 'تدهور الحالة', status: 'بانتظار سرير', avatar: 'avatars/patient-2.jpg' },
  { id: 2, no: 'TR-409', patient: 'محمد علي', from: 'العناية المركزة', to: 'الباطنة', reason: 'استقرار الحالة', status: 'معتمد', avatar: 'avatars/patient-3.jpg' },
  { id: 3, no: 'TR-405', patient: 'يوسف خالد', from: 'الجراحة', to: 'الجراحة (غرفة خاصة)', reason: 'طلب المريض', status: 'تم النقل', avatar: 'avatars/patient-1.jpg' },
]

// قائمة الخروج: المرضى المرشحون للخروج + خطوات الخروج
export const dischargeCandidates = [
  { id: 1, name: 'محمد علي', bed: 'M-101', days: 3, avatar: 'avatars/patient-3.jpg' },
  { id: 2, name: 'عبدالله ناصر', bed: 'M-104', days: 2, avatar: 'avatars/patient-4.jpg' },
  { id: 3, name: 'يوسف خالد', bed: 'S-201', days: 1, avatar: 'avatars/patient-1.jpg' },
]
export const DISCHARGE_STEPS = [
  'أمر الخروج من الطبيب',
  'مطابقة الأدوية وصرف أدوية الخروج',
  'تسوية الفاتورة أو موافقة التأمين',
  'ملخص الخروج وتعليمات المتابعة سُلّمت للمريض',
]

export const occupancyWeekly = [
  { label: 'س', value: 78 }, { label: 'ح', value: 82 }, { label: 'ن', value: 85 }, { label: 'ث', value: 80 },
  { label: 'ر', value: 88 }, { label: 'خ', value: 84 }, { label: 'ج', value: 72 },
]
export const admissionsByWard = [
  { label: 'باطنة', value: 14 }, { label: 'جراحة', value: 9 }, { label: 'عناية', value: 5 }, { label: 'أطفال', value: 8 },
]
