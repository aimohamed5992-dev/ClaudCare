import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../utils/format.dart';
import '../data/records.dart';
import 'imaging_screen.dart';
import 'lab_report_screen.dart';

enum _Section { labs('التحاليل'), imaging('الأشعة'), visits('الزيارات');
  const _Section(this.label);
  final String label;
}

class RecordsTab extends StatefulWidget {
  const RecordsTab({super.key});
  @override
  State<RecordsTab> createState() => _RecordsTabState();
}

class _RecordsTabState extends State<RecordsTab> {
  _Section _s = _Section.labs;

  void _push(Widget w) => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => w));

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s8),
      children: [
        Semantics(header: true, child: const Text('سجلي الطبي', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800))),
        const SizedBox(height: CcSpace.s4),
        CcCard(
          color: CcColors.blue50,
          child: Row(children: [
            Expanded(child: _Fact('فصيلة الدم', isolateLtr(healthSummary.blood))),
            Expanded(child: _Fact('الطول', healthSummary.height)),
            Expanded(child: _Fact('الوزن', healthSummary.weight)),
          ]),
        ),
        const SizedBox(height: CcSpace.s3),
        Wrap(spacing: 8, runSpacing: 8, children: [
          const CcBadge('الحساسية: لا يوجد', tone: CcTone.green),
          for (final c in healthSummary.chronic) CcBadge(c, tone: CcTone.orange),
        ]),
        const SizedBox(height: CcSpace.s5),
        Wrap(spacing: 8, runSpacing: 8, children: [for (final s in _Section.values) ChoiceChip(label: Text(s.label), selected: _s == s, onSelected: (_) => setState(() => _s = s))]),
        const SizedBox(height: CcSpace.s4),
        switch (_s) {
          _Section.labs => Column(children: [for (final r in labReports) Padding(padding: const EdgeInsets.only(bottom: CcSpace.s3), child: _LabTile(r: r, onTap: () => r.ready ? _push(LabReportScreen(reportId: r.id)) : ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('النتيجة قيد التنفيذ، ستصلك عند جاهزيتها'))))) ]),
          _Section.imaging => Column(children: [for (final s in imagingStudies) Padding(padding: const EdgeInsets.only(bottom: CcSpace.s3), child: _ImagingTile(s: s, onTap: () => _push(ImagingScreen(studyId: s.id))))]),
          _Section.visits => Column(children: [for (final v in visitRecords) Padding(padding: const EdgeInsets.only(bottom: CcSpace.s3), child: _VisitTile(v: v))]),
        },
      ],
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact(this.label, this.value);
  final String label, value;
  @override
  Widget build(BuildContext context) => Semantics(label: '$label: $value', excludeSemantics: true, child: Column(children: [Text(value, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: CcColors.blue)), Text(label, style: const TextStyle(fontSize: 12, color: CcColors.muted))]));
}

class _LabTile extends StatelessWidget {
  const _LabTile({required this.r, required this.onTap});
  final LabReport r;
  final VoidCallback onTap;
  @override
  Widget build(BuildContext context) => Semantics(
        button: true,
        label: '${r.name}، ${r.date}، ${r.ready ? (r.abnormal == 0 ? 'كل القيم طبيعية' : '${r.abnormal} قيم خارج المعدل') : 'قيد التنفيذ'}',
        excludeSemantics: true,
        child: CcCard(
          onTap: onTap,
          padding: const EdgeInsets.all(CcSpace.s3),
          child: Row(children: [
            Container(width: 44, height: 44, decoration: BoxDecoration(color: CcColors.blue50, borderRadius: BorderRadius.circular(CcRadius.md)), child: const Icon(Icons.science_outlined, color: CcColors.blue)),
            const SizedBox(width: CcSpace.s3),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(r.name, style: const TextStyle(fontWeight: FontWeight.w800)), Text('${r.date} · ${r.doctor}', style: const TextStyle(fontSize: 12, color: CcColors.muted))])),
            if (!r.ready) const CcBadge('قيد التنفيذ', tone: CcTone.orange) else if (r.abnormal == 0) const CcBadge('طبيعية', tone: CcTone.green) else CcBadge('${r.abnormal} خارج المعدل', tone: CcTone.orange),
          ]),
        ),
      );
}

class _ImagingTile extends StatelessWidget {
  const _ImagingTile({required this.s, required this.onTap});
  final ImagingStudy s;
  final VoidCallback onTap;
  @override
  Widget build(BuildContext context) => Semantics(
        button: true,
        label: '${s.name}، ${s.date}',
        excludeSemantics: true,
        child: CcCard(
          onTap: onTap,
          padding: const EdgeInsets.all(CcSpace.s3),
          child: Row(children: [
            ClipRRect(borderRadius: BorderRadius.circular(CcRadius.md), child: Image.asset(s.image, width: 64, height: 64, fit: BoxFit.cover)),
            const SizedBox(width: CcSpace.s3),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(s.name, style: const TextStyle(fontWeight: FontWeight.w800)), Text(s.date, style: const TextStyle(fontSize: 12, color: CcColors.muted)), const SizedBox(height: 2), Text(s.impression, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12, color: CcColors.greenText, fontWeight: FontWeight.w600))])),
            const Icon(Icons.chevron_left_rounded, color: CcColors.muted),
          ]),
        ),
      );
}

class _VisitTile extends StatelessWidget {
  const _VisitTile({required this.v});
  final VisitRecord v;
  @override
  Widget build(BuildContext context) => CcCard(
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [Expanded(child: Text(v.diagnosis, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15))), CcBadge(v.specialty)]),
          const SizedBox(height: 2),
          Text('${v.doctor} · ${v.date}', style: const TextStyle(fontSize: 12, color: CcColors.muted)),
          const SizedBox(height: CcSpace.s2),
          Text(v.note, style: const TextStyle(color: CcColors.slate, height: 1.6)),
          for (final m in v.meds) Padding(padding: const EdgeInsets.only(top: 6), child: Row(children: [const Icon(Icons.medication_outlined, size: 18, color: CcColors.sky), const SizedBox(width: 6), Expanded(child: Text(m, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600)))])),
        ]),
      );
}
