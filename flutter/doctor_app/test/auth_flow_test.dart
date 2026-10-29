import 'package:doctor_app/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

Future<void> launch(WidgetTester t) async {
  await t.pumpWidget(DoctorApp());
  await t.pump(const Duration(seconds: 3));
  await t.pumpAndSettle();
}

void main() {
  setUp(() => SharedPreferences.setMockInitialValues({'doctor.onboarding.seen': true}));

  testWidgets('دخول الطبيب: تحقق ← خطأ ← OTP ← الرئيسية ← جلسة محفوظة ← خروج', (t) async {
    t.view.physicalSize = const Size(780, 2400);
    t.view.devicePixelRatio = 2;
    addTearDown(t.view.reset);
    await launch(t);
    await t.tap(find.text('تسجيل الدخول'));
    await t.pumpAndSettle();
    expect(find.text('تسجيل دخول الطبيب'), findsOneWidget);

    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pump();
    expect(find.text('أدخل البريد الإلكتروني أو رقم الموظف'), findsOneWidget);

    await t.enterText(find.byType(TextField).at(0), 'bad@mail');
    await t.enterText(find.byType(TextField).at(1), 'wrongpw');
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pump();
    expect(find.text('صيغة البريد غير صحيحة'), findsOneWidget);

    await t.enterText(find.byType(TextField).at(0), 'mona@hospital.com');
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pumpAndSettle();
    expect(find.textContaining('بيانات الدخول غير صحيحة'), findsOneWidget);

    await t.enterText(find.byType(TextField).at(1), 'secret12');
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pumpAndSettle();
    expect(find.text('التحقق بخطوتين'), findsOneWidget);

    await t.enterText(find.byType(TextField), '000000');
    await t.pumpAndSettle();
    expect(find.text('الرمز غير صحيح، حاول مرة أخرى'), findsOneWidget);

    await t.enterText(find.byType(TextField), '123456');
    await t.pumpAndSettle();
    expect(find.text('د. منى القاسم'), findsWidgets);
    expect((await SharedPreferences.getInstance()).getString('doctor.session.name'), 'د. منى القاسم');

    // إعادة التشغيل تدخل مباشرة
    await t.pumpWidget(const SizedBox());
    await launch(t);
    expect(find.text('د. منى القاسم'), findsWidgets);

    await t.tap(find.text('حسابي'));
    await t.pumpAndSettle();
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الخروج'));
    await t.pumpAndSettle();
    await t.tap(find.widgetWithText(TextButton, 'تسجيل الخروج'));
    await t.pumpAndSettle();
    // بعد الخروج يبدأ التطبيق من Splash ثم Onboarding
    expect(find.text('جدولك في جيبك'), findsOneWidget);
    expect((await SharedPreferences.getInstance()).getString('doctor.session.name'), isNull);
  });

  testWidgets('نسيت كلمة المرور من شاشة الطبيب', (t) async {
    await launch(t);
    await t.tap(find.text('تسجيل الدخول'));
    await t.pumpAndSettle();
    await t.tap(find.text('نسيت كلمة المرور؟'));
    await t.pumpAndSettle();
    expect(find.text('استعادة كلمة المرور'), findsOneWidget);
  });
}
