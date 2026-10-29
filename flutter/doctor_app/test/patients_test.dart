import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:doctor_app/data/appointments.dart';
import 'package:doctor_app/data/patients.dart';
import 'package:doctor_app/shell/doctor_shell.dart';
import 'package:doctor_app/state/app_scope.dart';
import 'package:doctor_app/widgets/medication_sheet.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

late AppointmentsController appts;
late PatientsController pats;

Future<void> open(WidgetTester t) async {
  t.view.physicalSize = const Size(780, 2200);
  t.view.devicePixelRatio = 2;
  addTearDown(t.view.reset);
  appts = AppointmentsController();
  pats = PatientsController();
  await t.pumpWidget(AppScope(store: DoctorStore(appointments: appts, patients: pats), child: const CcApp(title: 't', home: DoctorShell(name: 'د. منى القاسم'))));
  await t.pumpAndSettle();
  await t.tap(find.text('المرضى'));
  await t.pumpAndSettle();
}

void main() {
  test('البحث والتصفية والتحقق من الأدوية', () {
    final c = PatientsController();
    expect(c.search('سكري').map((p) => p.name), contains('محمد علي'));
    expect(c.search('0598877665').single.name, 'عبدالله ناصر');
    final sara = c.byName('سارة أحمد')!;
    expect(checkMed(sara, drugs[0]).allergy, 'البنسلين'); // أموكسيسيلين
    expect(checkMed(sara, drugs[1]).allergy, isNull);
    expect(checkMed(sara, drugs.firstWhere((d) => d.name.contains('أملوديبين'))).duplicate, isTrue);
    expect(checkMed(c.byName('عبدالله ناصر')!, drugs.firstWhere((d) => d.group == 'الأسبرين')).allergy, 'الأسبرين');
  });

  testWidgets('قائمة المرضى: بحث وتصفية', (t) async {
    await open(t);
    expect(find.text('9 مريضًا'), findsOneWidget);
    await t.enterText(find.byType(TextField), 'ناصر');
    await t.pumpAndSettle();
    expect(find.text('عبدالله ناصر'), findsOneWidget);
    expect(find.text('محمد علي'), findsNothing);
    await t.enterText(find.byType(TextField), 'غير موجود');
    await t.pumpAndSettle();
    expect(find.text('لا نتائج'), findsOneWidget);
    await t.enterText(find.byType(TextField), '');
    await t.tap(find.text('أطفال'));
    await t.pumpAndSettle();
    expect(find.text('يوسف خالد'), findsOneWidget);
    expect(find.text('محمد علي'), findsNothing);
  });

  testWidgets('الملف الطبي: تحذير الحساسية والتبويبات', (t) async {
    await open(t);
    await t.tap(find.text('سارة أحمد'));
    await t.pumpAndSettle();
    expect(find.text('حساسية: البنسلين'), findsOneWidget);
    expect(find.text('148/92'), findsOneWidget);
    await t.tap(find.text('النتائج'));
    await t.pumpAndSettle();
    expect(find.text('بوتاسيوم'), findsOneWidget);
    expect(find.text('مرتفع'), findsOneWidget);
    await t.tap(find.text('الوصفات'));
    await t.pumpAndSettle();
    expect(find.text('RX-3298'), findsOneWidget);
  });

  testWidgets('كشف كامل: تحقق ← تشخيص ← وصفة (مع تحذير الحساسية) ← حفظ', (t) async {
    await open(t);
    await t.tap(find.text('سارة أحمد'));
    await t.pumpAndSettle();
    await t.tap(find.text('بدء كشف جديد'));
    await t.pumpAndSettle();

    // الخطوة 1: الشكوى إلزامية والقيم المعقولة
    await t.tap(find.text('التالي'));
    await t.pump();
    expect(find.text('اكتب الشكوى الرئيسية'), findsOneWidget);
    await t.enterText(find.byType(TextField).at(0), 'ألم في الصدر');
    await t.enterText(find.byType(TextField).at(1), '400');
    await t.enterText(find.byType(TextField).at(2), '80');
    await t.tap(find.text('التالي'));
    await t.pump();
    expect(find.text('الضغط غير معقول'), findsOneWidget);
    await t.enterText(find.byType(TextField).at(1), '130');
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();

    // الخطوة 2: التشخيص إلزامي
    await t.tap(find.text('التالي'));
    await t.pump();
    expect(find.text('اكتب التشخيص'), findsOneWidget);
    await t.tap(find.text('ارتفاع ضغط الدم')); // اقتراح
    await t.pump();
    await t.tap(find.text('تخطيط قلب'));
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();

    // الخطوة 3: أموكسيسيلين يعارض حساسية البنسلين
    await t.tap(find.text('إضافة دواء'));
    await t.pumpAndSettle();
    await t.tap(find.text('أموكسيسيلين 500 مجم'));
    await t.pump();
    await t.tap(find.text('مرتين يوميًا'));
    await t.pump();
    expect(find.textContaining('حساسية من البنسلين'), findsOneWidget);
    expect(t.widget<FilledButton>(find.widgetWithText(FilledButton, 'إضافة رغم التحذير')).onPressed, isNull);
    await t.tap(find.text('باراسيتامول 500 مجم')); // دواء آمن
    await t.pump();
    expect(find.textContaining('حساسية من البنسلين'), findsNothing);
    await t.ensureVisible(find.text('إضافة للوصفة'));
    await t.tap(find.text('إضافة للوصفة'));
    await t.pumpAndSettle();
    expect(find.textContaining('باراسيتامول 500 مجم – مرتين يوميًا لمدة 7 أيام'), findsOneWidget);

    await t.tap(find.text('إنهاء الكشف'));
    await t.pumpAndSettle();
    expect(find.text('تم حفظ الكشف'), findsOneWidget);
    expect(find.text('دواء واحد'), findsOneWidget);

    final p = pats.byName('سارة أحمد')!;
    expect(p.visits.first.diagnosis, 'ارتفاع ضغط الدم');
    expect(p.prescriptions.first.meds.single, contains('باراسيتامول'));
    expect(p.prescriptions.first.no, 'RX-3400');
  });

  testWidgets('إنهاء كشف موعد يحدّث حالته', (t) async {
    await open(t);
    await t.tap(find.text('الرئيسية'));
    await t.pumpAndSettle();
    await t.tap(find.text('عرض التفاصيل'));
    await t.pumpAndSettle();
    await t.tap(find.text('متابعة الكشف'));
    await t.pumpAndSettle();
    await t.enterText(find.byType(TextField).at(0), 'متابعة السكري');
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();
    await t.enterText(find.byType(TextField).at(0), 'سكري غير منضبط');
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();
    await t.tap(find.text('إنهاء الكشف'));
    await t.pumpAndSettle();
    expect(appts.byId(2).status, ApptStatus.done);
    await t.tap(find.text('العودة'));
    await t.pumpAndSettle();
    expect(find.text('بانتظارك في العيادة'), findsOneWidget);
  });

  testWidgets('الخروج من الكشف بعد إدخال بيانات يطلب تأكيدًا', (t) async {
    await open(t);
    await t.tap(find.text('نورة سعد'));
    await t.pumpAndSettle();
    await t.tap(find.text('بدء كشف جديد'));
    await t.pumpAndSettle();
    await t.enterText(find.byType(TextField).at(0), 'صداع');
    await t.pumpAndSettle();
    await t.tap(find.byTooltip('إغلاق'));
    await t.pumpAndSettle();
    expect(find.text('تجاهل هذا الكشف؟'), findsOneWidget);
    await t.tap(find.text('متابعة الكشف'));
    await t.pumpAndSettle();
    expect(find.text('كشف نورة سعد'), findsOneWidget);
    await t.tap(find.byTooltip('إغلاق'));
    await t.pumpAndSettle();
    await t.tap(find.text('تجاهل'));
    await t.pumpAndSettle();
    expect(find.text('كشف نورة سعد'), findsNothing);
  });
}
