import 'package:flutter/foundation.dart';
import 'clock.dart';
import 'doctors.dart';
import 'remote.dart';

export 'clock.dart' show demoNowMinutes;

enum ApptStatus {
  upcoming('قادم'),
  done('مكتمل'),
  cancelled('ملغى');

  const ApptStatus(this.label);
  final String label;
}

enum VisitType {
  clinic('كشف في العيادة', false),
  video('استشارة عن بُعد', true);

  const VisitType(this.label, this.isVideo);
  final String label;
  final bool isVideo;
}

const slotStart = 9 * 60, slotEnd = 16 * 60, slotStep = 30;

class PatientAppt {
  const PatientAppt({
    required this.id,
    required this.doctorId,
    required this.day,
    required this.minutes,
    required this.type,
    this.status = ApptStatus.upcoming,
    this.forWhom = 'أنا',
    this.reason = '',
    this.price = 0,
    this.paid = false,
    this.cancelReason,
    this.rating,
    this.ratingComment,
  });

  final int id, doctorId, day, minutes, price;
  final VisitType type;
  final ApptStatus status;
  final String forWhom, reason;
  final bool paid;
  final String? cancelReason, ratingComment;
  final int? rating;

  /// الساعات المتبقية للموعد (سالبة إن مضى).
  double get hoursUntil => day * 24 + (minutes - nowMinutes) / 60;

  PatientAppt copyWith({int? day, int? minutes, ApptStatus? status, String? cancelReason, int? rating, String? ratingComment}) => PatientAppt(
        id: id,
        doctorId: doctorId,
        day: day ?? this.day,
        minutes: minutes ?? this.minutes,
        type: type,
        status: status ?? this.status,
        forWhom: forWhom,
        reason: reason,
        price: price,
        paid: paid,
        cancelReason: cancelReason ?? this.cancelReason,
        rating: rating ?? this.rating,
        ratingComment: ratingComment ?? this.ratingComment,
      );
}

/// يحدد هل الخانة محجوزة مسبقًا عند الطبيب (محاكاة ثابتة).
bool _takenByOthers(int doctorId, int day, int minutes) => (doctorId * 7 + day * 5 + minutes ~/ 30) % 3 == 0;

class PatientAppointmentsController extends ChangeNotifier {
  PatientAppointmentsController([List<PatientAppt>? initial])
      : _items = List.of(initial ??
            const [
              PatientAppt(id: 1, doctorId: 1, day: 2, minutes: 10 * 60 + 30, type: VisitType.clinic, reason: 'متابعة ضغط الدم', price: 300, paid: true),
              PatientAppt(id: 2, doctorId: 3, day: 9, minutes: 17 * 60, type: VisitType.video, forWhom: 'زياد (ابني)', reason: 'سعال مستمر', price: 150),
              PatientAppt(id: 3, doctorId: 1, day: -12, minutes: 11 * 60, type: VisitType.clinic, status: ApptStatus.done, reason: 'ارتفاع ضغط', price: 300, paid: true, rating: 5),
              PatientAppt(id: 4, doctorId: 4, day: -30, minutes: 10 * 60, type: VisitType.clinic, status: ApptStatus.done, reason: 'طفح جلدي', price: 280, paid: true),
              PatientAppt(id: 5, doctorId: 6, day: -5, minutes: 15 * 60, type: VisitType.clinic, status: ApptStatus.cancelled, reason: 'فحص نظر', price: 260, cancelReason: 'ظرف طارئ'),
            ]);

  List<PatientAppt> _items;
  int _next = 100;

  /// عند الاتصال بالخادم تُنفَّذ التعديلات عليه أيضًا (تحديث متفائل محليًا ثم مزامنة).
  PatientRemote? remote;

  /// الخانات المتاحة من الخادم: طبيب ← يوم (بعد اليوم) ← دقائق.
  final Map<int, Map<int, List<int>>> _avail = {};

  void setAvailability(int doctorId, Map<int, List<int>> days) {
    _avail[doctorId] = days;
    notifyListeners();
  }

  void replaceAll(List<PatientAppt> items) {
    _items = List.of(items);
    notifyListeners();
  }

  List<PatientAppt> get all => List.unmodifiable(_items);

