import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../screens/appointments_tab.dart';
import '../screens/home_tab.dart';
import '../screens/patients_tab.dart';
import '../screens/account_tab.dart';
import '../screens/messages_tab.dart';
import '../screens/login_screen.dart';
import '../state/app_scope.dart';

/// الهيكل الرئيسي: تنقل سفلي بخمس تبويبات. التبويبات تحتفظ بحالتها (IndexedStack).
class DoctorShell extends StatefulWidget {
  const DoctorShell({super.key, this.name = ''});
  final String name;

  @override
  State<DoctorShell> createState() => _DoctorShellState();
}

class _DoctorShellState extends State<DoctorShell> {
  ValueNotifier<int>? _tab;
  ValueNotifier<String?>? _error;
  ValueNotifier<int>? _expired;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final store = AppScope.storeOf(context);
    final t = store.tab;
    if (_tab != t) {
      _tab?.removeListener(_onTab);
      _tab = t..addListener(_onTab);
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

  void _onError() {
    final msg = _error?.value;
    if (msg == null || !mounted) return;
    _error!.value = null;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(msg)));
  }

  /// انتهت الجلسة أو أُلغيت: نعيد الطبيب لشاشة الدخول.
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

  int get _index => AppScope.storeOf(context).tab.value;

  void _go(int i) => AppScope.storeOf(context).tab.value = i;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: IndexedStack(
          index: _index,
          children: [
            HomeTab(onOpenAppointments: () => _go(1)),
            const AppointmentsTab(),
            const PatientsTab(),
            const MessagesTab(),
            const AccountTab(),
          ],
        ),
      ),
      bottomNavigationBar: Container(
        decoration: const BoxDecoration(border: Border(top: BorderSide(color: CcColors.line))),
        child: NavigationBar(
          selectedIndex: _index,
          onDestinationSelected: _go,
          destinations: const [
            NavigationDestination(icon: Icon(Icons.home_outlined), selectedIcon: Icon(Icons.home_rounded), label: 'الرئيسية'),
            NavigationDestination(icon: Icon(Icons.calendar_month_outlined), selectedIcon: Icon(Icons.calendar_month_rounded), label: 'المواعيد'),
            NavigationDestination(icon: Icon(Icons.people_alt_outlined), selectedIcon: Icon(Icons.people_alt_rounded), label: 'المرضى'),
            NavigationDestination(icon: _MsgIcon(selected: false), selectedIcon: _MsgIcon(selected: true), label: 'الرسائل'),
            NavigationDestination(icon: Icon(Icons.person_outline_rounded), selectedIcon: Icon(Icons.person_rounded), label: 'حسابي'),
          ],
        ),
      ),
    );
  }
}

/// أيقونة الرسائل مع عدّاد غير المقروء.
class _MsgIcon extends StatelessWidget {
  const _MsgIcon({required this.selected});
  final bool selected;

  @override
  Widget build(BuildContext context) {
    AppScope.of(context);
    final n = AppScope.storeOf(context).conversations.unreadTotal;
    final icon = Icon(selected ? Icons.chat_bubble_rounded : Icons.chat_bubble_outline_rounded);
    return n == 0 ? icon : Badge(label: Text('$n'), backgroundColor: CcColors.orange, child: icon);
  }
}
