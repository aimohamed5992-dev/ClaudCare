import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/foundation.dart';
import 'appointments.dart' show Appointment;
import 'remote.dart';

enum LabFlag {
  normal('طبيعي', CcTone.green),
  high('مرتفع', CcTone.orange),
  low('منخفض', CcTone.orange),
  critical('حرج', CcTone.red);

  const LabFlag(this.label, this.tone);
  final String label;
  final CcTone tone;
}

class LabResult {
  const LabResult(this.name, this.value, this.unit, this.range, this.flag, this.date);
  final String name, value, unit, range, date;
  final LabFlag flag;
}

class Visit {
  const Visit({required this.date, required this.diagnosis, required this.note, this.doctor = 'د. منى القاسم'});
  final String date, diagnosis, note, doctor;
}

class Rx {
  const Rx({required this.no, required this.date, required this.meds, this.active = true});
  final String no, date;
  final List<String> meds;
  final bool active;
}

class Patient {
  Patient({
    required this.id,
    required this.name,
    required this.age,
    required this.gender,
    required this.avatar,
    required this.blood,
    required this.phone,
    required this.lastVisit,
    this.conditions = const [],
    this.allergies = const [],
    this.vitals = const {},
    this.activeMeds = const [],
    List<LabResult>? labs,
    List<Visit>? visits,
    List<Rx>? prescriptions,
  })  : labs = labs ?? [],
        visits = visits ?? [],
        prescriptions = prescriptions ?? [];

  final int id;
  final String name;
  final int age;
  final String gender, avatar, blood, phone;
  String lastVisit;
  final List<String> conditions, allergies, activeMeds;
  final Map<String, String> vitals;
  final List<LabResult> labs;
  final List<Visit> visits;
  final List<Rx> prescriptions;

  String get mainCondition => conditions.isEmpty ? 'بدون أمراض مزمنة' : conditions.first;
  bool get chronic => conditions.isNotEmpty;
}

const _a = 'assets/images/avatars/';

