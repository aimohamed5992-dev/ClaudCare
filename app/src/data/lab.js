export const labRequests = [
  { id: 1, order: 'LAB-2210', patient: 'محمد علي', test: 'صورة دم كاملة', doctor: 'د. منى القاسم', priority: 'عاجل', status: 'قيد التحليل', time: 'اليوم 08:10', avatar: 'avatars/patient-3.jpg' },
  { id: 2, order: 'LAB-2211', patient: 'سارة أحمد', test: 'سكر تراكمي', doctor: 'د. فهد عمر', priority: 'عادي', status: 'قيد التحليل', time: 'اليوم 08:25', avatar: 'avatars/patient-2.jpg' },
  { id: 3, order: 'LAB-2212', patient: 'يوسف خالد', test: 'وظائف كبد', doctor: 'د. ريم العتيبي', priority: 'عادي', status: 'جاهز', time: 'اليوم 07:55', avatar: 'avatars/patient-1.jpg' },
  { id: 4, order: 'LAB-2213', patient: 'نورة سعد', test: 'هرمون الغدة', doctor: 'د. زياد النجار', priority: 'عادي', status: 'بانتظار العينة', time: 'اليوم 09:05', avatar: 'avatars/patient-2.jpg' },
  { id: 5, order: 'LAB-2214', patient: 'عبدالله ناصر', test: 'وظائف كلى', doctor: 'د. عمر الشامي', priority: 'عاجل', status: 'بانتظار العينة', time: 'اليوم 09:10', avatar: 'avatars/patient-4.jpg' },
]

// معايير صورة الدم الكاملة لإدخال النتيجة: range = [أدنى, أعلى]
export const CBC_PARAMS = [
  { key: 'hgb', name: 'الهيموجلوبين', unit: 'g/dL', range: [12, 16] },
  { key: 'wbc', name: 'كريات الدم البيضاء', unit: '×10³/µL', range: [4, 11] },
  { key: 'rbc', name: 'كريات الدم الحمراء', unit: '×10⁶/µL', range: [4.2, 5.4] },
  { key: 'plt', name: 'الصفائح الدموية', unit: '×10³/µL', range: [150, 400] },
  { key: 'hct', name: 'الهيماتوكريت', unit: '%', range: [36, 46] },
]

export const labResults = [
  {
    id: 1, title: 'صورة دم كاملة (CBC) – محمد علي', date: 'اليوم', doctor: 'د. منى القاسم', lab: 'مختبر الفرع الرئيسي',
    rows: [
      { name: 'الهيموجلوبين', value: '10.8', unit: 'g/dL', range: '12.0 – 16.0', flag: 'منخفض' },
      { name: 'كريات الدم البيضاء', value: '12.4', unit: '×10³/µL', range: '4.0 – 11.0', flag: 'مرتفع' },
      { name: 'كريات الدم الحمراء', value: '4.5', unit: '×10⁶/µL', range: '4.2 – 5.4', flag: 'طبيعي' },
      { name: 'الصفائح الدموية', value: '260', unit: '×10³/µL', range: '150 – 400', flag: 'طبيعي' },
    ],
  },
  {
    id: 2, title: 'وظائف الكبد – يوسف خالد', date: 'اليوم', doctor: 'د. ريم العتيبي', lab: 'مختبر الفرع الرئيسي',
    rows: [
      { name: 'ALT', value: '24', unit: 'U/L', range: '7 – 35', flag: 'طبيعي' },
      { name: 'AST', value: '22', unit: 'U/L', range: '8 – 33', flag: 'طبيعي' },
      { name: 'البيليروبين', value: '0.7', unit: 'mg/dL', range: '0.1 – 1.2', flag: 'طبيعي' },
    ],
  },
  {
    id: 3, title: 'السكر التراكمي – ريم العلي', date: 'أمس', doctor: 'د. منى القاسم', lab: 'مختبر الفرع الرئيسي',
    rows: [
      { name: 'HbA1c', value: '5.9', unit: '%', range: '4.0 – 5.6', flag: 'مرتفع' },
      { name: 'سكر صائم', value: '98', unit: 'mg/dL', range: '70 – 100', flag: 'طبيعي' },
    ],
  },
]

export const samples = [
  { id: 1, code: 'S-88421', patient: 'محمد علي', type: 'دم', tube: 'أنبوب بنفسجي', collected: 'اليوم 07:50', status: 'تم الاستلام' },
  { id: 2, code: 'S-88422', patient: 'سارة أحمد', type: 'دم', tube: 'أنبوب ذهبي', collected: 'اليوم 08:15', status: 'تم الاستلام' },
  { id: 3, code: 'S-88423', patient: 'نورة سعد', type: 'دم', tube: 'أنبوب أخضر', collected: 'اليوم 09:00', status: 'بانتظار الاستلام' },
  { id: 4, code: 'S-88424', patient: 'عبدالله ناصر', type: 'بول', tube: 'عبوة بول', collected: 'اليوم 09:05', status: 'بانتظار الاستلام' },
  { id: 5, code: 'S-88419', patient: 'فؤاد سالم', type: 'دم', tube: 'أنبوب بنفسجي', collected: 'أمس 18:30', status: 'مرفوضة' },
]

export const devices = [
  { id: 1, name: 'محلل الكيمياء', model: 'Cobas 6000', status: 'يعمل', load: 72, calibrated: 'قبل 3 أيام' },
  { id: 2, name: 'محلل الدم', model: 'Sysmex XN-1000', status: 'معايرة', load: 0, calibrated: 'جارٍ الآن' },
  { id: 3, name: 'الطرد المركزي', model: 'Hettich 320', status: 'يعمل', load: 45, calibrated: 'قبل أسبوع' },
  { id: 4, name: 'محلل الهرمونات', model: 'Architect i1000', status: 'عطل', load: 0, calibrated: 'قبل شهر' },
]

export const qcRuns = [
  { id: 1, device: 'محلل الكيمياء', test: 'جلوكوز', level: 'مستوى 1', result: '+0.4 SD', status: 'مقبول' },
  { id: 2, device: 'محلل الكيمياء', test: 'كرياتينين', level: 'مستوى 2', result: '+2.3 SD', status: 'تحذير' },
  { id: 3, device: 'محلل الدم', test: 'هيموجلوبين', level: 'مستوى 1', result: '-0.8 SD', status: 'مقبول' },
  { id: 4, device: 'محلل الدم', test: 'صفائح', level: 'مستوى 3', result: '+3.4 SD', status: 'مرفوض' },
]

export const labHourly = [
  { label: '8', value: 25 }, { label: '10', value: 60 }, { label: '12', value: 85 }, { label: '14', value: 70 },
  { label: '16', value: 55 }, { label: '18', value: 40 }, { label: '20', value: 20 },
]
export const qcWeekly = [
  { label: 'س', value: 96 }, { label: 'ح', value: 98 }, { label: 'ن', value: 95 }, { label: 'ث', value: 99 },
  { label: 'ر', value: 97 }, { label: 'خ', value: 94 }, { label: 'ج', value: 98 },
]
