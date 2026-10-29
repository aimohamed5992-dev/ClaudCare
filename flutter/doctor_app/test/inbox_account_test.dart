import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:doctor_app/data/messages.dart';
import 'package:doctor_app/data/profile.dart';
import 'package:doctor_app/shell/doctor_shell.dart';
import 'package:doctor_app/state/app_scope.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

late DoctorStore store;

Future<void> open(WidgetTester t, {bool reply = false}) async {
  t.view.physicalSize = const Size(780, 2400);
  t.view.devicePixelRatio = 2;
  addTearDown(t.view.reset);
  store = DoctorStore(conversations: ConversationsController(autoReply: reply));
  await t.pumpWidget(AppScope(store: store, child: const CcApp(title: 't', home: DoctorShell())));
  await t.pumpAndSettle();
}

void main() {
  group('المنطق', () {
    test('الرسائل: القراءة والإرسال وصعود المحادثة', () {
      final c = ConversationsController();
      expect(c.unreadTotal, 3);
      c.markRead(1);
      expect(c.unreadTotal, 1);
      c.send(4, '  شكرًا  ');
      expect(c.all.first.id, 4);
      expect(c.byId(4).messages.last.text, 'شكرًا');
      expect(c.byId(4).messages.last.fromMe, isTrue);
      c.send(4, '   '); // فارغة تُتجاهل
      expect(c.byId(4).messages.last.text, 'شكرًا');
      expect(c.search('ممرضة').single.id, 1);
    });

    test('أوقات العمل: التحقق من المدة', () {
      final h = WorkingHoursController();
      expect(h.setRange(0, 9 * 60, 9 * 60 + 30), isNotNull);
      expect(h.days[0].from, 8 * 60); // لم يتغير
      expect(h.setRange(0, 9 * 60, 17 * 60), isNull);
      expect(h.days[0].from, 9 * 60);
      expect(h.activeDays, 5);
      h.toggle(5, true);
      expect(h.activeDays, 6);
    });
  });

  testWidgets('الرسائل: عدّاد الشارة ثم فتح محادثة يقرؤها ثم الإرسال', (t) async {
    await open(t);
    expect(find.bySemanticsLabel(RegExp('3 غير مقروءة')), findsNothing); // الشارة نص لا دلالة
    expect(find.text('3'), findsWidgets); // شارة التنقل
    await t.tap(find.text('الرسائل').last);
    await t.pumpAndSettle();
    expect(find.text('3 غير مقروءة'), findsOneWidget);
    await t.tap(find.text('نادين يوسف'));
    await t.pumpAndSettle();
    expect(store.conversations.byId(1).unread, 0);
    expect(find.textContaining('150/95'), findsOneWidget);

    // زر الإرسال معطّل بدون نص
    expect(t.widget<IconButton>(find.widgetWithIcon(IconButton, Icons.send_rounded)).onPressed, isNull);
    await t.enterText(find.byType(TextField), 'أعطيه الدواء الاحتياطي');
    await t.pump();
    await t.tap(find.byTooltip('إرسال'));
    await t.pumpAndSettle();
    expect(find.text('أعطيه الدواء الاحتياطي'), findsOneWidget);
    expect(t.widget<TextField>(find.byType(TextField)).controller!.text, isEmpty);

    await t.tap(find.byTooltip('رجوع'));
    await t.pumpAndSettle();
    expect(find.text('أنت: أعطيه الدواء الاحتياطي'), findsOneWidget);
    expect(find.text('1 غير مقروءة'), findsOneWidget); // بقي المختبر فقط
  }, skip: false);

  testWidgets('الرد التلقائي يظهر بعد «يكتب الآن»', (t) async {
    await open(t, reply: true);
    await t.tap(find.text('الرسائل').last);
    await t.pumpAndSettle();
    await t.tap(find.text('د. عمر الشامي'));
    await t.pumpAndSettle();
    await t.enterText(find.byType(TextField), 'شكرًا دكتور');
    await t.pump();
    await t.tap(find.byTooltip('إرسال'));
    await t.pump();
    expect(find.text('يكتب الآن…'), findsOneWidget);
    await t.pump(const Duration(milliseconds: 1500));
    await t.pumpAndSettle();
    expect(find.text('يكتب الآن…'), findsNothing);
    expect(find.text('تمام دكتورة، سأتابع ذلك فورًا.'), findsOneWidget);
  });

  testWidgets('الإشعارات: فتح، تصفية، قراءة الكل، حذف مع تراجع', (t) async {
    await open(t);
    expect(find.byTooltip('الإشعارات، 3 غير مقروءة'), findsOneWidget);
    await t.tap(find.byTooltip('الإشعارات، 3 غير مقروءة'));
    await t.pumpAndSettle();
    expect(find.text('نتيجة تحليل جاهزة'), findsOneWidget);
    await t.tap(find.text('غير المقروءة (3)'));
    await t.pumpAndSettle();
    expect(find.text('طلب موعد جديد'), findsNothing); // مقروء
    await t.tap(find.text('نتيجة تحليل جاهزة')); // يفتح ملف المريض ويقرأ
    await t.pumpAndSettle();
    expect(find.text('الملف الطبي'), findsOneWidget);
    expect(store.notifications.unreadCount, 2);
    await t.tap(find.byTooltip('رجوع'));
    await t.pumpAndSettle();
    await t.tap(find.text('قراءة الكل'));
    await t.pumpAndSettle();
    expect(store.notifications.unreadCount, 0);
    expect(find.text('قرأت كل الإشعارات.'), findsOneWidget);

    await t.tap(find.text('الكل'));
    await t.pumpAndSettle();
    await t.drag(find.text('تحديث جدول الأسبوع'), const Offset(500, 0));
    await t.pumpAndSettle();
    expect(find.text('تحديث جدول الأسبوع'), findsNothing);
    await t.tap(find.text('تراجع'));
    await t.pumpAndSettle();
    expect(find.text('تحديث جدول الأسبوع'), findsOneWidget);
  });

  testWidgets('إشعار موعد ينقل لتبويب المواعيد', (t) async {
    await open(t);
    await t.tap(find.byTooltip('الإشعارات، 3 غير مقروءة'));
    await t.pumpAndSettle();
    await t.tap(find.text('وصل المريض'));
    await t.pumpAndSettle();
    expect(store.tab.value, 1);
    expect(find.text('المواعيد'), findsWidgets);
    expect(find.text('عبدالله ناصر'), findsOneWidget); // محتوى تبويب المواعيد
  });

  testWidgets('تعديل الملف الشخصي: تحقق ثم حفظ ينعكس على الرئيسية', (t) async {
    await open(t);
    await t.tap(find.text('حسابي'));
    await t.pumpAndSettle();
    await t.tap(find.text('تعديل الملف الشخصي'));
    await t.pumpAndSettle();
    await t.enterText(find.byType(TextField).at(0), 'منى');
    await t.enterText(find.byType(TextField).at(1), '123');
    await t.enterText(find.byType(TextField).at(2), 'bad');
    await t.ensureVisible(find.text('حفظ التعديلات'));
    await t.tap(find.text('حفظ التعديلات'));
    await t.pump();
    expect(find.text('أدخل اسمك الثنائي على الأقل'), findsOneWidget);
    expect(find.text('صيغة البريد غير صحيحة'), findsOneWidget);
    await t.enterText(find.byType(TextField).at(0), 'د. منى الحسن');
    await t.enterText(find.byType(TextField).at(1), '0500000000');
    await t.enterText(find.byType(TextField).at(2), 'mona@x.com');
    await t.tap(find.text('حفظ التعديلات'));
    await t.pumpAndSettle();
    expect(store.profile.name, 'د. منى الحسن');
    expect(find.text('تم حفظ الملف الشخصي'), findsOneWidget);
    await t.tap(find.text('الرئيسية').last);
    await t.pumpAndSettle();
    expect(find.text('د. منى الحسن'), findsOneWidget);
  });

  testWidgets('الإعدادات: مفاتيح الإشعارات وتغيير كلمة المرور', (t) async {
    await open(t);
    await t.tap(find.text('حسابي'));
    await t.pumpAndSettle();
    await t.tap(find.text('الإعدادات'));
    await t.pumpAndSettle();
    expect(store.settings.sound, isTrue);
    await t.tap(find.text('الصوت'));
    await t.pumpAndSettle();
    expect(store.settings.sound, isFalse);
    await t.tap(find.text('تغيير كلمة المرور'));
    await t.pumpAndSettle();
    await t.tap(find.text('حفظ'));
    await t.pump();
    expect(find.text('أدخل كلمة المرور الحالية'), findsOneWidget);
    await t.enterText(find.byType(TextField).at(0), 'wrongpw');
    await t.enterText(find.byType(TextField).at(1), 'abc12345');
    await t.enterText(find.byType(TextField).at(2), 'abc12345');
    await t.tap(find.text('حفظ'));
    await t.pumpAndSettle();
    expect(find.text('كلمة المرور الحالية غير صحيحة'), findsOneWidget);
    await t.enterText(find.byType(TextField).at(0), 'oldpass1');
    await t.tap(find.text('حفظ'));
    await t.pumpAndSettle();
    expect(find.text('تم تغيير كلمة المرور'), findsOneWidget);
  });

  testWidgets('أوقات العمل: تعطيل يوم وعرض عدد الأيام', (t) async {
    await open(t);
    await t.tap(find.text('حسابي'));
    await t.pumpAndSettle();
    expect(find.text('5 أيام في الأسبوع'), findsOneWidget);
    await t.tap(find.text('أوقات العمل'));
    await t.pumpAndSettle();
    expect(find.text('إجازة'), findsNWidgets(2));
    await t.tap(find.byType(Switch).first);
    await t.pumpAndSettle();
    expect(store.hours.days[0].on, isFalse);
    expect(find.text('إجازة'), findsNWidgets(3));
  });
}
