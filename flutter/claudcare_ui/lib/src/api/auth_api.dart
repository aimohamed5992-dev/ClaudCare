import 'api_client.dart';

class AuthUser {
  AuthUser({required this.id, required this.fullName, required this.role, this.email, this.phone, this.employeeNo, this.avatarKey, this.birthYear});
  factory AuthUser.fromJson(Map<String, dynamic> j) => AuthUser(
      id: j['id'] as int, fullName: j['fullName'] as String, role: j['role'] as String, email: j['email'] as String?, phone: j['phone'] as String?, employeeNo: j['employeeNo'] as String?,
      avatarKey: j['avatarKey'] as String?, birthYear: j['birthYear'] as int?);
  final int id;
  final String fullName, role;
  final String? email, phone, employeeNo, avatarKey;
  final int? birthYear;
}

/// تحدي OTP: الوجهة المقنّعة والمهلة، ورمز التحدي لإكمال التحقق.
class OtpChallenge {
  OtpChallenge({required this.token, required this.destination, required this.resendAfterSeconds});
  factory OtpChallenge.fromJson(Map<String, dynamic> j) =>
      OtpChallenge(token: j['challengeToken'] as String, destination: j['destination'] as String, resendAfterSeconds: (j['resendAfterSeconds'] as num?)?.toInt() ?? 30);
  final String token, destination;
  final int resendAfterSeconds;
}

/// نتيجة الدخول: إما مستخدم بجلسة جاهزة أو تحدي OTP (الطاقم).
class LoginOutcome {
  const LoginOutcome.user(AuthUser this.user) : challenge = null;
  const LoginOutcome.otp(OtpChallenge this.challenge) : user = null;
  final AuthUser? user;
  final OtpChallenge? challenge;
}

/// مصادقة المستخدم عبر الـ API وتخزين الرموز.
class AuthApi {
  AuthApi(this.client);
  final ApiClient client;

  Future<AuthUser> _complete(Map<String, dynamic> auth) async {
    await client.tokens.save(access: auth['accessToken'] as String, refresh: auth['refreshToken'] as String);
    return AuthUser.fromJson(auth['user'] as Map<String, dynamic>);
  }

  Future<LoginOutcome> login(String identifier, String password) async {
    final j = await client.post('/api/auth/login', auth: false, body: {'identifier': identifier.trim(), 'password': password, 'deviceName': client.deviceName}) as Map<String, dynamic>;
    return j['status'] == 'authenticated' ? LoginOutcome.user(await _complete(j['auth'] as Map<String, dynamic>)) : LoginOutcome.otp(OtpChallenge.fromJson(j['challenge'] as Map<String, dynamic>));
  }

  /// يتحقق من رمز الدخول/التسجيل ويكمل الجلسة.
  Future<AuthUser> verifyLogin(String challengeToken, String code) async {
    final j = await client.post('/api/auth/otp/verify', auth: false, body: {'challengeToken': challengeToken, 'code': code, 'deviceName': client.deviceName}) as Map<String, dynamic>;
    return _complete(j['auth'] as Map<String, dynamic>);
  }

  /// يتحقق من رمز الاستعادة ويعيد رمز إعادة التعيين.
  Future<String> verifyReset(String challengeToken, String code) async {
    final j = await client.post('/api/auth/otp/verify', auth: false, body: {'challengeToken': challengeToken, 'code': code}) as Map<String, dynamic>;
    return j['resetToken'] as String;
  }

  Future<OtpChallenge> resend(String challengeToken) async =>
      OtpChallenge.fromJson(await client.post('/api/auth/otp/resend', auth: false, body: {'challengeToken': challengeToken}) as Map<String, dynamic>);

  Future<OtpChallenge> register({required String fullName, required String phone, required String nationalId, required String password, int? birthYear}) async =>
      OtpChallenge.fromJson(await client.post('/api/auth/register', auth: false, body: {'fullName': fullName, 'phone': phone, 'nationalId': nationalId, 'password': password, 'birthYear': birthYear}) as Map<String, dynamic>);

  Future<OtpChallenge> forgot(String identifier) async =>
      OtpChallenge.fromJson(await client.post('/api/auth/password/forgot', auth: false, body: {'identifier': identifier.trim()}) as Map<String, dynamic>);

  Future<void> reset(String resetToken, String newPassword) => client.post('/api/auth/password/reset', auth: false, body: {'resetToken': resetToken, 'newPassword': newPassword});

  Future<AuthUser> me() async => AuthUser.fromJson(await client.get('/api/auth/me') as Map<String, dynamic>);

  Future<void> changePassword(String current, String next) async {
    final j = await client.post('/api/auth/password/change', body: {'currentPassword': current, 'newPassword': next}) as Map<String, dynamic>;
    await _complete(j);
  }

  /// يُنهي الجلسة على الخادم (بأفضل جهد) ويمسح الرموز محليًا.
  Future<void> logout() async {
    final rt = await client.tokens.refresh;
    try {
      if (rt != null) await client.post('/api/auth/logout', auth: false, body: {'refreshToken': rt});
    } catch (_) {}
    await client.tokens.clear();
  }

  Future<bool> get hasSession async => (await client.tokens.refresh) != null;
}
