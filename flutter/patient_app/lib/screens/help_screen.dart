import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';

const _faq = [
  ('كيف أحجز موعدًا؟', 'من تبويب الأطباء اختر الطبيب ثم «احجز موعدًا»، واختر اليوم والوقت وأكّد الحجز.'),
  ('كيف ألغي أو أغيّر موعدي؟', 'من تبويب مواعيدي اختر الموعد ثم «إعادة جدولة» أو «إلغاء». الإلغاء مجاني قبل 24 ساعة.'),
  ('أين أجد نتائج تحاليلي وأشعتي؟', 'في تبويب سجلي، وتصلك إشعارات عند جاهزية أي نتيجة.'),
  ('كيف أضيف أحد أفراد عائلتي؟', 'من حسابي ثم «أفراد العائلة»، وبعدها يمكنك اختياره عند حجز موعد جديد.'),
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
                  child: ExpansionTile(title: Text(q, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)), childrenPadding: const EdgeInsets.fromLTRB(CcSpace.s4, 0, CcSpace.s4, CcSpace.s4), expandedCrossAxisAlignment: CrossAxisAlignment.start, children: [Text(a, style: const TextStyle(color: CcColors.muted, height: 1.7))]),
                ),
              ),
            ),
          const SizedBox(height: CcSpace.s5),
          const CcSectionHeader('تواصل معنا'),
          const SizedBox(height: CcSpace.s3),
          CcButton(label: 'اتصل بالمستشفى', icon: Icons.call_outlined, variant: CcButtonVariant.soft, onPressed: () => _soon(context, 'الاتصال')),
          const SizedBox(height: CcSpace.s3),
          CcButton(label: 'حالة طارئة؟ اتصل بالإسعاف 997', icon: Icons.emergency_outlined, variant: CcButtonVariant.danger, onPressed: () => _soon(context, 'الاتصال')),
        ],
      ),
    );
  }
}
