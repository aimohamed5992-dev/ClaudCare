import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:patient_app/data/invoices.dart';
import 'package:patient_app/data/medications.dart';
import 'package:patient_app/data/messages.dart';
import 'package:patient_app/shell/patient_shell.dart';
import 'package:patient_app/state/app_scope.dart';

late PatientStore store;

Future<void> open(WidgetTester t, {bool reply = false}) async {
  t.view.physicalSize = const Size(780, 2800);
  t.view.devicePixelRatio = 2;
  addTearDown(t.view.reset);
  store = PatientStore(conversations: ConversationsController(autoReply: reply));
  await t.pumpWidget(AppScope(store: store, child: const CcApp(title: 't', home: PatientShell())));
  await t.pumpAndSettle();
}

Future<void> goAccount(WidgetTester t, String item) async {
  await t.tap(find.text('حسابي').last);
  await t.pumpAndSettle();
  await t.tap(find.text(item));
  await t.pumpAndSettle();
}

void main() {
  group('منطق', () {
    test('الأدوية: الجرعات والالتزام وإعادة الصرف', () {
      final c = MedicationsController();
      expect(c.today.length, 4); // أملوديبين 1 + ميتفورمين 2 + فيتامين د 1
      expect(c.takenCount, 2);
      expect(c.adherence, 0.5);
      // ميتفورمين المسائي لم يحن بعد → قادمة، وفيتامين د 13:00 قادمة، لا فائتة
      expect(c.today.where((d) => d.state == DoseState.missed), isEmpty);
      c.toggleDose(3, 0);
      expect(c.takenCount, 3);
      c.toggleDose(3, 0);
      expect(c.takenCount, 2);
      expect(c.byId(2).endingSoon, isTrue);
      c.requestRefill(2);
      expect(c.byId(2).refill, RefillState.requested);
    });
    test('الفواتير: المستحق والدفع', () {
      final c = InvoicesController();
      expect(c.unpaid.length, 2);
      expect(c.byId(1).due, 80);
      expect(c.totalDue, 180); // 80 + 100
      c.pay(1, 'Apple Pay');
      expect(c.byId(1).status, InvStatus.paid);
      expect(c.unpaid.length, 1);
    });
    test('الإشعارات والمحادثات', () {
      final s = PatientStore();
      expect(s.notifications.unreadCount, 3);
      s.notifications.markAllRead();
      expect(s.notifications.unreadCount, 0);
      expect(s.conversations.unreadTotal, 1);
      s.conversations.markRead(1);
      expect(s.conversations.unreadTotal, 0);
      final c = s.conversations.forDoctor(5, 'د. زياد النجار', 'العظام', 'x');
      expect(c.messages.length, 1);
      expect(s.conversations.forDoctor(5, 'د. زياد النجار', 'العظام', 'x').id, c.id); // نفسها
    });
  });

  testWidgets('الأدوية: تأكيد جرعة وتحديث الالتزام وطلب إعادة صرف', (t) async {
    await open(t);
    await goAccount(t, 'أدويتي');
    expect(find.text('50%'), findsOneWidget);
    expect(find.text('تناولت 2 من 4 جرعات'), findsOneWidget);
    await t.tap(find.byTooltip('تأكيد تناول فيتامين د 50,000'));
    await t.pumpAndSettle();
    expect(find.text('75%'), findsOneWidget);
    expect(find.text('تنتهي بعد 4 أيام'), findsOneWidget);
    await t.dragUntilVisible(find.text('اطلب إعادة صرف الآن'), find.byType(ListView).last, const Offset(0, -200));
    await t.ensureVisible(find.text('اطلب إعادة صرف الآن'));
    await t.pumpAndSettle();
    await t.tap(find.text('اطلب إعادة صرف الآن'));
    await t.pumpAndSettle();
    expect(find.text('أُرسل طلب إعادة الصرف وهو قيد المراجعة.'), findsOneWidget);
    await t.tap(find.text('المنتهية (1)'));
    await t.pumpAndSettle();
    expect(find.text('كريم هيدروكورتيزون 1%'), findsOneWidget);
    expect(find.text('جدول اليوم'), findsNothing);
  });

  testWidgets('الفواتير: تفاصيل ثم دفع ينهي المستحق', (t) async {
    await open(t);
    await goAccount(t, 'فواتيري');
    expect(find.text('المستحق عليك'), findsOneWidget);
    await t.tap(find.widgetWithText(ChoiceChip, 'غير مدفوعة'));
    await t.pumpAndSettle();
    expect(find.text('كشف قلب + تخطيط'), findsOneWidget);
    expect(find.text('تحاليل مخبرية'), findsOneWidget);
    expect(find.text('أشعة صدر'), findsNothing); // مغطاة بالكامل بالتأمين
    expect(find.text('كشف جلدية'), findsNothing);
    await t.tap(find.text('كشف قلب + تخطيط'));
    await t.pumpAndSettle();
    expect(find.text('320 ر.س'), findsOneWidget);
    await t.tap(find.text('ادفع 80 ر.س'));
    await t.pumpAndSettle();
    await t.tap(find.text('Apple Pay'));
    await t.pump();
    await t.tap(find.widgetWithText(FilledButton, 'ادفع 80 ر.س'));
    await t.pumpAndSettle();
    expect(store.invoices.byId(1).status, InvStatus.paid);
    expect(find.text('تم دفع 80 ر.س بنجاح'), findsOneWidget);
  });

  testWidgets('الرسائل: فتح محادثة وإرسال ورد تلقائي', (t) async {
    await open(t, reply: true);
    await t.tap(find.byTooltip('الرسائل، 1 غير مقروءة'));
    await t.pumpAndSettle();
    await t.tap(find.text('د. منى القاسم'));
    await t.pumpAndSettle();
    expect(store.conversations.unreadTotal, 0);
    await t.enterText(find.byType(TextField), 'شكرًا دكتورة');
    await t.pump();
    await t.tap(find.byTooltip('إرسال'));
    await t.pump();
    expect(find.text('يكتب الآن…'), findsOneWidget);
    await t.pump(const Duration(milliseconds: 1500));
    await t.pumpAndSettle();
    expect(find.text('وصلتني رسالتك، سأرد عليك قريبًا.'), findsOneWidget);
  });

  testWidgets('الإشعارات: عدّاد الجرس وفتح إشعار الأدوية وقراءة الكل', (t) async {
    await open(t);
    await t.tap(find.byTooltip('الإشعارات، 3 غير مقروءة'));
    await t.pumpAndSettle();
    await t.tap(find.text('حان موعد جرعتك'));
    await t.pumpAndSettle();
    expect(find.text('أدويتي'), findsOneWidget);
    expect(store.notifications.unreadCount, 2);
    await t.tap(find.byTooltip('رجوع'));
    await t.pumpAndSettle();
    await t.tap(find.text('قراءة الكل'));
    await t.pumpAndSettle();
    expect(store.notifications.unreadCount, 0);
    await t.tap(find.text('غير المقروءة (0)'));
    await t.pumpAndSettle();
    expect(find.text('قرأت كل الإشعارات.'), findsOneWidget);
  });

  testWidgets('إشعار النتائج ينقل لتبويب سجلي', (t) async {
    await open(t);
    await t.tap(find.byTooltip('الإشعارات، 3 غير مقروءة'));
    await t.pumpAndSettle();
    await t.tap(find.text('نتيجة تحليل جاهزة'));
    await t.pumpAndSettle();
    expect(store.tab.value, 3);
    expect(find.text('سجلي الطبي'), findsOneWidget);
  });

  testWidgets('أفراد العائلة: إضافة تظهر في الحجز، وحذف مع تراجع', (t) async {
    await open(t);
    await goAccount(t, 'أفراد العائلة');
    await t.tap(find.text('إضافة فرد'));
    await t.pumpAndSettle();
    await t.tap(find.text('إضافة'));
    await t.pump();
    expect(find.text('أدخل الاسم'), findsOneWidget);
    await t.enterText(find.byType(TextField).at(0), 'نورة');
    await t.tap(find.text('ابنتي'));
    await t.enterText(find.byType(TextField).at(1), '2021');
    await t.tap(find.text('إضافة'));
    await t.pumpAndSettle();
    expect(find.text('نورة'), findsOneWidget);
    expect(store.family.bookingNames, contains('نورة (ابنتي)'));
    await t.tap(find.byTooltip('حذف نورة'));
    await t.pumpAndSettle();
    expect(find.text('نورة'), findsNothing);
    await t.tap(find.text('تراجع'));
    await t.pumpAndSettle();
    expect(find.text('نورة'), findsOneWidget);
  });

  testWidgets('تعديل الملف الشخصي وتغيير الاسم في الرئيسية', (t) async {
    await open(t);
    await t.tap(find.text('حسابي').last);
    await t.pumpAndSettle();
    await t.tap(find.byTooltip('تعديل الملف الشخصي'));
    await t.pumpAndSettle();
    await t.enterText(find.byType(TextField).at(0), 'ريم');
    await t.enterText(find.byType(TextField).at(2), 'bad');
    await t.ensureVisible(find.text('حفظ التعديلات'));
    await t.tap(find.text('حفظ التعديلات'));
    await t.pump();
    expect(find.text('أدخل اسمك الثنائي على الأقل'), findsOneWidget);
    expect(find.text('صيغة البريد غير صحيحة'), findsOneWidget);
    await t.enterText(find.byType(TextField).at(0), 'ريم العلي الأحمد');
    await t.enterText(find.byType(TextField).at(2), 'r@x.com');
    await t.tap(find.text('حفظ التعديلات'));
    await t.pumpAndSettle();
    expect(store.profile.name, 'ريم العلي الأحمد');
    await t.tap(find.text('الرئيسية').last);
    await t.pumpAndSettle();
    expect(find.text('ريم العلي الأحمد'), findsOneWidget);
  });

  testWidgets('التأمين والإعدادات وتغيير كلمة المرور', (t) async {
    await open(t);
    await goAccount(t, 'التأمين الصحي');
    expect(find.text('التعاونية للتأمين'), findsOneWidget);
    expect(find.text('المتبقي 38000 ر.س'), findsOneWidget);
    await t.tap(find.byTooltip('رجوع'));
    await t.pumpAndSettle();
    await t.tap(find.text('الإعدادات'));
    await t.pumpAndSettle();
    expect(store.settings.offers, isFalse);
    await t.tap(find.text('العروض والتوعية الصحية'));
    await t.pumpAndSettle();
    expect(store.settings.offers, isTrue);
    await t.tap(find.text('تغيير كلمة المرور'));
    await t.pumpAndSettle();
    await t.tap(find.text('حفظ'));
    await t.pump();
    expect(find.text('أدخل كلمة المرور الحالية'), findsOneWidget);
  });
}
