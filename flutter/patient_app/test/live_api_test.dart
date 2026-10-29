import 'dart:io';
import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:patient_app/data/appointments.dart';
import 'package:patient_app/data/doctors.dart';
import 'package:patient_app/data/records.dart';
import 'package:patient_app/data/remote.dart';
import 'package:patient_app/data/profile.dart' as prof;
import 'package:patient_app/state/app_scope.dart';

/// اختبار تكامل حقيقي مع الـ API الشغّال (يُتخطى افتراضيًا).
/// التشغيل: شغّل الـ API ثم
/// `flutter test test/live_api_test.dart --dart-define=LIVE_API=http://localhost:5080`
const _live = String.fromEnvironment('LIVE_API');

Future<void> _settle() => Future<void>.delayed(const Duration(milliseconds: 900));

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('تطبيق المريض مع الخادم: دخول وتحميل وحجز وإلغاء وسداد ومراسلة', () async {
    HttpOverrides.global = null; // السماح بالشبكة الحقيقية داخل flutter test
    final client = ApiClient(baseUrl: _live, deviceName: 'live-test');
    final remote = PatientRemote(client: client);
    final store = PatientStore(remote: remote);

    // 1) الدخول بحساب ريم ثم تحميل كل الأقسام
    final out = await remote.auth.login('0551234500', 'Demo@1234');
    expect(out.user?.role, 'Patient');
    remote.user = out.user;
    expect(await remote.loadAll(), isNull, reason: 'لا يجب أن يفشل أي قسم');

    expect(doctors.length, greaterThanOrEqualTo(8));
    expect(doctors.any((d) => d.name.contains('منى')), isTrue);
    expect(store.profile.name, contains('ريم'));
    expect(store.appointments.all, isNotEmpty);
    expect(store.invoices.all, isNotEmpty);
    expect(store.medications.active, isNotEmpty);
    expect(store.notifications.all, isNotEmpty);
    expect(store.conversations.all, isNotEmpty);
    expect(labReports.any((r) => r.items.any((i) => i.name.contains('السكر التراكمي'))), isTrue);
    expect(imagingStudies, isNotEmpty);
    expect(visitRecords, isNotEmpty);
    expect(healthSummary.allergies, contains('البنسلين'));
    expect(prof.insurance.policy, 'POL-8841-229');
    expect(store.settings.offers, isFalse);

    // 2) تفاصيل الطبيب والخانات الحقيقية
    final mona = doctors.firstWhere((d) => d.name.contains('منى'));
    await remote.loadDoctorDetail(mona.id);
    expect(doctorById(mona.id).about, isNotEmpty);
    expect(doctorById(mona.id).days, isNotEmpty);
    await remote.loadAvailability(mona.id, VisitType.clinic);
    final days = store.appointments.bookableDays(mona);
    expect(days, isNotEmpty);
    final day = days.first;
    final slot = store.appointments.slotsFor(mona, day).first;

    // 3) حجز (الدفع في العيادة) ثم يظهر في القائمة وفي الفواتير
    final before = store.appointments.all.length;
    final id = await store.appointments.bookRemote(doctor: mona, day: day, minutes: slot, type: VisitType.clinic, forWhom: 'أنا', reason: 'اختبار تكامل', payment: 'الدفع في العيادة', insured: true);
    expect(store.appointments.all.length, before + 1);
    expect(store.appointments.byId(id).status, ApptStatus.upcoming);
    expect(store.appointments.byId(id).day, day);
    await _settle();
    await remote.loadInvoices();
    final unpaid = store.invoices.unpaid;
    expect(unpaid, isNotEmpty);

    // الخانة المحجوزة لم تعد متاحة
    await remote.loadAvailability(mona.id, VisitType.clinic);
    expect(store.appointments.slotsFor(mona, day), isNot(contains(slot)));

    // 4) سداد الفاتورة بالبطاقة ثم التحقق من الخادم
    final inv = unpaid.first;
    store.invoices.pay(inv.id, 'بطاقة (مدى/فيزا)');
    await _settle();
    await remote.loadInvoices();
    expect(store.invoices.byId(inv.id).paidWith, isNotNull);

    // 5) إلغاء الموعد
    store.appointments.cancel(id, 'اختبار تكامل');
    await _settle();
    await remote.loadAppointments();
    expect(store.appointments.byId(id).status, ApptStatus.cancelled);

    // 6) مراسلة الطبيبة
    final conv = store.conversations.all.firstWhere((c) => c.doctorId != null);
    await remote.loadMessages(conv.id);
    final count = store.conversations.byId(conv.id).messages.length;
    expect(count, greaterThan(0));
    store.conversations.send(conv.id, 'رسالة اختبار ${DateTime.now().millisecondsSinceEpoch}');
    await _settle();
    await remote.loadMessages(conv.id);
    expect(store.conversations.byId(conv.id).messages.length, count + 1);

    // 7) إشعارات وإعدادات وعائلة
    final unread = store.notifications.unreadCount;
    store.notifications.markAllRead();
    await _settle();
    await remote.loadNotifications();
    expect(store.notifications.unreadCount, 0, reason: 'كان $unread');
    store.settings.set('offers', true);
    await _settle();
    await remote.loadProfile();
    expect(store.settings.offers, isTrue);
    store.settings.set('offers', false);
    store.family.add(const prof.FamilyMember('طفل اختبار', 'ابني', 2020));
    await _settle();
    await remote.loadProfile();
    final added = store.family.all.firstWhere((m) => m.name == 'طفل اختبار');
    expect(added.id, greaterThan(0));
    store.family.removeAt(store.family.all.indexOf(added));
    await _settle();
    await remote.loadProfile();
    expect(store.family.all.any((m) => m.name == 'طفل اختبار'), isFalse);

    // 8) أخطاء الخادم تصل برسائل مفهومة: حجز في الماضي/خانة غير متاحة
    await expectLater(
      store.appointments.bookRemote(doctor: mona, day: -3, minutes: slot, type: VisitType.clinic, forWhom: 'أنا', reason: 'x', payment: 'الدفع في العيادة', insured: false),
      throwsA(isA<ApiException>().having((e) => e.code, 'code', anyOf('slot_invalid', 'validation_failed'))),
    );

    // 9) الخروج يمسح الجلسة والبيانات
    await remote.logout();
    expect(store.appointments.all, isEmpty);
    expect(await client.tokens.access, isNull);
    await expectLater(remote.loadAppointments(), throwsA(isA<ApiException>().having((e) => e.status, 'status', 401)));
  }, skip: _live.isEmpty ? 'يتطلب API شغّالًا: --dart-define=LIVE_API=http://localhost:5080' : false, timeout: const Timeout(Duration(minutes: 2)));
}
