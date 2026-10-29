// type: appointment | result | alert | system | payment | message
const COMMON = [
  { id: 'c1', type: 'alert', title: 'تسجيل دخول من جهاز جديد', text: 'تم تسجيل الدخول إلى حسابك من متصفح Chrome على macOS.', time: 'قبل ساعتين', group: 'اليوم', unread: true },
  { id: 'c2', type: 'system', title: 'تحديث جديد للنظام', text: 'تمت إضافة تحسينات في سرعة الصفحات وإصلاحات عامة.', time: 'أمس 22:00', group: 'أمس', unread: false },
  { id: 'c3', type: 'system', title: 'مرحبًا بك في ClaudCare', text: 'ابدأ بإكمال بيانات ملفك الشخصي لتخصيص تجربتك.', time: 'قبل 3 أيام', group: 'هذا الأسبوع', unread: false },
]

const BY_ROLE = {
  admin: [
    { id: 'a1', type: 'system', title: 'طلب انضمام جديد', text: 'مركز الحياة الطبي أرسل طلب تسجيل بانتظار المراجعة.', time: 'قبل 10 دقائق', group: 'اليوم', unread: true },
    { id: 'a2', type: 'payment', title: 'تأخر دفع اشتراك', text: 'مستشفى السلام لم يسدد اشتراك الباقة الاحترافية.', time: 'قبل ساعة', group: 'اليوم', unread: true },
    { id: 'a3', type: 'system', title: 'نسخة احتياطية ناجحة', text: 'اكتملت النسخة الاحتياطية اليومية لكل المستشفيات.', time: 'اليوم 03:00', group: 'اليوم', unread: false },
  ],
  hospital: [
    { id: 'h1', type: 'alert', title: 'فرع الشرق قارب الامتلاء', text: 'نسبة إشغال الأسرّة وصلت إلى 93%.', time: 'قبل 15 دقيقة', group: 'اليوم', unread: true },
    { id: 'h2', type: 'message', title: 'طلب نقل مريض', text: 'طلب نقل مريض من فرع الشرق إلى الفرع الرئيسي.', time: 'قبل 20 دقيقة', group: 'اليوم', unread: true },
    { id: 'h3', type: 'payment', title: 'تقرير الإيراد الشهري جاهز', text: 'يمكنك الاطلاع على تقرير أكتوبر من صفحة التقارير.', time: 'أمس', group: 'أمس', unread: false },
  ],
  branch: [
    { id: 'b1', type: 'alert', title: 'ازدحام في قسم العظام', text: 'وقت الانتظار تجاوز 40 دقيقة.', time: 'قبل 10 دقائق', group: 'اليوم', unread: true },
    { id: 'b2', type: 'appointment', title: 'جدول المناوبات بحاجة لاعتماد', text: 'جدول الأسبوع القادم ينتظر اعتمادك.', time: 'قبل ساعة', group: 'اليوم', unread: true },
    { id: 'b3', type: 'system', title: 'طلب صيانة جهاز أشعة', text: 'تم تسجيل طلب صيانة لجهاز X-Ray 2.', time: 'أمس', group: 'أمس', unread: false },
  ],
  lab: [
    { id: 'l1', type: 'alert', title: 'طلب تحليل عاجل', text: 'صورة دم كاملة للمريض محمد علي.', time: 'قبل 5 دقائق', group: 'اليوم', unread: true },
    { id: 'l2', type: 'alert', title: 'عينة مرفوضة', text: 'العينة S-88419 مرفوضة بسبب تجلط الدم.', time: 'قبل ساعة', group: 'اليوم', unread: true },
    { id: 'l3', type: 'system', title: 'معايرة جهاز الدم', text: 'اكتملت معايرة محلل الدم بنجاح.', time: 'أمس', group: 'أمس', unread: false },
  ],
  radiology: [
    { id: 'r1', type: 'alert', title: 'فحص عاجل: سونار حوض', text: 'المريضة هند ماجد بانتظار التصوير.', time: 'قبل 8 دقائق', group: 'اليوم', unread: true },
    { id: 'r2', type: 'result', title: 'تقرير بانتظار الاعتماد', text: 'تقرير أشعة صدر للمريضة سارة أحمد (مسودة).', time: 'قبل ساعة', group: 'اليوم', unread: true },
    { id: 'r3', type: 'system', title: 'جهاز MRI متاح الآن', text: 'انتهى الفحص السابق ويمكن بدء الجدولة.', time: 'أمس', group: 'أمس', unread: false },
  ],
  pharmacy: [
    { id: 'ph1', type: 'message', title: 'وصفة عاجلة بانتظار الصرف', text: 'RX-3305 للمريضة سارة أحمد من د. فهد عمر.', time: 'قبل 10 دقائق', group: 'اليوم', unread: true },
    { id: 'ph2', type: 'alert', title: 'مخزون منخفض', text: 'أنسولين لانتوس: 6 عبوات فقط (الحد الأدنى 30).', time: 'قبل ساعة', group: 'اليوم', unread: true },
    { id: 'ph3', type: 'system', title: 'أصناف قاربت الانتهاء', text: 'سالبيوتامول بخاخ ينتهي في نوفمبر 2026.', time: 'أمس', group: 'أمس', unread: false },
  ],
  insurance: [
    { id: 'i1', type: 'alert', title: 'موافقة مسبقة عاجلة', text: 'PA-7721 – رنين مغناطيسي للمريضة سارة أحمد بانتظار القرار.', time: 'قبل 15 دقيقة', group: 'اليوم', unread: true },
    { id: 'i2', type: 'payment', title: 'مطالبة تجاوزت 30 يومًا', text: 'CL-30188 لدى شركة ملاذ لم تُسدد بعد.', time: 'قبل ساعتين', group: 'اليوم', unread: true },
    { id: 'i3', type: 'system', title: 'تجديد عقد ميدغلف', text: 'ينتهي العقد في ديسمبر 2026، يلزم بدء التجديد.', time: 'أمس', group: 'أمس', unread: false },
  ],
  finance: [
    { id: 'f1', type: 'payment', title: 'فاتورة متأخرة', text: 'INV-8801 للمريض يوسف خالد تجاوزت الاستحقاق 18 يومًا.', time: 'قبل ساعة', group: 'اليوم', unread: true },
    { id: 'f2', type: 'alert', title: 'مصروف بانتظار الاعتماد', text: 'EX-2210 صيانة جهاز الأشعة بمبلغ 12,500 ر.س.', time: 'قبل ساعتين', group: 'اليوم', unread: true },
    { id: 'f3', type: 'system', title: 'تذكير بالإقفال اليومي', text: 'لم يُقفل يوم 17 أكتوبر بعد.', time: 'أمس', group: 'أمس', unread: false },
  ],
  hr: [
    { id: 'h1', type: 'alert', title: 'ترخيص منتهٍ', text: 'ترخيص مزاولة د. عمر الشامي منتهٍ منذ 8 أيام.', time: 'قبل 20 دقيقة', group: 'اليوم', unread: true },
    { id: 'h2', type: 'appointment', title: 'طلب إجازة جديد', text: 'عمر الخطيب – إجازة طارئة غدًا.', time: 'قبل ساعة', group: 'اليوم', unread: true },
    { id: 'h3', type: 'system', title: 'تبديل مناوبة', text: 'كريم الدوسري يطلب تبديل المناوبة الليلية.', time: 'أمس', group: 'أمس', unread: false },
  ],
  emergency: [
    { id: 'e1', type: 'alert', title: 'إسعاف حرج في الطريق', text: 'AMB-12 – اشتباه جلطة دماغية، الوصول خلال 4 دقائق.', time: 'قبل دقيقتين', group: 'اليوم', unread: true },
    { id: 'e2', type: 'alert', title: 'انتظار تجاوز الحد', text: 'المريضة سارة أحمد (مستوى 2) تنتظر أكثر من 10 دقائق.', time: 'قبل 5 دقائق', group: 'اليوم', unread: true },
    { id: 'e3', type: 'system', title: 'أسرّة الطوارئ', text: 'تبقّى 5 أسرّة من 18.', time: 'أمس', group: 'أمس', unread: false },
  ],
  admissions: [
    { id: 'a1', type: 'alert', title: 'نقل بانتظار سرير', text: 'TR-410 – نورة سعد إلى العناية المركزة.', time: 'قبل 10 دقائق', group: 'اليوم', unread: true },
    { id: 'a2', type: 'appointment', title: 'طلب إدخال جديد', text: 'سارة أحمد من الطوارئ إلى الباطنة.', time: 'قبل 30 دقيقة', group: 'اليوم', unread: true },
    { id: 'a3', type: 'system', title: 'أسرّة بانتظار التنظيف', text: 'M-103 وI-303 جاهزان للتنظيف.', time: 'أمس', group: 'أمس', unread: false },
  ],
  surgery: [
    { id: 'sg1', type: 'alert', title: 'تحذير قبل العملية', text: 'SG-1043: المريضة تتناول مميّع دم، راجع الطبيب.', time: 'قبل 15 دقيقة', group: 'اليوم', unread: true },
    { id: 'sg2', type: 'appointment', title: 'عملية خلال ساعة', text: 'SG-1042 فهد العتيبي في OR-2 الساعة 09:00.', time: 'قبل ساعة', group: 'اليوم', unread: true },
    { id: 'sg3', type: 'system', title: 'OR-4 تحت الصيانة', text: 'فحص دوري لجهاز التخدير.', time: 'أمس', group: 'أمس', unread: false },
  ],
  doctor: [
    { id: 'd1', type: 'result', title: 'نتيجة صورة دم جاهزة', text: 'نتيجة المريض محمد علي تحتوي قيمًا خارج المعدل.', time: 'قبل 20 دقيقة', group: 'اليوم', unread: true },
    { id: 'd2', type: 'appointment', title: 'مريض في الانتظار', text: 'وصلت المريضة سارة أحمد لموعدها 09:30.', time: 'قبل 30 دقيقة', group: 'اليوم', unread: true },
    { id: 'd3', type: 'message', title: 'رسالة جديدة', text: 'الممرضة نادين يوسف: ضغط المريض في غرفة 206 مرتفع.', time: 'أمس', group: 'أمس', unread: false },
  ],
  nurse: [
    { id: 'n1', type: 'alert', title: 'جرعة متأخرة', text: 'أنسولين – غرفة 204 تأخر عن موعده 30 دقيقة.', time: 'قبل 5 دقائق', group: 'اليوم', unread: true },
    { id: 'n2', type: 'alert', title: 'علامات حيوية حرجة', text: 'أكسجين المريض فؤاد سالم (غرفة 210) 91%.', time: 'قبل 10 دقائق', group: 'اليوم', unread: true },
    { id: 'n3', type: 'appointment', title: 'تغيير في المناوبة', text: 'تم تعديل مناوبتك يوم الأربعاء إلى مسائية.', time: 'أمس', group: 'أمس', unread: false },
  ],
  reception: [
    { id: 'p1', type: 'appointment', title: 'تأكيد حجز', text: 'المريض يوسف خالد أكّد موعده غدًا 10:00.', time: 'قبل 10 دقائق', group: 'اليوم', unread: true },
    { id: 'p2', type: 'alert', title: 'مريض ينتظر منذ 25 دقيقة', text: 'عبدالله ناصر – عيادة الباطنة.', time: 'قبل 15 دقيقة', group: 'اليوم', unread: true },
    { id: 'p3', type: 'payment', title: 'فاتورة غير مدفوعة', text: 'الفاتورة INV-10481 ما زالت بانتظار التحصيل.', time: 'أمس', group: 'أمس', unread: false },
  ],
  patient: [
    { id: 'm1', type: 'appointment', title: 'تذكير بموعدك غدًا', text: 'موعدك مع د. منى القاسم غدًا 10:30 صباحًا.', time: 'قبل ساعة', group: 'اليوم', unread: true },
    { id: 'm2', type: 'result', title: 'نتيجة تحليلك جاهزة', text: 'صورة الدم الكاملة متاحة الآن في صفحة النتائج.', time: 'قبل 3 ساعات', group: 'اليوم', unread: true },
    { id: 'm3', type: 'payment', title: 'فاتورة مستحقة', text: 'الفاتورة INV-10377 بمبلغ 1,800 ر.س بانتظار السداد.', time: 'أمس', group: 'أمس', unread: false },
  ],
}

export const getNotifications = (roleKey) => [...(BY_ROLE[roleKey] ?? []), ...COMMON]

export const TYPE_META = {
  appointment: { icon: 'calendar', tone: 'blue', label: 'مواعيد' },
  result: { icon: 'flask', tone: 'green', label: 'نتائج' },
  alert: { icon: 'alert', tone: 'red', label: 'تنبيهات' },
  system: { icon: 'info', tone: 'gray', label: 'النظام' },
  payment: { icon: 'wallet', tone: 'orange', label: 'المدفوعات' },
  message: { icon: 'mail', tone: 'blue', label: 'رسائل' },
}
