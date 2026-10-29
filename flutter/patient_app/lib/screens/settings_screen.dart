import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../state/app_scope.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final s = AppScope.storeOf(context).settings;
    Widget sw(String key, String title, String sub, bool v) => SwitchListTile(value: v, onChanged: (x) => s.set(key, x), title: Text(title, style: const TextStyle(fontWeight: FontWeight.w700)), subtitle: Text(sub, style: const TextStyle(fontSize: 12)), activeTrackColor: CcColors.blue, contentPadding: EdgeInsets.zero);

    return Scaffold(
      appBar: AppBar(title: const Text('الإعدادات'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: ListenableBuilder(
        listenable: s,
        builder: (_, __) => ListView(
          padding: const EdgeInsets.all(CcSpace.s5),
          children: [
            const CcSectionHeader('الإشعارات'),
            sw('appointments', 'تذكير المواعيد', 'قبل موعدك بيوم وبساعة', s.appointments),
            sw('medications', 'تذكير الأدوية', 'عند حلول موعد كل جرعة', s.medications),
            sw('results', 'النتائج الجديدة', 'عند جاهزية تحليل أو أشعة', s.results),
            sw('offers', 'العروض والتوعية الصحية', 'رسائل ترويجية ونصائح', s.offers),
            const Divider(height: CcSpace.s8),
            const CcSectionHeader('الأمان'),
            sw('biometric', 'القفل بالبصمة', 'اطلب البصمة عند فتح التطبيق', s.biometric),
            ListTile(contentPadding: EdgeInsets.zero, title: const Text('تغيير كلمة المرور', style: TextStyle(fontWeight: FontWeight.w700)), trailing: const Icon(Icons.chevron_left_rounded, color: CcColors.muted), onTap: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const CcChangePasswordScreen()))),
            const Divider(height: CcSpace.s8),
            const CcSectionHeader('عام'),
            const ListTile(contentPadding: EdgeInsets.zero, title: Text('اللغة', style: TextStyle(fontWeight: FontWeight.w700)), trailing: Text('العربية', style: TextStyle(color: CcColors.muted))),
          ],
        ),
      ),
    );
  }
}
