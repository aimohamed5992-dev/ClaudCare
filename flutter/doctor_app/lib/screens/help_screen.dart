import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';

const _faq = [
  ('كيف أعيد جدولة موعد؟', 'من تبويب المواعيد اختر الموعد ثم «إعادة جدولة» واختر اليوم والخانة المتاحة. يُبلَّغ المريض تلقائيًا.'),
  ('كيف أصف دواءً لمريض لديه حساسية؟', 'يظهر تحذير أحمر عند اختيار دواء من عائلة يتحسس منها المريض، ولا يمكن إضافته إلا بإقرارك بمراجعة الحساسية.'),
  ('أين أجد نتائج مرضاي؟', 'في الملف الطبي للمريض تبويب «النتائج»، وتصلك إشعارات عند جاهزية نتيجة أو ظهور قيمة حرجة.'),
  ('كيف أغيّر أوقات دوامي؟', 'من حسابي ثم أوقات العمل: فعّل أو عطّل الأيام وعدّل البداية والنهاية.'),
];

class HelpScreen extends StatelessWidget {
  const HelpScreen({super.key});

  void _soon(BuildContext c, String what) => ScaffoldMessenger.of(c).showSnackBar(SnackBar(content: Text('$what غير متاح في النسخة التجريبية')));

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('المساعدة والدعم'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: ListView(
        padding: const EdgeInsets.all(CcSpace.s5),
        children: [
          const CcSectionHeader('أسئلة شائعة'),
          const SizedBox(height: CcSpace.s3),
          for (final (q, a) in _faq)
            Padding(
              padding: const EdgeInsets.only(bottom: CcSpace.s2),
              child: CcCard(
                padding: EdgeInsets.zero,
                child: Theme(
                  data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
                  child: ExpansionTile(
                    title: Text(q, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                    childrenPadding: const EdgeInsets.fromLTRB(CcSpace.s4, 0, CcSpace.s4, CcSpace.s4),
                    expandedCrossAxisAlignment: CrossAxisAlignment.start,
                    children: [Text(a, style: const TextStyle(color: CcColors.muted, height: 1.7))],
                  ),
                ),
              ),
            ),
          const SizedBox(height: CcSpace.s5),
          const CcSectionHeader('تواصل معنا'),
          const SizedBox(height: CcSpace.s3),
          CcButton(label: 'الدعم الفني', icon: Icons.support_agent_rounded, variant: CcButtonVariant.soft, onPressed: () => _soon(context, 'الاتصال بالدعم')),
          const SizedBox(height: CcSpace.s3),
          CcButton(label: 'الإبلاغ عن مشكلة', icon: Icons.bug_report_outlined, variant: CcButtonVariant.ghost, onPressed: () => _soon(context, 'الإبلاغ')),
        ],
      ),
    );
  }
}
