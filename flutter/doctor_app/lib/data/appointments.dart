import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/foundation.dart';
import 'clock.dart';
import 'remote.dart';

export 'clock.dart' show demoNowMinutes;

enum ApptStatus {
  pending('بانتظار التأكيد', CcTone.orange),
  confirmed('مؤكد', CcTone.green),
  arrived('وصل المريض', CcTone.blue),
  inProgress('قيد الكشف', CcTone.blue),
  done('منتهي', CcTone.gray),
  cancelled('ملغى', CcTone.red);

  const ApptStatus(this.label, this.tone);
  final String label;
  final CcTone tone;
}

enum ApptType {
  clinic('في العيادة', false),
  followUp('متابعة', false),
  video('استشارة عن بُعد', true);

  const ApptType(this.label, this.isVideo);
  final String label;
  final bool isVideo;
}

class Appointment {
  const Appointment({
    required this.id,
    required this.patient,
    required this.age,
    required this.avatar,
    required this.day,
    required this.minutes,
    required this.reason,
    this.duration = 30,
    this.type = ApptType.clinic,
    this.status = ApptStatus.confirmed,
    this.cancelReason,
    this.patientId = 0,
  });

  final int id;
  final int patientId;
  final String patient;
  final int age;
  final String avatar;
  final int day; // 0 = اليوم
  final int minutes; // من منتصف الليل
  final int duration;
  final String reason;
  final ApptType type;
  final ApptStatus status;
  final String? cancelReason;

  bool get active => status != ApptStatus.cancelled && status != ApptStatus.done;
  int get end => minutes + duration;

  Appointment copyWith({ApptStatus? status, int? day, int? minutes, String? cancelReason}) => Appointment(
        id: id,
        patient: patient,
        age: age,
        avatar: avatar,
        day: day ?? this.day,
        minutes: minutes ?? this.minutes,
        reason: reason,
        duration: duration,
        type: type,
        status: status ?? this.status,
        cancelReason: cancelReason ?? this.cancelReason,
        patientId: patientId,
      );
}

const _a = 'assets/images/avatars/';

/// بيانات تجريبية. الوقت الحالي المحاكى = 09:15 صباحًا حتى تكون الشاشات ثابتة (انظر clock.dart).
const clinicStart = 8 * 60, clinicEnd = 16 * 60, slotStep = 30;

final _seed = <Appointment>[
  const Appointment(id: 1, patient: 'عبدالله ناصر', age: 70, avatar: '${_a}patient-4.webp', day: 0, minutes: 8 * 60 + 30, reason: 'متابعة ضغط الدم', type: ApptType.followUp, status: ApptStatus.done),
  const Appointment(id: 2, patient: 'محمد علي', age: 52, avatar: '${_a}patient-3.webp', day: 0, minutes: 9 * 60, reason: 'متابعة السكري', type: ApptType.followUp, status: ApptStatus.inProgress),
  const Appointment(id: 3, patient: 'سارة أحمد', age: 34, avatar: '${_a}patient-2.webp', day: 0, minutes: 9 * 60 + 30, reason: 'ألم في الصدر', status: ApptStatus.arrived),
  const Appointment(id: 4, patient: 'يوسف خالد', age: 8, avatar: '${_a}patient-1.webp', day: 0, minutes: 10 * 60, reason: 'كشف دوري'),
  const Appointment(id: 5, patient: 'نورة سعد', age: 41, avatar: '${_a}patient-2.webp', day: 0, minutes: 10 * 60 + 30, reason: 'مراجعة نتائج التحاليل', status: ApptStatus.pending),
  const Appointment(id: 6, patient: 'ريم العلي', age: 28, avatar: '${_a}patient-2.webp', day: 0, minutes: 11 * 60 + 30, reason: 'استشارة عن نتائج الفحص', type: ApptType.video),
  const Appointment(id: 7, patient: 'هند ماجد', age: 22, avatar: '${_a}patient-2.webp', day: 0, minutes: 13 * 60, reason: 'فحص ما قبل العملية', status: ApptStatus.pending),
  const Appointment(id: 8, patient: 'زياد الحربي', age: 7, avatar: '${_a}patient-1.webp', day: 0, minutes: 14 * 60, reason: 'متابعة بعد الالتهاب', type: ApptType.video),
  const Appointment(id: 9, patient: 'ريم العلي', age: 28, avatar: '${_a}patient-2.webp', day: 1, minutes: 9 * 60, reason: 'مراجعة تحاليل', type: ApptType.followUp),
  const Appointment(id: 10, patient: 'فهد العتيبي', age: 52, avatar: '${_a}patient-4.webp', day: 1, minutes: 10 * 60 + 30, reason: 'فحص ما قبل العملية', status: ApptStatus.pending),
  const Appointment(id: 11, patient: 'محمد علي', age: 52, avatar: '${_a}patient-3.webp', day: 1, minutes: 11 * 60 + 30, reason: 'تقييم الأدوية', type: ApptType.followUp),
  const Appointment(id: 12, patient: 'سارة أحمد', age: 34, avatar: '${_a}patient-2.webp', day: 2, minutes: 9 * 60 + 30, reason: 'متابعة ضغط الدم', type: ApptType.followUp),
  const Appointment(id: 13, patient: 'نورة سعد', age: 41, avatar: '${_a}patient-2.webp', day: 2, minutes: 12 * 60, reason: 'استشارة', status: ApptStatus.pending, type: ApptType.video),
];

