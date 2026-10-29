import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/records.dart';

class LabReportScreen extends StatelessWidget {
  const LabReportScreen({super.key, required this.reportId});
  final int reportId;

  @override
  Widget build(BuildContext context) {
    final r = labReports.firstWhere((x) => x.id == reportId);
    void soon(String what) => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$what غير متاح في النسخة التجريبية')));

    return Scaffold(
      appBar: AppBar(title: const Text('نتيجة التحليل'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: ListView(
        padding: const EdgeInsets.all(CcSpace.s5),
        children: [
          Semantics(header: true, child: Text(r.name, style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800))),
          const SizedBox(height: 4),
          Text('${r.date} · ${r.doctor}', style: const TextStyle(color: CcColors.muted)),
          const SizedBox(height: CcSpace.s4),
          CcNotice(r.abnormal == 0 ? 'كل القيم ضمن المعدل الطبيعي.' : '${r.abnormal} ${r.abnormal <= 2 ? 'قيمة' : 'قيم'} خارج المعدل. راجع طبيبك لتفسير النتيجة.', error: r.abnormal != 0),
          const SizedBox(height: CcSpace.s4),
          for (final i in r.items) Padding(padding: const EdgeInsets.only(bottom: CcSpace.s3), child: _Item(i: i)),
          const SizedBox(height: CcSpace.s2),
          Row(children: [
            Expanded(child: CcButton(label: 'تنزيل PDF', icon: Icons.download_rounded, variant: CcButtonVariant.soft, onPressed: () => soon('التنزيل'))),
            const SizedBox(width: CcSpace.s3),
            Expanded(child: CcButton(label: 'مشاركة', icon: Icons.ios_share_rounded, variant: CcButtonVariant.ghost, onPressed: () => soon('المشاركة'))),
          ]),
        ],
      ),
    );
  }
}

class _Item extends StatelessWidget {
  const _Item({required this.i});
  final LabItem i;

  @override
  Widget build(BuildContext context) {
    final f = i.flag;
    final color = switch (f) { Flag.normal => CcColors.green, Flag.critical => CcColors.redText, _ => CcColors.orange };
    return Semantics(
      label: '${i.name}: ${i.valueText} ${i.unit}، المعدل ${i.range}، ${f.label}',
      excludeSemantics: true,
      child: CcCard(
        child: Column(children: [
          Row(children: [
            Expanded(child: Text(i.name, style: const TextStyle(fontWeight: FontWeight.w800))),
            Text('${i.valueText} ${i.unit}', textDirection: TextDirection.ltr, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
          ]),
          const SizedBox(height: CcSpace.s2),
          // شريط المدى: المنطقة الطبيعية في الوسط ومؤشر القيمة
          LayoutBuilder(builder: (_, box) {
            final w = box.maxWidth;
            return SizedBox(
              height: 18,
              child: Stack(clipBehavior: Clip.none, children: [
                Positioned(top: 6, left: 0, right: 0, child: Container(height: 6, decoration: BoxDecoration(color: CcColors.line, borderRadius: BorderRadius.circular(3)))),
                Positioned(top: 6, left: w * 0.25, width: w * 0.5, child: Container(height: 6, decoration: BoxDecoration(color: CcColors.green50, border: Border.all(color: CcColors.green.withValues(alpha: 0.5)), borderRadius: BorderRadius.circular(3)))),
                Positioned(top: 1, left: (w * i.position - 8).clamp(0, w - 16), child: Container(width: 16, height: 16, decoration: BoxDecoration(color: color, shape: BoxShape.circle, border: Border.all(color: Colors.white, width: 2)))),
              ]),
            );
          }),
          const SizedBox(height: CcSpace.s2),
          Row(children: [Text('المعدل: ${i.range}', style: const TextStyle(fontSize: 12, color: CcColors.muted)), const Spacer(), CcBadge(f.label, tone: f.tone)]),
        ]),
      ),
    );
  }
}
