import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/foundation.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';
import 'appointments.dart';
import 'clock.dart';
import 'doctors.dart';
import 'invoices.dart';
import 'medications.dart';
import 'messages.dart';
import 'notifications.dart';
import 'profile.dart';
import 'records.dart';

const _avatarDir = 'assets/images/';
const _fallbackAvatar = 'assets/images/avatars/doc-m1.webp';

/// ربط تطبيق المريض بالـ API: يحمّل البيانات إلى الـ controllers الحالية ويزامن تعديلاتها.
/// التعديلات الصغيرة متفائلة (تظهر فورًا ثم تُرسل)، وعند فشل الإرسال يُعاد تحميل القسم وتظهر رسالة في [lastError].
class PatientRemote {
  PatientRemote({required this.client}) : auth = AuthApi(client);

  final ApiClient client;
  final AuthApi auth;
  final lastError = ValueNotifier<String?>(null);
  final sessionExpired = ValueNotifier<int>(0);
  PatientStore? _store;
  AuthUser? user;

  PatientStore get store => _store!;

  void bind(PatientStore s) {
    _store = s;
    useRealClock();
    client.onSessionExpired = () => sessionExpired.value++;
  }

  // ------------------------------------------------------------ تحميل

  /// يحمّل كل الأقسام بالتوازي. يعيد رسالة أول خطأ (أو null)؛ فشل قسم لا يمنع بقية الأقسام.
  Future<String?> loadAll() async {
    String? error;
    await Future.wait([
      for (final f in [loadProfile, loadDoctors, loadAppointments, loadInvoices, loadNotifications, loadMedications, loadRecords, loadConversations, loadInsurance, loadFavorites])
        f().catchError((Object e) {
          if (e is ApiException && e.isUnauthorized) throw e;
          error ??= e is ApiException ? e.message : 'تعذّر تحميل بعض البيانات';
        }),
    ]);
    return error;
  }

  Future<void> loadProfile() async {
    final j = await client.get('/api/me/profile') as Map<String, dynamic>;
    store.profile.load(name: j['fullName'] as String, phone: j['phone'] as String?, email: j['email'] as String?, birthYear: j['birthYear'] as int?);
    final fam = await client.get('/api/me/family') as List;
    store.family.replaceAll([for (final f in fam) FamilyMember(f['name'] as String, f['relation'] as String, f['birthYear'] as int, id: f['id'] as int)]);
    final st = await client.get('/api/me/settings') as Map<String, dynamic>;
    store.settings.load({for (final e in (st['values'] as Map).entries) e.key as String: e.value as bool});
  }

  Future<void> loadDoctors() async {
    final sp = await client.get('/api/specialties') as List;
    for (final s in sp) {
      final key = s['key'] as String;
      if (!specialties.any((x) => x.key == key)) specialties.add(Specialty(key, s['nameAr'] as String, 'assets/images/specialties/cardiology.webp'));
    }
    final j = await client.get('/api/doctors', query: {'pageSize': 50}) as Map<String, dynamic>;
    doctors = [for (final d in (j['items'] as List)) _doctor(d as Map<String, dynamic>)];
    store.doctorsChanged();
  }

  Doctor _doctor(Map<String, dynamic> d) {
    final next = d['nextSlot'] as Map<String, dynamic>?;
    final key = d['specialtyKey'] as String;
    if (!specialties.any((x) => x.key == key)) specialties.add(Specialty(key, d['specialtyName'] as String, 'assets/images/specialties/cardiology.webp'));
    return Doctor(
      id: d['id'] as int,
      name: d['fullName'] as String,
      specialty: key,
      avatar: avatarAsset(d['avatarKey'] as String?),
      rating: (d['rating'] as num).toDouble(),
      reviewsCount: d['reviewsCount'] as int,
      years: d['yearsExperience'] as int,
      patients: 0,
      price: (d['consultationPrice'] as num).round(),
      videoPrice: (d['videoPrice'] as num?)?.round(),
      nextSlot: next == null ? 'لا مواعيد قريبة' : '${dayLabel(DateTime.parse(next['date'] as String), today)} ${fmtTime(next['startMinutes'] as int)}',
      about: '',
    );
  }

  static String avatarAsset(String? key) => key == null ? _fallbackAvatar : '$_avatarDir$key.webp';

  static const _dayNames = {'Sunday': 'الأحد', 'Monday': 'الاثنين', 'Tuesday': 'الثلاثاء', 'Wednesday': 'الأربعاء', 'Thursday': 'الخميس', 'Friday': 'الجمعة', 'Saturday': 'السبت'};

