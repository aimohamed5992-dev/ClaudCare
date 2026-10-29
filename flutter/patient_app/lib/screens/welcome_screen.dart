import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'login_screen.dart';
import 'register_screen.dart';

class WelcomeScreen extends StatelessWidget {
  const WelcomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return CcWelcome(
      image: 'assets/images/welcome/patient-welcome.webp',
      title: 'أهلًا بك في ClaudCare',
      subtitle: 'سجّل دخولك أو أنشئ حسابًا جديدًا لتبدأ رحلتك الصحية.',
      primaryLabel: 'تسجيل الدخول',
      onPrimary: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const LoginScreen())),
      secondaryLabel: 'إنشاء حساب جديد',
      onSecondary: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const RegisterScreen())),
    );
  }
}
