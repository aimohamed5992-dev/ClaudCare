import 'package:flutter/widgets.dart';
import '../data/appointments.dart';
import '../data/doctors.dart';
import '../data/invoices.dart';
import '../data/medications.dart';
import '../data/messages.dart';
import '../data/notifications.dart';
import '../data/profile.dart';
import '../data/records.dart' as rec;
import '../data/doctors.dart' as docs;
import '../data/remote.dart';

/// كل حالة تطبيق المريض. الأجزاء غير الممرَّرة تُنشأ ببياناتها التجريبية.
class PatientStore {
  PatientStore({
    String? name,
    FavoritesController? favorites,
    PatientAppointmentsController? appointments,
    MedicationsController? medications,
    InvoicesController? invoices,
    NotificationsController? notifications,
    ConversationsController? conversations,
    PatientProfile? profile,
    FamilyController? family,
    SettingsController? settings,
    this.remote,
  })  : favorites = favorites ?? FavoritesController(initial: remote == null ? null : <int>{}),
        appointments = appointments ?? PatientAppointmentsController(remote == null ? null : const []),
        medications = medications ?? (remote == null ? MedicationsController() : MedicationsController(const [])),
        invoices = invoices ?? InvoicesController(remote == null ? null : const []),
        notifications = notifications ?? NotificationsController(remote == null ? null : []),
        conversations = conversations ?? (remote == null ? ConversationsController(autoReply: true) : ConversationsController(initial: [])),
        profile = profile ?? PatientProfile(),
        family = family ?? FamilyController(initial: remote == null ? null : []),
        settings = settings ?? SettingsController() {
    if (name != null) this.profile.name = name;
    final r = remote;
    if (r != null) {
      this.favorites.remote = r;
      this.appointments.remote = r;
      this.medications.remote = r;
      this.invoices.remote = r;
      this.notifications.remote = r;
      this.conversations.remote = r;
      this.profile.remote = r;
      this.family.remote = r;
      this.settings.remote = r;
      r.bind(this);
    }
  }

  /// غير null عند الاتصال بالخادم؛ null للبيانات التجريبية (الاختبارات).
  final PatientRemote? remote;

  /// تُرفع عند تغيّر قوائم عامة (الأطباء والسجلات والتأمين) كي تُعاد بناء الشاشات التي تقرؤها.
  final revision = ValueNotifier<int>(0);
  void doctorsChanged() => revision.value++;
  void recordsChanged() => revision.value++;

  /// مسح كل بيانات المستخدم (عند الخروج).
  void reset() {
    appointments.replaceAll(const []);
    invoices.replaceAll(const []);
    notifications.replaceAll(const []);
    medications.replaceAll(const [], {});
    conversations.replaceAll(const []);
    family.replaceAll(const []);
    favorites.replaceAll(const []);
    rec.labReports = const [];
    rec.imagingStudies = const [];
    rec.visitRecords = const [];
    docs.doctors = const [];
    tab.value = 0;
    revision.value++;
  }

  final FavoritesController favorites;
  final PatientAppointmentsController appointments;
  final MedicationsController medications;
  final InvoicesController invoices;
  final NotificationsController notifications;
  final ConversationsController conversations;
  final PatientProfile profile;
  final FamilyController family;
  final SettingsController settings;

  String get name => profile.name;
  set name(String v) => profile.name = v;

  /// التبويب المحدد في الهيكل، وتصفية تخصص تطلبها الرئيسية من تبويب الأطباء.
  final tab = ValueNotifier<int>(0);
  final specialtyFilter = ValueNotifier<String?>(null);
  final focusSearch = ValueNotifier<int>(0);

  Listenable get listenable => Listenable.merge([revision, favorites, appointments, medications, invoices, notifications, conversations, profile, family, settings]);
}

class AppScope extends InheritedNotifier<Listenable> {
  AppScope({super.key, required this.store, required super.child}) : super(notifier: store.listenable);

  final PatientStore store;

  static PatientStore storeOf(BuildContext c) => c.dependOnInheritedWidgetOfExactType<AppScope>()!.store;
}