  /// تفاصيل الطبيب (النبذة، الدوام، التقييمات) عند فتح ملفه.
  Future<void> loadDoctorDetail(int id) async {
    final j = await client.get('/api/doctors/$id') as Map<String, dynamic>;
    final hours = (j['workingHours'] as List).where((h) => h['isActive'] == true).toList();
    final days = [for (final h in hours) _dayNames[h['dayOfWeek']] ?? h['dayOfWeek'] as String];
    final first = hours.isEmpty ? null : hours.first;
    final base = _doctor(j['summary'] as Map<String, dynamic>);
    final reviews = [
      for (final r in (j['latestReviews'] as List))
        Review(r['patientName'] as String, r['rating'] as int, (r['comment'] as String?) ?? '', relativeTime(DateTime.parse(r['createdAt'] as String).toLocal(), DateTime.now())),
    ];
    final detailed = base.withDetail(
      about: j['about'] as String,
      branch: j['branchName'] as String,
      days: days,
      hours: first == null ? '' : '${fmtTime(first['startMinutes'] as int)} – ${fmtTime(first['endMinutes'] as int)}',
      languages: [for (final l in (j['languages'] as List)) l as String],
      patients: j['patientsCount'] as int,
      reviews: reviews,
    );
    doctors = [for (final d in doctors) d.id == id ? detailed : d];
    if (!doctors.any((d) => d.id == id)) doctors = [...doctors, detailed];
    store.doctorsChanged();
  }

  /// الخانات المتاحة خلال 14 يومًا للنوع المطلوب.
  Future<void> loadAvailability(int doctorId, VisitType type) async {
    final j = await client.get('/api/doctors/$doctorId/availability', query: {'from': isoOfOffset(0), 'days': 14, 'type': type.isVideo ? 'Video' : 'Clinic'}) as Map<String, dynamic>;
    final byDay = <int, List<int>>{};
    for (final d in (j['days'] as List)) {
      byDay[dayOffsetOf(d['date'] as String)] = [for (final m in (d['slots'] as List)) m as int];
    }
    store.appointments.setAvailability(doctorId, byDay);
  }

  Future<void> loadFavorites() async {
    final l = await client.get('/api/me/favorites/doctors') as List;
    store.favorites.replaceAll([for (final d in l) d['id'] as int]);
  }

  PatientAppt _appt(Map<String, dynamic> a) {
    final doc = a['doctor'] as Map<String, dynamic>;
    final id = doc['id'] as int;
    if (!doctors.any((d) => d.id == id)) {
      final key = a['specialtyName'] as String;
      doctors = [
        ...doctors,
        Doctor(id: id, name: doc['fullName'] as String, specialty: specialties.firstWhere((s) => s.label == key, orElse: () => specialties.first).key, avatar: avatarAsset(doc['avatarKey'] as String?),
            rating: 0, reviewsCount: 0, years: 0, patients: 0, price: (a['price'] as num).round(), nextSlot: '', about: ''),
      ];
    }
    final status = a['status'] as String;
    return PatientAppt(
      id: a['id'] as int,
      doctorId: id,
      day: dayOffsetOf(a['date'] as String),
      minutes: a['startMinutes'] as int,
      type: a['type'] == 'Video' ? VisitType.video : VisitType.clinic,
      status: switch (status) { 'Done' => ApptStatus.done, 'Cancelled' || 'NoShow' => ApptStatus.cancelled, _ => ApptStatus.upcoming },
      forWhom: (a['familyMemberName'] as String?) ?? 'أنا',
      reason: a['reason'] as String,
      price: (a['due'] as num).round(),
      paid: a['paid'] as bool,
      cancelReason: a['cancelReason'] as String?,
      rating: (a['reviewed'] as bool) ? (_ratings[a['id']] ?? 5) : null,
    );
  }

  final _ratings = <int, int>{};

  Future<void> loadAppointments() async {
    final j = await client.get('/api/appointments', query: {'pageSize': 50}) as Map<String, dynamic>;
    store.appointments.replaceAll([for (final a in (j['items'] as List)) _appt(a as Map<String, dynamic>)]);
    store.doctorsChanged();
  }