List<Patient> _seed() => [
      Patient(
        id: 1, name: 'محمد علي', age: 52, gender: 'ذكر', avatar: '${_a}patient-3.webp', blood: 'A+', phone: '0551234567', lastVisit: '12 أكتوبر',
        conditions: ['سكري النوع الثاني', 'ارتفاع الكوليسترول'], allergies: [],
        vitals: {'الضغط': '135/85', 'النبض': '78', 'السكر': '168', 'الوزن': '88 كجم'},
        activeMeds: ['ميتفورمين 500 مجم', 'أوميبرازول 20 مجم'],
        labs: const [
          LabResult('سكر تراكمي (HbA1c)', '8.1', '%', '4 – 5.6', LabFlag.high, '12 أكتوبر'),
          LabResult('كوليسترول كلي', '212', 'مجم/دل', '< 200', LabFlag.high, '12 أكتوبر'),
          LabResult('وظائف الكلى (كرياتينين)', '0.9', 'مجم/دل', '0.7 – 1.3', LabFlag.normal, '12 أكتوبر'),
        ],
        visits: [
          const Visit(date: '12 أكتوبر', diagnosis: 'سكري غير منضبط', note: 'رفع جرعة الميتفورمين وإعادة التحليل بعد 3 أشهر.'),
          const Visit(date: '14 يوليو', diagnosis: 'متابعة سكري', note: 'التزم بالحمية. السكر التراكمي 7.4.'),
        ],
        prescriptions: [const Rx(no: 'RX-3301', date: '12 أكتوبر', meds: ['ميتفورمين 500 مجم – مرتين يوميًا', 'أوميبرازول 20 مجم – مرة صباحًا'])],
      ),
      Patient(
        id: 2, name: 'سارة أحمد', age: 34, gender: 'أنثى', avatar: '${_a}patient-2.webp', blood: 'O+', phone: '0557654321', lastVisit: '2 أكتوبر',
        conditions: ['ارتفاع ضغط الدم'], allergies: ['البنسلين'],
        vitals: {'الضغط': '148/92', 'النبض': '84', 'السكر': '96', 'الوزن': '70 كجم'},
        activeMeds: ['أملوديبين 5 مجم'],
        labs: const [
          LabResult('صوديوم', '139', 'ميلي مول/لتر', '135 – 145', LabFlag.normal, '2 أكتوبر'),
          LabResult('بوتاسيوم', '5.4', 'ميلي مول/لتر', '3.5 – 5.1', LabFlag.high, '2 أكتوبر'),
        ],
        visits: [const Visit(date: '2 أكتوبر', diagnosis: 'ارتفاع ضغط الدم', note: 'بدء أملوديبين 5 مجم ومتابعة القياس المنزلي.')],
        prescriptions: [const Rx(no: 'RX-3298', date: '2 أكتوبر', meds: ['أملوديبين 5 مجم – مرة يوميًا'])],
      ),
      Patient(
        id: 3, name: 'يوسف خالد', age: 8, gender: 'ذكر', avatar: '${_a}patient-1.webp', blood: 'B+', phone: '0560011223', lastVisit: '28 سبتمبر',
        conditions: ['ربو قصبي'], allergies: ['الغبار'],
        vitals: {'الضغط': '100/65', 'النبض': '92', 'الحرارة': '36.8', 'الوزن': '26 كجم'},
        activeMeds: ['بخاخ سالبيوتامول'],
        visits: [const Visit(date: '28 سبتمبر', diagnosis: 'ربو قصبي – نوبة خفيفة', note: 'بخاخ عند اللزوم وتجنب المثيرات.')],
        prescriptions: [const Rx(no: 'RX-3290', date: '28 سبتمبر', meds: ['بخاخ سالبيوتامول – عند الحاجة'], active: false)],
      ),
      Patient(
        id: 4, name: 'عبدالله ناصر', age: 70, gender: 'ذكر', avatar: '${_a}patient-4.webp', blood: 'AB+', phone: '0598877665', lastVisit: '18 أكتوبر',
        conditions: ['قصور في القلب', 'ارتفاع ضغط الدم'], allergies: ['الأسبرين'],
        vitals: {'الضغط': '150/90', 'النبض': '70', 'SpO₂': '95%', 'الوزن': '79 كجم'},
        activeMeds: ['أملوديبين 5 مجم', 'فيوروسيميد 40 مجم'],
        labs: const [
          LabResult('كرياتينين', '2.1', 'مجم/دل', '0.7 – 1.3', LabFlag.critical, '18 أكتوبر'),
          LabResult('بوتاسيوم', '4.8', 'ميلي مول/لتر', '3.5 – 5.1', LabFlag.normal, '18 أكتوبر'),
        ],
        visits: [const Visit(date: '18 أكتوبر', diagnosis: 'قصور القلب – مستقر', note: 'ارتفاع الكرياتينين: مراجعة جرعة المدر وإعادة التحليل.')],
        prescriptions: [const Rx(no: 'RX-3287', date: '18 أكتوبر', meds: ['فيوروسيميد 40 مجم – صباحًا'])],
      ),
      Patient(
        id: 5, name: 'نورة سعد', age: 41, gender: 'أنثى', avatar: '${_a}patient-2.webp', blood: 'A-', phone: '0544455566', lastVisit: '5 أكتوبر',
        conditions: ['قصور الغدة الدرقية'], allergies: [],
        vitals: {'الضغط': '118/76', 'النبض': '66', 'الوزن': '64 كجم'},
        activeMeds: ['ليفوثيروكسين 100 مكجم'],
        labs: const [LabResult('TSH', '6.8', 'mIU/L', '0.4 – 4.0', LabFlag.high, '5 أكتوبر')],
        visits: [const Visit(date: '5 أكتوبر', diagnosis: 'قصور الغدة الدرقية', note: 'زيادة الجرعة وإعادة TSH بعد 6 أسابيع.')],
      ),
      Patient(
        id: 6, name: 'ريم العلي', age: 28, gender: 'أنثى', avatar: '${_a}patient-2.webp', blood: 'O+', phone: '0551234500', lastVisit: '12 أكتوبر',
        conditions: ['مقدمات السكري'], allergies: [],
        vitals: {'الضغط': '112/72', 'النبض': '72', 'السكر': '112', 'الوزن': '61 كجم'},
        activeMeds: ['فيتامين د 50,000'],
        labs: const [LabResult('سكر صائم', '112', 'مجم/دل', '70 – 99', LabFlag.high, '12 أكتوبر'), LabResult('فيتامين د', '14', 'نانوجرام/مل', '30 – 100', LabFlag.low, '12 أكتوبر')],
        visits: [const Visit(date: '12 أكتوبر', diagnosis: 'مقدمات السكري ونقص فيتامين د', note: 'حمية ونشاط بدني وجرعة فيتامين د أسبوعية.')],
      ),
      Patient(
        id: 7, name: 'هند ماجد', age: 22, gender: 'أنثى', avatar: '${_a}patient-2.webp', blood: 'B+', phone: '0533322110', lastVisit: '1 أكتوبر',
        allergies: ['اللاتكس'], vitals: {'الضغط': '110/70', 'النبض': '74', 'الوزن': '55 كجم'},
        visits: [const Visit(date: '1 أكتوبر', diagnosis: 'التهاب الزائدة المشتبه', note: 'إحالة للجراحة وطلب فحوصات ما قبل العملية.')],
      ),
      Patient(
        id: 8, name: 'فهد العتيبي', age: 52, gender: 'ذكر', avatar: '${_a}patient-4.webp', blood: 'O-', phone: '0566677700', lastVisit: '20 سبتمبر',
        conditions: ['فتق إربي'], vitals: {'الضغط': '128/82', 'النبض': '76', 'الوزن': '92 كجم'},
        visits: [const Visit(date: '20 سبتمبر', diagnosis: 'فتق إربي أيمن', note: 'مرشح لإصلاح جراحي.')],
      ),
      Patient(
        id: 9, name: 'زياد الحربي', age: 7, gender: 'ذكر', avatar: '${_a}patient-1.webp', blood: 'A+', phone: '0500099887', lastVisit: '10 أكتوبر',
        allergies: ['البنسلين'], vitals: {'النبض': '96', 'الحرارة': '37.1', 'الوزن': '24 كجم'},
        visits: [const Visit(date: '10 أكتوبر', diagnosis: 'التهاب لوزتين', note: 'أزيثروميسين 5 أيام (حساسية بنسلين).')],
      ),
    ];

