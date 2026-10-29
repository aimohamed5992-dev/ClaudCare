import 'package:flutter/foundation.dart';
import 'remote.dart';

enum NotifType { appointment, result, message, system }

class NotifItem {
  NotifItem({required this.id, required this.type, required this.title, required this.text, required this.time, required this.group, this.unread = true, this.patientId, this.conversationId});
  final int id;
  final NotifType type;
  final String title, text, time, group;
  bool unread;
  final int? patientId, conversationId;
}

class NotificationsController extends ChangeNotifier {
  NotificationsController([List<NotifItem>? initial]) : _items = initial ?? _seed();
  List<NotifItem> _items;
  DoctorRemote? remote;

  void replaceAll(List<NotifItem> items) {
    _items = List.of(items);
    notifyListeners();
  }

  static List<NotifItem> _seed() => [
        NotifItem(id: 1, type: NotifType.result, title: 'نتيجة تحليل جاهزة', text: 'محمد علي – صورة دم كاملة، فيها قيم خارج المعدل.', time: 'قبل 20 دقيقة', group: 'اليوم', patientId: 1),
        NotifItem(id: 2, type: NotifType.appointment, title: 'وصل المريض', text: 'سارة أحمد وصلت لموعدها 09:30 وهي بانتظارك.', time: 'قبل 25 دقيقة', group: 'اليوم'),
        NotifItem(id: 3, type: NotifType.message, title: 'رسالة جديدة', text: 'نادين يوسف: ضغط المريض في غرفة 206 مرتفع.', time: 'قبل 30 دقيقة', group: 'اليوم', conversationId: 1),
        NotifItem(id: 4, type: NotifType.appointment, title: 'طلب موعد جديد', text: 'نورة سعد طلبت موعدًا بعد غد وينتظر تأكيدك.', time: 'قبل ساعتين', group: 'اليوم', unread: false),
        NotifItem(id: 5, type: NotifType.result, title: 'قيمة حرجة', text: 'عبدالله ناصر – كرياتينين 2.1 مجم/دل.', time: 'أمس 05:10 م', group: 'أمس', patientId: 4, unread: false),
        NotifItem(id: 6, type: NotifType.system, title: 'تحديث جدول الأسبوع', text: 'أضافت الإدارة عيادة مسائية يوم الأربعاء.', time: 'أمس 09:00 ص', group: 'أمس', unread: false),
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

  int? _removedAt;
  NotifItem? _removed;

  void remove(int id) {
    _removedAt = _items.indexWhere((n) => n.id == id);
    _removed = _items.removeAt(_removedAt!);
    notifyListeners();
    remote?.deleteNotification(_removed!.id);
  }

  void undoRemove() {
    if (_removed == null) return;
    _items.insert(_removedAt!.clamp(0, _items.length), _removed!);
    final id = _removed!.id;
    _removed = null;
    notifyListeners();
    remote?.restoreNotification(id);
  }
}
