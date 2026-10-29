import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/foundation.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';
import '../widgets/medication_sheet.dart';
import 'appointments.dart';
import 'clock.dart';
import 'messages.dart';
import 'notifications.dart';
import 'patients.dart';
import 'profile.dart';

const _avatarDir = 'assets/images/';
const _patientAvatar = 'assets/images/avatars/patient-2.webp';

/// أسماء الفحوصات في شاشة الكشف ← مفاتيح الدليل في الخادم (تحاليل/أشعة).
const _labKeys = {'صورة دم كاملة': 'cbc', 'سكر تراكمي': 'hba1c', 'وظائف الكلى': 'kidney', 'وظائف الكبد': 'liver'};
const _imagingKeys = {'أشعة صدر': 'chest-xray', 'تخطيط قلب': 'ecg'};
const _timesPerDay = {'مرة يوميًا': 1, 'مرتين يوميًا': 2, '3 مرات يوميًا': 3, 'عند الحاجة': 0};
const _serverDays = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

/// ربط تطبيق الطبيب بالـ API (نمط [PatientRemote] نفسه: تحميل إلى الـ controllers ومزامنة متفائلة).
class DoctorRemote {
  DoctorRemote({required this.client}) : auth = AuthApi(client);

  final ApiClient client;
  final AuthApi auth;
  final lastError = ValueNotifier<String?>(null);
  final sessionExpired = ValueNotifier<int>(0);
  DoctorStore? _store;
  AuthUser? user;

  DoctorStore get store => _store!;

  void bind(DoctorStore s) {
    _store = s;
    useRealClock();
    client.onSessionExpired = () => sessionExpired.value++;
  }

  // ------------------------------------------------------------ تحميل

  Future<String?> loadAll() async {
    String? error;
    await Future.wait([
      for (final f in [loadProfile, loadHours, loadPatients, loadAppointments, loadNotifications, loadConversations, loadCatalog])
        f().catchError((Object e) {
          if (e is ApiException && e.isUnauthorized) throw e;
          error ??= e is ApiException ? e.message : 'تعذّر تحميل بعض البيانات';
        }),
    ]);
    return error;
  }

  void refresh() {
    for (final f in [loadAppointments, loadNotifications, loadConversations]) {
      _bg(f);
    }
  }

  Future<void> loadProfile() async {
    final j = await client.get('/api/me/profile') as Map<String, dynamic>;
    String? branch;
    try {
      final d = await client.get('/api/doctors/${j['id']}') as Map<String, dynamic>;
      branch = d['branchName'] as String?;
    } on ApiException catch (_) {}
    store.profile.load(
      name: j['fullName'] as String,
      specialty: j['specialty'] as String?,
      hospital: branch == null ? null : 'مستشفى ClaudCare – $branch',
      license: user?.employeeNo,
      phone: j['phone'] as String?,
      email: j['email'] as String?,
      bio: j['about'] as String?,
    );
    final st = await client.get('/api/me/settings') as Map<String, dynamic>;
    store.settings.load({for (final e in (st['values'] as Map).entries) e.key as String: e.value as bool});
  }

  List<({bool on, int from, int to})>? _serverHours;

  Future<void> loadHours() async {
    final l = await client.get('/api/doctors/me/working-hours') as List;
    final byDay = {for (final h in l) h['dayOfWeek'] as String: h as Map<String, dynamic>};
    _serverHours = [
      for (final d in _serverDays)
        (on: byDay[d]?['isActive'] == true, from: (byDay[d]?['startMinutes'] as int?) ?? 8 * 60, to: (byDay[d]?['endMinutes'] as int?) ?? 16 * 60),
    ];
    store.hours.load(_serverHours!);
  }

  /// دوام اليوم المنسوب إلى اليوم الحالي (الأحد = 0).
  WorkDay? workDayFor(int dayOffset) {
    final d = today.add(Duration(days: dayOffset));
    return store.hours.days[d.weekday % 7];
  }

  Future<void> loadCatalog() async {
    final l = (await client.get('/api/drugs', query: {'pageSize': 50}) as Map<String, dynamic>)['items'] as List;
    drugs = [for (final d in l) Drug(d['name'] as String, d['group'] as String, id: d['id'] as int)];
  }

  Future<void> loadPatients() async {
    final j = await client.get('/api/patients', query: {'pageSize': 50}) as Map<String, dynamic>;
    final old = {for (final p in store.patients.all) p.id: p};
    store.patients.replaceAll([
      for (final s in (j['items'] as List))
        old[s['id'] as int] ?? _patientStub(s as Map<String, dynamic>),
    ]);
  }

