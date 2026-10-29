import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget host(Widget child) => CcApp(title: 't', home: Scaffold(body: Center(child: child)));

void main() {
  testWidgets('التطبيق عربي RTL بخلفية بيضاء', (t) async {
    late TextDirection dir;
    await t.pumpWidget(CcApp(title: 't', home: Builder(builder: (c) {
      dir = Directionality.of(c);
      return const Scaffold(body: SizedBox());
    })));
    expect(dir, TextDirection.rtl);
    expect(ccTheme().scaffoldBackgroundColor, Colors.white);
  });

  testWidgets('الزر: ينفذ عند الضغط ويتعطل أثناء التحميل', (t) async {
    var taps = 0;
    await t.pumpWidget(host(CcButton(label: 'حفظ', onPressed: () => taps++)));
    await t.tap(find.text('حفظ'));
    expect(taps, 1);
    await t.pumpWidget(host(CcButton(label: 'حفظ', loading: true, onPressed: () => taps++)));
    await t.tap(find.byType(FilledButton), warnIfMissed: false);
    expect(taps, 1);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('حقل كلمة المرور يبدّل الإظهار', (t) async {
    await t.pumpWidget(host(const CcTextField(label: 'كلمة المرور', obscure: true)));
    expect(t.widget<TextField>(find.byType(TextField)).obscureText, isTrue);
    await t.tap(find.byType(IconButton));
    await t.pump();
    expect(t.widget<TextField>(find.byType(TextField)).obscureText, isFalse);
  });

  testWidgets('الأفاتار يعرض الحروف الأولى بدون صورة', (t) async {
    await t.pumpWidget(host(const CcAvatar(name: 'د. منى القاسم')));
    expect(find.text('مق'), findsOneWidget);
  });

  testWidgets('مؤشر الصفحات يصف الصفحة الحالية', (t) async {
    await t.pumpWidget(host(const CcPageDots(count: 3, index: 1)));
    expect(find.bySemanticsLabel('الصفحة 2 من 3'), findsOneWidget);
  });
}
