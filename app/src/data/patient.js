export const myAppointments = [
  { id: 1, doctor: 'د. منى القاسم', dept: 'الباطنة', date: 'غدًا 19 أكتوبر', time: '10:30 ص', place: 'الفرع الرئيسي – عيادة 4', status: 'قادم', avatar: 'avatars/doc-f1.jpg', reason: 'مراجعة نتائج التحاليل' },
  { id: 2, doctor: 'د. عمر الشامي', dept: 'القلب', date: '27 أكتوبر', time: '01:00 م', place: 'الفرع الرئيسي – عيادة 9', status: 'قادم', avatar: 'avatars/doc-m2.jpg', reason: 'تخطيط قلب دوري' },
  { id: 3, doctor: 'د. منى القاسم', dept: 'الباطنة', date: '12 أكتوبر', time: '09:00 ص', place: 'الفرع الرئيسي – عيادة 4', status: 'مكتمل', avatar: 'avatars/doc-f1.jpg', reason: 'متابعة السكر' },
  { id: 4, doctor: 'د. فهد عمر', dept: 'الطوارئ', date: '2 أكتوبر', time: '07:20 م', place: 'الطوارئ', status: 'مكتمل', avatar: 'avatars/doc-m3.jpg', reason: 'ألم في الصدر' },
  { id: 5, doctor: 'د. ريم العتيبي', dept: 'الأطفال', date: '5 سبتمبر', time: '11:00 ص', place: 'فرع الشمال', status: 'ملغي', avatar: 'avatars/doc-f3.jpg', reason: 'استشارة' },
]

export const labReports = [
  {
    id: 1, title: 'صورة دم كاملة (CBC)', date: '15 أكتوبر 2026', doctor: 'د. منى القاسم', lab: 'مختبر الفرع الرئيسي',
    rows: [
      { name: 'الهيموجلوبين', value: '13.2', unit: 'g/dL', range: '12.0 – 16.0', flag: 'طبيعي' },
      { name: 'كريات الدم البيضاء', value: '11.8', unit: '×10³/µL', range: '4.0 – 11.0', flag: 'مرتفع' },
      { name: 'كريات الدم الحمراء', value: '4.6', unit: '×10⁶/µL', range: '4.2 – 5.4', flag: 'طبيعي' },
      { name: 'الصفائح الدموية', value: '250', unit: '×10³/µL', range: '150 – 400', flag: 'طبيعي' },
      { name: 'الهيماتوكريت', value: '40', unit: '%', range: '36 – 46', flag: 'طبيعي' },
    ],
  },
  {
    id: 2, title: 'السكر التراكمي (HbA1c)', date: '12 أكتوبر 2026', doctor: 'د. منى القاسم', lab: 'مختبر الفرع الرئيسي',
    rows: [
      { name: 'HbA1c', value: '5.9', unit: '%', range: '4.0 – 5.6', flag: 'مرتفع' },
      { name: 'سكر صائم', value: '98', unit: 'mg/dL', range: '70 – 100', flag: 'طبيعي' },
    ],
  },
  {
    id: 3, title: 'وظائف الكبد', date: '20 سبتمبر 2026', doctor: 'د. فهد عمر', lab: 'مختبر الفرع الرئيسي',
    rows: [
      { name: 'ALT', value: '28', unit: 'U/L', range: '7 – 35', flag: 'طبيعي' },
      { name: 'AST', value: '24', unit: 'U/L', range: '8 – 33', flag: 'طبيعي' },
      { name: 'ALP', value: '90', unit: 'U/L', range: '44 – 147', flag: 'طبيعي' },
      { name: 'البيليروبين', value: '0.8', unit: 'mg/dL', range: '0.1 – 1.2', flag: 'طبيعي' },
    ],
  },
]

