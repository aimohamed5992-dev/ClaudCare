import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:patient_app/main.dart';
import 'package:shared_preferences/shared_preferences.dart';

Future<void> launch(WidgetTester t) async {
  await t.pumpWidget(PatientApp());
  await t.pump(const Duration(seconds: 3));
  await t.pumpAndSettle();
}

void main() {
  setUp(() => SharedPreferences.setMockInitialValues({'patient.onboarding.seen': true}));

  testWidgets('دخول المريض بالجوال ثم جلسة محفوظة', (t) async {
    t.view.physicalSize = const Size(780, 2600);
    t.view.devicePixelRatio = 2;
    addTearDown(t.view.reset);
    await launch(t);
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pumpAndSettle();
    expect(find.text('أهلًا بعودتك'), findsOneWidget);

    await t.enterText(find.byType(TextField).at(0), '0412345678');
    await t.enterText(find.byType(TextField).at(1), 'secret12');
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pump();
    expect(find.textContaining('أدخل جوالًا يبدأ بـ 05'), findsOneWidget);

    await t.enterText(find.byType(TextField).at(0), '1098765432'); // هوية
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pumpAndSettle();
    expect(find.text('ريم العلي'), findsOneWidget);

    await t.pumpWidget(const SizedBox());
    await launch(t);
    expect(find.text('ريم العلي'), findsOneWidget);
    await t.tap(find.text('حسابي'));
    await t.pumpAndSettle();
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الخروج'));
    await t.pumpAndSettle();
    await t.tap(find.widgetWithText(TextButton, 'تسجيل الخروج'));
    await t.pumpAndSettle();
    // بعد الخروج يبدأ التطبيق من Splash ثم Onboarding
    expect(find.text('احجز موعدك بسهولة'), findsOneWidget);
  });

  testWidgets('إنشاء حساب: تحقق من الحقول ثم OTP ثم الرئيسية', (t) async {
    await launch(t);
    await t.tap(find.text('إنشاء حساب جديد'));
    await t.pumpAndSettle();
    await t.ensureVisible(find.text('إنشاء الحساب'));
    await t.tap(find.text('إنشاء الحساب'));
    await t.pumpAndSettle();
    expect(find.text('أدخل اسمك الثنائي على الأقل'), findsOneWidget);
    expect(find.text('أدخل رقم الجوال'), findsOneWidget);
    expect(find.text('أدخل رقم الهوية'), findsOneWidget);

    final fields = find.byType(TextField);
    await t.enterText(fields.at(0), 'سارة أحمد');
    await t.enterText(fields.at(1), '0501234567');
    await t.enterText(fields.at(2), '1098765432');
    await t.enterText(fields.at(3), 'abc12345');
    await t.enterText(fields.at(4), 'abc12345');
    await t.ensureVisible(find.byType(CheckboxListTile));
    await t.tap(find.byType(CheckboxListTile));
    await t.pump();
    await t.ensureVisible(find.text('إنشاء الحساب'));
    await t.tap(find.text('إنشاء الحساب'));
    await t.pumpAndSettle();
    expect(find.text('رمز التحقق'), findsOneWidget);
    expect(find.textContaining('67'), findsWidgets);

    await t.enterText(find.byType(TextField), '123456');
    await t.pumpAndSettle();
    expect(find.text('سارة أحمد'), findsOneWidget);
  });
}
