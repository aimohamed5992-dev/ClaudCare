import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/patients.dart';
import '../state/app_scope.dart';
import 'patient_record_screen.dart';

enum _Filter {
  all('الكل'),
  chronic('أمراض مزمنة'),
  kids('أطفال'),
  seniors('كبار السن'),
  allergy('لديهم حساسية');

  const _Filter(this.label);
  final String label;

  bool test(Patient p) => switch (this) {
        _Filter.all => true,
        _Filter.chronic => p.chronic,
        _Filter.kids => p.age < 14,
        _Filter.seniors => p.age >= 65,
        _Filter.allergy => p.allergies.isNotEmpty,
      };
}

class PatientsTab extends StatefulWidget {
  const PatientsTab({super.key});
  @override
  State<PatientsTab> createState() => _PatientsTabState();
}

class _PatientsTabState extends State<PatientsTab> {
  final _q = TextEditingController();
  _Filter _filter = _Filter.all;

  @override
  void dispose() {
    _q.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = AppScope.patientsOf(context);
    AppScope.of(context); // اشتراك في التحديثات
    final items = c.search(_q.text).where(_filter.test).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s3),
          child: Row(
            children: [
              Expanded(child: Semantics(header: true, child: const Text('المرضى', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800)))),
              CcBadge('${c.all.length} مريضًا'),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: CcSpace.s5),
          child: TextField(
            controller: _q,
            onChanged: (_) => setState(() {}),
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: 'ابحث بالاسم أو المرض أو الجوال',
              prefixIcon: const Icon(Icons.search_rounded, color: CcColors.muted),
              suffixIcon: _q.text.isEmpty ? null : IconButton(tooltip: 'مسح البحث', icon: const Icon(Icons.close_rounded), onPressed: () => setState(_q.clear)),
            ),
          ),
        ),
        const SizedBox(height: CcSpace.s3),
        SizedBox(
          height: 44,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: CcSpace.s5),
            itemCount: _Filter.values.length,
            separatorBuilder: (_, __) => const SizedBox(width: 8),
            itemBuilder: (_, i) => ChoiceChip(label: Text(_Filter.values[i].label), selected: _filter == _Filter.values[i], onSelected: (_) => setState(() => _filter = _Filter.values[i])),
          ),
        ),
        const SizedBox(height: CcSpace.s2),
        Expanded(
          child: items.isEmpty
              ? const CcEmptyState(image: 'assets/images/empty/empty-patients.webp', title: 'لا نتائج', text: 'جرّب كلمة بحث أخرى أو غيّر التصفية.')
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s8),
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: CcSpace.s3),
                  itemBuilder: (_, i) => _PatientCard(p: items[i]),
                ),
        ),
      ],
    );
  }
}

class _PatientCard extends StatelessWidget {
  const _PatientCard({required this.p});
  final Patient p;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: '${p.name}، ${p.age} سنة، ${p.mainCondition}${p.allergies.isEmpty ? '' : '، لديه حساسية'}',
      excludeSemantics: true,
      child: CcCard(
        onTap: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => PatientRecordScreen(patientId: p.id))),
        padding: const EdgeInsets.all(CcSpace.s3),
        child: Row(
          children: [
            CcAvatar(name: p.name, asset: p.avatar, size: 52),
            const SizedBox(width: CcSpace.s3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(p.name, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
                  Text('${p.age} سنة · ${p.gender}', style: const TextStyle(fontSize: 13, color: CcColors.muted)),
                  const SizedBox(height: 2),
                  Text(p.mainCondition, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13, color: CcColors.blue, fontWeight: FontWeight.w600)),
                ],
              ),
            ),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                CcBadge(p.blood, tone: CcTone.gray),
                if (p.allergies.isNotEmpty) const Padding(padding: EdgeInsets.only(top: 6), child: Icon(Icons.warning_amber_rounded, size: 20, color: CcColors.redText)),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
