import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';
import 'edit_profile_screen.dart';
import 'help_screen.dart';
import 'session.dart';
import 'settings_screen.dart';
import 'splash_gate.dart';
import 'working_hours_screen.dart';

class AccountTab extends StatelessWidget {
  const AccountTab({super.key});

  Future<void> _logout(BuildContext context) async {
    final remote = AppScope.storeOf(context).remote;
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('تسجيل الخروج؟'),
        content: const Text('ستحتاج لإدخال بياناتك مرة أخرى عند العودة.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(c).pop(false), child: const Text('إلغاء')),
          TextButton(onPressed: () => Navigator.of(c).pop(true), style: TextButton.styleFrom(foregroundColor: CcColors.redText), child: const Text('تسجيل الخروج')),
        ],
      ),
    );
    if (ok != true) return;
    if (remote != null) {
      await remote.logout();
    } else {
      await doctorSession.signOut();
    }
    if (!context.mounted) return;
    // بعد الخروج يبدأ التطبيق من الصفر: Splash ثم Onboarding ثم الترحيب
    await doctorOnboardingStore.reset();
    if (!context.mounted) return;
    Navigator.of(context).pushAndRemoveUntil(ccFadeRoute(const SplashGate()), (_) => false);
  }

  void _push(BuildContext context, Widget page) => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => page));

  @override
  Widget build(BuildContext context) {
    AppScope.of(context);
    final s = AppScope.storeOf(context);
    final p = s.profile;
    return ListView(
      padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s8),
      children: [
        Semantics(header: true, child: const Text('حسابي', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800))),
        const SizedBox(height: CcSpace.s4),
        CcCard(
          child: Column(
            children: [
              Row(children: [
                CcAvatar(name: p.name, asset: 'assets/images/avatars/doc-f1.webp', size: 68),
                const SizedBox(width: CcSpace.s4),
                Expanded(
                  child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                    Text(p.name, style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w800)),
                    Text(p.specialty, style: const TextStyle(color: CcColors.blue, fontWeight: FontWeight.w700)),
                    Text(p.hospital, style: const TextStyle(fontSize: 12, color: CcColors.muted)),
                  ]),
                ),
              ]),
              const SizedBox(height: CcSpace.s4),
              Row(children: [
                Expanded(child: _Stat(value: '${s.patients.all.length}', label: 'مريض')),
                Expanded(child: _Stat(value: '4.9', label: 'التقييم')),
                Expanded(child: _Stat(value: '12', label: 'سنة خبرة')),
              ]),
              const SizedBox(height: CcSpace.s3),
              Align(alignment: AlignmentDirectional.centerStart, child: CcBadge('الترخيص ${isolateLtr(p.license)}', tone: CcTone.gray)),
            ],
          ),
        ),
        const SizedBox(height: CcSpace.s5),
        _Item(icon: Icons.person_outline_rounded, title: 'تعديل الملف الشخصي', onTap: () => _push(context, const EditProfileScreen())),
        _Item(icon: Icons.schedule_rounded, title: 'أوقات العمل', subtitle: '${s.hours.activeDays} أيام في الأسبوع', onTap: () => _push(context, const WorkingHoursScreen())),
        _Item(icon: Icons.settings_outlined, title: 'الإعدادات', onTap: () => _push(context, const SettingsScreen())),
        _Item(icon: Icons.help_outline_rounded, title: 'المساعدة والدعم', onTap: () => _push(context, const HelpScreen())),
        const SizedBox(height: CcSpace.s5),
        CcButton(label: 'تسجيل الخروج', icon: Icons.logout_rounded, variant: CcButtonVariant.danger, onPressed: () => _logout(context)),
        const SizedBox(height: CcSpace.s4),
        const Center(child: Text('ClaudCare للأطباء · الإصدار 1.0.0', style: TextStyle(fontSize: 12, color: CcColors.muted))),
      ],
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.value, required this.label});
  final String value, label;
  @override
  Widget build(BuildContext context) => Semantics(
        label: '$label: $value',
        excludeSemantics: true,
        child: Column(children: [Text(value, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: CcColors.blue)), Text(label, style: const TextStyle(fontSize: 12, color: CcColors.muted))]),
      );
}

class _Item extends StatelessWidget {
  const _Item({required this.icon, required this.title, required this.onTap, this.subtitle});
  final IconData icon;
  final String title;
  final String? subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: CcSpace.s2),
        child: CcCard(
          onTap: onTap,
          padding: const EdgeInsets.symmetric(horizontal: CcSpace.s4, vertical: CcSpace.s3),
          child: Row(children: [
            Container(width: 40, height: 40, decoration: BoxDecoration(color: CcColors.blue50, borderRadius: BorderRadius.circular(CcRadius.md)), child: Icon(icon, color: CcColors.blue, size: 22)),
            const SizedBox(width: CcSpace.s3),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(title, style: const TextStyle(fontWeight: FontWeight.w700)), if (subtitle != null) Text(subtitle!, style: const TextStyle(fontSize: 12, color: CcColors.muted))])),
            const Icon(Icons.chevron_left_rounded, color: CcColors.muted),
          ]),
        ),
      );
}
