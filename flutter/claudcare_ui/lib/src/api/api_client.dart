import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'token_store.dart';

/// خطأ من الخادم (ProblemDetails) أو من الشبكة (status = 0).
class ApiException implements Exception {
  ApiException(this.status, this.code, this.message, {this.errors = const {}, this.data = const {}});
  final int status;
  final String code, message;

  /// أخطاء الحقول (validation_failed): اسم الحقل ← رسائله.
  final Map<String, List<String>> errors;

  /// بقية حقول الخطأ (مثل conflicts أو missing).
  final Map<String, dynamic> data;

  bool get isNetwork => status == 0;
  bool get isUnauthorized => status == 401;
  bool get isNotFound => status == 404;

  /// أول رسالة تفيد المستخدم: خطأ حقل إن وُجد وإلا العنوان.
  String get userMessage => errors.values.expand((e) => e).firstOrNull ?? message;

  @override
  String toString() => 'ApiException($status, $code, $message)';
}

/// عميل HTTP للـ API: رأس Bearer، تجديد الرمز مرة واحدة عند 401 (دون تكرار متزامن)، وأخطاء موحدة.
class ApiClient {
  ApiClient({required String baseUrl, TokenStore? tokens, http.Client? client, this.timeout = const Duration(seconds: 20), this.deviceName})
      : baseUrl = baseUrl.endsWith('/') ? baseUrl.substring(0, baseUrl.length - 1) : baseUrl,
        tokens = tokens ?? MemoryTokenStore(),
        _http = client ?? http.Client();

  final String baseUrl;
  final TokenStore tokens;
  final Duration timeout;
  final String? deviceName;
  final http.Client _http;

  /// تُستدعى عند فشل تجديد الجلسة (انتهت أو أُلغيت) ليعيد التطبيق المستخدم لشاشة الدخول.
  void Function()? onSessionExpired;

  Future<bool>? _refreshing;

  Future<dynamic> get(String path, {Map<String, dynamic>? query}) => _send('GET', path, query: query);
  Future<dynamic> post(String path, {Object? body, bool auth = true}) => _send('POST', path, body: body, auth: auth);
  Future<dynamic> put(String path, {Object? body}) => _send('PUT', path, body: body);
  Future<dynamic> patch(String path, {Object? body}) => _send('PATCH', path, body: body);
  Future<dynamic> delete(String path) => _send('DELETE', path);

  Uri _uri(String path, Map<String, dynamic>? query) {
    final q = <String, String>{
      for (final e in (query ?? {}).entries)
        if (e.value != null) e.key: e.value.toString(),
    };
    return Uri.parse('$baseUrl$path').replace(queryParameters: q.isEmpty ? null : q);
  }

  Future<dynamic> _send(String method, String path, {Map<String, dynamic>? query, Object? body, bool auth = true, bool retry = true}) async {
    final headers = <String, String>{'accept': 'application/json', if (body != null) 'content-type': 'application/json', if (deviceName != null) 'x-device-name': deviceName!};
    if (auth) {
      final t = await tokens.access;
      if (t != null) headers['authorization'] = 'Bearer $t';
    }
    http.Response res;
    try {
      final req = http.Request(method, _uri(path, query))..headers.addAll(headers);
      if (body != null) req.body = jsonEncode(body);
      res = await http.Response.fromStream(await _http.send(req).timeout(timeout));
    } on TimeoutException {
      throw ApiException(0, 'timeout', 'انتهت مهلة الاتصال بالخادم، حاول مجددًا.');
    } on ApiException {
      rethrow;
    } catch (_) {
      throw ApiException(0, 'network', 'تعذّر الاتصال بالخادم، تحقق من الشبكة.');
    }

    if (res.statusCode == 401 && auth && retry && await tokens.refresh != null) {
      if (await (_refreshing ??= _refresh().whenComplete(() => _refreshing = null))) {
        return _send(method, path, query: query, body: body, auth: auth, retry: false);
      }
      onSessionExpired?.call();
    }
    return _decode(res);
  }

  dynamic _decode(http.Response res) {
    dynamic json;
    if (res.bodyBytes.isNotEmpty) {
      try {
        json = jsonDecode(utf8.decode(res.bodyBytes));
      } catch (_) {
        json = null;
      }
    }
    if (res.statusCode >= 200 && res.statusCode < 300) return json;
    final map = json is Map<String, dynamic> ? json : const <String, dynamic>{};
    final errors = <String, List<String>>{};
    if (map['errors'] is Map) {
      (map['errors'] as Map).forEach((k, v) => errors[k.toString()] = [for (final m in (v as List)) m.toString()]);
    }
    final known = {'type', 'title', 'status', 'detail', 'instance', 'traceId', 'code', 'errors'};
    throw ApiException(
      res.statusCode,
      (map['code'] ?? 'http_${res.statusCode}').toString(),
      (map['title'] ?? map['detail'] ?? 'حدث خطأ غير متوقع (${res.statusCode})').toString(),
      errors: errors,
      data: {for (final e in map.entries) if (!known.contains(e.key)) e.key: e.value},
    );
  }

  /// يجدد الجلسة برمز التجديد. true عند النجاح؛ وعند الفشل تُمسح الرموز.
  Future<bool> _refresh() async {
    final rt = await tokens.refresh;
    if (rt == null) return false;
    try {
      final res = await _http
          .post(_uri('/api/auth/refresh', null), headers: {'content-type': 'application/json', if (deviceName != null) 'x-device-name': deviceName!}, body: jsonEncode({'refreshToken': rt, if (deviceName != null) 'deviceName': deviceName}))
          .timeout(timeout);
      if (res.statusCode == 200) {
        final j = jsonDecode(utf8.decode(res.bodyBytes)) as Map<String, dynamic>;
        await tokens.save(access: j['accessToken'] as String, refresh: j['refreshToken'] as String);
        return true;
      }
      if (res.statusCode == 401 || res.statusCode == 400) await tokens.clear();
    } catch (_) {
      // خلل شبكة مؤقت: لا نمسح الجلسة
    }
    return false;
  }

  void close() => _http.close();
}
