import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'data/remote.dart';
import 'screens/splash_gate.dart';
import 'state/app_scope.dart';

void main() {
  // الاتصال بالخادم: عنوانه من --dart-define=API_URL أو الافتراضي لكل منصة
  final client = ApiClient(baseUrl: defaultApiBaseUrl(), tokens: PrefsTokenStore('patient'), deviceName: 'ClaudCare Patient');
  runApp(PatientApp(store: PatientStore(remote: PatientRemote(client: client))));
}

class PatientApp extends StatefulWidget {
  const PatientApp({super.key, this.store});

  /// للاختبارات: حالة جاهزة بدل البيانات التجريبية.
  final PatientStore? store;

  @override
  State<PatientApp> createState() => _PatientAppState();
}

class _PatientAppState extends State<PatientApp> {
  late final PatientStore _store = widget.store ?? PatientStore();

  @override
  Widget build(BuildContext context) {
    // الحالة فوق MaterialApp لتصل إلى كل الشاشات المفتوحة بـ Navigator
    return AppScope(store: _store, child: const CcApp(title: 'ClaudCare', home: SplashGate()));
  }
}
