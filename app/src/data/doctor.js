export const doctorAppointments = [
  { id: 1, patient: 'محمد علي', age: 52, reason: 'متابعة سكري', time: '09:00', date: 'اليوم', status: 'بالانتظار', avatar: 'avatars/patient-3.jpg' },
  { id: 2, patient: 'سارة أحمد', age: 34, reason: 'ألم في الصدر', time: '09:30', date: 'اليوم', status: 'حاضر', avatar: 'avatars/patient-2.jpg' },
  { id: 3, patient: 'يوسف خالد', age: 8, reason: 'كشف دوري', time: '10:00', date: 'اليوم', status: 'مجدول', avatar: 'avatars/patient-1.jpg' },
  { id: 4, patient: 'نورة سعد', age: 41, reason: 'نتائج تحاليل', time: '10:30', date: 'اليوم', status: 'مجدول', avatar: 'avatars/patient-2.jpg' },
  { id: 5, patient: 'عبدالله ناصر', age: 70, reason: 'ضغط مرتفع', time: '08:30', date: 'اليوم', status: 'مكتمل', avatar: 'avatars/patient-4.jpg' },
  { id: 6, patient: 'ريم العلي', age: 28, reason: 'مراجعة تحاليل', time: '10:30', date: 'غدًا', status: 'مجدول', avatar: 'avatars/patient-2.jpg' },
]

export const doctorPatients = [
  { id: 1, name: 'محمد علي', age: 52, gender: 'ذكر', condition: 'سكري النوع الثاني', last: '12 أكتوبر', blood: 'A+', phone: '0551234567', avatar: 'avatars/patient-3.jpg' },
  { id: 2, name: 'سارة أحمد', age: 34, gender: 'أنثى', condition: 'ارتفاع ضغط الدم', last: '2 أكتوبر', blood: 'O+', phone: '0557654321', avatar: 'avatars/patient-2.jpg' },
  { id: 3, name: 'يوسف خالد', age: 8, gender: 'ذكر', condition: 'ربو قصبي', last: '28 سبتمبر', blood: 'B+', phone: '0560011223', avatar: 'avatars/patient-1.jpg' },
  { id: 4, name: 'عبدالله ناصر', age: 70, gender: 'ذكر', condition: 'قصور في القلب', last: '18 أكتوبر', blood: 'AB+', phone: '0598877665', avatar: 'avatars/patient-4.jpg' },
  { id: 5, name: 'ريم العلي', age: 28, gender: 'أنثى', condition: 'مقدمات السكري', last: '12 أكتوبر', blood: 'O+', phone: '0551234567', avatar: 'avatars/patient-2.jpg' },
]

export const prescriptions = [
  { id: 1, no: 'RX-3301', patient: 'محمد علي', meds: 'ميتفورمين 500 مجم + أوميبرازول 20 مجم', duration: '30 يومًا', date: '12 أكتوبر', status: 'نشطة', avatar: 'avatars/patient-3.jpg' },
  { id: 2, no: 'RX-3298', patient: 'سارة أحمد', meds: 'أملوديبين 5 مجم', duration: '60 يومًا', date: '2 أكتوبر', status: 'مُصرفة', avatar: 'avatars/patient-2.jpg' },
  { id: 3, no: 'RX-3290', patient: 'يوسف خالد', meds: 'بخاخ سالبيوتامول', duration: '14 يومًا', date: '28 سبتمبر', status: 'منتهية', avatar: 'avatars/patient-1.jpg' },
  { id: 4, no: 'RX-3287', patient: 'ريم العلي', meds: 'فيتامين د 50,000', duration: '12 أسبوعًا', date: '12 أكتوبر', status: 'نشطة', avatar: 'avatars/patient-2.jpg' },
]

export const orders = [
  { id: 1, no: 'LAB-2210', kind: 'مختبر', test: 'صورة دم كاملة', patient: 'محمد علي', date: 'اليوم 08:10', priority: 'عاجل', status: 'قيد التنفيذ' },
  { id: 2, no: 'RAD-1182', kind: 'أشعة', test: 'أشعة صدر', patient: 'سارة أحمد', date: 'اليوم 08:40', priority: 'عادي', status: 'قيد التنفيذ' },
  { id: 3, no: 'LAB-2209', kind: 'مختبر', test: 'سكر تراكمي', patient: 'يوسف خالد', date: 'اليوم 07:55', priority: 'عادي', status: 'النتيجة جاهزة' },
  { id: 4, no: 'RAD-1181', kind: 'أشعة', test: 'رنين مغناطيسي للمخ', patient: 'ليلى كريم', date: 'أمس 16:20', priority: 'عادي', status: 'النتيجة جاهزة' },
  { id: 5, no: 'LAB-2208', kind: 'مختبر', test: 'وظائف كلى', patient: 'عبدالله ناصر', date: 'أمس 12:05', priority: 'عاجل', status: 'تم الطلب' },
]

export const conversations = [
  {
    id: 1, name: 'نادين يوسف', role: 'ممرضة – قسم الباطنة', avatar: 'avatars/nurse-f1.jpg', unread: 2,
    messages: [
      { from: 'them', text: 'دكتورة منى، ضغط المريض في غرفة 206 مرتفع (160/100).', time: '08:42' },
      { from: 'me', text: 'سجّلي القراءة مرة أخرى بعد 15 دقيقة وأبلغيني.', time: '08:44' },
      { from: 'them', text: 'تمت إعادة القياس: 150/95. هل أعطيه الدواء الاحتياطي؟', time: '09:02' },
    ],
  },
  {
    id: 2, name: 'سلمى حسن', role: 'فنية مختبر', avatar: 'avatars/lab-tech.jpg', unread: 0,
    messages: [
      { from: 'them', text: 'نتيجة صورة الدم لمحمد علي جاهزة.', time: 'أمس' },
      { from: 'me', text: 'شكرًا، سأراجعها الآن.', time: 'أمس' },
    ],
  },
  {
    id: 3, name: 'عمر الخطيب', role: 'أخصائي أشعة', avatar: 'avatars/radiologist.jpg', unread: 1,
    messages: [{ from: 'them', text: 'تقرير أشعة الصدر لسارة أحمد تم اعتماده.', time: '08:50' }],
  },
]

export const weekVisits = [
  { label: 'س', value: 14 }, { label: 'ح', value: 18 }, { label: 'ن', value: 16 }, { label: 'ث', value: 20 },
  { label: 'ر', value: 17 }, { label: 'خ', value: 12 }, { label: 'ج', value: 5 },
]

export const LAB_TESTS = ['صورة دم كاملة', 'سكر تراكمي', 'وظائف كبد', 'وظائف كلى', 'دهون الدم', 'فيتامين د']
export const RAD_TESTS = ['أشعة صدر', 'سونار بطن', 'مقطعية للبطن', 'رنين مغناطيسي للمخ']
