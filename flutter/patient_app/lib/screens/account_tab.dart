import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';
import 'edit_profile_screen.dart';
import 'family_screen.dart';
import 'help_screen.dart';
import 'insurance_screen.dart';
import 'invoices_screen.dart';
import 'medications_screen.dart';
import 'messages_screen.dart';
import 'session.dart';
import 'settings_screen.dart';
import 'splash_gate.dart';

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
      await patientSession.signOut();
    }
    if (!context.mounted) return;
    // بعد الخروج يبدأ التطبيق من الصفر: Splash ثم Onboarding ثم الترحيب
    await patientOnboardingStore.reset();
    if (!context.mounted) return;
    Navigator.of(context).pushAndRemoveUntil(ccFadeRoute(const SplashGate()), (_) => false);
  }

  void _push(BuildContext c, Widget w) => Navigator.of(c).push(MaterialPageRoute<void>(builder: (_) => w));

  @override
  Widget build(BuildContext context) {
    final s = AppScope.storeOf(context);
    final p = s.profile;
    final due = s.invoices.totalDue;
    final unreadMsgs = s.conversations.unreadTotal;
    return ListView(
      padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s8),
      children: [
        Semantics(header: true, child: const Text('حسابي', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800))),
        const SizedBox(height: CcSpace.s4),
        CcCard(
          child: Row(children: [
            CcAvatar(name: p.name, asset: 'assets/images/avatars/patient-2.webp', size: 64),
            const SizedBox(width: CcSpace.s4),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(p.name, style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w800)), Text('${DateTime.now().year - p.birthYear} سنة · ${isolateLtr(p.phone)}', maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(color: CcColors.muted, fontSize: 13))])),
            IconButton(tooltip: 'تعديل الملف الشخصي', icon: const Icon(Icons.edit_outlined, color: CcColors.blue), onPressed: () => _push(context, const EditProfileScreen())),
          ]),
        ),
        const SizedBox(height: CcSpace.s5),
        _Item(icon: Icons.medication_outlined, title: 'أدويتي', subtitle: '${s.medications.active.length} أدوية نشطة', onTap: () => _push(context, const MedicationsScreen())),
        _Item(icon: Icons.receipt_long_outlined, title: 'فواتيري', subtitle: due > 0 ? 'مستحق $due ر.س' : 'لا مستحقات', onTap: () => _push(context, const InvoicesScreen())),
        _Item(icon: Icons.chat_bubble_outline_rounded, title: 'الرسائل', subtitle: unreadMsgs > 0 ? '$unreadMsgs غير مقروءة' : null, onTap: () => _push(context, const MessagesScreen())),
        _Item(icon: Icons.shield_outlined, title: 'التأمين الصحي', onTap: () => _push(context, const InsuranceScreen())),
        _Item(icon: Icons.family_restroom_rounded, title: 'أفراد العائلة', subtitle: '${s.family.all.length} أفراد', onTap: () => _push(context, const FamilyScreen())),
        _Item(icon: Icons.settings_outlined, title: 'الإعدادات', onTap: () => _push(context, const SettingsScreen())),
        _Item(icon: Icons.help_outline_rounded, title: 'المساعدة والدعم', onTap: () => _push(context, const HelpScreen())),
        const SizedBox(height: CcSpace.s5),
        CcButton(label: 'تسجيل الخروج', icon: Icons.logout_rounded, variant: CcButtonVariant.danger, onPressed: () => _logout(context)),
        const SizedBox(height: CcSpace.s4),
        const Center(child: Text('ClaudCare · الإصدار 1.0.0', style: TextStyle(fontSize: 12, color: CcColors.muted))),
      ],
    );
  }
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