  Patient _patientStub(Map<String, dynamic> s) => Patient(
        id: s['id'] as int,
        name: s['fullName'] as String,
        age: (s['age'] as int?) ?? 0,
        gender: s['gender'] == 'Female' ? 'أنثى' : 'ذكر',
        avatar: _avatar(s['avatarKey'] as String?),
        blood: (s['bloodType'] as String?) ?? '—',
        phone: (s['phone'] as String?) ?? '',
        lastVisit: s['lastVisit'] == null ? '—' : fmtIsoShort(s['lastVisit'] as String),
        conditions: s['mainCondition'] == null ? const [] : [s['mainCondition'] as String],
      );

  static String _avatar(String? key) => key == null ? _patientAvatar : '$_avatarDir$key.webp';

  final _detailRequested = <int>{};

  /// تفاصيل ملف المريض (حساسية، أمراض، علامات، تحاليل، زيارات، وصفات) مرة لكل جلسة؛ `force` للتحديث بعد حفظ كشف.
  void ensurePatientDetail(int id, {bool force = false}) {
    if (force) _detailRequested.remove(id);
    if (_detailRequested.add(id)) _bg(() => loadPatientDetail(id));
  }

  Future<void> loadPatientDetail(int id) async {
    final rec = await client.get('/api/patients/$id/record') as Map<String, dynamic>;
    final visits = await client.get('/api/patients/$id/visits', query: {'pageSize': 20}) as Map<String, dynamic>;
    final rx = await client.get('/api/patients/$id/prescriptions', query: {'pageSize': 20}) as Map<String, dynamic>;
    final labs = await client.get('/api/patients/$id/lab-orders', query: {'pageSize': 20}) as Map<String, dynamic>;
    final s = rec['summary'] as Map<String, dynamic>;
    final base = _patientStub(s);
    final v = rec['latestVitals'] as Map<String, dynamic>?;
    final vitals = <String, String>{
      if (v?['systolicBp'] != null) 'الضغط': '${v!['systolicBp']}/${v['diastolicBp']}',
      if (v?['heartRate'] != null) 'النبض': '${v!['heartRate']}',
      if (v?['temperatureC'] != null) 'الحرارة': '${v!['temperatureC']}',
      if (v?['spo2'] != null) 'SpO₂': '${v!['spo2']}%',
      if (rec['weightKg'] != null) 'الوزن': '${(rec['weightKg'] as num).round()} كجم',
    };
    final labRows = <LabResult>[];
    for (final o in (labs['items'] as List)) {
      if (o['ready'] != true) continue;
      final date = fmtShortDate(DateTime.parse((o['resultAt'] ?? o['orderedAt']) as String).toLocal());
      for (final i in (o['items'] as List)) {
        final min = i['refMin'], max = i['refMax'];
        labRows.add(LabResult(i['name'] as String, _num(i['value']), i['unit'] as String, min != null && max != null ? '${_num(min)} – ${_num(max)}' : (max != null ? '< ${_num(max)}' : '—'),
            switch (i['flag'] as String) { 'High' => LabFlag.high, 'Low' => LabFlag.low, 'Critical' => LabFlag.critical, _ => LabFlag.normal }, date));
      }
    }
    final p = Patient(
      id: id,
      name: base.name,
      age: base.age,
      gender: base.gender,
      avatar: base.avatar,
      blood: base.blood,
      phone: base.phone,
      lastVisit: base.lastVisit,
      conditions: [for (final c in (rec['conditions'] as List)) c['name'] as String],
      allergies: [for (final a in (rec['allergies'] as List)) a['name'] as String],
      vitals: vitals,
      activeMeds: [for (final m in (rec['activeMedications'] as List)) m['name'] as String],
      labs: labRows,
      visits: [
        for (final x in (visits['items'] as List))
          Visit(date: fmtIsoShort(x['date'] as String), diagnosis: x['diagnosis'] as String, note: (x['notes'] as String?) ?? (x['complaint'] as String), doctor: x['doctorName'] as String),
      ],
      prescriptions: [
        for (final r in (rx['items'] as List))
          Rx(
            no: r['no'] as String,
            date: fmtShortDate(DateTime.parse(r['issuedAt'] as String).toLocal()),
            meds: [for (final i in (r['items'] as List)) '${i['drugName']} – ${i['frequency']}'],
            active: r['status'] != 'Rejected' && r['status'] != 'Dispensed',
          ),
      ],
    );
    store.patients.upsert(p);
  }

  static String _num(Object? v) {
    final d = (v as num).toDouble();
    return d == d.roundToDouble() ? d.toInt().toString() : d.toString();
  }

  // ------------------------------------------------------------ المواعيد

