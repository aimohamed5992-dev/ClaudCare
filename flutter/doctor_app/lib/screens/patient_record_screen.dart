import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/patients.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';
import 'consultation_screen.dart';

/// الملف الطبي: أربعة تبويبات + زر كشف جديد.
class PatientRecordScreen extends StatelessWidget {
  const PatientRecordScreen({super.key, required this.patientId});
  final int patientId;

  @override
  Widget build(BuildContext context) {
    AppScope.of(context);
    AppScope.storeOf(context).remote?.ensurePatientDetail(patientId);
    final p = AppScope.patientsOf(context).byId(patientId);
    return DefaultTabController(
      length: 4,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('الملف الطبي'),
          leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop()),
        ),
        body: Column(
          children: [
            _Header(p: p),
            const TabBar(
              isScrollable: true,
              tabAlignment: TabAlignment.start,
              labelColor: CcColors.blue,
              unselectedLabelColor: CcColors.muted,
              indicatorColor: CcColors.blue,
              labelStyle: TextStyle(fontFamily: kCcFont, fontWeight: FontWeight.w800),
              unselectedLabelStyle: TextStyle(fontFamily: kCcFont, fontWeight: FontWeight.w600),
              tabs: [Tab(text: 'نظرة عامة'), Tab(text: 'الزيارات'), Tab(text: 'النتائج'), Tab(text: 'الوصفات')],
            ),
            const Divider(height: 1),
            Expanded(
              child: TabBarView(children: [_Overview(p: p), _Visits(p: p), _Labs(p: p), _Prescriptions(p: p)]),
            ),
          ],
        ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s3),
            child: CcButton(
              label: 'بدء كشف جديد',
              icon: Icons.medical_services_outlined,
              onPressed: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => ConsultationScreen(patientId: p.id))),
            ),
          ),
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.p});
  final Patient p;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s3),
      child: Column(
        children: [
          Row(
            children: [
              CcAvatar(name: p.name, asset: p.avatar, size: 64),
              const SizedBox(width: CcSpace.s4),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Semantics(header: true, child: Text(p.name, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
                    Text('${p.age} سنة · ${p.gender}', style: const TextStyle(color: CcColors.muted)),
                    const SizedBox(height: 4),
                    Wrap(spacing: 6, children: [CcBadge('فصيلة ${isolateLtr(p.blood)}', tone: CcTone.gray), CcBadge('آخر زيارة ${p.lastVisit}')]),
                  ],
                ),
              ),
              IconButton.outlined(
                tooltip: 'اتصال بـ ${p.name}',
                icon: const Icon(Icons.call_outlined),
                style: IconButton.styleFrom(side: const BorderSide(color: CcColors.line)),
                onPressed: () => ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('الاتصال بالرقم ${p.phone} غير متاح في النسخة التجريبية'))),
              ),
            ],
          ),
          if (p.allergies.isNotEmpty) ...[
            const SizedBox(height: CcSpace.s3),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(CcSpace.s3),
              decoration: BoxDecoration(color: CcColors.red50, borderRadius: BorderRadius.circular(CcRadius.md)),
              child: Row(
                children: [
                  const Icon(Icons.warning_amber_rounded, color: CcColors.redText, size: 20),
                  const SizedBox(width: CcSpace.s2),
                  Expanded(child: Text('حساسية: ${p.allergies.join('، ')}', style: const TextStyle(color: CcColors.redText, fontWeight: FontWeight.w700))),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _Overview extends StatelessWidget {
  const _Overview({required this.p});
  final Patient p;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(CcSpace.s5),
      children: [
        const CcSectionHeader('آخر العلامات الحيوية'),
        const SizedBox(height: CcSpace.s3),
        if (p.vitals.isEmpty)
          const Text('لا توجد قياسات مسجلة', style: TextStyle(color: CcColors.muted))
        else
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: CcSpace.s3,
            crossAxisSpacing: CcSpace.s3,
            childAspectRatio: 2.4,
            children: [for (final e in p.vitals.entries) CcStatTile(icon: Icons.monitor_heart_outlined, value: e.value, label: e.key)],
          ),
        const SizedBox(height: CcSpace.s6),
        const CcSectionHeader('الحالات المزمنة'),
        const SizedBox(height: CcSpace.s3),
        if (p.conditions.isEmpty) const Text('لا توجد', style: TextStyle(color: CcColors.muted)) else Wrap(spacing: 8, runSpacing: 8, children: [for (final c in p.conditions) CcBadge(c)]),
        const SizedBox(height: CcSpace.s6),
        const CcSectionHeader('الأدوية الحالية'),
        const SizedBox(height: CcSpace.s3),
        if (p.activeMeds.isEmpty)
          const Text('لا توجد أدوية حالية', style: TextStyle(color: CcColors.muted))
        else
          for (final m in p.activeMeds)
            Padding(padding: const EdgeInsets.only(bottom: CcSpace.s2), child: CcCard(padding: const EdgeInsets.all(CcSpace.s3), child: Row(children: [const Icon(Icons.medication_outlined, color: CcColors.sky), const SizedBox(width: CcSpace.s3), Expanded(child: Text(m, style: const TextStyle(fontWeight: FontWeight.w700)))]))),
      ],
    );
  }
}

