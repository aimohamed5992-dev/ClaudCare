export const INSURERS = ['التعاونية', 'بوبا', 'ميدغلف', 'ملاذ']

// طلبات الموافقة المسبقة
export const approvals = [
  { id: 1, no: 'PA-7721', patient: 'سارة أحمد', insurer: 'التعاونية', procedure: 'رنين مغناطيسي للمخ', amount: '2,350 ر.س', priority: 'عاجل', status: 'قيد المراجعة', submitted: 'قبل ساعة', doctor: 'د. منى القاسم', avatar: 'avatars/patient-2.jpg' },
  { id: 2, no: 'PA-7719', patient: 'محمد علي', insurer: 'بوبا', procedure: 'قسطرة قلبية تشخيصية', amount: '14,800 ر.س', priority: 'عاجل', status: 'معلومات ناقصة', submitted: 'قبل 3 ساعات', doctor: 'د. عمر الشامي', avatar: 'avatars/patient-3.jpg' },
  { id: 3, no: 'PA-7714', patient: 'عبدالله ناصر', insurer: 'ميدغلف', procedure: 'جلسات علاج طبيعي (10)', amount: '3,000 ر.س', priority: 'عادي', status: 'موافق', submitted: 'أمس', doctor: 'د. زياد النجار', avatar: 'avatars/patient-4.jpg' },
  { id: 4, no: 'PA-7708', patient: 'يوسف خالد', insurer: 'ملاذ', procedure: 'استئصال لوزتين', amount: '9,500 ر.س', priority: 'عادي', status: 'مرفوض', submitted: 'قبل يومين', doctor: 'د. ريم العتيبي', avatar: 'avatars/patient-1.jpg' },
  { id: 5, no: 'PA-7722', patient: 'نورة سعد', insurer: 'التعاونية', procedure: 'تصوير مقطعي للبطن', amount: '1,900 ر.س', priority: 'عادي', status: 'قيد المراجعة', submitted: 'قبل 20 د', doctor: 'د. فهد عمر', avatar: 'avatars/patient-2.jpg' },
]

// الحالة التي تُعرض في شاشة المراجعة (PA-7721)
export const reviewCase = {
  no: 'PA-7721',
  patient: 'سارة أحمد',
  age: 34,
  avatar: 'avatars/patient-2.jpg',
  insurer: 'التعاونية',
  policy: 'POL-8841-229',
  tier: 'ذهبي',
  coverage: 0.8,
  limit: 100000,
  used: 62000,
  expiry: '2027-03-31',
  procedure: 'رنين مغناطيسي للمخ (MRI)',
  diagnosis: 'G43.9 – صداع نصفي',
  doctor: 'د. منى القاسم',
  items: [
    { id: 1, name: 'رنين مغناطيسي للمخ', amount: 1800, covered: true },
    { id: 2, name: 'مادة التباين', amount: 350, covered: true },
    { id: 3, name: 'غرفة انتظار VIP', amount: 200, covered: false },
  ],
  docs: [
    { id: 1, name: 'طلب الطبيب.pdf', ok: true },
    { id: 2, name: 'التقرير الطبي.pdf', ok: false },
    { id: 3, name: 'صورة الهوية.jpg', ok: true },
  ],
}

export const REJECT_REASONS = ['اختر السبب…', 'الإجراء غير مشمول بالوثيقة', 'تجاوز الحد السنوي', 'فترة الانتظار لم تنتهِ', 'الوثيقة منتهية', 'لا توجد مبررات طبية كافية']

export const claims = [
  { id: 1, no: 'CL-30211', patient: 'محمد علي', insurer: 'بوبا', amount: '4,200 ر.س', date: '2026-10-16', status: 'مُرسلة', avatar: 'avatars/patient-3.jpg' },
  { id: 2, no: 'CL-30208', patient: 'ريم العلي', insurer: 'التعاونية', amount: '620 ر.س', date: '2026-10-15', status: 'مدفوعة', avatar: 'avatars/patient-2.jpg' },
  { id: 3, no: 'CL-30199', patient: 'عبدالله ناصر', insurer: 'ميدغلف', amount: '8,900 ر.س', date: '2026-10-12', status: 'مدفوعة جزئيًا', avatar: 'avatars/patient-4.jpg' },
  { id: 4, no: 'CL-30188', patient: 'يوسف خالد', insurer: 'ملاذ', amount: '1,350 ر.س', date: '2026-09-14', status: 'مرفوضة', avatar: 'avatars/patient-1.jpg' },
  { id: 5, no: 'CL-30215', patient: 'سارة أحمد', insurer: 'التعاونية', amount: '350 ر.س', date: '2026-10-18', status: 'مسودة', avatar: 'avatars/patient-2.jpg' },
]

// قاعدة تحقق تجريبية: المفتاح = رقم الهوية أو الوثيقة
export const policies = {
  1234567890: { name: 'محمد علي', policy: 'POL-5521-100', insurer: 'بوبا', tier: 'فضي', copay: '20%', limit: 50000, used: 12000, expiry: '2027-01-31', active: true },
  1098765432: { name: 'سارة أحمد', policy: 'POL-8841-229', insurer: 'التعاونية', tier: 'ذهبي', copay: '20%', limit: 100000, used: 62000, expiry: '2027-03-31', active: true },
  1122334455: { name: 'عبدالله ناصر', policy: 'POL-3310-775', insurer: 'ميدغلف', tier: 'أساسي', copay: '30%', limit: 30000, used: 30000, expiry: '2026-06-30', active: false },
}

export const companies = [
  { id: 1, name: 'التعاونية', contract: 'ساري حتى 2027-06', discount: '12%', pending: 14, days: 38, status: 'نشط', contact: 'claims@tawuniya.example' },
  { id: 2, name: 'بوبا', contract: 'ساري حتى 2027-02', discount: '10%', pending: 9, days: 45, status: 'نشط', contact: 'provider@bupa.example' },
  { id: 3, name: 'ميدغلف', contract: 'ساري حتى 2026-12', discount: '8%', pending: 6, days: 52, status: 'قيد التجديد', contact: 'network@medgulf.example' },
  { id: 4, name: 'ملاذ', contract: 'ساري حتى 2027-09', discount: '9%', pending: 3, days: 33, status: 'نشط', contact: 'care@malath.example' },
]

export const claimsByStatus = [
  { label: 'مسودة', value: 12 }, { label: 'مُرسلة', value: 64 }, { label: 'مدفوعة', value: 148 },
  { label: 'جزئية', value: 21 }, { label: 'مرفوضة', value: 17 },
]
export const claimsMonthly = [
  { label: 'ماي', value: 410 }, { label: 'يون', value: 445 }, { label: 'يول', value: 468 },
  { label: 'أغس', value: 502 }, { label: 'سبت', value: 531 }, { label: 'أكت', value: 566 },
]
export const approvalsWeekly = [
  { label: 'س', value: 18 }, { label: 'ح', value: 24 }, { label: 'ن', value: 21 }, { label: 'ث', value: 30 },
  { label: 'ر', value: 26 }, { label: 'خ', value: 33 }, { label: 'ج', value: 9 },
]
