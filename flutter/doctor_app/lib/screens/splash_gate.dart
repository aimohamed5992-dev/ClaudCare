import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../shell/doctor_shell.dart';
import '../state/app_scope.dart';
import 'onboarding_screen.dart';
import 'session.dart';
import 'welcome_screen.dart';

/// للتجربة: `--dart-define=ALWAYS_ONBOARD=true` يعرض الـ onboarding في كل تشغيل.
const _alwaysOnboard = bool.fromEnvironment('ALWAYS_ONBOARD');
const doctorOnboardingStore = OnboardingStore('doctor.onboarding.seen');

class SplashGate extends StatelessWidget {
  const SplashGate({super.key});

  Future<void> _route(BuildContext context) async {
    final remote = AppScope.storeOf(context).remote;
    if (remote != null) {
      if (await remote.auth.hasSession) {
        try {
          remote.user = await remote.auth.me();
          await remote.loadAll();
          if (!context.mounted) return;
          Navigator.of(context).pushReplacement(ccFadeRoute(DoctorShell(name: remote.user!.fullName)));
          return;
        } on ApiException catch (e) {
          if (!e.isUnauthorized) {
            remote.lastError.value = e.message;
            if (!context.mounted) return;
            Navigator.of(context).pushReplacement(ccFadeRoute(DoctorShell(name: remote.user?.fullName ?? '')));
            return;
          }
          await remote.auth.logout();
        }
      }
      final seenRemote = !_alwaysOnboard && await doctorOnboardingStore.seen;
      if (!context.mounted) return;
      Navigator.of(context).pushReplacement(ccFadeRoute(seenRemote ? const WelcomeScreen() : const OnboardingScreen()));
      return;
    }
    final name = await doctorSession.userName;
    final seen = !_alwaysOnboard && await doctorOnboardingStore.seen;
    if (!context.mounted) return;
    if (name != null) {
      Navigator.of(context).pushReplacement(ccFadeRoute(DoctorShell(name: name)));
      return;
    }
    Navigator.of(context).pushReplacement(ccFadeRoute(seen ? const WelcomeScreen() : const OnboardingScreen()));
  }

  @override
  Widget build(BuildContext context) {
    return CcSplash(
      logoAsset: 'assets/images/logo.png',
      tagline: 'رعاية أذكى لكل مريض',
      badge: 'تطبيق الأطباء',
      onFinished: () => _route(context),
    );
  }
}