/// قاعدة أدوية مبسطة للوصفة. group = العائلة الدوائية (للتحقق من الحساسية).
class Drug {
  const Drug(this.name, this.group, {this.id = 0});
  final String name, group;
  final int id;
}

List<Drug> drugs = const [
  Drug('أموكسيسيلين 500 مجم', 'البنسلين'),
  Drug('أزيثروميسين 500 مجم', 'ماكروليد'),
  Drug('باراسيتامول 500 مجم', 'مسكن'),
  Drug('إيبوبروفين 400 مجم', 'مضاد التهاب'),
  Drug('أسبرين 81 مجم', 'الأسبرين'),
  Drug('ميتفورمين 500 مجم', 'السكري'),
  Drug('أملوديبين 5 مجم', 'الضغط'),
  Drug('أوميبرازول 20 مجم', 'المعدة'),
  Drug('بخاخ سالبيوتامول', 'الربو'),
  Drug('فيتامين د 50,000', 'فيتامين'),
];

const frequencies = ['مرة يوميًا', 'مرتين يوميًا', '3 مرات يوميًا', 'عند الحاجة'];
const testOptions = ['صورة دم كاملة', 'سكر تراكمي', 'وظائف الكلى', 'وظائف الكبد', 'أشعة صدر', 'تخطيط قلب'];
const followUps = ['بعد أسبوع', 'بعد أسبوعين', 'بعد شهر', 'عند اللزوم'];

class PatientsController extends ChangeNotifier {
  PatientsController([List<Patient>? initial]) : _items = initial ?? _seed();
  List<Patient> _items;
  DoctorRemote? remote;

  void replaceAll(List<Patient> items) {
    _items = List.of(items);
    notifyListeners();
  }

  /// يضيف/يستبدل مريضًا بنفس المعرّف (تفاصيل وصلت من الخادم).
  void upsert(Patient p) {
    final i = _items.indexWhere((x) => x.id == p.id);
    i < 0 ? _items.add(p) : _items[i] = p;
    notifyListeners();
  }

  /// مريض الموعد: بالمعرّف (الخادم) وإلا بالاسم (البيانات التجريبية).
  Patient? forAppointment(Appointment a) {
    if (a.patientId != 0) {
      for (final p in _items) {
        if (p.id == a.patientId) return p;
      }
    }
    return byName(a.patient);
  }
  int _rx = 3400;

  List<Patient> get all => List.unmodifiable(_items);
  Patient byId(int id) => _items.firstWhere((p) => p.id == id);
  Patient? byName(String name) {
    for (final p in _items) {
      if (p.name == name) return p;
    }
    return null;
  }

  List<Patient> search(String q) {
    final s = q.trim();
    return s.isEmpty ? all : _items.where((p) => p.name.contains(s) || p.conditions.any((c) => c.contains(s)) || p.phone.contains(s)).toList();
  }

  String nextRxNo() => 'RX-${_rx++}';

  void addVisit(int id, Visit v, {Rx? rx}) {
    final p = byId(id);
    p.visits.insert(0, v);
    p.lastVisit = v.date;
    if (rx != null) {
      p.prescriptions.insert(0, rx);
    }
    notifyListeners();
  }
}
