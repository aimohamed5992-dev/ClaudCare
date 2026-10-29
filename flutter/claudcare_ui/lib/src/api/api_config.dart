import 'dart:io' show Platform;

/// عنوان الـ API. يُمرَّر عند التشغيل: `--dart-define=API_URL=http://192.168.1.5:5080`.
/// الافتراضي: محاكي iOS على الجهاز نفسه `localhost`، ومحاكي أندرويد `10.0.2.2`.
String defaultApiBaseUrl() {
  const fromEnv = String.fromEnvironment('API_URL');
  if (fromEnv.isNotEmpty) return fromEnv;
  try {
    if (Platform.isAndroid) return 'http://10.0.2.2:5080';
  } catch (_) {}
  return 'http://localhost:5080';
}
