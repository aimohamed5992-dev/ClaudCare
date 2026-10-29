import 'package:shared_preferences/shared_preferences.dart';

/// يتذكر أن المستخدم أنهى الـ onboarding حتى لا يظهر في كل تشغيل. المفتاح مستقل لكل تطبيق.
class OnboardingStore {
  const OnboardingStore(this.key);
  final String key;

  Future<bool> get seen async {
    try {
      return (await SharedPreferences.getInstance()).getBool(key) ?? false;
    } catch (_) {
      return false;
    }
  }

  Future<void> markSeen() async {
    try {
      await (await SharedPreferences.getInstance()).setBool(key, true);
    } catch (_) {}
  }

  Future<void> reset() async {
    try {
      await (await SharedPreferences.getInstance()).remove(key);
    } catch (_) {}
  }
}
