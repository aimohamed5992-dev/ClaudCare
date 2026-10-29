import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'login_screen.dart';

class WelcomeScreen extends StatelessWidget {
  const WelcomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return CcWelcome(
      image: 'assets/images/welcome/doctor-welcome.webp',
      title: 'أهلًا دكتور',
      subtitle: 'سجّل دخولك بحساب المستشفى لإدارة مواعيدك ومرضاك.',
      primaryLabel: 'تسجيل الدخول',
      onPrimary: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const LoginScreen())),
      footer: 'الحسابات تُنشأ من إدارة المستشفى',
    );
  }
}
