export const branches = [
  { id: 1, name: 'الفرع الرئيسي', manager: 'د. سامي الراشد', patients: 312, occupancy: 85, status: 'يعمل', avatar: 'avatars/doc-m2.jpg', city: 'الرياض – العليا', phone: '011 555 0101' },
  { id: 2, name: 'فرع الشمال', manager: 'د. ليلى حسن', patients: 148, occupancy: 70, status: 'يعمل', avatar: 'avatars/doc-f3.jpg', city: 'الرياض – الياسمين', phone: '011 555 0102' },
  { id: 3, name: 'فرع الغرب', manager: 'د. ياسر منصور', patients: 96, occupancy: 62, status: 'يعمل', avatar: 'avatars/doc-m1.jpg', city: 'الرياض – ظهرة لبن', phone: '011 555 0103' },
  { id: 4, name: 'فرع الشرق', manager: 'د. هدى سالم', patients: 121, occupancy: 93, status: 'ضغط عالٍ', avatar: 'avatars/doc-f1.jpg', city: 'الرياض – الرمال', phone: '011 555 0104' },
]

export const departments = [
  { id: 1, name: 'الطوارئ', head: 'د. فهد عمر', doctors: 14, beds: 30, status: 'يعمل', avatar: 'avatars/doc-m3.jpg' },
  { id: 2, name: 'الباطنة', head: 'د. منى القاسم', doctors: 22, beds: 60, status: 'يعمل', avatar: 'avatars/doc-f1.jpg' },
  { id: 3, name: 'الأطفال', head: 'د. ريم العتيبي', doctors: 12, beds: 40, status: 'يعمل', avatar: 'avatars/doc-f3.jpg' },
  { id: 4, name: 'العظام', head: 'د. زياد النجار', doctors: 9, beds: 28, status: 'يعمل', avatar: 'avatars/doc-m1.jpg' },
  { id: 5, name: 'القلب', head: 'د. عمر الشامي', doctors: 11, beds: 24, status: 'يعمل', avatar: 'avatars/doc-m2.jpg' },
  { id: 6, name: 'العناية المركزة', head: 'د. نهى الفارس', doctors: 8, beds: 16, status: 'ضغط عالٍ', avatar: 'avatars/doc-f2.jpg' },
]

export const staff = [
  { id: 1, name: 'منى القاسم', title: 'استشارية باطنة', type: 'أطباء', dept: 'الباطنة', branch: 'الفرع الرئيسي', status: 'على رأس العمل', avatar: 'avatars/doc-f1.jpg' },
  { id: 2, name: 'فهد عمر', title: 'استشاري طوارئ', type: 'أطباء', dept: 'الطوارئ', branch: 'الفرع الرئيسي', status: 'على رأس العمل', avatar: 'avatars/doc-m3.jpg' },
  { id: 3, name: 'نادين يوسف', title: 'ممرضة أولى', type: 'تمريض', dept: 'الباطنة', branch: 'فرع الشمال', status: 'على رأس العمل', avatar: 'avatars/nurse-f1.jpg' },
  { id: 4, name: 'خالد منصور', title: 'ممرض', type: 'تمريض', dept: 'الطوارئ', branch: 'فرع الشرق', status: 'إجازة', avatar: 'avatars/nurse-m1.jpg' },
  { id: 5, name: 'سلمى حسن', title: 'فنية مختبر', type: 'فنيون', dept: 'المختبر', branch: 'الفرع الرئيسي', status: 'على رأس العمل', avatar: 'avatars/lab-tech.jpg' },
  { id: 6, name: 'عمر الخطيب', title: 'أخصائي أشعة', type: 'فنيون', dept: 'الأشعة', branch: 'فرع الغرب', status: 'على رأس العمل', avatar: 'avatars/radiologist.jpg' },
  { id: 7, name: 'هبة ناصر', title: 'موظفة استقبال', type: 'إداريون', dept: 'الاستقبال', branch: 'الفرع الرئيسي', status: 'على رأس العمل', avatar: 'avatars/reception-f1.jpg' },
  { id: 8, name: 'سعيد المطيري', title: 'موظف استقبال', type: 'إداريون', dept: 'الاستقبال', branch: 'فرع الشمال', status: 'إجازة', avatar: 'avatars/reception-m1.jpg' },
]

export const beds = [
  { id: 1, branch: 'الفرع الرئيسي', dept: 'الباطنة', total: 60, used: 51, free: 9, occupancy: 85 },
  { id: 2, branch: 'الفرع الرئيسي', dept: 'العناية المركزة', total: 16, used: 15, free: 1, occupancy: 94 },
  { id: 3, branch: 'فرع الشمال', dept: 'الأطفال', total: 40, used: 28, free: 12, occupancy: 70 },
  { id: 4, branch: 'فرع الغرب', dept: 'العظام', total: 28, used: 17, free: 11, occupancy: 61 },
  { id: 5, branch: 'فرع الشرق', dept: 'الطوارئ', total: 30, used: 28, free: 2, occupancy: 93 },
]

export const invoices = [
  { id: 1, no: 'INV-10482', patient: 'محمد علي', item: 'كشف وتحاليل', amount: '620 ر.س', date: '2026-10-18', status: 'مدفوعة' },
  { id: 2, no: 'INV-10481', patient: 'سارة أحمد', item: 'أشعة صدر', amount: '350 ر.س', date: '2026-10-18', status: 'معلّقة' },
  { id: 3, no: 'INV-10480', patient: 'يوسف خالد', item: 'عملية صغرى', amount: '4,800 ر.س', date: '2026-10-17', status: 'مدفوعة' },
  { id: 4, no: 'INV-10479', patient: 'نورة سعد', item: 'إقامة يومان', amount: '2,400 ر.س', date: '2026-10-17', status: 'متأخرة' },
  { id: 5, no: 'INV-10478', patient: 'عبدالله ناصر', item: 'استشارة', amount: '250 ر.س', date: '2026-10-16', status: 'مدفوعة' },
]

export const weekly = [
  { label: 'س', value: 60 }, { label: 'ح', value: 52 }, { label: 'ن', value: 66 }, { label: 'ث', value: 58 },
  { label: 'ر', value: 74 }, { label: 'خ', value: 70 }, { label: 'ج', value: 80 },
]
export const incomeByMonth = [
  { label: 'ماي', value: 31 }, { label: 'يون', value: 35 }, { label: 'يول', value: 38 },
  { label: 'أغس', value: 41 }, { label: 'سبت', value: 44 }, { label: 'أكت', value: 47 },
]
