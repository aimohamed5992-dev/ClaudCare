import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:patient_app/data/appointments.dart';
import 'package:patient_app/data/doctors.dart';
import 'package:patient_app/data/records.dart';
import 'package:patient_app/shell/patient_shell.dart';
import 'package:patient_app/state/app_scope.dart';
import 'package:patient_app/utils/format.dart';

late PatientStore store;

Future<void> open(WidgetTester t) async {
  t.view.physicalSize = const Size(780, 2600);
  t.view.devicePixelRatio = 2;
  addTearDown(t.view.reset);
  store = PatientStore();
  await t.pumpWidget(AppScope(store: store, child: const CcApp(title: 't', home: PatientShell())));
  await t.pumpAndSettle();
}

/// يفتح ملف طبيب ثم شاشة الحجز.
Future<void> openBooking(WidgetTester t, String doctor) async {
  await t.tap(find.text('الأطباء').last);
  await t.pumpAndSettle();
  await t.enterText(find.byType(TextField), doctor.replaceFirst('د. ', ''));
  await t.pumpAndSettle();
  await t.tap(find.text(doctor).first);
  await t.pumpAndSettle();
  await t.tap(find.text('احجز موعدًا'));
  await t.pumpAndSettle();
}

void main() {
  group('منطق المواعيد', () {
    final c = PatientAppointmentsController();
    final d = doctorById(1);

    test('الخانات: لا ماضي ولا تعارض مع موعد المريض', () {
      expect(c.slotsFor(d, 0).every((m) => m > demoNowMinutes), isTrue);
      // موعد المريض مع د. منى بعد يومين الساعة 10:30
      expect(c.slotsFor(d, 2).contains(10 * 60 + 30), isFalse);
      expect(c.slotsFor(d, 2, excludeApptId: 1).contains(10 * 60 + 30), isTrue); // باستثناء موعده نفسه تصبح متاحة لإعادة الجدولة
      expect(c.bookableDays(d), isNotEmpty);
      expect(c.bookableDays(d).every((i) => i >= 0 && i < 14), isTrue);
    });

    test('الحجز والإلغاء والتراجع والتقييم وإعادة الجدولة', () {
      final x = PatientAppointmentsController();
      final id = x.book(doctor: d, day: 3, minutes: 11 * 60, type: VisitType.clinic, forWhom: 'أنا', reason: 'متابعة', price: 60, paid: true);
      expect(x.byId(id).status, ApptStatus.upcoming);
      expect(x.upcoming.length, 3);
      final prev = x.byId(id);
      x.cancel(id, 'حجزت بالخطأ');
      expect(x.byStatus(ApptStatus.cancelled).any((a) => a.id == id), isTrue);
      x.restore(id, prev);
      expect(x.byId(id).status, ApptStatus.upcoming);
      x.reschedule(id, 4, 12 * 60);
      expect((x.byId(id).day, x.byId(id).minutes), (4, 12 * 60));
      x.rate(3, 4, 'جيد');
      expect(x.byId(3).rating, 4);
    });

    test('قيم التحاليل وعلاماتها', () {
      expect(const LabItem('x', 5, 'u', 4, 6).flag, Flag.normal);
      expect(const LabItem('x', 7, 'u', 4, 6).flag, Flag.high);
      expect(const LabItem('x', 3, 'u', 4, 6).flag, Flag.low);
      expect(const LabItem('x', 20, 'u', 4, 6).flag, Flag.critical);
      expect(labReports.first.abnormal, 1); // الصفائح 148
      expect(const LabItem('x', 5, 'u', 4, 6).position, closeTo(0.5, 0.01));
    });
  });

  testWidgets('رحلة الحجز كاملة حتى النجاح وظهور الموعد في «مواعيدي»', (t) async {
    await open(t);
    await openBooking(t, 'د. ريم العتيبي');
    expect(find.text('حجز موعد'), findsOneWidget);

    // لا يمكن المتابعة بدون وقت
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();
    expect(find.text('اختر اليوم والوقت'), findsOneWidget);

    // د. ريم لديها استشارة عن بُعد
    await t.tap(find.text('استشارة عن بُعد'));
    await t.pumpAndSettle();
    final d = doctorById(3);
    final days = store.appointments.bookableDays(d);
    final firstDay = days.first;
    final slot = store.appointments.slotsFor(d, firstDay).first;

    final base = DateTime.now();
    await t.tap(find.bySemanticsLabel(dayLabel(base.add(Duration(days: firstDay)), base)));
    await t.pumpAndSettle();
    expect(find.text('اختر يومًا أولًا لعرض الأوقات المتاحة.'), findsNothing);
    await t.tap(find.text(_fmt(slot)));
    await t.pumpAndSettle();
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();

    // الخطوة 2: سبب إلزامي
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();
    expect(find.text('اختر سبب الزيارة'), findsOneWidget);
    await t.tap(find.text('نتائج تحاليل'));
    await t.pumpAndSettle();
    await t.tap(find.text('زياد (ابني)'));
    await t.pumpAndSettle();
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();

    // الخطوة 3: التأمين يخفض المستحق (150 → تحمّل 20% = 30)
    expect(find.text('30 ر.س'), findsWidgets);
    await t.tap(find.byType(Switch));
    await t.pumpAndSettle();
    expect(find.text('150 ر.س'), findsWidgets);
    await t.tap(find.text('تأكيد الحجز والدفع'));
    await t.pumpAndSettle();

    expect(find.text('تم تأكيد حجزك'), findsOneWidget);
    expect(store.appointments.upcoming.length, 3);
    final booked = store.appointments.upcoming.firstWhere((a) => a.id >= 100);
    expect((booked.doctorId, booked.minutes, booked.forWhom), (3, slot, 'زياد (ابني)'));
    expect(booked.type, VisitType.video);

    await t.tap(find.text('عرض مواعيدي'));
    await t.pumpAndSettle();
    expect(store.tab.value, 2);
    expect(find.text('القادمة (3)'), findsOneWidget);
  });

  testWidgets('مواعيدي: إلغاء مع تراجع، وتبويب السابقة والتقييم', (t) async {
    await open(t);
    await t.tap(find.text('مواعيدي').last);
    await t.pumpAndSettle();
    expect(find.text('القادمة (2)'), findsOneWidget);
    await t.tap(find.text('د. منى القاسم').first);
    await t.pumpAndSettle();
    await t.tap(find.text('إلغاء الموعد'));
    await t.pumpAndSettle();
    expect(find.textContaining('الإلغاء مجاني'), findsOneWidget);
    expect(t.widget<FilledButton>(find.widgetWithText(FilledButton, 'تأكيد الإلغاء')).onPressed, isNull);
    await t.tap(find.text('حجزت بالخطأ'));
    await t.pump();
    await t.tap(find.text('تأكيد الإلغاء'));
    await t.pumpAndSettle();
    expect(store.appointments.upcoming.length, 1);
    expect(find.text('القادمة (1)'), findsOneWidget);
    await t.tap(find.text('تراجع'));
    await t.pumpAndSettle();
    expect(store.appointments.upcoming.length, 2);

    await t.tap(find.text('السابقة (2)'));
    await t.pumpAndSettle();
    expect(find.text('بانتظار تقييمك'), findsOneWidget);
    await t.tap(find.text('د. هالة منصور'));
    await t.pumpAndSettle();
    await t.tap(find.text('قيّم الزيارة'));
    await t.pumpAndSettle();
    expect(t.widget<FilledButton>(find.widgetWithText(FilledButton, 'إرسال التقييم')).onPressed, isNull);
    await t.tap(find.byTooltip('4 من 5'));
    await t.pump();
    await t.tap(find.text('إرسال التقييم'));
    await t.pumpAndSettle();
    expect(store.appointments.byId(4).rating, 4);
    expect(find.text('بانتظار تقييمك'), findsNothing);
  });

  testWidgets('مكالمة الفيديو مقفلة قبل موعدها', (t) async {
    await open(t);
    await t.tap(find.text('مواعيدي').last);
    await t.pumpAndSettle();
    await t.tap(find.text('د. ريم العتيبي').first);
    await t.pumpAndSettle();
    expect(find.textContaining('المكالمة تُفتح قبل الموعد'), findsOneWidget);
    expect(t.widget<FilledButton>(find.widgetWithText(FilledButton, 'المكالمة تُفتح قبل الموعد بـ 10 دقائق')).onPressed, isNull);
  });

  testWidgets('سجلي: ملخص صحي وتحاليل وتقرير بقيم خارج المعدل', (t) async {
    await open(t);
    await t.tap(find.text('سجلي').last);
    await t.pumpAndSettle();
    expect(find.text('سجلي الطبي'), findsOneWidget);
    expect(find.text('الحساسية: لا يوجد'), findsOneWidget);
    expect(find.text('صورة دم كاملة (CBC)'), findsOneWidget);
    await t.tap(find.text('وظائف الغدة الدرقية')); // قيد التنفيذ
    await t.pump();
    expect(find.textContaining('قيد التنفيذ'), findsWidgets);

    await t.tap(find.text('سكر صائم وفيتامين د'));
    await t.pumpAndSettle();
    expect(find.text('نتيجة التحليل'), findsOneWidget);
    expect(find.textContaining('3 قيم خارج المعدل'), findsOneWidget);
    expect(find.text('مرتفع'), findsWidgets);
    expect(find.text('حرج'), findsOneWidget); // فيتامين د 14 أقل بكثير من المعدل
    await t.tap(find.byTooltip('رجوع'));
    await t.pumpAndSettle();
  });

  testWidgets('سجلي: الأشعة والزيارات', (t) async {
    await open(t);
    await t.tap(find.text('سجلي').last);
    await t.pumpAndSettle();
    await t.tap(find.text('الأشعة'));
    await t.pumpAndSettle();
    await t.tap(find.text('أشعة سينية للصدر'));
    await t.pumpAndSettle();
    expect(find.text('نتيجة الأشعة'), findsOneWidget);
    expect(find.byType(InteractiveViewer), findsOneWidget);
    expect(find.text('أشعة صدر طبيعية.'), findsOneWidget);
    await t.tap(find.byTooltip('رجوع'));
    await t.pumpAndSettle();
    await t.tap(find.text('الزيارات'));
    await t.pumpAndSettle();
    expect(find.text('ارتفاع طفيف في ضغط الدم'), findsOneWidget);
    expect(find.text('أملوديبين 5 مجم – مرة يوميًا'), findsOneWidget);
  });
}

String _fmt(int m) {
  final h = m ~/ 60, mm = m % 60;
  final h12 = h % 12 == 0 ? 12 : h % 12;
  return '${h12.toString().padLeft(2, '0')}:${mm.toString().padLeft(2, '0')} ${h < 12 ? 'ص' : 'م'}';
}
