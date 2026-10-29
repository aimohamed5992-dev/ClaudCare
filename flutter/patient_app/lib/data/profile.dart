import 'package:flutter/foundation.dart';
import 'remote.dart';

class PatientProfile extends ChangeNotifier {
  String name = 'ريم العلي';
  String phone = '0551234500';
  String email = 'reem@example.com';
  final String nationalId = '1098765432';
  int birthYear = 1998;
  PatientRemote? remote;

  void load({required String name, String? phone, String? email, int? birthYear}) {
    this.name = name;
    this.phone = phone ?? '';
    this.email = email ?? '';
    this.birthYear = birthYear ?? this.birthYear;
    notifyListeners();
  }

  void update({String? name, String? phone, String? email, int? birthYear}) {
    this.name = name ?? this.name;
    this.phone = phone ?? this.phone;
    this.email = email ?? this.email;
    this.birthYear = birthYear ?? this.birthYear;
    notifyListeners();
    remote?.updateProfile();
  }
}

class FamilyMember {
  const FamilyMember(this.name, this.relation, this.birthYear, {this.id = 0});
  final String name, relation;
  final int birthYear, id;
}

class FamilyController extends ChangeNotifier {
  FamilyController({List<FamilyMember>? initial}) : _items = initial ?? [const FamilyMember('زياد', 'ابني', 2019), const FamilyMember('سلمى', 'والدتي', 1966)];
  List<FamilyMember> _items;
  PatientRemote? remote;

  void replaceAll(List<FamilyMember> items) {
    _items = List.of(items);
    notifyListeners();
  }
  List<FamilyMember> get all => List.unmodifiable(_items);

  /// أسماء الاختيار في الحجز: «أنا» ثم الأفراد.
  List<String> get bookingNames => ['أنا', for (final m in _items) '${m.name} (${m.relation})'];

  void add(FamilyMember m) {
    _items.add(m);
    notifyListeners();
    remote?.addFamily(m);
  }

  void removeAt(int i) {
    final removed = _items.removeAt(i);
    notifyListeners();
    remote?.removeFamily(removed);
  }
}

class SettingsController extends ChangeNotifier {
  bool appointments = true, medications = true, results = true, offers = false, biometric = false;
  PatientRemote? remote;

  void load(Map<String, bool> v) {
    appointments = v['appointments'] ?? appointments;
    medications = v['medications'] ?? medications;
    results = v['results'] ?? results;
    offers = v['offers'] ?? offers;
    biometric = v['biometric'] ?? biometric;
    notifyListeners();
  }

  void set(String key, bool v) {
    switch (key) {
      case 'appointments':
        appointments = v;
      case 'medications':
        medications = v;
      case 'results':
        results = v;
      case 'offers':
        offers = v;
      case 'biometric':
        biometric = v;
    }
    notifyListeners();
    remote?.setSetting(key, v);
  }
}

/// وثيقة التأمين (تجريبية، وتُستبدل ببيانات الخادم).
var insurance = (insurer: 'التعاونية للتأمين', tier: 'الذهبية', policy: 'POL-8841-229', expiry: '31 مارس 2027', copay: '20%', limit: 100000, used: 62000);
