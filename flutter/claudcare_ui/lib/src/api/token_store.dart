import 'package:shared_preferences/shared_preferences.dart';

/// تخزين رموز الجلسة. الافتراضي في الذاكرة (اختبارات)، و[PrefsTokenStore] للتطبيقات.
abstract class TokenStore {
  Future<String?> get access;
  Future<String?> get refresh;
  Future<void> save({required String access, required String refresh});
  Future<void> clear();
}

class MemoryTokenStore implements TokenStore {
  String? _a, _r;
  @override
  Future<String?> get access async => _a;
  @override
  Future<String?> get refresh async => _r;
  @override
  Future<void> save({required String access, required String refresh}) async {
    _a = access;
    _r = refresh;
  }

  @override
  Future<void> clear() async => _a = _r = null;
}

class PrefsTokenStore implements TokenStore {
  PrefsTokenStore(this.prefix);
  final String prefix;

  Future<SharedPreferences?> _prefs() async {
    try {
      return await SharedPreferences.getInstance();
    } catch (_) {
      return null;
    }
  }

  @override
  Future<String?> get access async => (await _prefs())?.getString('$prefix.access');
  @override
  Future<String?> get refresh async => (await _prefs())?.getString('$prefix.refresh');

  @override
  Future<void> save({required String access, required String refresh}) async {
    final p = await _prefs();
    await p?.setString('$prefix.access', access);
    await p?.setString('$prefix.refresh', refresh);
  }

  @override
  Future<void> clear() async {
    final p = await _prefs();
    await p?.remove('$prefix.access');
    await p?.remove('$prefix.refresh');
  }
}