  Future<void> loadInvoices() async {
    final j = await client.get('/api/invoices', query: {'pageSize': 50}) as Map<String, dynamic>;
    final items = <Invoice>[];
    for (final i in ((j['page'] as Map)['items'] as List)) {
      final status = i['status'] as String;
      if (status == 'Cancelled') continue;
      final lines = [for (final x in (i['items'] as List)) InvItem(x['description'] as String, (x['total'] as num).round())];
      final total = lines.fold<int>(0, (s, x) => s + x.amount);
      final remaining = (i['remaining'] as num).round();
      final payments = (i['payments'] as List).where((p) => (p['amount'] as num) > 0).toList();
      items.add(Invoice(
        id: i['id'] as int,
        no: i['no'] as String,
        title: i['title'] as String,
        date: fmtIsoDate(i['issueDate'] as String),
        items: lines,
        // التغطية الظاهرة تجعل «المستحق» = المتبقي الفعلي (يشمل الخصم والدفع الجزئي)
        covered: status == 'Paid' ? (i['insuranceCovered'] as num).round() : total - remaining,
        paidWith: status == 'Paid' ? _methodLabel(payments.isEmpty ? null : payments.last['method'] as String) : null,
      ));
    }
    store.invoices.replaceAll(items);
  }

  static String _methodLabel(String? m) => switch (m) { 'ApplePay' => 'Apple Pay', 'Cash' => 'نقدًا', 'BankTransfer' => 'تحويل بنكي', _ => 'بطاقة (مدى/فيزا)' };

  Future<void> loadNotifications() async {
    final j = await client.get('/api/notifications', query: {'pageSize': 50}) as Map<String, dynamic>;
    final now = DateTime.now();
    final items = <NotifItem>[];
    for (final n in ((j['page'] as Map)['items'] as List)) {
      final at = DateTime.parse(n['createdAt'] as String).toLocal();
      final days = DateTime(now.year, now.month, now.day).difference(DateTime(at.year, at.month, at.day)).inDays;
      items.add(NotifItem(
        id: n['id'] as int,
        type: switch (n['type'] as String) {
          'Appointment' => NotifType.appointment,
          'Result' => NotifType.result,
          'Medication' => NotifType.medication,
          'Invoice' || 'Payment' => NotifType.invoice,
          'Message' => NotifType.message,
          _ => NotifType.system,
        },
        title: n['title'] as String,
        text: n['text'] as String,
        time: relativeTime(at, now),
        group: days <= 0 ? 'اليوم' : (days == 1 ? 'أمس' : 'سابقًا'),
        unread: !(n['read'] as bool),
      ));
    }
    store.notifications.replaceAll(items);
  }

  Future<void> loadMedications() async {
    final l = await client.get('/api/me/medications') as List;
    final today = await client.get('/api/me/medications/today') as Map<String, dynamic>;
    final taken = <String>{
      for (final d in (today['doses'] as List))
        if (d['state'] == 'Taken') '${d['medicationId']}-${d['slotIndex']}',
    };
    store.medications.replaceAll([
      for (final m in l)
        Medication(
          id: m['id'] as int,
          name: m['name'] as String,
          dose: (m['dose'] as String?) ?? '',
          times: [for (final t in (m['times'] as List)) t as int],
          daysLeft: m['daysLeft'] as int,
          doctor: m['doctorName'] as String,
          instructions: (m['instructions'] as String?) ?? '',
          active: m['active'] as bool,
          reminder: m['reminderEnabled'] as bool,
          refill: m['refillStatus'] == 'Requested' ? RefillState.requested : RefillState.none,
        ),
    ], taken);
  }

  Future<void> loadRecords() async {
    final hs = await client.get('/api/me/health-summary') as Map<String, dynamic>;
    healthSummary = (
      blood: (hs['bloodType'] as String?) ?? '—',
      allergies: [for (final a in (hs['allergies'] as List)) a['name'] as String].ifEmpty(['لا يوجد']),
      chronic: [for (final c in (hs['conditions'] as List)) c['name'] as String],
      height: hs['heightCm'] == null ? '—' : '${hs['heightCm']} سم',
      weight: hs['weightKg'] == null ? '—' : '${(hs['weightKg'] as num).round()} كجم',
    );
    final labs = await client.get('/api/me/lab-results', query: {'pageSize': 50}) as Map<String, dynamic>;
    labReports = [
      for (final l in (labs['items'] as List))
        LabReport(
          id: l['id'] as int,
          name: l['testName'] as String,
          date: fmtDate(DateTime.parse((l['resultAt'] ?? l['orderedAt']) as String).toLocal()),
          doctor: (l['doctorName'] as String?) ?? '—',
          ready: l['ready'] as bool,
          items: [
            for (final i in (l['items'] as List))
              LabItem(i['name'] as String, (i['value'] as num).toDouble(), i['unit'] as String, (i['refMin'] as num?)?.toDouble() ?? 0, (i['refMax'] as num?)?.toDouble() ?? ((i['value'] as num).toDouble() * 2 + 1)),
          ],
        ),
    ];
    final img = await client.get('/api/me/imaging', query: {'pageSize': 50}) as Map<String, dynamic>;
    imagingStudies = [
      for (final o in (img['items'] as List))
        if (o['report'] != null)
          ImagingStudy(
            id: o['id'] as int,
            name: o['studyName'] as String,
            date: fmtDate(DateTime.parse((o['report']['reportedAt'] ?? o['orderedAt']) as String).toLocal()),
            image: (o['modality'] as String) == 'MRI' ? 'assets/images/medical/mri-brain.webp' : 'assets/images/medical/xray-chest.webp',
            findings: o['report']['findings'] as String,
            impression: o['report']['impression'] as String,
            radiologist: 'قسم الأشعة',
          ),
    ];
    final v = await client.get('/api/me/visits', query: {'pageSize': 50}) as Map<String, dynamic>;
    visitRecords = [
      for (final x in (v['items'] as List))
        VisitRecord(
          date: fmtIsoDate(x['date'] as String),
          doctor: x['doctorName'] as String,
          specialty: (x['specialty'] as String?) ?? '',
          diagnosis: x['diagnosis'] as String,
          note: (x['notes'] as String?) ?? (x['followUp'] as String?) ?? '',
          meds: [for (final m in (x['medications'] as List)) m as String],
        ),
    ];
    store.recordsChanged();
  }