/// حالة المواعيد المشتركة بين الرئيسية وتبويب المواعيد.
class AppointmentsController extends ChangeNotifier {
  AppointmentsController([List<Appointment>? initial]) : _items = List.of(initial ?? _seed);

  List<Appointment> _items;
  int _nextId = 100;
  DoctorRemote? remote;

  void replaceAll(List<Appointment> items) {
    _items = List.of(items);
    notifyListeners();
  }

  List<Appointment> get all => List.unmodifiable(_items);

  List<Appointment> forDay(int day) => _items.where((a) => a.day == day).toList()..sort((a, b) => a.minutes.compareTo(b.minutes));

  int countToday(bool Function(Appointment) test) => forDay(0).where(test).length;

  /// الحالي (قيد الكشف) ثم الواصل ثم أقرب موعد قادم اليوم.
  Appointment? get focus {
    final today = forDay(0);
    for (final s in [ApptStatus.inProgress, ApptStatus.arrived]) {
      final m = today.where((a) => a.status == s);
      if (m.isNotEmpty) return m.first;
    }
    final upcoming = today.where((a) => a.active && a.minutes >= nowMinutes);
    return upcoming.isEmpty ? null : upcoming.first;
  }

  Appointment byId(int id) => _items.firstWhere((a) => a.id == id);

  void _replace(int id, Appointment Function(Appointment) f) {
    final i = _items.indexWhere((a) => a.id == id);
    _items[i] = f(_items[i]);
    notifyListeners();
  }

  void confirm(int id) {
    _replace(id, (a) => a.copyWith(status: ApptStatus.confirmed));
    remote?.act(id, 'confirm');
  }

  void start(int id) {
    _replace(id, (a) => a.copyWith(status: ApptStatus.inProgress));
    remote?.act(id, 'start');
  }

  /// إنهاء الكشف: على الخادم يتم بحفظ الكشف نفسه، فلا نرسل شيئًا هنا.
  void finish(int id) => _replace(id, (a) => a.copyWith(status: ApptStatus.done));

  void cancel(int id, String reason) {
    _replace(id, (a) => a.copyWith(status: ApptStatus.cancelled, cancelReason: reason));
    remote?.cancelAppointment(id, reason);
  }

  /// «تراجع»: لا عكس في الخادم لتأكيد/إلغاء، فنعيد التحميل لنعرض الحقيقة.
  void restore(int id, ApptStatus status) {
    _replace(id, (a) => a.copyWith(status: status));
    remote?.reloadAppointments();
  }

  void reschedule(int id, int day, int minutes) {
    _replace(id, (a) => a.copyWith(day: day, minutes: minutes, status: ApptStatus.confirmed));
    remote?.rescheduleAppointment(id, day, minutes);
  }

  /// خانات متاحة في [day] لا تتعارض مع مواعيد أخرى (وفي اليوم الحالي بعد الوقت الحالي).
  List<int> freeSlots(int day, int duration, {int? excludeId}) {
    final busy = _items.where((a) => a.day == day && a.active && a.id != excludeId).toList();
    var (start, end) = (clinicStart, clinicEnd);
    if (remote != null) {
      // الدوام الفعلي لذلك اليوم من إعدادات الطبيب (الأحد = 0)
      final w = remote!.workDayFor(day);
      if (w == null || !w.on) return const [];
      (start, end) = (w.from, w.to);
    }
    return [
      for (var m = start; m + duration <= end; m += slotStep)
        if ((day > 0 || m > nowMinutes) && !busy.any((b) => m < b.end && b.minutes < m + duration)) m,
    ];
  }

  int get nextId => _nextId++;
}
