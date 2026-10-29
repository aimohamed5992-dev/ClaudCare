import 'package:flutter/widgets.dart';

/// يحترم إعداد «تقليل الحركة» في النظام: المدة صفر عند تفعيله.
Duration ccMotion(BuildContext context, Duration d) => MediaQuery.maybeDisableAnimationsOf(context) == true ? Duration.zero : d;

/// انتقال تلاشٍ بين الشاشات الرئيسية (Splash ← Onboarding ← Welcome).
Route<T> ccFadeRoute<T>(Widget page) => PageRouteBuilder<T>(
      transitionDuration: const Duration(milliseconds: 450),
      reverseTransitionDuration: const Duration(milliseconds: 250),
      pageBuilder: (_, __, ___) => page,
      transitionsBuilder: (context, animation, _, child) {
        if (MediaQuery.maybeDisableAnimationsOf(context) == true) return child;
        return FadeTransition(opacity: CurvedAnimation(parent: animation, curve: Curves.easeOut), child: child);
      },
    );
