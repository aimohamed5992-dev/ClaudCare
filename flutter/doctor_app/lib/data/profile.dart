import 'package:flutter/foundation.dart';
import 'remote.dart';

class DoctorProfile extends ChangeNotifier {
  String name = 'د. منى القاسم';
  String specialty = 'استشارية أمراض القلب';
  String hospital = 'مستشفى ClaudCare – الفرع الرئيسي';
  String license = 'SCFHS-38877';
  String phone = '0551234567';
  String email = 'mona@hospital.com';
  String bio = 'استشارية أمراض القلب والأوعية الدموية بخبرة 12 عامًا.';
  DoctorRemote? remote;

  void load({required String name, String? specialty, String? hospital, String? license, String? phone, String? email, String? bio}) {
    this.name = name;
    this.specialty = specialty ?? this.specialty;
    this.hospital = hospital ?? this.hospital;
    this.license = license ?? this.license;
    this.phone = phone ?? '';
    this.email = email ?? '';
    this.bio = bio ?? '';
    notifyListeners();
  }

  static const specialties = ['استشارية أمراض القلب', 'طب الباطنة', 'طب الأطفال', 'الجراحة العامة', 'طب الأسرة'];

  void update({String? name, String? specialty, String? phone, String? email, String? bio}) {
    this.name = name ?? this.name;
    this.specialty = specialty ?? this.specialty;
    this.phone = phone ?? this.phone;
    this.email = email ?? this.email;
    this.bio = bio ?? this.bio;
    notifyListeners();
    remote?.updateProfile();
  }

  String get shortName => name.replaceFirst('د. ', '');
}

class SettingsController extends ChangeNotifier {
  bool newAppointments = true, messages = true, labResults = true, sound = true, biometric = false;
  DoctorRemote? remote;

  void load(Map<String, bool> v) {
    newAppointments = v['newAppointments'] ?? newAppointments;
    messages = v['messages'] ?? messages;
    labResults = v['labResults'] ?? labResults;
    sound = v['sound'] ?? sound;
    biometric = v['biometric'] ?? biometric;
    notifyListeners();
  }

  void set(String key, bool v) {
    switch (key) {
      case 'appointments':
        newAppointments = v;
      case 'messages':
        messages = v;
      case 'labs':
        labResults = v;
      case 'sound':
        sound = v;
      case 'biometric':
        biometric = v;
    }
    notifyListeners();
    remote?.setSetting(key, v);
  }
}

class WorkDay {
  WorkDay(this.name, {this.on = true, this.from = 8 * 60, this.to = 16 * 60});
  final String name;
  bool on;
  int from, to;
}

class WorkingHoursController extends ChangeNotifier {
  DoctorRemote? remote;

  final days = [
    WorkDay('الأحد'),
    WorkDay('الاثنين'),
    WorkDay('الثلاثاء'),
    WorkDay('الأربعاء', to: 20 * 60),
    WorkDay('الخميس'),
    WorkDay('الجمعة', on: false),
    WorkDay('السبت', on: false),
  ];

  /// نص الخطأ إن كان الوقت غير صالح (النهاية قبل البداية أو أقل من ساعة).
  static String? check(int from, int to) => to - from < 60 ? 'يجب أن تكون المدة ساعة على الأقل ونهاية الدوام بعد بدايته' : null;

  void toggle(int i, bool v) {
    days[i].on = v;
    notifyListeners();
    remote?.saveHours();
  }

  /// يستبدل الدوام بما جاء من الخادم (الأحد أولًا).
  void load(List<({bool on, int from, int to})> server) {
    for (var i = 0; i < days.length && i < server.length; i++) {
      days[i]
        ..on = server[i].on
        ..from = server[i].from
        ..to = server[i].to;
    }
    notifyListeners();
  }

  /// يعيد رسالة خطأ أو null عند النجاح.
  String? setRange(int i, int from, int to) {
    final e = check(from, to);
    if (e != null) return e;
    days[i].from = from;
    days[i].to = to;
    notifyListeners();
    remote?.saveHours();
    return null;
  }

  int get activeDays => days.where((d) => d.on).length;
}