  Future<void> loadInsurance() async {
    try {
      final j = await client.get('/api/me/insurance') as Map<String, dynamic>;
      insurance = (
        insurer: j['insurer'] as String,
        tier: j['tier'] as String,
        policy: j['policyNo'] as String,
        expiry: fmtIsoDate(j['expiresAt'] as String),
        copay: '${((j['copayRate'] as num) * 100).round()}%',
        limit: (j['annualLimit'] as num).round(),
        used: (j['used'] as num).round(),
      );
    } on ApiException catch (e) {
      if (!e.isNotFound) rethrow;
      insurance = (insurer: 'لا توجد وثيقة تأمين', tier: '—', policy: '—', expiry: '—', copay: '—', limit: 1, used: 0);
    }
    store.recordsChanged();
  }

  Future<void> loadConversations() async {
    final l = await client.get('/api/conversations') as List;
    store.conversations.replaceAll([
      for (final c in l)
        Conversation(
          id: c['id'] as int,
          name: c['title'] as String,
          role: c['kind'] == 'Support' ? 'ClaudCare' : _roleLabel(c['role'] as String?),
          avatar: c['kind'] == 'Support' ? 'assets/images/avatars/reception-f1.webp' : avatarAsset(c['avatarKey'] as String?),
          doctorId: c['otherUserId'] as int?,
          unread: c['unread'] as int,
          messages: const [],
        )
          ..lastText = c['lastMessage'] as String?
          ..lastTime = relativeTime(DateTime.parse(c['lastMessageAt'] as String).toLocal(), DateTime.now()),
    ]);
  }

  static String _roleLabel(String? r) => switch (r) { 'Doctor' => 'الطبيب', 'Pharmacy' => 'الصيدلية', 'Lab' => 'المختبر', _ => r ?? '' };

  Future<void> loadMessages(int conversationId) async {
    final j = await client.get('/api/conversations/$conversationId/messages', query: {'take': 100}) as Map<String, dynamic>;
    store.conversations.setMessages(conversationId, [
      for (final m in (j['messages'] as List))
        Message(fromMe: m['mine'] as bool, text: m['text'] as String, time: relativeTime(DateTime.parse(m['sentAt'] as String).toLocal(), DateTime.now())),
    ]);
  }

  /// تحديث خفيف للأقسام المتغيرة (عند تبديل التبويب).
  void refresh() {
    for (final f in [loadAppointments, loadInvoices, loadNotifications, loadConversations, loadMedications]) {
      _bg(f);
    }
  }

  final _detailRequested = <int>{};

  /// يطلب تفاصيل الطبيب مرة واحدة لكل جلسة.
  void ensureDoctorDetail(int id) {
    if (_detailRequested.add(id)) _bg(() => loadDoctorDetail(id));
  }

  // ------------------------------------------------------------ حجز ومواعيد

