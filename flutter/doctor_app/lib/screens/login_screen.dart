import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../data/remote.dart';
import '../shell/doctor_shell.dart';
import '../state/app_scope.dart';
import 'session.dart';

String? _validateId(String? v) {
  final s = (v ?? '').trim();
  if (s.isEmpty) return 'أدخل البريد الإلكتروني أو رقم الموظف';
  if (s.contains('@')) return ccEmail(s);
  return s.length >= 4 ? null : 'رقم الموظف 4 أحرف على الأقل';
}

class LoginScreen extends StatelessWidget {
  const LoginScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return CcLoginScreen(
      header: Image.asset('assets/images/logo.png', height: 56, semanticLabel: 'ClaudCare'),
      title: 'تسجيل دخول الطبيب',
      subtitle: 'استخدم بيانات حسابك في المستشفى.',
      identifierLabel: 'البريد الإلكتروني أو رقم الموظف',
      identifierHint: 'name@hospital.com',
      identifierIcon: Icons.badge_outlined,
      identifierKeyboard: TextInputType.emailAddress,
      validateIdentifier: _validateId,
      demoAccounts: kDebugMode
          ? const [
              CcDemoAccount('الطبيب', 'DR-1042', 'Demo@1234'),
              CcDemoAccount('بالبريد', 'mona@hospital.com', 'Demo@1234'),
            ]
          : const [],
      footer: AppScope.storeOf(context).remote == null ? 'للتجربة: أي بيانات صحيحة الشكل، وكلمة المرور wrongpw لإظهار الخطأ' : 'للتجربة: DR-1042 وكلمة المرور Demo@1234 ثم الرمز 123456',
      onForgot: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => _forgot(context))),
      onLogin: (id, pw) async {
        final remote = AppScope.storeOf(context).remote;
        if (remote != null) return _remoteLogin(context, remote, id, pw);
        await Future<void>.delayed(const Duration(milliseconds: 900));
        if (pw == 'wrongpw') return 'بيانات الدخول غير صحيحة، تحقق وحاول مرة أخرى.';
        if (!context.mounted) return null;
        // التحقق بخطوتين للأطباء
        Navigator.of(context).push(MaterialPageRoute<void>(
          builder: (_) => CcOtpScreen(
            title: 'التحقق بخطوتين',
            destination: 'جوالك المسجّل 05•• ••• 67',
            demoHint: 'للتجربة: الرمز 123456',
            onResend: () async {},
            onVerify: (code) async {
              await Future<void>.delayed(const Duration(milliseconds: 600));
              return code == '123456' ? null : 'الرمز غير صحيح، حاول مرة أخرى';
            },
            onVerified: (ctx) async {
              await doctorSession.signIn(demoDoctorName);
              if (!ctx.mounted) return;
              Navigator.of(ctx).pushAndRemoveUntil(ccFadeRoute(const DoctorShell(name: demoDoctorName)), (_) => false);
            },
          ),
        ));
        return null;
      },
    );
  }
}

/// دخول الطبيب: كلمة المرور ثم رمز التحقق بخطوتين (يرسله الخادم للطاقم).
Future<String?> _remoteLogin(BuildContext context, DoctorRemote remote, String id, String pw) async {
  try {
    final out = await remote.auth.login(id, pw);
    Future<String?> finish(AuthUser user) async {
      if (user.role != 'Doctor') {
        await remote.auth.logout();
        return 'هذا التطبيق للأطباء فقط.';
      }
      remote.user = user;
      await remote.loadAll();
      return null;
    }

    if (out.user != null) {
      final err = await finish(out.user!);
      if (err != null) return err;
      if (!context.mounted) return null;
      Navigator.of(context).pushAndRemoveUntil(ccFadeRoute(DoctorShell(name: remote.user!.fullName)), (_) => false);
      return null;
    }
    var challenge = out.challenge!;
    if (!context.mounted) return null;
    Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => CcOtpScreen(
        title: 'التحقق بخطوتين',
        destination: challenge.destination,
        onResend: () async {
          try {
            challenge = await remote.auth.resend(challenge.token);
          } on ApiException catch (_) {}
        },
        onVerify: (code) async {
          try {
            final user = await remote.auth.verifyLogin(challenge.token, code);
            return await finish(user);
          } on ApiException catch (e) {
            return e.userMessage;
          }
        },
        onVerified: (ctx) {
          Navigator.of(ctx).pushAndRemoveUntil(ccFadeRoute(DoctorShell(name: remote.user!.fullName)), (_) => false);
        },
      ),
    ));
    return null;
  } on ApiException catch (e) {
    return e.userMessage;
  }
}

Widget _forgot(BuildContext context) {
  final remote = AppScope.storeOf(context).remote;
  OtpChallenge? challenge;
  String? resetToken;
  return CcForgotScreen(
    identifierLabel: 'البريد الإلكتروني أو رقم الموظف',
    identifierIcon: Icons.badge_outlined,
    validateIdentifier: _validateId,
    maskDestination: (_) => challenge?.destination ?? 'جوالك المسجّل',
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
