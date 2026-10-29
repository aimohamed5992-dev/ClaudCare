export const employees = [
  { id: 1, name: 'د. منى القاسم', role: 'طبيبة قلب', dept: 'العيادات', hired: '2019-03-01', phone: '0501234567', status: 'على رأس العمل', avatar: 'avatars/doc-f1.jpg' },
  { id: 2, name: 'د. عمر الشامي', role: 'طبيب باطنة', dept: 'العيادات', hired: '2017-09-15', phone: '0507654321', status: 'على رأس العمل', avatar: 'avatars/doc-m1.jpg' },
  { id: 3, name: 'نادين يوسف', role: 'ممرضة', dept: 'التمريض', hired: '2021-01-10', phone: '0551112233', status: 'إجازة', avatar: 'avatars/nurse-f1.jpg' },
  { id: 4, name: 'هبة ناصر', role: 'موظفة استقبال', dept: 'الاستقبال', hired: '2022-06-01', phone: '0544455566', status: 'على رأس العمل', avatar: 'avatars/reception-f1.jpg' },
  { id: 5, name: 'لمى الشهري', role: 'صيدلانية', dept: 'الصيدلية', hired: '2020-02-20', phone: '0533344455', status: 'على رأس العمل', avatar: 'avatars/pharmacist-f1.jpg' },
  { id: 6, name: 'عمر الخطيب', role: 'أخصائي أشعة', dept: 'الأشعة', hired: '2018-11-05', phone: '0566677788', status: 'تحت التجربة', avatar: 'avatars/radiologist.jpg' },
]
export const DEPTS = ['العيادات', 'التمريض', 'الاستقبال', 'الصيدلية', 'الأشعة', 'المختبر']

// طلبات الإجازة. onLeave = عدد زملاء القسم في إجازة بنفس الفترة، maxOff = الحد الأقصى المسموح
export const leaveRequests = [
  { id: 1, name: 'نادين يوسف', dept: 'التمريض', type: 'سنوية', from: '2026-10-25', to: '2026-10-29', days: 5, balance: 12, onLeave: 1, maxOff: 2, avatar: 'avatars/nurse-f1.jpg' },
  { id: 2, name: 'هبة ناصر', dept: 'الاستقبال', type: 'سنوية', from: '2026-11-02', to: '2026-11-13', days: 10, balance: 6, onLeave: 0, maxOff: 1, avatar: 'avatars/reception-f1.jpg' },
  { id: 3, name: 'لمى الشهري', dept: 'الصيدلية', type: 'مرضية', from: '2026-10-20', to: '2026-10-21', days: 2, balance: 15, onLeave: 1, maxOff: 1, avatar: 'avatars/pharmacist-f1.jpg' },
  { id: 4, name: 'عمر الخطيب', dept: 'الأشعة', type: 'طارئة', from: '2026-10-19', to: '2026-10-19', days: 1, balance: 3, onLeave: 0, maxOff: 1, avatar: 'avatars/radiologist.jpg' },
]
export const REJECT_REASONS = ['اختر السبب…', 'نقص تغطية القسم', 'الرصيد غير كافٍ', 'فترة ذروة تشغيلية', 'مستندات ناقصة']

export const shifts = [
  { id: 1, name: 'نادين يوسف', dept: 'التمريض', shift: 'صباحية', time: '07:00 – 15:00', day: 'الأحد', status: 'مؤكدة', avatar: 'avatars/nurse-f1.jpg' },
  { id: 2, name: 'سلمى عادل', dept: 'التمريض', shift: 'مسائية', time: '15:00 – 23:00', day: 'الأحد', status: 'مؤكدة', avatar: 'avatars/nurse-f2.jpg' },
  { id: 3, name: 'كريم الدوسري', dept: 'التمريض', shift: 'ليلية', time: '23:00 – 07:00', day: 'الأحد', status: 'بانتظار التبديل', avatar: 'avatars/nurse-m1.jpg' },
  { id: 4, name: 'هبة ناصر', dept: 'الاستقبال', shift: 'صباحية', time: '08:00 – 16:00', day: 'الاثنين', status: 'مؤكدة', avatar: 'avatars/reception-f1.jpg' },
  { id: 5, name: 'د. عمر الشامي', dept: 'العيادات', shift: 'مسائية', time: '15:00 – 23:00', day: 'الاثنين', status: 'مؤكدة', avatar: 'avatars/doc-m1.jpg' },
]

// تراخيص مزاولة المهنة. days = الأيام المتبقية (سالب = منتهٍ)
export const licenses = [
  { id: 1, name: 'د. عمر الشامي', role: 'طبيب باطنة', no: 'SCFHS-44120', expiry: '2026-10-10', days: -8, avatar: 'avatars/doc-m1.jpg' },
  { id: 2, name: 'د. منى القاسم', role: 'طبيبة قلب', no: 'SCFHS-38877', expiry: '2026-11-20', days: 33, avatar: 'avatars/doc-f1.jpg' },
  { id: 3, name: 'لمى الشهري', role: 'صيدلانية', no: 'SCFHS-51002', expiry: '2027-01-15', days: 89, avatar: 'avatars/pharmacist-f1.jpg' },
  { id: 4, name: 'نادين يوسف', role: 'ممرضة', no: 'SCFHS-60731', expiry: '2027-08-01', days: 287, avatar: 'avatars/nurse-f1.jpg' },
]
export const licenseStatus = (d) => (d < 0 ? 'منتهي' : d <= 60 ? 'ينتهي قريبًا' : 'ساري')

export const headcountByDept = [
  { label: 'عيادات', value: 84 }, { label: 'تمريض', value: 126 }, { label: 'استقبال', value: 22 },
  { label: 'صيدلة', value: 18 }, { label: 'أشعة', value: 24 }, { label: 'مختبر', value: 38 },
]
export const attendanceWeekly = [
  { label: 'س', value: 96 }, { label: 'ح', value: 94 }, { label: 'ن', value: 97 }, { label: 'ث', value: 95 },
  { label: 'ر', value: 93 }, { label: 'خ', value: 96 }, { label: 'ج', value: 88 },
]
