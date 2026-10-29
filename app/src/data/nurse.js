export const wardPatients = [
  { id: 1, name: 'خالد منصور', age: 61, room: '204', doctor: 'د. منى القاسم', condition: 'التهاب رئوي', status: 'مستقر', last: 'قبل 30 د', avatar: 'avatars/patient-3.jpg' },
  { id: 2, name: 'ريم عادل', age: 35, room: '206', doctor: 'د. فهد عمر', condition: 'ارتفاع ضغط', status: 'مراقبة', last: 'قبل 50 د', avatar: 'avatars/patient-2.jpg' },
  { id: 3, name: 'فؤاد سالم', age: 74, room: '210', doctor: 'د. عمر الشامي', condition: 'قصور قلب', status: 'حرج', last: 'قبل 10 د', avatar: 'avatars/patient-4.jpg' },
  { id: 4, name: 'ليان طارق', age: 9, room: '212', doctor: 'د. ريم العتيبي', condition: 'التهاب شعبي', status: 'مستقر', last: 'قبل ساعة', avatar: 'avatars/patient-1.jpg' },
]

export const vitalsHistory = [
  { id: 1, patient: 'فؤاد سالم', bp: '90/60', pulse: 112, temp: 38.1, spo2: 91, time: 'قبل 10 د', flags: ['ضغط منخفض', 'نبض مرتفع', 'حرارة مرتفعة', 'أكسجين منخفض'] },
  { id: 2, patient: 'ريم عادل', bp: '150/95', pulse: 88, temp: 36.8, spo2: 97, time: 'قبل 50 د', flags: ['ضغط مرتفع'] },
  { id: 3, patient: 'خالد منصور', bp: '120/80', pulse: 76, temp: 36.7, spo2: 98, time: 'قبل 30 د', flags: [] },
]

export const doses = [
  { id: 1, patient: 'خالد منصور', room: '204', drug: 'أنسولين', dose: '10 وحدات', time: '08:30', status: 'متأخرة' },
  { id: 2, patient: 'ريم عادل', room: '206', drug: 'مضاد حيوي', dose: '1 جم وريدي', time: '09:00', status: 'قادمة' },
  { id: 3, patient: 'فؤاد سالم', room: '210', drug: 'مسكن', dose: '500 مجم', time: '09:15', status: 'قادمة' },
  { id: 4, patient: 'ليان طارق', room: '212', drug: 'بخاخ موسّع شعب', dose: 'بختان', time: '07:30', status: 'تم الإعطاء' },
  { id: 5, patient: 'فؤاد سالم', room: '210', drug: 'مدر للبول', dose: '40 مجم', time: '10:00', status: 'قادمة' },
]

export const notes = [
  { id: 1, patient: 'فؤاد سالم', category: 'حالة حرجة', text: 'انخفاض الأكسجين إلى 91%، تم رفع الأكسجين وإبلاغ الطبيب المناوب.', time: 'قبل 10 د', author: 'نادين يوسف' },
  { id: 2, patient: 'ريم عادل', category: 'ملاحظة عامة', text: 'المريضة تشكو من صداع خفيف، الضغط 150/95، تمت متابعة القراءة.', time: 'قبل 50 د', author: 'نادين يوسف' },
  { id: 3, patient: 'ليان طارق', category: 'تثقيف', text: 'تم شرح طريقة استخدام البخاخ لوالدة المريضة.', time: 'قبل ساعتين', author: 'لمى الحسن' },
]

export const shifts = [
  { id: 1, name: 'نادين يوسف', avatar: 'avatars/nurse-f1.jpg', days: ['صباحي', 'صباحي', 'مسائي', 'مسائي', 'إجازة', 'صباحي', 'إجازة'] },
  { id: 2, name: 'لمى الحسن', avatar: 'avatars/nurse-f3.jpg', days: ['مسائي', 'مسائي', 'صباحي', 'صباحي', 'صباحي', 'إجازة', 'إجازة'] },
  { id: 3, name: 'خالد منصور', avatar: 'avatars/nurse-m1.jpg', days: ['ليلي', 'ليلي', 'ليلي', 'إجازة', 'إجازة', 'ليلي', 'ليلي'] },
  { id: 4, name: 'سمية العمر', avatar: 'avatars/nurse-f2.jpg', days: ['إجازة', 'صباحي', 'صباحي', 'مسائي', 'مسائي', 'صباحي', 'صباحي'] },
]

export const DAYS = ['السبت', 'الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة']

export const wardChart = [
  { label: '6', value: 40 }, { label: '8', value: 75 }, { label: '10', value: 55 }, { label: '12', value: 68 },
  { label: '14', value: 50 }, { label: '16', value: 60 }, { label: '18', value: 35 },
]
