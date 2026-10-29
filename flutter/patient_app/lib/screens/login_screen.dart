import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../shell/patient_shell.dart';
import '../state/app_scope.dart';
import 'register_screen.dart';
import 'session.dart';

/// جوال (05…) أو هوية وطنية (10 أرقام).
String? _validateId(String? v) {
  if ((v ?? '').trim().isEmpty) return 'أدخل رقم الجوال أو الهوية';
  return (ccPhone(v) == null || ccNationalId(v) == null) ? null : 'أدخل جوالًا يبدأ بـ 05 أو هوية من 10 أرقام';
}

class LoginScreen extends StatelessWidget {
  const LoginScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return CcLoginScreen(
      header: Image.asset('assets/images/logo.png', height: 56, semanticLabel: 'ClaudCare'),
      title: 'أهلًا بعودتك',
      subtitle: 'سجّل دخولك لإدارة مواعيدك ونتائجك.',
      identifierLabel: 'رقم الجوال أو الهوية',
      identifierHint: '05XXXXXXXX',
      identifierIcon: Icons.phone_iphone_rounded,
      identifierKeyboard: TextInputType.phone,
      validateIdentifier: _validateId,
      showBiometric: true,
      demoAccounts: kDebugMode
          ? const [
              CcDemoAccount('المريض', '0551234500', 'Demo@1234'),
              CcDemoAccount('بالهوية', '1098765432', 'Demo@1234'),
            ]
          : const [],
      footer: AppScope.storeOf(context).remote == null ? 'للتجربة: أي بيانات صحيحة الشكل، وكلمة المرور wrongpw لإظهار الخطأ' : 'للتجربة: 0551234500 وكلمة المرور Demo@1234',
      onRegister: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const RegisterScreen())),
      onForgot: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => _forgot(context))),
      onLogin: (id, pw) async {
        final remote = AppScope.storeOf(context).remote;
        if (remote != null) {
          try {
            final out = await remote.auth.login(id, pw);
            final user = out.user;
            if (user == null) return 'هذا التطبيق للمرضى فقط.';
            if (user.role != 'Patient') {
              await remote.auth.logout();
              return 'هذا التطبيق للمرضى فقط.';
            }
            remote.user = user;
            await remote.loadAll();
            if (!context.mounted) return null;
            Navigator.of(context).pushAndRemoveUntil(ccFadeRoute(PatientShell(name: user.fullName)), (_) => false);
            return null;
          } on ApiException catch (e) {
            return e.userMessage;
          }
        }
        await Future<void>.delayed(const Duration(milliseconds: 900));
        if (pw == 'wrongpw') return 'بيانات الدخول غير صحيحة، تحقق وحاول مرة أخرى.';
        await patientSession.signIn(demoPatientName);
        if (!context.mounted) return null;
        Navigator.of(context).pushAndRemoveUntil(ccFadeRoute(const PatientShell(name: demoPatientName)), (_) => false);
        return null;
      },
    );
  }
}

/// استعادة كلمة المرور: على الخادم عند الاتصال، وإلا المحاكاة.
Widget _forgot(BuildContext context) {
  final remote = AppScope.storeOf(context).remote;
  OtpChallenge? challenge;
  String? resetToken;
  return CcForgotScreen(
    identifierLabel: 'رقم الجوال أو الهوية',
    identifierIcon: Icons.phone_iphone_rounded,
    identifierKeyboard: TextInputType.phone,
    validateIdentifier: _validateId,
    maskDestination: (id) => remote == null || challenge == null ? maskPhone(id) : challenge!.destination,
    onRequest: remote == null
        ? null
        : (id) async {
            try {
              challenge = await remote.auth.forgot(id);
              return null;
            } on ApiException catch (e) {
              return e.userMessage;
            }
          },
    onVerifyCode: remote == null
        ? null
        : (code) async {
            try {
              resetToken = await remote.auth.verifyReset(challenge!.token, code);
              return null;
            } on ApiException catch (e) {
              return e.userMessage;
            }
          },
    onResend: remote == null ? null : () async {
      try {
        challenge = await remote.auth.resend(challenge!.token);
      } on ApiException catch (_) {}
    },
    onReset: remote == null
        ? null
        : (pw) async {
            try {
              await remote.auth.reset(resetToken!, pw);
              return null;
            } on ApiException catch (e) {
              return e.userMessage;
            }
          },
  );
}
