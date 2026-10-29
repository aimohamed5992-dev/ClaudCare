import 'package:doctor_app/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  setUp(() => SharedPreferences.setMockInitialValues({}));

  testWidgets('أول تشغيل: Splash ← Onboarding (3 شرائح) ← Welcome', (t) async {
    await t.pumpWidget(DoctorApp());
    expect(find.text('تطبيق الأطباء'), findsOneWidget);
    expect(Directionality.of(t.element(find.text('تطبيق الأطباء'))), TextDirection.rtl);

    await t.pump(const Duration(seconds: 3));
    await t.pumpAndSettle();
    expect(find.text('جدولك في جيبك'), findsOneWidget);
    expect(find.text('تخطي'), findsOneWidget);

    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();
    expect(find.text('سجلات مرضاك ونتائجهم فورًا'), findsOneWidget);
    await t.tap(find.text('التالي'));
    await t.pumpAndSettle();
    expect(find.text('وصفات واستشارات عن بُعد'), findsOneWidget);
    expect(find.text('ابدأ الآن'), findsOneWidget);

    await t.tap(find.text('ابدأ الآن'));
    await t.pumpAndSettle();
    expect(find.text('أهلًا دكتور'), findsOneWidget);
    expect((await SharedPreferences.getInstance()).getBool('doctor.onboarding.seen'), isTrue);
  });

  testWidgets('التخطي ينقل مباشرة إلى الترحيب، والتشغيل التالي يتجاوز الـ onboarding', (t) async {
    await t.pumpWidget(DoctorApp());
    await t.pump(const Duration(seconds: 3));
    await t.pumpAndSettle();
    await t.tap(find.text('تخطي'));
    await t.pumpAndSettle();
    expect(find.text('أهلًا دكتور'), findsOneWidget);

    await t.pumpWidget(const SizedBox());
    await t.pumpWidget(DoctorApp());
    await t.pump(const Duration(seconds: 3));
    await t.pumpAndSettle();
    expect(find.text('جدولك في جيبك'), findsNothing);
    expect(find.text('أهلًا دكتور'), findsOneWidget);
  });
}
