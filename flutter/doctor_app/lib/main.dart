import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'data/remote.dart';
import 'screens/splash_gate.dart';
import 'state/app_scope.dart';

void main() {
  // الاتصال بالخادم: عنوانه من --dart-define=API_URL أو الافتراضي لكل منصة
  final client = ApiClient(baseUrl: defaultApiBaseUrl(), tokens: PrefsTokenStore('doctor'), deviceName: 'ClaudCare Doctor');
  runApp(DoctorApp(store: DoctorStore(remote: DoctorRemote(client: client))));
}

class DoctorApp extends StatefulWidget {
  const DoctorApp({super.key, this.store});

  /// للاختبارات: حالة جاهزة بدل البيانات التجريبية.
  final DoctorStore? store;

  @override
  State<DoctorApp> createState() => _DoctorAppState();
}

class _DoctorAppState extends State<DoctorApp> {
  late final DoctorStore _store = widget.store ?? DoctorStore();

  @override
  Widget build(BuildContext context) {
    // الحالة فوق MaterialApp لتصل إلى كل الشاشات المفتوحة بـ Navigator
    return AppScope(
      store: _store,
      child: const CcApp(title: 'ClaudCare للأطباء', home: SplashGate()),
    );
  }
}