  List<PatientAppt> byStatus(ApptStatus s) {
    final l = _items.where((a) => a.status == s).toList();
    l.sort((a, b) => s == ApptStatus.upcoming ? (a.day != b.day ? a.day.compareTo(b.day) : a.minutes.compareTo(b.minutes)) : (b.day != a.day ? b.day.compareTo(a.day) : b.minutes.compareTo(a.minutes)));
    return l;
  }

  List<PatientAppt> get upcoming => byStatus(ApptStatus.upcoming);
  PatientAppt? get next => upcoming.isEmpty ? null : upcoming.first;
  PatientAppt byId(int id) => _items.firstWhere((a) => a.id == id);

  /// أيام الحجز المتاحة لطبيب خلال 14 يومًا (حسب أيام دوامه).
  List<int> bookableDays(Doctor d) {
    if (remote != null) return (_avail[d.id] ?? const {}).entries.where((e) => e.value.isNotEmpty).map((e) => e.key).toList()..sort();
    final base = DateTime.now();
    const names = ['الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت', 'الأحد'];
    return [
      for (var i = 0; i < 14; i++)
        if (d.days.contains(names[base.add(Duration(days: i)).weekday - 1]) && slotsFor(d, i).isNotEmpty) i,
    ];
  }

  /// الخانات المتاحة: بعد الآن (اليوم)، غير محجوزة عند الطبيب، ولا تتعارض مع موعد للمريض نفسه.
  List<int> slotsFor(Doctor d, int day, {int? excludeApptId}) {
    if (remote != null) return List.of(_avail[d.id]?[day] ?? const []);
    final mine = _items.where((a) => a.status == ApptStatus.upcoming && a.day == day && a.id != excludeApptId).map((a) => a.minutes).toSet();
    return [
      for (var m = slotStart; m < slotEnd; m += slotStep)
        if ((day > 0 || m > nowMinutes) && !_takenByOthers(d.id, day, m) && !mine.contains(m)) m,
    ];
  }

  /// يسجل حجزًا جديدًا ويعيد رقمه.
  int book({required Doctor doctor, required int day, required int minutes, required VisitType type, required String forWhom, required String reason, required int price, required bool paid}) {
    final id = _next++;
    _items.add(PatientAppt(id: id, doctorId: doctor.id, day: day, minutes: minutes, type: type, forWhom: forWhom, reason: reason, price: price, paid: paid));
    notifyListeners();
    return id;
  }

  void _replace(int id, PatientAppt Function(PatientAppt) f) {
    final i = _items.indexWhere((a) => a.id == id);
    _items[i] = f(_items[i]);
    notifyListeners();
  }

  /// حجز على الخادم (ينتظر الرد ليعرف التسعير والدفع الفعليين). يرمي [ApiException] عند الرفض.
  Future<int> bookRemote({required Doctor doctor, required int day, required int minutes, required VisitType type, required String forWhom, required String reason, required String payment, required bool insured}) async {
    final appt = await remote!.book(doctor: doctor, day: day, minutes: minutes, type: type, forWhom: forWhom, reason: reason, payment: payment, insured: insured);
    _items.removeWhere((a) => a.id == appt.id);
    _items.add(appt);
    notifyListeners();
    return appt.id;
  }

  void restore(int id, PatientAppt previous) {
    _replace(id, (_) => previous);
    remote?.restoreAppointment(id, previous);
  }

  void cancel(int id, String reason) {
    _replace(id, (a) => a.copyWith(status: ApptStatus.cancelled, cancelReason: reason));
    remote?.cancelAppointment(id, reason);
  }

  void reschedule(int id, int day, int minutes) {
    _replace(id, (a) => a.copyWith(day: day, minutes: minutes));
    remote?.rescheduleAppointment(id, day, minutes);
  }

  void rate(int id, int stars, String comment) {
    _replace(id, (a) => a.copyWith(rating: stars, ratingComment: comment));
    remote?.rateAppointment(id, stars, comment);
  }
}

/// سعر الكشف حسب النوع.
int priceFor(Doctor d, VisitType t) => t == VisitType.video ? (d.videoPrice ?? d.price) : d.price;
