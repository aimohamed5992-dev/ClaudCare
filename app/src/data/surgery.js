export const ROOMS = ['OR-1', 'OR-2', 'OR-3', 'OR-4']

export const cases = [
  { id: 1, no: 'SG-1041', patient: 'محمد علي', procedure: 'قسطرة قلبية علاجية', room: 'OR-1', time: '08:00 – 10:00', surgeon: 'د. عمر الشامي', anesthetist: 'د. هالة منصور', status: 'جارية', avatar: 'avatars/patient-3.jpg' },
  { id: 2, no: 'SG-1042', patient: 'فهد العتيبي', procedure: 'إصلاح فتق إربي', room: 'OR-2', time: '09:00 – 10:30', surgeon: 'د. زياد النجار', anesthetist: 'د. هالة منصور', status: 'مجدولة', avatar: 'avatars/patient-4.jpg' },
  { id: 3, no: 'SG-1043', patient: 'سارة أحمد', procedure: 'استئصال المرارة بالمنظار', room: 'OR-3', time: '11:00 – 13:00', surgeon: 'د. ريم العتيبي', anesthetist: 'د. سامي الراشد', status: 'مجدولة', avatar: 'avatars/patient-2.jpg' },
  { id: 4, no: 'SG-1044', patient: 'يوسف خالد', procedure: 'استئصال لوزتين', room: 'OR-2', time: '13:00 – 14:00', surgeon: 'د. ريم العتيبي', anesthetist: 'د. سامي الراشد', status: 'مجدولة', avatar: 'avatars/patient-1.jpg' },
  { id: 5, no: 'SG-1039', patient: 'نورة سعد', procedure: 'تنظير مفصل الركبة', room: 'OR-4', time: 'أمس 14:00', surgeon: 'د. زياد النجار', anesthetist: 'د. هالة منصور', status: 'تمت', avatar: 'avatars/patient-2.jpg' },
]
export const CASE_TONES = { جارية: 'blue', مجدولة: 'orange', تمت: 'green', ملغاة: 'red' }

export const ROOM_STATES = ['متاحة', 'قيد العملية', 'تنظيف وتعقيم', 'صيانة']
export const ROOM_TONES = { متاحة: 'green', 'قيد العملية': 'blue', 'تنظيف وتعقيم': 'orange', صيانة: 'red' }
export const rooms = [
  { id: 'OR-1', state: 'قيد العملية', note: 'SG-1041 · قسطرة قلبية' },
  { id: 'OR-2', state: 'متاحة', note: 'العملية التالية 09:00' },
  { id: 'OR-3', state: 'تنظيف وتعقيم', note: 'جاهزة خلال 20 دقيقة' },
  { id: 'OR-4', state: 'صيانة', note: 'فحص دوري لجهاز التخدير' },
]

export const team = [
  { id: 1, name: 'د. عمر الشامي', role: 'جرّاح', cases: 2, status: 'في العمليات', avatar: 'avatars/doc-m1.jpg' },
  { id: 2, name: 'د. ريم العتيبي', role: 'جرّاحة', cases: 2, status: 'متاحة', avatar: 'avatars/doc-f2.jpg' },
  { id: 3, name: 'د. هالة منصور', role: 'أخصائية تخدير', cases: 3, status: 'في العمليات', avatar: 'avatars/doc-f3.jpg' },
  { id: 4, name: 'د. سامي الراشد', role: 'أخصائي تخدير', cases: 2, status: 'متاح', avatar: 'avatars/doc-m3.jpg' },
  { id: 5, name: 'نادين يوسف', role: 'ممرضة عمليات', cases: 3, status: 'في العمليات', avatar: 'avatars/nurse-f1.jpg' },
  { id: 6, name: 'كريم الدوسري', role: 'فني عمليات', cases: 1, status: 'إجازة', avatar: 'avatars/nurse-m1.jpg' },
]
export const TEAM_TONES = { 'في العمليات': 'blue', متاحة: 'green', متاح: 'green', إجازة: 'gray' }

// قائمة ما قبل العملية. لكل حالة بنود جاهزة مبدئيًا (done) وتحذيرات تمنع المتابعة.
export const CHECK_ITEMS = [
  { key: 'identity', label: 'تأكيد هوية المريض والإجراء' },
  { key: 'consent', label: 'الموافقة الجراحية موقّعة' },
  { key: 'site', label: 'تحديد موضع العملية على الجسد' },
  { key: 'fasting', label: 'الصيام مؤكد (6 ساعات على الأقل)' },
  { key: 'allergy', label: 'مراجعة الحساسية والأدوية' },
  { key: 'labs', label: 'التحاليل والأشعة متوفرة' },
  { key: 'anesthesia', label: 'تقييم التخدير مكتمل' },
]
export const checklistCases = [
  { id: 1, no: 'SG-1042', patient: 'فهد العتيبي', procedure: 'إصلاح فتق إربي', allergy: 'لا يوجد', alerts: [], done: ['identity', 'consent', 'fasting', 'labs'] },
  { id: 2, no: 'SG-1043', patient: 'سارة أحمد', procedure: 'استئصال المرارة بالمنظار', allergy: 'البنسلين', alerts: ['المريضة تتناول مميّع دم (وارفرين) – راجع الطبيب قبل العملية'], done: ['identity', 'allergy'] },
  { id: 3, no: 'SG-1044', patient: 'يوسف خالد', procedure: 'استئصال لوزتين', allergy: 'لا يوجد', alerts: [], done: [] },
]

export const casesWeekly = [
  { label: 'س', value: 9 }, { label: 'ح', value: 11 }, { label: 'ن', value: 8 }, { label: 'ث', value: 12 },
  { label: 'ر', value: 10 }, { label: 'خ', value: 13 }, { label: 'ج', value: 3 },
]
export const roomUtil = [
  { label: 'OR-1', value: 86 }, { label: 'OR-2', value: 72 }, { label: 'OR-3', value: 64 }, { label: 'OR-4', value: 41 },
]
