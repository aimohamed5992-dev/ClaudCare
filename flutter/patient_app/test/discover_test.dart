import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:patient_app/data/doctors.dart';
import 'package:patient_app/screens/doctor_profile_screen.dart';
import 'package:patient_app/widgets/doctor_card.dart';
import 'package:patient_app/shell/patient_shell.dart';
import 'package:patient_app/state/app_scope.dart';

late PatientStore store;

Future<void> open(WidgetTester t) async {
  t.view.physicalSize = const Size(780, 2400);
  t.view.devicePixelRatio = 2;
  addTearDown(t.view.reset);
  store = PatientStore();
  await t.pumpWidget(AppScope(store: store, child: const CcApp(title: 't', home: PatientShell())));
  await t.pumpAndSettle();
}

void main() {
  group('منطق البحث', () {
    test('بالاسم والتخصص والتصفية', () {
      expect(searchDoctors(query: 'منى').single.id, 1);
      expect(searchDoctors(query: 'الأطفال').map((d) => d.id), contains(3));
      expect(searchDoctors(specialty: 'cardiology').single.id, 1);
      expect(searchDoctors(query: 'غير موجود'), isEmpty);
    });
    test('الترتيب', () {
      final byPrice = searchDoctors(sort: DoctorSort.price).map((d) => d.price).toList();
      expect(byPrice, [...byPrice]..sort());
      expect(searchDoctors(sort: DoctorSort.rating).first.rating, 4.9);
      expect(searchDoctors(sort: DoctorSort.experience).first.id, 2); // 15 سنة
    });
    test('المفضلة فقط', () {
      expect(searchDoctors(favoritesOnly: true, favorites: {3, 5}).map((d) => d.id).toSet(), {3, 5});
    });
  });

  testWidgets('الرئيسية: تحية وموعد قادم وتخصصات', (t) async {
    await open(t);
    expect(find.text('ريم العلي'), findsOneWidget);
    expect(find.text('موعدك القادم'), findsOneWidget);
    expect(find.text('بعد 2 أيام'), findsOneWidget);
    expect(find.text('د. منى القاسم'), findsWidgets);
    expect(find.text('القلب'), findsWidgets);
    expect(find.text('الفحص الشامل'), findsOneWidget);
  });

  testWidgets('النقر على تخصص ينقل للأطباء مع التصفية', (t) async {
    await open(t);
    await t.tap(find.text('الأطفال').first);
    await t.pumpAndSettle();
    expect(store.tab.value, 1);
    expect(store.specialtyFilter.value, 'pediatrics');
    expect(find.text('1 طبيبًا'), findsOneWidget);
    expect(find.text('د. ريم العتيبي'), findsWidgets);
    expect(find.text('د. عمر الشامي'), findsNothing);
  });

  testWidgets('شريط البحث في الرئيسية يفتح تبويب الأطباء', (t) async {
    await open(t);
    await t.tap(find.bySemanticsLabel('ابحث عن طبيب أو تخصص'));
    await t.pumpAndSettle();
    expect(store.tab.value, 1);
    expect(find.text('8 طبيبًا'), findsOneWidget);
  });

  testWidgets('تبويب الأطباء: بحث وحالة فارغة ومسح التصفية', (t) async {
    await open(t);
    await t.tap(find.text('الأطباء').last);
    await t.pumpAndSettle();
    await t.enterText(find.byType(TextField), 'زياد');
    await t.pumpAndSettle();
    expect(find.text('1 طبيبًا'), findsOneWidget);
    expect(find.text('د. زياد النجار'), findsOneWidget);
    await t.enterText(find.byType(TextField), 'xyz');
    await t.pumpAndSettle();
    expect(find.text('لا نتائج'), findsOneWidget);
    await t.tap(find.text('مسح التصفية'));
    await t.pumpAndSettle();
    expect(find.text('8 طبيبًا'), findsOneWidget);
  });

  testWidgets('الترتيب بالسعر والمفضلة', (t) async {
    await open(t);
    await t.tap(find.text('الأطباء').last);
    await t.pumpAndSettle();
    await t.tap(find.text('الأعلى تقييمًا'));
    await t.pumpAndSettle();
    await t.tap(find.text('الأقل سعرًا'));
    await t.pumpAndSettle();
    expect(find.text('الأقل سعرًا'), findsOneWidget); // أصبح عنوان الشارة
    // أرخص طبيب (240 ر.س: ياسر العمر) يظهر أولًا
    expect(t.widgetList<DoctorCard>(find.byType(DoctorCard)).first.doctor.id, 8);

    // المفضلة: د. منى مضافة افتراضيًا
    await t.tap(find.text('المفضلة'));
    await t.pumpAndSettle();
    expect(find.text('1 طبيبًا'), findsOneWidget);
    expect(find.text('د. منى القاسم'), findsOneWidget);
    await t.tap(find.byTooltip('إزالة د. منى القاسم من المفضلة'));
    await t.pumpAndSettle();
    expect(find.text('لا أطباء في المفضلة'), findsOneWidget);
    expect(store.favorites.has(1), isFalse);
  });

  testWidgets('ملف الطبيب: المحتوى والمفضلة وزر الحجز', (t) async {
    await open(t);
    await t.tap(find.text('الأطباء').last);
    await t.pumpAndSettle();
    await t.tap(find.text('د. عمر الشامي'));
    await t.pumpAndSettle();
    expect(find.text('ملف الطبيب'), findsOneWidget);
    expect(find.text('خبير ومطمئن في تعامله.'), findsOneWidget); // تقييم مكتوب
    expect(find.text('350 ر.س'), findsWidgets);
    expect(find.text('استشارة عن بُعد'), findsOneWidget);
    await t.tap(find.byTooltip('إضافة للمفضلة'));
    await t.pump();
    expect(store.favorites.has(2), isTrue);
    await t.tap(find.text('احجز موعدًا'));
    await t.pumpAndSettle();
    expect(find.text('حجز موعد'), findsOneWidget);
    await t.tap(find.byTooltip('رجوع'));
    await t.pumpAndSettle();
    await t.tap(find.byTooltip('رجوع'));
    await t.pumpAndSettle();
    expect(find.text('الأطباء'), findsWidgets);
  });

  testWidgets('طبيب بلا استشارة عن بُعد وبلا تقييمات مكتوبة', (t) async {
    await open(t);
    await t.tap(find.text('الأطباء').last);
    await t.pumpAndSettle();
    await t.enterText(find.byType(TextField), 'زياد');
    await t.pumpAndSettle();
    await t.tap(find.text('د. زياد النجار'));
    await t.pumpAndSettle();
    expect(find.text('ملف الطبيب'), findsOneWidget);
    expect(find.text('استشارة عن بُعد'), findsNothing);
    await t.scrollUntilVisible(find.text('لا توجد تقييمات مكتوبة بعد.'), 300, scrollable: find.descendant(of: find.byType(DoctorProfileScreen), matching: find.byType(Scrollable)));
    await t.pumpAndSettle();
    expect(find.text('لا توجد تقييمات مكتوبة بعد.'), findsOneWidget);
  });
}
