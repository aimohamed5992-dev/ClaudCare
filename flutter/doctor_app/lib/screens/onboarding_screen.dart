import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'splash_gate.dart';
import 'welcome_screen.dart';

const _pages = [
  CcOnboardingPage(
    image: 'assets/images/onboarding/doctor-1-schedule.webp',
    title: 'جدولك في جيبك',
    body: 'تابع مواعيدك اليومية، وأكّدها أو أعد جدولتها بنقرة واحدة من أي مكان.',
  ),
  CcOnboardingPage(
    image: 'assets/images/onboarding/doctor-2-records.webp',
    title: 'سجلات مرضاك ونتائجهم فورًا',
    body: 'اطّلع على الملف الطبي والتحاليل والأشعة قبل أن يدخل المريض إلى عيادتك.',
  ),
  CcOnboardingPage(
    image: 'assets/images/onboarding/doctor-3-consult.webp',
    title: 'وصفات واستشارات عن بُعد',
    body: 'اكتب الوصفة إلكترونيًا وأجرِ استشارات مرئية آمنة مع مرضاك.',
  ),
];

class OnboardingScreen extends StatelessWidget {
  const OnboardingScreen({super.key});

  Future<void> _done(BuildContext context) async {
    await doctorOnboardingStore.markSeen();
    if (!context.mounted) return;
    Navigator.of(context).pushReplacement(ccFadeRoute(const WelcomeScreen()));
  }

  @override
  Widget build(BuildContext context) => CcOnboarding(pages: _pages, onDone: () => _done(context));
}