  Appointment _appt(Map<String, dynamic> a) {
    final pt = a['patient'] as Map<String, dynamic>;
    final pid = pt['id'] as int;
    if (!store.patients.all.any((p) => p.id == pid)) {
      store.patients.upsert(Patient(id: pid, name: pt['fullName'] as String, age: (pt['age'] as int?) ?? 0, gender: '—', avatar: _avatar(pt['avatarKey'] as String?), blood: '—', phone: '', lastVisit: '—'));
    }
    return Appointment(
      id: a['id'] as int,
      patientId: pid,
      patient: (a['familyMemberName'] as String?) ?? pt['fullName'] as String,
      age: (pt['age'] as int?) ?? 0,
      avatar: _avatar(pt['avatarKey'] as String?),
      day: dayOffsetOf(a['date'] as String),
      minutes: a['startMinutes'] as int,
      duration: a['durationMinutes'] as int,
      reason: a['reason'] as String,
      type: switch (a['type'] as String) { 'Video' => ApptType.video, 'FollowUp' => ApptType.followUp, _ => ApptType.clinic },
      status: switch (a['status'] as String) {
        'Pending' => ApptStatus.pending,
        'Arrived' => ApptStatus.arrived,
        'InProgress' => ApptStatus.inProgress,
        'Done' => ApptStatus.done,
        'Cancelled' || 'NoShow' => ApptStatus.cancelled,
        _ => ApptStatus.confirmed,
      },
      cancelReason: a['cancelReason'] as String?,
    );
  }

  Future<void> loadAppointments() async {
    final j = await client.get('/api/appointments', query: {'from': isoOfOffset(-1), 'to': isoOfOffset(30), 'pageSize': 50}) as Map<String, dynamic>;
    store.appointments.replaceAll([for (final a in (j['items'] as List)) _appt(a as Map<String, dynamic>)]);
  }

  void reloadAppointments() => _bg(loadAppointments);

  void act(int id, String action) => _write(() => client.post('/api/appointments/$id/$action'), loadAppointments);
  void cancelAppointment(int id, String reason) => _write(() => client.post('/api/appointments/$id/cancel', body: {'reason': reason}), loadAppointments);
  void rescheduleAppointment(int id, int day, int minutes) =>
      _write(() => client.post('/api/appointments/$id/reschedule', body: {'date': isoOfOffset(day), 'startMinutes': minutes}), loadAppointments);

  // ------------------------------------------------------------ الكشف

  /// يحفظ الكشف كاملًا ويعيد رقم الوصفة (أو null). يرمي [ApiException] (مثل allergy_conflict) ليعرضه المستدعي.
  Future<String?> saveVisit({
    required int patientId,
    int? appointmentId,
    required String complaint,
    int? sys, int? dia, int? hr, num? temp, int? spo2,
    required String diagnosis,
    String? notes,
    required String followUp,
    required Set<String> tests,
    required List<Med> meds,
    required Patient patient,
  }) async {
    final body = {
      'appointmentId': appointmentId,
      'patientId': appointmentId == null ? patientId : null,
      'complaint': complaint,
      'vitals': (sys ?? dia ?? hr ?? temp ?? spo2) == null ? null : {'systolicBp': sys, 'diastolicBp': dia, 'heartRate': hr, 'temperatureC': temp, 'spo2': spo2},
      'diagnosis': diagnosis,
      'notes': notes,
      'followUp': followUp,
      'labTests': [for (final t in tests) if (_labKeys.containsKey(t)) _labKeys[t]],
      'imagingStudies': [for (final t in tests) if (_imagingKeys.containsKey(t)) _imagingKeys[t]],
      'prescription': [
        for (final m in meds)
          {
            'drugId': m.drug.id == 0 ? null : m.drug.id,
            'drugName': m.drug.id == 0 ? m.drug.name : null,
            'frequency': m.frequency,
            'timesPerDay': _timesPerDay[m.frequency] ?? 1,
            'durationDays': m.days,
            'instructions': m.note.isEmpty ? null : m.note,
            // الورقة تفرض إقرار الطبيب قبل إضافة دواء يتعارض مع حساسية معروفة
            'allergyOverride': checkMed(patient, m.drug).allergy != null,
          },
      ],
    };
    final j = await client.post('/api/visits', body: body) as Map<String, dynamic>;
    ensurePatientDetail(patientId, force: true);
    _bg(loadAppointments);
    return j['prescriptionNo'] as String?;
  }

  // ------------------------------------------------------------ بقية الأقسام

