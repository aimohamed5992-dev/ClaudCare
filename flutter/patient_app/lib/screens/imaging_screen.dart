import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/records.dart';

class ImagingScreen extends StatelessWidget {
  const ImagingScreen({super.key, required this.studyId});
  final int studyId;

  @override
  Widget build(BuildContext context) {
    final s = imagingStudies.firstWhere((x) => x.id == studyId);
    return Scaffold(
      appBar: AppBar(title: const Text('نتيجة الأشعة'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: ListView(
        padding: const EdgeInsets.all(CcSpace.s5),
        children: [
          Semantics(header: true, child: Text(s.name, style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800))),
          const SizedBox(height: 4),
          Text('${s.date} · ${s.radiologist}', style: const TextStyle(color: CcColors.muted)),
          const SizedBox(height: CcSpace.s4),
          Semantics(
            label: 'صورة ${s.name}، يمكن تكبيرها بالقرص',
            image: true,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(CcRadius.lg),
              child: Container(
                color: Colors.black,
                height: 320,
                child: InteractiveViewer(minScale: 1, maxScale: 4, child: Image.asset(s.image, fit: BoxFit.contain)),
              ),
            ),
          ),
          const SizedBox(height: 6),
          const Center(child: Text('اقرص بإصبعين للتكبير', style: TextStyle(fontSize: 12, color: CcColors.muted))),
          const SizedBox(height: CcSpace.s5),
          const CcSectionHeader('التقرير'),
          const SizedBox(height: CcSpace.s2),
          Text(s.findings, style: const TextStyle(color: CcColors.slate, height: 1.8)),
          const SizedBox(height: CcSpace.s4),
          CcCard(color: CcColors.green50, child: Row(children: [const Icon(Icons.check_circle_outline_rounded, color: CcColors.greenText), const SizedBox(width: CcSpace.s3), Expanded(child: Text(s.impression, style: const TextStyle(fontWeight: FontWeight.w800, color: CcColors.greenText)))])),
          const SizedBox(height: CcSpace.s5),
          CcButton(label: 'تنزيل التقرير', icon: Icons.download_rounded, variant: CcButtonVariant.soft, onPressed: () => ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('التنزيل غير متاح في النسخة التجريبية')))),
        ],
      ),
    );
  }
}
