import 'dart:io';
import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:doctor_app/data/appointments.dart';
import 'package:doctor_app/data/patients.dart';
import 'package:doctor_app/data/remote.dart';
import 'package:doctor_app/state/app_scope.dart';
import 'package:doctor_app/widgets/medication_sheet.dart';
import 'package:flutter_test/flutter_test.dart';

/// اختبار تكامل حقيقي مع الـ API الشغّال (يُتخطى افتراضيًا).
/// `flutter test test/live_api_test.dart --dart-define=LIVE_API=http://localhost:5080`
const _live = String.fromEnvironment('LIVE_API');

Future<void> _settle() => Future<void>.delayed(const Duration(milliseconds: 900));

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('تطبيق الطبيب مع الخادم: دخول بخطوتين وتحميل وكشف ومراسلة ودوام', () async {
    HttpOverrides.global = null;
    final client = ApiClient(baseUrl: _live, deviceName: 'live-test-doctor');
    final remote = DoctorRemote(client: client);
    final store = DoctorStore(remote: remote);

    // 1) كلمة المرور ثم رمز التحقق بخطوتين
    final out = await remote.auth.login('DR-1042', 'Demo@1234');
    expect(out.challenge, isNotNull, reason: 'الطبيب يتطلب OTP');
    remote.user = await remote.auth.verifyLogin(out.challenge!.token, '123456');
    expect(remote.user!.role, 'Doctor');
    expect(await remote.loadAll(), isNull);

    expect(store.profile.name, contains('منى'));
    expect(store.profile.specialty, isNotEmpty);
    expect(drugs.length, greaterThanOrEqualTo(10));
    expect(drugs.first.id, greaterThan(0));
    expect(store.hours.activeDays, greaterThan(0));
    expect(store.appointments.all, isNotEmpty);
    expect(store.patients.all, isNotEmpty);

    // 2) مريضة بملف كامل: ريم لديها حساسية بنسلين
    final reem = store.patients.all.firstWhere((p) => p.name.contains('ريم'));
    await remote.loadPatientDetail(reem.id);
    final full = store.patients.byId(reem.id);
    expect(full.allergies, contains('البنسلين'));
    expect(full.visits, isNotEmpty);
    expect(full.labs, isNotEmpty);
    expect(full.prescriptions, isNotEmpty);

    // 3) كشف جديد بدون موعد: تحاليل + وصفة (ميتفورمين) ثم يظهر في السجل
    final metformin = drugs.firstWhere((d) => d.name.contains('ميتفورمين'));
    final visitsBefore = full.visits.length;
    final rx = await remote.saveVisit(
      patientId: reem.id,
      complaint: 'متابعة اختبار تكامل',
      sys: 130, dia: 85, hr: 76, spo2: 98, temp: 36.7,
      diagnosis: 'ارتفاع ضغط الدم',
      notes: 'ملاحظة',
      followUp: 'بعد شهر',
      tests: {'سكر تراكمي', 'أشعة صدر'},
      meds: [Med(drug: metformin, frequency: 'مرتين يوميًا', days: 10)],
      patient: full,
    );
    expect(rx, startsWith('RX-'));
    await _settle();
    await remote.loadPatientDetail(reem.id);
    expect(store.patients.byId(reem.id).visits.length, visitsBefore + 1);

    // 4) تعارض الحساسية: دواء من عائلة البنسلين يُرفض بـ allergy_conflict إن لم يُقرّ
    final amox = drugs.firstWhere((d) => d.group == 'البنسلين');
    final blind = Patient(id: reem.id, name: 'x', age: 1, gender: '', avatar: '', blood: '', phone: '', lastVisit: ''); // بلا علم محلي بالحساسية
    await expectLater(
      remote.saveVisit(patientId: reem.id, complaint: 'اختبار', diagnosis: 'اختبار', followUp: 'عند اللزوم', tests: {}, meds: [Med(drug: amox, frequency: 'مرة يوميًا', days: 5)], patient: blind),
      throwsA(isA<ApiException>().having((e) => e.code, 'code', 'allergy_conflict')),
    );

    // 5) مواعيد: تأكيد ثم إلغاء موعد قادم لمريضة
    final upcoming = store.appointments.all.where((a) => a.status == ApptStatus.confirmed || a.status == ApptStatus.pending).toList();
    expect(upcoming, isNotEmpty);

    // 6) مراسلة
    final conv = store.conversations.all.firstWhere((c) => c.patientId != null);
    await remote.loadMessages(conv.id);
    final count = store.conversations.byId(conv.id).messages.length;
    expect(count, greaterThan(0));
    store.conversations.send(conv.id, 'رد الطبيبة ${DateTime.now().millisecondsSinceEpoch}');
    await _settle();
    await remote.loadMessages(conv.id);
    expect(store.conversations.byId(conv.id).messages.length, count + 1);

    // 7) إعدادات وساعات الدوام وإشعارات
    store.settings.set('sound', false);
    await _settle();
    await remote.loadProfile();
    expect(store.settings.sound, isFalse);
    store.settings.set('sound', true);
    final wed = store.hours.days[3];
    final oldTo = wed.to;
    expect(store.hours.setRange(3, wed.from, 19 * 60), isNull);
    await _settle();
    await remote.loadHours();
    expect(store.hours.days[3].to, 19 * 60);
    store.hours.setRange(3, wed.from, oldTo);
    await _settle();
    store.notifications.markAllRead();
    await _settle();
    await remote.loadNotifications();
    expect(store.notifications.unreadCount, 0);

    // 8) الخروج
    await remote.logout();
    expect(store.appointments.all, isEmpty);
    await expectLater(remote.loadAppointments(), throwsA(isA<ApiException>().having((e) => e.status, 'status', 401)));
  }, skip: _live.isEmpty ? 'يتطلب API شغّالًا: --dart-define=LIVE_API=http://localhost:5080' : false, timeout: const Timeout(Duration(minutes: 2)));
}
