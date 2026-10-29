import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('المدققات', () {
    test('الجوال', () {
      expect(ccPhone('0501234567'), isNull);
      expect(ccPhone('٠٥٠١٢٣٤٥٦٧'), isNull); // أرقام عربية
      expect(ccPhone('+966501234567'), isNull);
      expect(ccPhone('0412345678'), isNotNull);
      expect(ccPhone(''), isNotNull);
    });
    test('الهوية', () {
      expect(ccNationalId('1098765432'), isNull);
      expect(ccNationalId('3098765432'), isNotNull);
      expect(ccNationalId('12345'), isNotNull);
    });
    test('البريد', () {
      expect(ccEmail('a@b.co'), isNull);
      expect(ccEmail('a@b'), isNotNull);
    });
    test('كلمة المرور الجديدة وقوتها', () {
      expect(ccNewPassword('abc12345'), isNull);
      expect(ccNewPassword('abcdefgh'), isNotNull);
      expect(ccNewPassword('a1'), isNotNull);
      expect(ccPasswordStrength('abc12345') < ccPasswordStrength('Abc12345!xyz'), isTrue);
    });
  });

  testWidgets('تسجيل الدخول: تحقق ثم خطأ خادم ثم نجاح', (t) async {
    var calls = 0;
    await t.pumpWidget(CcApp(
      title: 't',
      home: CcLoginScreen(
        title: 'دخول',
        subtitle: 's',
        identifierLabel: 'الجوال',
        validateIdentifier: ccPhone,
        onForgot: () {},
        onLogin: (id, pw) async {
          calls++;
          return pw == 'wrongpw' ? 'بيانات غير صحيحة' : null;
        },
      ),
    ));
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pump();
    expect(find.text('أدخل رقم الجوال'), findsOneWidget);
    expect(find.text('أدخل كلمة المرور'), findsOneWidget);
    expect(calls, 0);

    await t.enterText(find.byType(TextField).at(0), '0501234567');
    await t.enterText(find.byType(TextField).at(1), 'wrongpw');
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pumpAndSettle();
    expect(calls, 1);
    expect(find.text('بيانات غير صحيحة'), findsOneWidget);

    await t.enterText(find.byType(TextField).at(1), 'goodpw1');
    await t.tap(find.widgetWithText(FilledButton, 'تسجيل الدخول'));
    await t.pumpAndSettle();
    expect(calls, 2);
    expect(find.text('بيانات غير صحيحة'), findsNothing);
  });

  testWidgets('OTP: يتحقق تلقائيًا عند اكتمال الرمز، ويرفض الخاطئ', (t) async {
    var ok = false;
    await t.pumpWidget(CcApp(
      title: 't',
      home: CcOtpScreen(
        destination: '05•• ••• 67',
        onVerify: (c) async => c == '123456' ? null : 'الرمز غير صحيح',
        onVerified: (_) => ok = true,
        resendSeconds: 2,
      ),
    ));
    expect(find.textContaining('إعادة الإرسال بعد'), findsOneWidget);
    await t.enterText(find.byType(TextField), '111111');
    await t.pumpAndSettle();
    expect(find.text('الرمز غير صحيح'), findsOneWidget);
    expect(ok, isFalse);
    await t.enterText(find.byType(TextField), '١٢٣٤٥٦'); // أرقام عربية تُحوَّل
    await t.pumpAndSettle();
    expect(ok, isTrue);
    await t.pump(const Duration(seconds: 3));
    expect(find.text('إعادة إرسال الرمز'), findsOneWidget);
  });

  testWidgets('استعادة كلمة المرور: معرّف ← OTP ← كلمة جديدة ← نجاح', (t) async {
    await t.pumpWidget(CcApp(
      title: 't',
      home: Builder(builder: (c) => Scaffold(body: TextButton(onPressed: () => Navigator.of(c).push(MaterialPageRoute<void>(builder: (_) => CcForgotScreen(identifierLabel: 'الجوال', validateIdentifier: ccPhone, maskDestination: (s) => '05•••67'))), child: const Text('نسيت')))),
    ));
    await t.tap(find.text('نسيت'));
    await t.pumpAndSettle();
    await t.tap(find.text('إرسال الرمز'));
    await t.pump();
    expect(find.text('أدخل رقم الجوال'), findsOneWidget);
    await t.enterText(find.byType(TextField), '0501234567');
    await t.tap(find.text('إرسال الرمز'));
    await t.pumpAndSettle();
    expect(find.text('رمز التحقق'), findsOneWidget);
    await t.enterText(find.byType(TextField), '123456');
    await t.pumpAndSettle();
    expect(find.text('كلمة مرور جديدة'), findsOneWidget);
    await t.enterText(find.byType(TextField).at(0), 'abc12345');
    await t.enterText(find.byType(TextField).at(1), 'different1');
    await t.tap(find.text('حفظ كلمة المرور'));
    await t.pump();
    expect(find.text('كلمتا المرور غير متطابقتين'), findsOneWidget);
    await t.enterText(find.byType(TextField).at(1), 'abc12345');
    await t.tap(find.text('حفظ كلمة المرور'));
    await t.pumpAndSettle();
    expect(find.text('تم تغيير كلمة المرور'), findsOneWidget);
    await t.tap(find.text('العودة لتسجيل الدخول'));
    await t.pumpAndSettle();
    expect(find.text('نسيت'), findsOneWidget);
  });
}