export const studies = [
  { id: 1, title: 'أشعة سينية للصدر', date: '2 أكتوبر 2026', doctor: 'د. عمر الخطيب', image: 'xray-chest.png', status: 'التقرير جاهز', report: 'الرئتان سليمتان وخاليتان من الارتشاحات أو الكتل. حجم القلب ضمن الحدود الطبيعية. الزوايا الضلعية الحجابية واضحة. لا يوجد دليل على انصباب جنبي.' },
  { id: 2, title: 'رنين مغناطيسي للمخ', date: '18 سبتمبر 2026', doctor: 'د. عمر الخطيب', image: 'mri-brain.png', status: 'التقرير جاهز', report: 'لا توجد آفات بؤرية. البطينات الجانبية بحجم طبيعي. لا دليل على نزف أو احتشاء حاد. بنية خط الوسط طبيعية.' },
  { id: 3, title: 'مقطعية للبطن', date: '3 سبتمبر 2026', doctor: 'د. نهى الفارس', image: 'ct-abdomen.png', status: 'التقرير جاهز', report: 'الكبد والطحال والكليتان بمظهر طبيعي. لا توجد كتل أو تجمعات سائلة. الأمعاء بدون علامات انسداد.' },
  { id: 4, title: 'أشعة سينية لليد', date: '20 أغسطس 2026', doctor: 'د. عمر الخطيب', image: 'xray-hand.png', status: 'التقرير جاهز', report: 'العظام سليمة، لا توجد كسور أو خلع. المسافات المفصلية محفوظة.' },
  { id: 5, title: 'تصوير بالموجات فوق الصوتية', date: 'اليوم', doctor: 'د. هند الزهراني', image: 'ultrasound.png', status: 'بانتظار التقرير', report: 'التقرير قيد الإعداد من قبل أخصائي الأشعة وسيظهر هنا فور اعتماده.' },
]

export const medications = [
  { id: 1, name: 'ميتفورمين 500 مجم', dose: 'قرص واحد', schedule: 'مرتين يوميًا بعد الأكل', purpose: 'ضبط السكر', left: 18, remind: true },
  { id: 2, name: 'فيتامين د 50,000', dose: 'كبسولة', schedule: 'مرة أسبوعيًا', purpose: 'نقص فيتامين د', left: 60, remind: true },
  { id: 3, name: 'أوميبرازول 20 مجم', dose: 'كبسولة', schedule: 'مرة صباحًا قبل الفطور', purpose: 'حماية المعدة', left: 7, remind: false },
]

export const records = {
  profile: { name: 'ريم العلي', avatar: 'avatars/patient-2.jpg', mrn: 'P-10432', age: '28 سنة', blood: 'O+', height: '165 سم', weight: '64 كجم', phone: '0551234567', insurance: 'التعاونية – ذهبي' },
  allergies: ['البنسلين', 'الفول السوداني'],
  vaccines: [
    { name: 'الإنفلونزا الموسمية', date: 'أكتوبر 2026' },
    { name: 'كوفيد-19 (معزز)', date: 'مارس 2026' },
    { name: 'الكزاز', date: 'يناير 2023' },
  ],
  conditions: [
    { id: 1, name: 'مقدمات السكري', since: '2026', doctor: 'د. منى القاسم', status: 'تحت المتابعة' },
    { id: 2, name: 'نقص فيتامين د', since: '2025', doctor: 'د. منى القاسم', status: 'تحت العلاج' },
    { id: 3, name: 'التهاب معدة مزمن', since: '2024', doctor: 'د. فهد عمر', status: 'مستقر' },
  ],
  timeline: [
    { id: 1, date: '12 أكتوبر 2026', title: 'زيارة باطنة', text: 'متابعة السكر، طلب تحليل HbA1c وصورة دم.' },
    { id: 2, date: '2 أكتوبر 2026', title: 'زيارة طوارئ', text: 'ألم في الصدر. تخطيط القلب وأشعة الصدر طبيعيان.' },
    { id: 3, date: '18 سبتمبر 2026', title: 'رنين مغناطيسي للمخ', text: 'صداع متكرر. النتيجة طبيعية.' },
    { id: 4, date: '3 سبتمبر 2026', title: 'مقطعية للبطن', text: 'ألم بطني. لا توجد ملاحظات.' },
  ],
}

export const patientInvoices = [
  { id: 1, no: 'INV-10482', item: 'كشف باطنة وتحاليل', date: '15 أكتوبر', amount: '620 ر.س', status: 'مدفوعة', insurance: 'تغطية 80%' },
  { id: 2, no: 'INV-10431', item: 'أشعة صدر', date: '2 أكتوبر', amount: '350 ر.س', status: 'مدفوعة', insurance: 'تغطية 80%' },
  { id: 3, no: 'INV-10377', item: 'رنين مغناطيسي', date: '18 سبتمبر', amount: '1,800 ر.س', status: 'مستحقة', insurance: 'بانتظار موافقة التأمين' },
  { id: 4, no: 'INV-10290', item: 'مقطعية للبطن', date: '3 سبتمبر', amount: '1,400 ر.س', status: 'مدفوعة', insurance: 'تغطية 70%' },
]
