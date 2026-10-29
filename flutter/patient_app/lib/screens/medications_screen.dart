import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/medications.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';

class MedicationsScreen extends StatefulWidget {
  const MedicationsScreen({super.key});
  @override
  State<MedicationsScreen> createState() => _MedicationsScreenState();
}

class _MedicationsScreenState extends State<MedicationsScreen> {
  bool _ended = false;

  @override
  Widget build(BuildContext context) {
    final c = AppScope.storeOf(context).medications;
    final doses = c.today;

    return Scaffold(
      appBar: AppBar(title: const Text('أدويتي'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: ListView(
        padding: const EdgeInsets.all(CcSpace.s5),
        children: [
          if (!_ended) ...[
            CcCard(
              color: CcColors.blue50,
              child: Row(children: [
                SizedBox(
                  width: 56,
                  height: 56,
                  child: Stack(alignment: Alignment.center, children: [
                    CircularProgressIndicator(value: c.adherence, strokeWidth: 6, backgroundColor: Colors.white, color: CcColors.blue, semanticsLabel: 'الالتزام بجرعات اليوم', semanticsValue: '${(c.adherence * 100).round()}%'),
                    Text('${(c.adherence * 100).round()}%', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 13)),
                  ]),
                ),
                const SizedBox(width: CcSpace.s4),
                Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [const Text('جرعات اليوم', style: TextStyle(fontWeight: FontWeight.w800)), Text('تناولت ${c.takenCount} من ${doses.length} جرعات', style: const TextStyle(color: CcColors.muted))])),
              ]),
            ),
            const SizedBox(height: CcSpace.s5),
            const CcSectionHeader('جدول اليوم'),
            const SizedBox(height: CcSpace.s3),
            for (final d in doses) Padding(padding: const EdgeInsets.only(bottom: CcSpace.s2), child: _DoseTile(dose: d, onToggle: () => c.toggleDose(d.med.id, d.slot))),
            const SizedBox(height: CcSpace.s4),
          ],
          Wrap(spacing: 8, runSpacing: 8, children: [
            ChoiceChip(label: Text('النشطة (${c.active.length})'), selected: !_ended, onSelected: (_) => setState(() => _ended = false)),
            ChoiceChip(label: Text('المنتهية (${c.ended.length})'), selected: _ended, onSelected: (_) => setState(() => _ended = true)),
          ]),
          const SizedBox(height: CcSpace.s3),
          if (_ended && c.ended.isEmpty) const CcEmptyState(icon: Icons.medication_outlined, title: 'لا أدوية منتهية'),
          for (final m in _ended ? c.ended : c.active) Padding(padding: const EdgeInsets.only(bottom: CcSpace.s3), child: _MedCard(m: m)),
        ],
      ),
    );
  }
}

class _DoseTile extends StatelessWidget {
  const _DoseTile({required this.dose, required this.onToggle});
  final Dose dose;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    final (label, tone) = switch (dose.state) { DoseState.taken => ('تم', CcTone.green), DoseState.missed => ('فائتة', CcTone.red), DoseState.upcoming => ('قادمة', CcTone.blue) };
    return Semantics(
      container: true,
      label: '${dose.med.name}، ${fmtTime(dose.minutes)}، $label',
      child: CcCard(
        padding: const EdgeInsets.symmetric(horizontal: CcSpace.s3, vertical: CcSpace.s2),
        child: Row(children: [
          SizedBox(width: 62, child: Text(fmtTime(dose.minutes), style: const TextStyle(fontWeight: FontWeight.w800, color: CcColors.blue, fontSize: 13))),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(dose.med.name, style: const TextStyle(fontWeight: FontWeight.w700)), Text('${dose.med.dose}${dose.med.instructions.isEmpty ? '' : ' · ${dose.med.instructions}'}', style: const TextStyle(fontSize: 12, color: CcColors.muted))])),
          CcBadge(label, tone: tone),
          IconButton(
            tooltip: dose.state == DoseState.taken ? 'إلغاء تأكيد جرعة ${dose.med.name}' : 'تأكيد تناول ${dose.med.name}',
            onPressed: onToggle,
            icon: Icon(dose.state == DoseState.taken ? Icons.check_circle_rounded : Icons.radio_button_unchecked_rounded, color: dose.state == DoseState.taken ? CcColors.green : CcColors.muted),
          ),
        ]),
      ),
    );
  }
}

class _MedCard extends StatelessWidget {
  const _MedCard({required this.m});
  final Medication m;

  @override
  Widget build(BuildContext context) {
    final c = AppScope.storeOf(context).medications;
    return CcCard(
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Container(width: 44, height: 44, decoration: BoxDecoration(color: CcColors.blue50, borderRadius: BorderRadius.circular(CcRadius.md)), child: const Icon(Icons.medication_outlined, color: CcColors.blue)),
          const SizedBox(width: CcSpace.s3),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(m.name, style: const TextStyle(fontWeight: FontWeight.w800)), Text('${m.dose} · ${m.times.length == 1 ? 'مرة يوميًا' : '${m.times.length} مرات يوميًا'}', style: const TextStyle(fontSize: 12, color: CcColors.muted))])),
          if (m.active) CcBadge(m.endingSoon ? 'تنتهي بعد ${m.daysLeft} أيام' : 'متبقٍ ${m.daysLeft} يومًا', tone: m.endingSoon ? CcTone.orange : CcTone.gray) else const CcBadge('منتهٍ', tone: CcTone.gray),
        ]),
        const SizedBox(height: CcSpace.s2),
        Text('وصفها ${m.doctor}', style: const TextStyle(fontSize: 12, color: CcColors.muted)),
        if (m.active) ...[
          const Divider(height: CcSpace.s6),
          Row(children: [
            const Icon(Icons.notifications_active_outlined, size: 20, color: CcColors.muted),
            const SizedBox(width: CcSpace.s2),
            const Expanded(child: Text('تذكير بالجرعات', style: TextStyle(fontWeight: FontWeight.w700))),
            Switch(value: m.reminder, onChanged: (v) => c.setReminder(m.id, v), activeTrackColor: CcColors.blue),
          ]),
          if (m.refill == RefillState.requested)
            const CcNotice('أُرسل طلب إعادة الصرف وهو قيد المراجعة.', error: false)
          else
            CcButton(label: m.endingSoon ? 'اطلب إعادة صرف الآن' : 'طلب إعادة صرف', icon: Icons.refresh_rounded, variant: m.endingSoon ? CcButtonVariant.primary : CcButtonVariant.soft, onPressed: () {
              c.requestRefill(m.id);
              ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('أُرسل طلب إعادة صرف ${m.name}')));
            }),
        ],
      ]),
    );
  }
}