  Future<void> loadNotifications() async {
    final j = await client.get('/api/notifications', query: {'pageSize': 50}) as Map<String, dynamic>;
    final now = DateTime.now();
    store.notifications.replaceAll([
      for (final n in ((j['page'] as Map)['items'] as List))
        () {
          final at = DateTime.parse(n['createdAt'] as String).toLocal();
          final days = DateTime(now.year, now.month, now.day).difference(DateTime(at.year, at.month, at.day)).inDays;
          return NotifItem(
            id: n['id'] as int,
            type: switch (n['type'] as String) { 'Appointment' => NotifType.appointment, 'Result' || 'Alert' => NotifType.result, 'Message' => NotifType.message, _ => NotifType.system },
            title: n['title'] as String,
            text: n['text'] as String,
            time: relativeTime(at, now),
            group: days <= 0 ? 'اليوم' : (days == 1 ? 'أمس' : 'سابقًا'),
            unread: !(n['read'] as bool),
            conversationId: n['refType'] == 'Conversation' ? n['refId'] as int? : null,
          );
        }(),
    ]);
  }

  void markNotificationRead(int id) => _write(() => client.post('/api/notifications/$id/read'), loadNotifications);
  void markAllNotificationsRead() => _write(() => client.post('/api/notifications/read-all'), loadNotifications);
  void deleteNotification(int id) => _write(() => client.delete('/api/notifications/$id'), loadNotifications);
  void restoreNotification(int id) => _write(() => client.post('/api/notifications/$id/restore'), loadNotifications);

  Future<void> loadConversations() async {
    final l = await client.get('/api/conversations') as List;
    store.conversations.replaceAll([
      for (final c in l)
        Conversation(
          id: c['id'] as int,
          name: c['title'] as String,
          role: _roleLabel(c['kind'] as String, c['role'] as String?),
          avatar: c['kind'] == 'Support' ? 'assets/images/avatars/reception-f1.webp' : _avatar(c['avatarKey'] as String?),
          patientId: c['role'] == 'Patient' ? c['otherUserId'] as int? : null,
          unread: c['unread'] as int,
          messages: const [],
        )
          ..lastText = c['lastMessage'] as String?
          ..lastTime = relativeTime(DateTime.parse(c['lastMessageAt'] as String).toLocal(), DateTime.now()),
    ]);
  }

  static String _roleLabel(String kind, String? r) => kind == 'Support'
      ? 'الدعم'
      : switch (r) { 'Patient' => 'مريض', 'Doctor' => 'طبيب', 'Nurse' => 'تمريض', 'Pharmacy' => 'الصيدلية', 'Lab' => 'المختبر', 'Reception' => 'الاستقبال', _ => r ?? '' };

  Future<void> loadMessages(int id) async {
    final j = await client.get('/api/conversations/$id/messages', query: {'take': 100}) as Map<String, dynamic>;
    store.conversations.setMessages(id, [
      for (final m in (j['messages'] as List)) Message(fromMe: m['mine'] as bool, text: m['text'] as String, time: relativeTime(DateTime.parse(m['sentAt'] as String).toLocal(), DateTime.now())),
    ]);
  }

  void markConversationRead(int id) => _write(() => client.post('/api/conversations/$id/read'), loadConversations);
  void sendMessage(int id, String text) => _write(() => client.post('/api/conversations/$id/messages', body: {'text': text}), () => loadMessages(id));

  void updateProfile() {
    final p = store.profile;
    _write(() => client.put('/api/me/profile', body: {'fullName': p.name, 'email': p.email.isEmpty ? null : p.email, 'about': p.bio}), loadProfile);
  }

  static const _settingKeys = {'appointments': 'newAppointments', 'labs': 'labResults'};
  void setSetting(String key, bool v) => _write(() => client.put('/api/me/settings', body: {'values': {_settingKeys[key] ?? key: v}}), loadProfile);

  /// يرسل جدول الدوام كاملًا؛ يحذّر إن وقعت مواعيد قادمة خارجه.
  void saveHours() {
    final days = store.hours.days;
    _write(() async {
      final r = await client.put('/api/doctors/me/working-hours', body: {
        'days': [
          for (var i = 0; i < 7; i++) {'dayOfWeek': _serverDays[i], 'isActive': days[i].on, 'startMinutes': days[i].from, 'endMinutes': days[i].to},
        ],
      }) as Map<String, dynamic>;
      final outside = r['futureAppointmentsOutsideHours'] as int;
      if (outside > 0) lastError.value = 'لديك $outside مواعيد قادمة خارج الدوام الجديد؛ راجعها.';
    }, loadHours);
  }

  Future<void> logout() async {
    await auth.logout();
    user = null;
    _detailRequested.clear();
    store.reset();
  }

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
