export const todayDepartments = [
  { id: 1, name: 'الطوارئ', on: 'د. فهد عمر', patients: 46, wait: '12 د', status: 'متوسط' },
  { id: 2, name: 'الباطنة', on: 'د. منى القاسم', patients: 88, wait: '25 د', status: 'متوسط' },
  { id: 3, name: 'الأطفال', on: 'د. ريم العتيبي', patients: 64, wait: '8 د', status: 'جيد' },
  { id: 4, name: 'العظام', on: 'د. زياد النجار', patients: 37, wait: '40 د', status: 'مزدحم' },
]

export const appointments = [
  { id: 1, patient: 'محمد علي', doctor: 'د. منى القاسم', dept: 'الباطنة', time: '09:00', date: 'اليوم', status: 'بالانتظار', avatar: 'avatars/patient-3.jpg', reason: 'متابعة سكري' },
  { id: 2, patient: 'سارة أحمد', doctor: 'د. فهد عمر', dept: 'القلب', time: '09:30', date: 'اليوم', status: 'حاضر', avatar: 'avatars/patient-2.jpg', reason: 'ألم في الصدر' },
  { id: 3, patient: 'يوسف خالد', doctor: 'د. ريم العتيبي', dept: 'الأطفال', time: '10:00', date: 'اليوم', status: 'مجدول', avatar: 'avatars/patient-1.jpg', reason: 'كشف دوري' },
  { id: 4, patient: 'عبدالله ناصر', doctor: 'د. زياد النجار', dept: 'العظام', time: '10:30', date: 'اليوم', status: 'مكتمل', avatar: 'avatars/patient-4.jpg', reason: 'كسر في الساعد' },
  { id: 5, patient: 'ريم العلي', doctor: 'د. منى القاسم', dept: 'الباطنة', time: '11:00', date: 'غدًا', status: 'مجدول', avatar: 'avatars/patient-2.jpg', reason: 'نتائج تحاليل' },
  { id: 6, patient: 'نورة سعد', doctor: 'د. عمر الشامي', dept: 'القلب', time: '12:00', date: 'غدًا', status: 'ملغي', avatar: 'avatars/patient-2.jpg', reason: 'تخطيط قلب' },
]

export const branchStaff = [
  { id: 1, name: 'منى القاسم', title: 'استشارية باطنة', dept: 'الباطنة', shift: 'صباحي', status: 'على رأس العمل', avatar: 'avatars/doc-f1.jpg' },
  { id: 2, name: 'فهد عمر', title: 'استشاري طوارئ', dept: 'الطوارئ', shift: 'صباحي', status: 'على رأس العمل', avatar: 'avatars/doc-m3.jpg' },
  { id: 3, name: 'ريم العتيبي', title: 'أخصائية أطفال', dept: 'الأطفال', shift: 'مسائي', status: 'خارج الدوام', avatar: 'avatars/doc-f3.jpg' },
  { id: 4, name: 'نادين يوسف', title: 'ممرضة أولى', dept: 'الباطنة', shift: 'صباحي', status: 'على رأس العمل', avatar: 'avatars/nurse-f1.jpg' },
  { id: 5, name: 'خالد منصور', title: 'ممرض', dept: 'الطوارئ', shift: 'ليلي', status: 'خارج الدوام', avatar: 'avatars/nurse-m1.jpg' },
  { id: 6, name: 'لمى الحسن', title: 'ممرضة', dept: 'الأطفال', shift: 'صباحي', status: 'على رأس العمل', avatar: 'avatars/nurse-f3.jpg' },
]

export const rooms = [
  { id: 1, room: '204', type: 'غرفة عادية', floor: 'الثاني', patient: 'خالد منصور', status: 'مشغولة' },
  { id: 2, room: '206', type: 'غرفة عادية', floor: 'الثاني', patient: 'ريم عادل', status: 'مشغولة' },
  { id: 3, room: '208', type: 'غرفة خاصة', floor: 'الثاني', patient: '—', status: 'متاحة' },
  { id: 4, room: '210', type: 'عناية مركزة', floor: 'الثاني', patient: 'فؤاد سالم', status: 'مشغولة' },
  { id: 5, room: '212', type: 'غرفة عادية', floor: 'الثاني', patient: '—', status: 'قيد التنظيف' },
  { id: 6, room: '305', type: 'غرفة خاصة', floor: 'الثالث', patient: '—', status: 'متاحة' },
]

export const diagnostics = [
  { id: 1, order: 'LAB-2210', kind: 'مختبر', test: 'صورة دم كاملة', patient: 'محمد علي', date: 'اليوم 08:10', status: 'قيد التحليل' },
  { id: 2, order: 'RAD-1182', kind: 'أشعة', test: 'أشعة صدر', patient: 'سارة أحمد', date: 'اليوم 08:40', status: 'بانتظار التقرير' },
  { id: 3, order: 'LAB-2209', kind: 'مختبر', test: 'سكر تراكمي', patient: 'يوسف خالد', date: 'اليوم 07:55', status: 'جاهز' },
  { id: 4, order: 'RAD-1181', kind: 'أشعة', test: 'رنين مغناطيسي للمخ', patient: 'ليلى كريم', date: 'أمس 16:20', status: 'جاهز' },
  { id: 5, order: 'LAB-2208', kind: 'مختبر', test: 'وظائف كبد', patient: 'نورة سعد', date: 'أمس 12:05', status: 'جاهز' },
]

export const hourly = [
  { label: '8', value: 30 }, { label: '10', value: 60 }, { label: '12', value: 80 }, { label: '14', value: 66 },
  { label: '16', value: 72 }, { label: '18', value: 50 }, { label: '20', value: 34 },
]
export const monthlyVisits = [
  { label: 'ماي', value: 2600 }, { label: 'يون', value: 2850 }, { label: 'يول', value: 3010 },
  { label: 'أغس', value: 3290 }, { label: 'سبت', value: 3410 }, { label: 'أكت', value: 3720 },
]
