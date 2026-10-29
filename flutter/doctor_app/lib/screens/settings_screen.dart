import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../state/app_scope.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    AppScope.of(context);
    final s = AppScope.storeOf(context).settings;
    Widget sw(String key, String title, String sub, bool v) => SwitchListTile(
          value: v,
          onChanged: (x) => s.set(key, x),
          title: Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
          subtitle: Text(sub, style: const TextStyle(fontSize: 12)),
          activeTrackColor: CcColors.blue,
          contentPadding: EdgeInsets.zero,
        );

    return Scaffold(
      appBar: AppBar(title: const Text('الإعدادات'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: ListView(
        padding: const EdgeInsets.all(CcSpace.s5),
        children: [
          const CcSectionHeader('الإشعارات'),
          sw('appointments', 'مواعيد جديدة', 'عند طلب أو تأكيد أو إلغاء موعد', s.newAppointments),
          sw('messages', 'الرسائل', 'عند وصول رسالة جديدة', s.messages),
          sw('labs', 'النتائج المخبرية', 'عند جاهزية نتيجة أو قيمة حرجة', s.labResults),
          sw('sound', 'الصوت', 'تنبيه صوتي للإشعارات', s.sound),
          const Divider(height: CcSpace.s8),
          const CcSectionHeader('الأمان'),
          sw('biometric', 'القفل بالبصمة', 'اطلب البصمة عند فتح التطبيق', s.biometric),
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('تغيير كلمة المرور', style: TextStyle(fontWeight: FontWeight.w700)),
            trailing: const Icon(Icons.chevron_left_rounded, color: CcColors.muted),
            onTap: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const CcChangePasswordScreen())),
          ),
          const Divider(height: CcSpace.s8),
          const CcSectionHeader('عام'),
          const ListTile(contentPadding: EdgeInsets.zero, title: Text('اللغة', style: TextStyle(fontWeight: FontWeight.w700)), trailing: Text('العربية', style: TextStyle(color: CcColors.muted))),
        ],
      ),
    );
  }
}
