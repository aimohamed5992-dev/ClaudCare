import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:doctor_app/data/appointments.dart';
import 'package:doctor_app/state/app_scope.dart';
import 'package:doctor_app/shell/doctor_shell.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Future<AppointmentsController> open(WidgetTester t) async {
  t.view.physicalSize = const Size(780, 1688);
  t.view.devicePixelRatio = 2;
  addTearDown(t.view.reset);
  final c = AppointmentsController();
  await t.pumpWidget(AppScope(store: DoctorStore(appointments: c), child: const CcApp(title: 't', home: DoctorShell(name: 'د. منى القاسم'))));
  await t.pumpAndSettle();
  return c;
}

void main() {
  group('المنطق', () {
    test('الحالي ثم الواصل ثم القادم', () {
      final c = AppointmentsController();
      expect(c.focus!.patient, 'محمد علي'); // قيد الكشف
      c.finish(c.focus!.id);
      expect(c.focus!.patient, 'سارة أحمد'); // وصل
      c.start(c.focus!.id);
      c.finish(c.focus!.id);
      expect(c.focus!.patient, 'يوسف خالد'); // القادم
    });

    test('الخانات المتاحة تتجنب التعارض والماضي', () {
      final c = AppointmentsController();
      final today = c.freeSlots(0, 30);
      expect(today.every((m) => m > demoNowMinutes), isTrue);
      expect(today.contains(10 * 60), isFalse); // موعد يوسف
      expect(today.contains(12 * 60), isTrue);
      // استثناء الموعد نفسه يحرر خانته
      expect(c.freeSlots(0, 30, excludeId: 4).contains(10 * 60), isTrue);
      expect(c.freeSlots(1, 30).contains(9 * 60), isFalse);
    });

    test('إعادة الجدولة تؤكد الموعد وتنقله', () {
      final c = AppointmentsController();
      c.reschedule(5, 1, 14 * 60);
      final a = c.byId(5);
      expect((a.day, a.minutes, a.status), (1, 14 * 60, ApptStatus.confirmed));
    });
  });

  testWidgets('الرئيسية: إحصاءات وبطاقة الموعد الحالي والتنقل', (t) async {
    await open(t);
    expect(find.text('د. منى القاسم'), findsOneWidget);
    expect(find.text('الكشف الحالي'), findsOneWidget);
    expect(find.bySemanticsLabel('مواعيد اليوم: 8'), findsOneWidget);
    expect(find.bySemanticsLabel('بانتظار التأكيد: 2'), findsOneWidget);
    await t.tap(find.text('المواعيد').last);
    await t.pumpAndSettle();
    expect(find.text('عبدالله ناصر'), findsOneWidget);
    await t.tap(find.text('المرضى'));
    await t.pumpAndSettle();
    expect(find.text('محمد علي'), findsOneWidget);
  });

  testWidgets('تأكيد موعد معلّق ثم التراجع', (t) async {
    final c = await open(t);
    await t.tap(find.text('المواعيد').last);
    await t.pumpAndSettle();
    await t.tap(find.text('بانتظار التأكيد').first); // فلتر
    await t.pumpAndSettle();
    expect(find.text('نورة سعد'), findsOneWidget);
    expect(find.text('يوسف خالد'), findsNothing);
    await t.tap(find.text('نورة سعد'));
    await t.pumpAndSettle();
    await t.tap(find.text('تأكيد الموعد'));
    await t.pumpAndSettle();
    expect(c.byId(5).status, ApptStatus.confirmed);
    expect(find.text('تم تأكيد موعد نورة سعد'), findsOneWidget);
    await t.tap(find.text('تراجع'));
    await t.pumpAndSettle();
    expect(c.byId(5).status, ApptStatus.pending);
  });

  testWidgets('الإلغاء يتطلب سببًا', (t) async {
    final c = await open(t);
    await t.tap(find.text('المواعيد').last);
    await t.pumpAndSettle();
    await t.tap(find.text('يوسف خالد'));
    await t.pumpAndSettle();
    await t.tap(find.text('إلغاء الموعد'));
    await t.pumpAndSettle();
    expect(t.widget<FilledButton>(find.widgetWithText(FilledButton, 'تأكيد الإلغاء')).onPressed, isNull);
    await t.tap(find.text('إجازة'));
    await t.pump();
    await t.tap(find.text('تأكيد الإلغاء'));
    await t.pumpAndSettle();
    expect(c.byId(4).status, ApptStatus.cancelled);
    expect(c.byId(4).cancelReason, 'إجازة');
  });

  testWidgets('إعادة الجدولة: اختيار يوم وخانة ثم الحفظ', (t) async {
    final c = await open(t);
    await t.tap(find.text('المواعيد').last);
    await t.pumpAndSettle();
    await t.dragUntilVisible(find.text('هند ماجد'), find.byType(ListView).last, const Offset(0, -200));
    await t.tap(find.text('هند ماجد'));
    await t.pumpAndSettle();
    await t.tap(find.text('إعادة جدولة'));
    await t.pumpAndSettle();
    expect(t.widget<FilledButton>(find.widgetWithText(FilledButton, 'تأكيد الموعد الجديد')).onPressed, isNull);
    await t.tap(find.text('03:00 م'));
    await t.pump();
    await t.tap(find.text('تأكيد الموعد الجديد'));
    await t.pumpAndSettle();
    final a = c.byId(7);
    expect((a.day, a.minutes), (0, 15 * 60));
    expect(a.status, ApptStatus.confirmed);
  });

  testWidgets('الموعد الجاري يفتح متابعة الكشف', (t) async {
    await open(t);
    await t.tap(find.text('عرض التفاصيل'));
    await t.pumpAndSettle();
    await t.tap(find.text('متابعة الكشف'));
    await t.pumpAndSettle();
    expect(find.text('كشف محمد علي'), findsOneWidget);
    expect(find.text('الشكوى الرئيسية'), findsOneWidget);
  });

  testWidgets('اليوم الفارغ يعرض حالة فارغة', (t) async {
    await open(t);
    await t.tap(find.text('المواعيد').last);
    await t.pumpAndSettle();
    await t.tap(find.bySemanticsLabel(RegExp('0 مواعيد')).first);
    await t.pumpAndSettle();
    expect(find.text('لا توجد مواعيد'), findsOneWidget);
  });
}
