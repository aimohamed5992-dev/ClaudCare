// فواتير المرضى. total/paid أرقام لاستخدامها في التحصيل
export const invoices = [
  { id: 1, no: 'INV-8842', patient: 'سارة أحمد', service: 'رنين مغناطيسي + استشارة', total: 2350, paid: 0, due: '2026-10-25', status: 'غير مدفوعة', avatar: 'avatars/patient-2.jpg' },
  { id: 2, no: 'INV-8839', patient: 'محمد علي', service: 'قسطرة قلبية تشخيصية', total: 14800, paid: 8000, due: '2026-10-22', status: 'مدفوعة جزئيًا', avatar: 'avatars/patient-3.jpg' },
  { id: 3, no: 'INV-8830', patient: 'عبدالله ناصر', service: 'جلسات علاج طبيعي', total: 3000, paid: 3000, due: '2026-10-15', status: 'مدفوعة', avatar: 'avatars/patient-4.jpg' },
  { id: 4, no: 'INV-8801', patient: 'يوسف خالد', service: 'استئصال لوزتين', total: 9500, paid: 0, due: '2026-09-30', status: 'متأخرة', avatar: 'avatars/patient-1.jpg' },
  { id: 5, no: 'INV-8845', patient: 'نورة سعد', service: 'تصوير مقطعي للبطن', total: 1900, paid: 0, due: '2026-10-28', status: 'غير مدفوعة', avatar: 'avatars/patient-2.jpg' },
]
export const money = (n) => `${Number(n).toLocaleString('en-US', { minimumFractionDigits: n % 1 ? 2 : 0, maximumFractionDigits: 2 })} ر.س`
export const fmtInvoice = (i) => ({ ...i, totalText: money(i.total), remainingText: money(i.total - i.paid) })

export const expenses = [
  { id: 1, no: 'EX-2210', title: 'صيانة جهاز الأشعة', category: 'صيانة', amount: '12,500 ر.س', date: '2026-10-16', by: 'قسم الأشعة', status: 'بانتظار الاعتماد' },
  { id: 2, no: 'EX-2208', title: 'مستلزمات معقمات', category: 'مستلزمات', amount: '4,200 ر.س', date: '2026-10-15', by: 'التمريض', status: 'معتمد' },
  { id: 3, no: 'EX-2205', title: 'فاتورة الكهرباء', category: 'خدمات', amount: '28,900 ر.س', date: '2026-10-10', by: 'الإدارة', status: 'مدفوع' },
  { id: 4, no: 'EX-2199', title: 'تدريب إنعاش قلبي', category: 'تدريب', amount: '6,000 ر.س', date: '2026-10-05', by: 'الموارد البشرية', status: 'مرفوض' },
]
export const EXPENSE_CATEGORIES = ['صيانة', 'مستلزمات', 'خدمات', 'تدريب', 'أخرى']

export const payroll = [
  { id: 1, name: 'د. منى القاسم', role: 'طبيبة', base: '28,000 ر.س', extras: '+3,200', deductions: '−1,400', net: '29,800 ر.س', status: 'مصروف', avatar: 'avatars/doc-f1.jpg' },
  { id: 2, name: 'د. عمر الشامي', role: 'طبيب', base: '30,000 ر.س', extras: '+2,500', deductions: '−1,500', net: '31,000 ر.س', status: 'مصروف', avatar: 'avatars/doc-m1.jpg' },
  { id: 3, name: 'نادين يوسف', role: 'ممرضة', base: '9,500 ر.س', extras: '+1,200', deductions: '−475', net: '10,225 ر.س', status: 'قيد الاعتماد', avatar: 'avatars/nurse-f1.jpg' },
  { id: 4, name: 'هبة ناصر', role: 'استقبال', base: '7,000 ر.س', extras: '+400', deductions: '−350', net: '7,050 ر.س', status: 'قيد الاعتماد', avatar: 'avatars/reception-f1.jpg' },
  { id: 5, name: 'لمى الشهري', role: 'صيدلانية', base: '14,000 ر.س', extras: '+900', deductions: '−700', net: '14,200 ر.س', status: 'مسودة', avatar: 'avatars/pharmacist-f1.jpg' },
]

// الإقفال اليومي: المتوقع من النظام لكل طريقة دفع
export const closeExpected = [
  { key: 'cash', label: 'نقدًا', expected: 18450, counted: true },
  { key: 'card', label: 'بطاقات (مدى/فيزا)', expected: 42300, counted: true },
  { key: 'insurance', label: 'تأمين (آجل)', expected: 27800, counted: false },
]

export const revenueMonthly = [
  { label: 'ماي', value: 1.9 }, { label: 'يون', value: 2.1 }, { label: 'يول', value: 2.2 },
  { label: 'أغس', value: 2.4 }, { label: 'سبت', value: 2.6 }, { label: 'أكت', value: 2.8 },
]
export const revenueByDept = [
  { label: 'الجراحة', value: 38 }, { label: 'الأشعة', value: 24 }, { label: 'المختبر', value: 18 },
  { label: 'العيادات', value: 31 }, { label: 'الصيدلية', value: 16 },
]