class _Visits extends StatelessWidget {
  const _Visits({required this.p});
  final Patient p;

  @override
  Widget build(BuildContext context) {
    if (p.visits.isEmpty) return const CcEmptyState(icon: Icons.history_rounded, title: 'لا زيارات سابقة');
    return ListView.builder(
      padding: const EdgeInsets.all(CcSpace.s5),
      itemCount: p.visits.length,
      itemBuilder: (_, i) {
        final v = p.visits[i];
        return Padding(
          padding: const EdgeInsets.only(bottom: CcSpace.s3),
          child: CcCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(children: [Expanded(child: Text(v.diagnosis, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15))), CcBadge(v.date, tone: CcTone.gray)]),
                const SizedBox(height: CcSpace.s2),
                Text(v.note, style: const TextStyle(color: CcColors.muted, height: 1.6)),
                const SizedBox(height: CcSpace.s2),
                Text(v.doctor, style: const TextStyle(fontSize: 12, color: CcColors.blue, fontWeight: FontWeight.w700)),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _Labs extends StatelessWidget {
  const _Labs({required this.p});
  final Patient p;

  @override
  Widget build(BuildContext context) {
    if (p.labs.isEmpty) return const CcEmptyState(image: 'assets/images/empty/empty-results.webp', title: 'لا نتائج بعد');
    return ListView.builder(
      padding: const EdgeInsets.all(CcSpace.s5),
      itemCount: p.labs.length,
      itemBuilder: (_, i) {
        final l = p.labs[i];
        return Padding(
          padding: const EdgeInsets.only(bottom: CcSpace.s3),
          child: Semantics(
            label: '${l.name}: ${l.value} ${l.unit}، ${l.flag.label}',
            excludeSemantics: true,
            child: CcCard(
              child: Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(l.name, style: const TextStyle(fontWeight: FontWeight.w800)),
                        Text('المعدل: ${l.range} · ${l.date}', style: const TextStyle(fontSize: 12, color: CcColors.muted)),
                      ],
                    ),
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text('${l.value} ${l.unit}', textDirection: TextDirection.ltr, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                      const SizedBox(height: 4),
                      CcBadge(l.flag.label, tone: l.flag.tone),
                    ],
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}

class _Prescriptions extends StatelessWidget {
  const _Prescriptions({required this.p});
  final Patient p;

  @override
  Widget build(BuildContext context) {
    if (p.prescriptions.isEmpty) return const CcEmptyState(icon: Icons.receipt_long_outlined, title: 'لا وصفات');
    return ListView.builder(
      padding: const EdgeInsets.all(CcSpace.s5),
      itemCount: p.prescriptions.length,
      itemBuilder: (_, i) {
        final r = p.prescriptions[i];
        return Padding(
          padding: const EdgeInsets.only(bottom: CcSpace.s3),
          child: CcCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(children: [Expanded(child: Text(r.no, textDirection: TextDirection.ltr, textAlign: TextAlign.start, style: const TextStyle(fontWeight: FontWeight.w800))), CcBadge(r.active ? 'نشطة' : 'منتهية', tone: r.active ? CcTone.green : CcTone.gray)]),
                Text(r.date, style: const TextStyle(fontSize: 12, color: CcColors.muted)),
                const SizedBox(height: CcSpace.s2),
                for (final m in r.meds) Padding(padding: const EdgeInsets.only(top: 4), child: Text('• $m')),
              ],
            ),
          ),
        );
      },
    );
  }
}
