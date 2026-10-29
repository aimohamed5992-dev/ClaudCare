import 'package:claudcare_ui/claudcare_ui.dart';

enum Flag {
  normal('طبيعي', CcTone.green),
  high('مرتفع', CcTone.orange),
  low('منخفض', CcTone.orange),
  critical('حرج', CcTone.red);

  const Flag(this.label, this.tone);
  final String label;
  final CcTone tone;
}

class LabItem {
  const LabItem(this.name, this.value, this.unit, this.min, this.max);
  final String name, unit;
  final double value, min, max;

  Flag get flag {
    if (value < min * 0.6 || value > max * 1.6) return Flag.critical;
    if (value < min) return Flag.low;
    if (value > max) return Flag.high;
    return Flag.normal;
  }

  /// موضع القيمة على شريط المدى (0..1) مع هامش من الجانبين.
  double get position {
    final span = max - min;
    return ((value - (min - span * 0.5)) / (span * 2)).clamp(0.0, 1.0);
  }

  String get range => '${_f(min)} – ${_f(max)}';
  String get valueText => _f(value);
  static String _f(double v) => v == v.roundToDouble() ? v.toInt().toString() : v.toString();
}

class LabReport {
  const LabReport({required this.id, required this.name, required this.date, required this.doctor, required this.items, this.ready = true});
  final int id;
  final String name, date, doctor;
  final List<LabItem> items;
  final bool ready;

  int get abnormal => items.where((i) => i.flag != Flag.normal).length;
}

class ImagingStudy {
  const ImagingStudy({required this.id, required this.name, required this.date, required this.image, required this.findings, required this.impression, required this.radiologist});
  final int id;
  final String name, date, image, findings, impression, radiologist;
}

class VisitRecord {
  const VisitRecord({required this.date, required this.doctor, required this.specialty, required this.diagnosis, required this.note, this.meds = const []});
  final String date, doctor, specialty, diagnosis, note;
  final List<String> meds;
}

var healthSummary = (blood: 'O+', allergies: ['لا يوجد'], chronic: ['مقدمات السكري'], height: '165 سم', weight: '61 كجم');

List<LabReport> labReports = const [
  LabReport(
    id: 1, name: 'صورة دم كاملة (CBC)', date: '12 أكتوبر 2026', doctor: 'د. منى القاسم',
    items: [
      LabItem('الهيموجلوبين', 12.8, 'جم/دل', 12, 16),
      LabItem('كريات الدم البيضاء', 7.2, '×10³/µL', 4, 11),
      LabItem('الصفائح الدموية', 148, '×10³/µL', 150, 400),
      LabItem('الهيماتوكريت', 38, '%', 36, 48),
    ],
  ),
  LabReport(
    id: 2, name: 'سكر صائم وفيتامين د', date: '12 أكتوبر 2026', doctor: 'د. منى القاسم',
    items: [LabItem('سكر صائم', 112, 'مجم/دل', 70, 99), LabItem('فيتامين د', 14, 'نانوجرام/مل', 30, 100), LabItem('السكر التراكمي', 5.9, '%', 4, 5.6)],
  ),
  LabReport(
    id: 3, name: 'وظائف الكلى', date: '3 سبتمبر 2026', doctor: 'د. عمر الشامي',
    items: [LabItem('كرياتينين', 0.8, 'مجم/دل', 0.6, 1.2), LabItem('يوريا', 28, 'مجم/دل', 15, 45)],
  ),
  LabReport(id: 4, name: 'وظائف الغدة الدرقية', date: 'اليوم', doctor: 'د. منى القاسم', items: [], ready: false),
];

List<ImagingStudy> imagingStudies = const [
  ImagingStudy(
    id: 1, name: 'أشعة سينية للصدر', date: '12 أكتوبر 2026', image: 'assets/images/medical/xray-chest.webp', radiologist: 'د. عمر الخطيب',
    findings: 'الحقول الرئوية صافية دون كثافات أو ارتشاح. الظل القلبي ضمن الحدود الطبيعية. الزاويتان الضلعيتان الحجابيتان حادتان.',
    impression: 'أشعة صدر طبيعية.',
  ),
  ImagingStudy(
    id: 2, name: 'رنين مغناطيسي للمخ', date: '20 أغسطس 2026', image: 'assets/images/medical/mri-brain.webp', radiologist: 'د. عمر الخطيب',
    findings: 'لا توجد آفات بؤرية ولا دلائل على نزف أو احتشاء. البطينات الدماغية بحجم طبيعي وخط الوسط في موضعه.',
    impression: 'فحص رنين مغناطيسي للمخ طبيعي.',
  ),
];

List<VisitRecord> visitRecords = const [
  VisitRecord(date: '12 أكتوبر 2026', doctor: 'د. منى القاسم', specialty: 'القلب', diagnosis: 'ارتفاع طفيف في ضغط الدم', note: 'قياس الضغط المنزلي يوميًا وتقليل الملح، ومراجعة بعد 3 أسابيع.', meds: ['أملوديبين 5 مجم – مرة يوميًا']),
  VisitRecord(date: '14 سبتمبر 2026', doctor: 'د. هالة منصور', specialty: 'الجلدية', diagnosis: 'التهاب جلدي تلامسي', note: 'تجنب المهيّجات واستخدام الكريم الموضعي أسبوعين.', meds: ['كريم هيدروكورتيزون 1% – مرتين يوميًا']),
  VisitRecord(date: '3 سبتمبر 2026', doctor: 'د. عمر الشامي', specialty: 'الأعصاب', diagnosis: 'صداع توتري', note: 'تنظيم النوم والإكثار من الماء، لا حاجة لفحوصات إضافية.'),
];
