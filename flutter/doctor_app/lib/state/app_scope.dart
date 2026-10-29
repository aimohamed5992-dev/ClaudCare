import 'package:flutter/widgets.dart';
import '../data/appointments.dart';
import '../data/messages.dart';
import '../data/notifications.dart';
import '../data/patients.dart';
import '../data/profile.dart';
import '../data/patients.dart' as pat;
import '../data/remote.dart';

/// كل حالة التطبيق في مكان واحد. الأجزاء غير الممرَّرة تُنشأ ببياناتها التجريبية.
class DoctorStore {
  DoctorStore({
    AppointmentsController? appointments,
    PatientsController? patients,
    ConversationsController? conversations,
    NotificationsController? notifications,
    DoctorProfile? profile,
    SettingsController? settings,
    WorkingHoursController? hours,
    this.remote,
  })  : appointments = appointments ?? AppointmentsController(remote == null ? null : const []),
        patients = patients ?? PatientsController(remote == null ? null : []),
        conversations = conversations ?? (remote == null ? ConversationsController(autoReply: true) : ConversationsController(initial: [])),
        notifications = notifications ?? NotificationsController(remote == null ? null : []),
        profile = profile ?? DoctorProfile(),
        settings = settings ?? SettingsController(),
        hours = hours ?? WorkingHoursController() {
    final r = remote;
    if (r != null) {
      this.appointments.remote = r;
      this.patients.remote = r;
      this.conversations.remote = r;
      this.notifications.remote = r;
      this.profile.remote = r;
      this.settings.remote = r;
      this.hours.remote = r;
      r.bind(this);
    }
  }

  /// غير null عند الاتصال بالخادم؛ null للبيانات التجريبية (الاختبارات).
  final DoctorRemote? remote;

  /// مسح بيانات المستخدم عند الخروج.
  void reset() {
    appointments.replaceAll(const []);
    patients.replaceAll([]);
    notifications.replaceAll(const []);
    conversations.replaceAll([]);
    pat.drugs = const [];
    tab.value = 0;
  }

  final AppointmentsController appointments;
  final PatientsController patients;
  final ConversationsController conversations;
  final NotificationsController notifications;
  final DoctorProfile profile;
  final SettingsController settings;
  final WorkingHoursController hours;

  /// التبويب المحدد في الهيكل؛ تغييره من أي شاشة ينقل المستخدم.
  final tab = ValueNotifier<int>(0);

  Listenable get listenable => Listenable.merge([appointments, patients, conversations, notifications, profile, settings, hours]);
}

/// يمرّر [DoctorStore] لكل الشاشات (ومنها المفتوحة بـ Navigator). يوضع فوق MaterialApp.
class AppScope extends InheritedNotifier<Listenable> {
  AppScope({super.key, required this.store, required super.child}) : super(notifier: store.listenable);

  final DoctorStore store;

  static DoctorStore storeOf(BuildContext c) => c.dependOnInheritedWidgetOfExactType<AppScope>()!.store;

  /// اختصارات؛ كلها تشترك في التحديثات.
  static AppointmentsController of(BuildContext c) => storeOf(c).appointments;
  static PatientsController patientsOf(BuildContext c) => storeOf(c).patients;
}
