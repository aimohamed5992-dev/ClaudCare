import 'package:shared_preferences/shared_preferences.dart';

/// جلسة دخول تجريبية (UI فقط): تحفظ أن المستخدم مسجّل وبياناته المعروضة.
class SessionStore {
  const SessionStore(this.key);
  final String key;

  Future<String?> get userName async {
    try {
      return (await SharedPreferences.getInstance()).getString(key);
    } catch (_) {
      return null;
    }
  }

  Future<void> signIn(String name) async {
    try {
      await (await SharedPreferences.getInstance()).setString(key, name);
    } catch (_) {}
  }

  Future<void> signOut() async {
    try {
      await (await SharedPreferences.getInstance()).remove(key);
    } catch (_) {}
  }
}
