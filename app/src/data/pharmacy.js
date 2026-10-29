// وصفات واردة من الأطباء (أرقام الوصفات متسقة مع صفحة الوصفات عند الطبيب)
export const rxQueue = [
  { id: 1, no: 'RX-3305', patient: 'سارة أحمد', doctor: 'د. فهد عمر', itemsText: 'أموكسيسيلين + باراسيتامول + أوميبرازول', time: 'قبل 10 د', priority: 'عاجل', status: 'بانتظار الصرف', avatar: 'avatars/patient-2.jpg' },
  { id: 2, no: 'RX-3301', patient: 'محمد علي', doctor: 'د. منى القاسم', itemsText: 'ميتفورمين 500 + أوميبرازول 20', time: 'قبل 25 د', priority: 'عادي', status: 'بانتظار الصرف', avatar: 'avatars/patient-3.jpg' },
  { id: 3, no: 'RX-3303', patient: 'عبدالله ناصر', doctor: 'د. عمر الشامي', itemsText: 'أملوديبين 5 + أسبرين 81', time: 'قبل 40 د', priority: 'عادي', status: 'قيد التجهيز', avatar: 'avatars/patient-4.jpg' },
  { id: 4, no: 'RX-3298', patient: 'ريم العلي', doctor: 'د. منى القاسم', itemsText: 'فيتامين د 50,000', time: 'قبل ساعة', priority: 'عادي', status: 'تم الصرف', avatar: 'avatars/patient-2.jpg' },
  { id: 5, no: 'RX-3290', patient: 'يوسف خالد', doctor: 'د. ريم العتيبي', itemsText: 'بخاخ سالبيوتامول', time: 'أمس', priority: 'عادي', status: 'مرفوضة', avatar: 'avatars/patient-1.jpg' },
]

// الوصفة التي تُعرض في شاشة الصرف. conflict = مادة تتعارض مع حساسية المريض.
export const dispenseRx = {
  no: 'RX-3305',
  patient: 'سارة أحمد',
  age: 34,
  doctor: 'د. فهد عمر',
  avatar: 'avatars/patient-2.jpg',
  allergies: ['البنسلين', 'الفول السوداني'],
  items: [
    {
      id: 1, name: 'أموكسيسيلين 500 مجم', form: 'كبسولات', qty: 21, stock: 140, price: 3.5, conflict: 'البنسلين',
      substitute: { name: 'أزيثروميسين 500 مجم', form: 'أقراص', qty: 6, stock: 48, price: 9 },
    },
    { id: 2, name: 'باراسيتامول 500 مجم', form: 'أقراص', qty: 20, stock: 600, price: 0.6 },
    { id: 3, name: 'أوميبرازول 20 مجم', form: 'كبسولات', qty: 14, stock: 0, price: 2.1 },
  ],
}

// level = نسبة المخزون من الحد الآمن (0–100)
export const inventory = [
  { id: 1, name: 'باراسيتامول 500 مجم', category: 'مسكنات', qty: 600, min: 200, level: 100, expiry: '2027-08', supplier: 'الدوائية المتحدة', status: 'متوفر' },
  { id: 2, name: 'ميتفورمين 500 مجم', category: 'السكري', qty: 320, min: 150, level: 90, expiry: '2027-04', supplier: 'الدوائية المتحدة', status: 'متوفر' },
  { id: 3, name: 'أموكسيسيلين 500 مجم', category: 'مضادات حيوية', qty: 140, min: 120, level: 55, expiry: '2026-12', supplier: 'نماء الطبية', status: 'قارب الانتهاء' },
  { id: 4, name: 'أنسولين لانتوس', category: 'السكري', qty: 6, min: 30, level: 15, expiry: '2027-01', supplier: 'نماء الطبية', status: 'منخفض' },
  { id: 5, name: 'أوميبرازول 20 مجم', category: 'الجهاز الهضمي', qty: 0, min: 100, level: 0, expiry: '—', supplier: 'الدوائية المتحدة', status: 'نفد' },
  { id: 6, name: 'أملوديبين 5 مجم', category: 'الضغط', qty: 210, min: 100, level: 80, expiry: '2027-06', supplier: 'الشرق للأدوية', status: 'متوفر' },
  { id: 7, name: 'أزيثروميسين 500 مجم', category: 'مضادات حيوية', qty: 48, min: 40, level: 40, expiry: '2027-02', supplier: 'نماء الطبية', status: 'متوفر' },
  { id: 8, name: 'سالبيوتامول بخاخ', category: 'الجهاز التنفسي', qty: 18, min: 25, level: 30, expiry: '2026-11', supplier: 'الشرق للأدوية', status: 'قارب الانتهاء' },
]

export const purchaseOrders = [
  { id: 1, no: 'PO-5120', supplier: 'نماء الطبية', items: 6, total: '18,400 ر.س', date: '2026-10-17', status: 'مُرسل' },
  { id: 2, no: 'PO-5118', supplier: 'الدوائية المتحدة', items: 12, total: '42,950 ر.س', date: '2026-10-14', status: 'مستلم جزئيًا' },
  { id: 3, no: 'PO-5112', supplier: 'الشرق للأدوية', items: 4, total: '9,300 ر.س', date: '2026-10-09', status: 'مستلم' },
  { id: 4, no: 'PO-5121', supplier: 'نماء الطبية', items: 2, total: '3,600 ر.س', date: '2026-10-18', status: 'مسودة' },
]

export const sales = [
  { id: 1, no: 'S-9041', patient: 'ريم العلي', items: 'فيتامين د', amount: '120 ر.س', method: 'بطاقة', time: 'اليوم 09:40', status: 'مدفوعة' },
  { id: 2, no: 'S-9040', patient: 'بيع مباشر', items: 'مسكن + كمادات', amount: '48 ر.س', method: 'نقدًا', time: 'اليوم 09:12', status: 'مدفوعة' },
  { id: 3, no: 'S-9039', patient: 'عبدالله ناصر', items: 'أملوديبين + أسبرين', amount: '86 ر.س', method: 'تأمين', time: 'اليوم 08:50', status: 'مدفوعة' },
  { id: 4, no: 'S-9036', patient: 'نورة سعد', items: 'سيروم وريدي', amount: '35 ر.س', method: 'نقدًا', time: 'أمس 17:20', status: 'مرتجع' },
]

export const salesWeekly = [
  { label: 'س', value: 38 }, { label: 'ح', value: 45 }, { label: 'ن', value: 41 }, { label: 'ث', value: 52 },
  { label: 'ر', value: 47 }, { label: 'خ', value: 60 }, { label: 'ج', value: 22 },
]
export const salesMonthly = [
  { label: 'ماي', value: 118 }, { label: 'يون', value: 126 }, { label: 'يول', value: 131 },
  { label: 'أغس', value: 140 }, { label: 'سبت', value: 152 }, { label: 'أكت', value: 163 },
]
export const topCategories = [
  { label: 'مسكنات', value: 320 }, { label: 'مضادات', value: 240 }, { label: 'السكري', value: 210 },
  { label: 'الضغط', value: 180 }, { label: 'تنفسي', value: 95 },
]
