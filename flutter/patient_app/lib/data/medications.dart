import 'package:flutter/foundation.dart';
import 'clock.dart';
import 'remote.dart';

enum RefillState { none, requested }

class Medication {
  const Medication({
    required this.id,
    required this.name,
    required this.dose,
    required this.times,
    required this.daysLeft,
    required this.doctor,
    this.instructions = '',
    this.active = true,
    this.reminder = true,
    this.refill = RefillState.none,
  });

  final int id, daysLeft;
  final String name, dose, doctor, instructions;
  /// أوقات الجرعات اليومية بالدقائق من منتصف الليل.
  final List<int> times;
  final bool active, reminder;
  final RefillState refill;

  bool get endingSoon => active && daysLeft <= 5;

  Medication copyWith({bool? reminder, RefillState? refill}) => Medication(
        id: id,
        name: name,
        dose: dose,
        times: times,
        daysLeft: daysLeft,
        doctor: doctor,
        instructions: instructions,
        active: active,
        reminder: reminder ?? this.reminder,
        refill: refill ?? this.refill,
      );
}

enum DoseState { taken, missed, upcoming }

class Dose {
  const Dose(this.med, this.slot, this.minutes, this.state);
  final Medication med;
  final int slot, minutes;
  final DoseState state;
}

class MedicationsController extends ChangeNotifier {
  MedicationsController([List<Medication>? initial])
      : _items = List.of(initial ??
            const [
              Medication(id: 1, name: 'أملوديبين 5 مجم', dose: 'قرص واحد', times: [8 * 60], daysLeft: 20, doctor: 'د. منى القاسم', instructions: 'بعد الفطور'),
              Medication(id: 2, name: 'ميتفورمين 500 مجم', dose: 'قرص واحد', times: [8 * 60, 20 * 60], daysLeft: 4, doctor: 'د. منى القاسم', instructions: 'مع الأكل'),
              Medication(id: 3, name: 'فيتامين د 50,000', dose: 'كبسولة', times: [13 * 60], daysLeft: 40, doctor: 'د. منى القاسم', instructions: 'بعد الغداء'),
              Medication(id: 4, name: 'كريم هيدروكورتيزون 1%', dose: 'طبقة رقيقة', times: [9 * 60, 21 * 60], daysLeft: 0, doctor: 'د. هالة منصور', active: false, reminder: false),
            ]),
        _taken = {'1-0', '2-0'};

  List<Medication> _items;
  final Set<String> _taken;
  PatientRemote? remote;

  void replaceAll(List<Medication> items, Set<String> taken) {
    _items = List.of(items);
    _taken
      ..clear()
      ..addAll(taken);
    notifyListeners();
  }

  List<Medication> get active => _items.where((m) => m.active).toList();
  List<Medication> get ended => _items.where((m) => !m.active).toList();
  Medication byId(int id) => _items.firstWhere((m) => m.id == id);

  /// جرعات اليوم لكل الأدوية النشطة مرتبة زمنيًا.
  List<Dose> get today {
    final l = <Dose>[];
    for (final m in active) {
      for (var i = 0; i < m.times.length; i++) {
        final taken = _taken.contains('${m.id}-$i');
        l.add(Dose(m, i, m.times[i], taken ? DoseState.taken : (m.times[i] < nowMinutes ? DoseState.missed : DoseState.upcoming)));
      }
    }
    l.sort((a, b) => a.minutes.compareTo(b.minutes));
    return l;
  }

  int get takenCount => today.where((d) => d.state == DoseState.taken).length;
  double get adherence => today.isEmpty ? 1 : takenCount / today.length;

  void toggleDose(int medId, int slot) {
    final k = '$medId-$slot';
    final taking = !_taken.contains(k);
    taking ? _taken.add(k) : _taken.remove(k);
    notifyListeners();
    remote?.logDose(medId, slot, taking);
  }

  void _replace(int id, Medication Function(Medication) f) {
    final i = _items.indexWhere((m) => m.id == id);
    _items[i] = f(_items[i]);
    notifyListeners();
  }

  void setReminder(int id, bool v) {
    _replace(id, (m) => m.copyWith(reminder: v));
    remote?.setReminder(id, v);
  }

  void requestRefill(int id) {
    _replace(id, (m) => m.copyWith(refill: RefillState.requested));
    remote?.requestRefill(id);
  }
}
