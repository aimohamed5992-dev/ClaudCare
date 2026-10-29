import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:patient_app/main.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  setUp(() => SharedPreferences.setMockInitialValues({}));

  testWidgets('أول تشغيل: Splash ← Onboarding (3 شرائح) ← Welcome', (t) async {
    await t.pumpWidget(PatientApp());
    expect(find.text('تطبيق المرضى'), findsOneWidget);
    expect(Directionality.of(t.element(find.text('تطبيق المرضى'))), TextDirection.rtl);

    await t.pump(const Duration(seconds: 3));
    await t.pumpAndSettle();
    expect(find.text('احجز موعدك بسهولة'), findsOneWidget);

    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();
    expect(find.text('نتائجك وأشعتك في مكان واحد'), findsOneWidget);
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();
    expect(find.text('رعاية لك ولعائلتك'), findsOneWidget);

    await t.tap(find.text('ابدأ الآن'));
    await t.pumpAndSettle();
    expect(find.text('أهلًا بك في ClaudCare'), findsOneWidget);
    expect(find.text('إنشاء حساب جديد'), findsOneWidget);
    expect((await SharedPreferences.getInstance()).getBool('patient.onboarding.seen'), isTrue);
  });

  testWidgets('التخطي ثم إعادة التشغيل يتجاوز الـ onboarding', (t) async {
    await t.pumpWidget(PatientApp());
    await t.pump(const Duration(seconds: 3));
    await t.pumpAndSettle();
    await t.tap(find.text('تخطي'));
    await t.pumpAndSettle();
    expect(find.text('أهلًا بك في ClaudCare'), findsOneWidget);

    await t.pumpWidget(const SizedBox());
    await t.pumpWidget(PatientApp());
    await t.pump(const Duration(seconds: 3));
    await t.pumpAndSettle();
    expect(find.text('احجز موعدك بسهولة'), findsNothing);
    expect(find.text('أهلًا بك في ClaudCare'), findsOneWidget);
  });
}
