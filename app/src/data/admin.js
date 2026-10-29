export const hospitals = [
  { id: 1, name: 'مستشفى النور', city: 'الرياض', manager: 'د. خالد العمر', branches: 4, plan: 'مؤسسات', status: 'نشط', since: '2023-02-10' },
  { id: 2, name: 'مستشفى الأمل', city: 'جدة', manager: 'د. هدى سالم', branches: 2, plan: 'احترافية', status: 'نشط', since: '2023-06-01' },
  { id: 3, name: 'مركز الشفاء', city: 'الدمام', manager: 'د. ياسر منصور', branches: 1, plan: 'أساسية', status: 'قيد المراجعة', since: '2026-10-02' },
  { id: 4, name: 'مستشفى السلام', city: 'المدينة', manager: 'د. ليلى حسن', branches: 3, plan: 'احترافية', status: 'موقوف', since: '2022-11-20' },
  { id: 5, name: 'مستشفى الرحمة', city: 'مكة', manager: 'د. أنس الحربي', branches: 5, plan: 'مؤسسات', status: 'نشط', since: '2022-04-15' },
  { id: 6, name: 'مركز الحياة الطبي', city: 'أبها', manager: 'د. نورة القحطاني', branches: 1, plan: 'أساسية', status: 'قيد المراجعة', since: '2026-09-28' },
  { id: 7, name: 'مستشفى الفجر', city: 'الخبر', manager: 'د. فيصل الدوسري', branches: 2, plan: 'احترافية', status: 'نشط', since: '2024-01-09' },
]

export const users = [
  { id: 1, name: 'منى القاسم', email: 'mona@alnoor.com', role: 'طبيب', hospital: 'مستشفى النور', last: 'اليوم 09:12', status: 'نشط', avatar: 'avatars/doc-f1.jpg' },
  { id: 2, name: 'سامي الراشد', email: 'sami@alnoor.com', role: 'مدير فرع', hospital: 'مستشفى النور', last: 'اليوم 08:40', status: 'نشط', avatar: 'avatars/doc-m2.jpg' },
  { id: 3, name: 'نادين يوسف', email: 'nadine@alamal.com', role: 'ممرضة', hospital: 'مستشفى الأمل', last: 'أمس 22:05', status: 'نشط', avatar: 'avatars/nurse-f1.jpg' },
  { id: 4, name: 'سلمى حسن', email: 'salma@alrahma.com', role: 'فني مختبر', hospital: 'مستشفى الرحمة', last: 'أمس 17:30', status: 'نشط', avatar: 'avatars/lab-tech.jpg' },
  { id: 5, name: 'عمر الخطيب', email: 'omar@alfajr.com', role: 'أخصائي أشعة', hospital: 'مستشفى الفجر', last: 'قبل 3 أيام', status: 'معلّق', avatar: 'avatars/radiologist.jpg' },
  { id: 6, name: 'هبة ناصر', email: 'heba@alamal.com', role: 'استقبال', hospital: 'مستشفى الأمل', last: 'اليوم 07:55', status: 'نشط', avatar: 'avatars/reception-f1.jpg' },
  { id: 7, name: 'ريم العلي', email: 'reem@mail.com', role: 'مريض', hospital: 'مستشفى النور', last: 'قبل أسبوع', status: 'غير مفعّل', avatar: 'avatars/patient-2.jpg' },
]

export const subscriptions = [
  { id: 1, hospital: 'مستشفى النور', plan: 'مؤسسات', renewal: '2026-12-10', amount: '48,000 ر.س', status: 'مدفوع' },
  { id: 2, hospital: 'مستشفى الأمل', plan: 'احترافية', renewal: '2026-11-01', amount: '24,000 ر.س', status: 'قريب التجديد' },
  { id: 3, hospital: 'مستشفى السلام', plan: 'احترافية', renewal: '2026-09-20', amount: '24,000 ر.س', status: 'متأخر' },
  { id: 4, hospital: 'مستشفى الرحمة', plan: 'مؤسسات', renewal: '2027-02-15', amount: '60,000 ر.س', status: 'مدفوع' },
  { id: 5, hospital: 'مركز الشفاء', plan: 'أساسية', renewal: '2026-10-25', amount: '9,600 ر.س', status: 'قريب التجديد' },
  { id: 6, hospital: 'مستشفى الفجر', plan: 'احترافية', renewal: '2027-01-09', amount: '24,000 ر.س', status: 'مدفوع' },
]

export const growth = [
  { label: 'ماي', value: 38 }, { label: 'يون', value: 46 }, { label: 'يول', value: 52 },
  { label: 'أغس', value: 61 }, { label: 'سبت', value: 70 }, { label: 'أكت', value: 84 },
]
export const revenue = [
  { label: 'ماي', value: 210 }, { label: 'يون', value: 232 }, { label: 'يول', value: 248 },
  { label: 'أغس', value: 275 }, { label: 'سبت', value: 301 }, { label: 'أكت', value: 318 },
]
