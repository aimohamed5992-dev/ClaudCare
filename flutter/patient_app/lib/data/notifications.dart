import 'package:flutter/foundation.dart';
import 'remote.dart';

enum NotifType { appointment, result, medication, invoice, message, system }

class NotifItem {
  NotifItem({required this.id, required this.type, required this.title, required this.text, required this.time, required this.group, this.unread = true});
  final int id;
  final NotifType type;
  final String title, text, time, group;
  bool unread;
}

class NotificationsController extends ChangeNotifier {
  NotificationsController([List<NotifItem>? initial]) : _items = initial ?? _seed();
  List<NotifItem> _items;
  PatientRemote? remote;

  void replaceAll(List<NotifItem> items) {
    _items = List.of(items);
    notifyListeners();
  }

  static List<NotifItem> _seed() => [
        NotifItem(id: 1, type: NotifType.result, title: 'نتيجة تحليل جاهزة', text: 'نتيجة «سكر صائم وفيتامين د» أصبحت متاحة في سجلك.', time: 'قبل 20 دقيقة', group: 'اليوم'),
        NotifItem(id: 2, type: NotifType.medication, title: 'حان موعد جرعتك', text: 'ميتفورمين 500 مجم – مع الأكل.', time: 'قبل 45 دقيقة', group: 'اليوم'),
        NotifItem(id: 3, type: NotifType.message, title: 'رسالة من د. منى القاسم', text: 'استمري على نفس الجرعة وسجّلي القياس يوميًا.', time: 'قبل ساعة', group: 'اليوم'),
        NotifItem(id: 4, type: NotifType.appointment, title: 'تذكير بموعدك', text: 'موعدك مع د. منى القاسم بعد يومين الساعة 10:30 ص.', time: 'أمس 06:00 م', group: 'أمس', unread: false),
        NotifItem(id: 5, type: NotifType.invoice, title: 'فاتورة بانتظار السداد', text: 'INV-9120 – كشف قلب + تخطيط.', time: 'أمس 09:10 ص', group: 'أمس', unread: false),
        NotifItem(id: 6, type: NotifType.system, title: 'مرحبًا بك في ClaudCare', text: 'يمكنك الآن إدارة مواعيدك ونتائجك من تطبيق واحد.', time: 'قبل أسبوع', group: 'سابقًا', unread: false),
      ];

  List<NotifItem> get all => List.unmodifiable(_items);
  int get unreadCount => _items.where((n) => n.unread).length;

  void markRead(int id) {
    final n = _items.firstWhere((n) => n.id == id);
    if (!n.unread) return;
    n.unread = false;
    notifyListeners();
    remote?.markNotificationRead(id);
  }

  void markAllRead() {
    for (final n in _items) {
      n.unread = false;
    }
    notifyListeners();
    remote?.markAllNotificationsRead();
  }

  int? _at;
  NotifItem? _removed;
  void remove(int id) {
    _at = _items.indexWhere((n) => n.id == id);
    _removed = _items.removeAt(_at!);
    notifyListeners();
    remote?.deleteNotification(_removed!.id);
  }

  void undoRemove() {
    if (_removed == null) return;
    _items.insert(_at!.clamp(0, _items.length), _removed!);
    final id = _removed!.id;
    _removed = null;
    notifyListeners();
    remote?.restoreNotification(id);
  }
}
