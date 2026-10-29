import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import 'splash_gate.dart';
import 'welcome_screen.dart';

const _pages = [
  CcOnboardingPage(
    image: 'assets/images/onboarding/patient-1-book.webp',
    title: 'احجز موعدك بسهولة',
    body: 'ابحث عن طبيبك، واختر الوقت المناسب، وأكّد حجزك في ثوانٍ.',
  ),
  CcOnboardingPage(
    image: 'assets/images/onboarding/patient-2-results.webp',
    title: 'نتائجك وأشعتك في مكان واحد',
    body: 'تابع نتائج التحاليل والأشعة والتقارير الطبية فور صدورها.',
  ),
  CcOnboardingPage(
    image: 'assets/images/onboarding/patient-3-care.webp',
    title: 'رعاية لك ولعائلتك',
    body: 'أدِر مواعيد أسرتك وأدويتها وفواتيرها وتأمينها من تطبيق واحد.',
  ),
];

class OnboardingScreen extends StatelessWidget {
  const OnboardingScreen({super.key});

  Future<void> _done(BuildContext context) async {
    await patientOnboardingStore.markSeen();
    if (!context.mounted) return;
    Navigator.of(context).pushReplacement(ccFadeRoute(const WelcomeScreen()));
  }

  @override
  Widget build(BuildContext context) => CcOnboarding(pages: _pages, onDone: () => _done(context));
}