  Future<PatientAppt> book({required Doctor doctor, required int day, required int minutes, required VisitType type, required String forWhom, required String reason, required String payment, required bool insured}) async {
    final famIndex = store.family.bookingNames.indexOf(forWhom) - 1;
    final fam = famIndex >= 0 && famIndex < store.family.all.length ? store.family.all[famIndex] : null;
    final method = payment.contains('Apple') ? 'ApplePay' : (payment.contains('العيادة') ? 'PayAtClinic' : 'Card');
    final j = await client.post('/api/appointments', body: {
      'doctorId': doctor.id,
      'date': isoOfOffset(day),
      'startMinutes': minutes,
      'type': type.isVideo ? 'Video' : 'Clinic',
      'reason': reason,
      'familyMemberId': fam == null || fam.id == 0 ? null : fam.id,
      'useInsurance': insured,
      'paymentMethod': method,
    }) as Map<String, dynamic>;
    // تتغير الفواتير والإشعارات بالحجز
    _bg(loadInvoices);
    _bg(loadNotifications);
    _bg(loadInsurance);
    return _appt(j);
  }

  void cancelAppointment(int id, String reason) => _write(() async {
        await client.post('/api/appointments/$id/cancel', body: {'reason': reason});
        _bg(loadInvoices);
      }, loadAppointments);

  void rescheduleAppointment(int id, int day, int minutes) =>
      _write(() => client.post('/api/appointments/$id/reschedule', body: {'date': isoOfOffset(day), 'startMinutes': minutes}), loadAppointments);

  void rateAppointment(int id, int stars, String comment) {
    _ratings[id] = stars;
    _write(() => client.post('/api/appointments/$id/review', body: {'rating': stars, 'comment': comment.isEmpty ? null : comment}), loadAppointments);
  }

  /// «تراجع» عن إلغاء: لا يوجد عكس في الخادم، فنعيد التحميل لنعرض الحقيقة.
  void restoreAppointment(int id, PatientAppt previous) => _bg(loadAppointments);

  // ------------------------------------------------------------ بقية الكتابات

  void payInvoice(int id, String methodLabel) => _write(() async {
        await client.post('/api/invoices/$id/pay', body: {'method': methodLabel.contains('Apple') ? 'ApplePay' : 'Card'});
        _bg(loadNotifications);
        _bg(loadAppointments);
      }, loadInvoices);

  void logDose(int medId, int slot, bool taken) => _write(() => client.post('/api/me/medications/$medId/doses', body: {'slotIndex': slot, 'taken': taken}), loadMedications);
  void setReminder(int id, bool v) => _write(() => client.patch('/api/me/medications/$id', body: {'reminderEnabled': v}), loadMedications);
  void requestRefill(int id) => _write(() => client.post('/api/me/medications/$id/refill'), loadMedications);

  void markNotificationRead(int id) => _write(() => client.post('/api/notifications/$id/read'), loadNotifications);
  void markAllNotificationsRead() => _write(() => client.post('/api/notifications/read-all'), loadNotifications);
  void deleteNotification(int id) => _write(() => client.delete('/api/notifications/$id'), loadNotifications);
  void restoreNotification(int id) => _write(() => client.post('/api/notifications/$id/restore'), loadNotifications);

  void markConversationRead(int id) => _write(() => client.post('/api/conversations/$id/read'), loadConversations);
  void sendMessage(int id, String text) => _write(() => client.post('/api/conversations/$id/messages', body: {'text': text}), () => loadMessages(id));

  void updateProfile() {
    final p = store.profile;
    _write(() => client.put('/api/me/profile', body: {'fullName': p.name, 'email': p.email.isEmpty ? null : p.email, 'birthYear': p.birthYear}), loadProfile);
  }

  void addFamily(FamilyMember m) => _write(() => client.post('/api/me/family', body: {'name': m.name, 'relation': m.relation, 'birthYear': m.birthYear}), loadProfile);
  void removeFamily(FamilyMember m) => _write(() => m.id == 0 ? Future.value() : client.delete('/api/me/family/${m.id}'), loadProfile);
  void setSetting(String key, bool v) => _write(() => client.put('/api/me/settings', body: {'values': {key: v}}), loadProfile);
  void setFavorite(int id, bool on) => _write(() => on ? client.put('/api/me/favorites/doctors/$id') : client.delete('/api/me/favorites/doctors/$id'), loadFavorites);

  // ------------------------------------------------------------ الجلسة

  Future<void> logout() async {
    await auth.logout();
    user = null;
    _detailRequested.clear();
    store.reset();
  }

  /// يشغّل الكتابة؛ عند الفشل يعرض الرسالة ويعيد تحميل القسم ليعود للحقيقة.
  void _write(Future<dynamic> Function() action, Future<void> Function() reload) {
    () async {
      try {
        await action();
      } on ApiException catch (e) {
        lastError.value = e.userMessage;
        _bg(reload);
      }
    }();
  }

  void _bg(Future<void> Function() f) {
    f().catchError((Object _) {});
  }
}

extension on List<String> {
  List<String> ifEmpty(List<String> other) => isEmpty ? other : this;
}
