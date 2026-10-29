import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../screens/appointments_tab.dart';
import '../screens/doctors_tab.dart';
import '../screens/home_tab.dart';
import '../screens/account_tab.dart';
import '../screens/records_tab.dart';
import '../screens/login_screen.dart';
import '../state/app_scope.dart';

/// الهيكل الرئيسي: تنقل سفلي بخمس تبويبات. التبويبات تحتفظ بحالتها.
class PatientShell extends StatefulWidget {
  const PatientShell({super.key, this.name});

  /// اسم المريض بعد الدخول (يُحفظ في الحالة).
  final String? name;

  @override
  State<PatientShell> createState() => _PatientShellState();
}

class _PatientShellState extends State<PatientShell> {
  ValueNotifier<int>? _tab;
  ValueNotifier<String?>? _error;
  ValueNotifier<int>? _expired;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final store = AppScope.storeOf(context);
    if (widget.name != null) store.name = widget.name!;
    if (_tab != store.tab) {
      _tab?.removeListener(_onTab);
      _tab = store.tab..addListener(_onTab);
    }
    final remote = store.remote;
    if (remote != null && _error != remote.lastError) {
      _error?.removeListener(_onError);
      _expired?.removeListener(_onExpired);
      _error = remote.lastError..addListener(_onError);
      _expired = remote.sessionExpired..addListener(_onExpired);
    }
  }

  void _onTab() {
    setState(() {});
    AppScope.storeOf(context).remote?.refresh();
  }

  /// رسالة فشل مزامنة (شبكة/رفض الخادم) لأي تعديل أُجري.
  void _onError() {
    final msg = _error?.value;
    if (msg == null || !mounted) return;
    _error!.value = null;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
  }

  /// انتهت الجلسة أو أُلغيت: نعيد المستخدم لشاشة الدخول.
  void _onExpired() {
    if (!mounted) return;
    AppScope.storeOf(context).reset();
    Navigator.of(context).pushAndRemoveUntil(ccFadeRoute(const LoginScreen()), (_) => false);
  }

  @override
  void dispose() {
    _tab?.removeListener(_onTab);
    _error?.removeListener(_onError);
    _expired?.removeListener(_onExpired);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final store = AppScope.storeOf(context);
    final i = store.tab.value;
    return Scaffold(
      body: SafeArea(
        child: IndexedStack(
          index: i,
          children: const [
            HomeTab(),
            DoctorsTab(),
            AppointmentsTab(),
            RecordsTab(),
            AccountTab(),
          ],
        ),
      ),
      bottomNavigationBar: Container(
        decoration: const BoxDecoration(border: Border(top: BorderSide(color: CcColors.line))),
        child: NavigationBar(
          selectedIndex: i,
          onDestinationSelected: (v) => store.tab.value = v,
          destinations: const [
            NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home_rounded), label: 'الرئيسية'),
            NavigationDestination(icon: Icon(Icons.medical_services_outlined), selectedIcon: Icon(Icons.medical_services_rounded), label: 'الأطباء'),
            NavigationDestination(icon: Icon(Icons.calendar_month_outlined), selectedIcon: Icon(Icons.calendar_month_rounded), label: 'مواعيدي'),
            NavigationDestination(icon: Icon(Icons.folder_open_outlined), selectedIcon: Icon(Icons.folder_rounded), label: 'سجلي'),
            NavigationDestination(icon: Icon(Icons.person_outline_rounded), selectedIcon: Icon(Icons.person_rounded), label: 'حسابي'),
          ],
        ),
      ),
    );
  }
}
