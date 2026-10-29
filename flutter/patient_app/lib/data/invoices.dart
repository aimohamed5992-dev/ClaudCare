import 'package:flutter/foundation.dart';
import 'remote.dart';

enum InvStatus {
  unpaid('غير مدفوعة'),
  paid('مدفوعة'),
  covered('مغطاة بالتأمين');

  const InvStatus(this.label);
  final String label;
}

class InvItem {
  const InvItem(this.name, this.amount);
  final String name;
  final int amount;
}

class Invoice {
  const Invoice({required this.id, required this.no, required this.title, required this.date, required this.items, required this.covered, this.paidWith});
  final int id;
  final String no, title, date;
  final List<InvItem> items;
  final int covered;
  final String? paidWith;

  int get total => items.fold(0, (s, i) => s + i.amount);
  int get due => total - covered;
  InvStatus get status => due == 0 ? InvStatus.covered : (paidWith != null ? InvStatus.paid : InvStatus.unpaid);

  Invoice withPayment(String method) => Invoice(id: id, no: no, title: title, date: date, items: items, covered: covered, paidWith: method);
}

class InvoicesController extends ChangeNotifier {
  InvoicesController([List<Invoice>? initial])
      : _items = List.of(initial ??
            const [
              Invoice(id: 1, no: 'INV-9120', title: 'كشف قلب + تخطيط', date: '12 أكتوبر 2026', covered: 320, items: [InvItem('كشف استشاري', 300), InvItem('تخطيط قلب', 100)]),
              Invoice(id: 2, no: 'INV-9088', title: 'تحاليل مخبرية', date: '12 أكتوبر 2026', covered: 180, items: [InvItem('صورة دم كاملة', 90), InvItem('سكر صائم', 40), InvItem('فيتامين د', 150)]),
              Invoice(id: 3, no: 'INV-8990', title: 'كشف جلدية', date: '14 سبتمبر 2026', covered: 224, paidWith: 'بطاقة (مدى/فيزا)', items: [InvItem('كشف أخصائي', 280)]),
              Invoice(id: 4, no: 'INV-8841', title: 'أشعة صدر', date: '20 أغسطس 2026', covered: 220, items: [InvItem('أشعة سينية للصدر', 220)]),
            ]);

  List<Invoice> _items;
  PatientRemote? remote;

  void replaceAll(List<Invoice> items) {
    _items = List.of(items);
    notifyListeners();
  }

  List<Invoice> get all => List.unmodifiable(_items);
  List<Invoice> get unpaid => _items.where((i) => i.status == InvStatus.unpaid).toList();
  int get totalDue => unpaid.fold(0, (s, i) => s + i.due);
  Invoice byId(int id) => _items.firstWhere((i) => i.id == id);

  void pay(int id, String method) {
    final i = _items.indexWhere((x) => x.id == id);
    _items[i] = _items[i].withPayment(method);
    notifyListeners();
    remote?.payInvoice(id, method);
  }
}
