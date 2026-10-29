import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/patients.dart';

class Med {
  const Med({required this.drug, required this.frequency, required this.days, this.note = ''});
  final Drug drug;
  final String frequency;
  final int days;
  final String note;

  String get text => '${drug.name} – $frequency لمدة $days ${days <= 10 ? 'أيام' : 'يومًا'}${note.isEmpty ? '' : ' ($note)'}';
}

/// تحذيرات دواء لمريض: تعارض حساسية (يمنع إلا بإقرار) وتكرار مع الأدوية الحالية.
class MedCheck {
  const MedCheck({this.allergy, this.duplicate = false});
  final String? allergy;
  final bool duplicate;
}

MedCheck checkMed(Patient p, Drug d, {List<Med> inPrescription = const []}) {
  final hit = p.allergies.where((a) => d.group == a).toList();
  final dup = p.activeMeds.contains(d.name) || inPrescription.any((m) => m.drug.name == d.name);
  return MedCheck(allergy: hit.isEmpty ? null : hit.first, duplicate: dup);
}

/// ورقة إضافة دواء للوصفة. تعيد [Med] أو null عند الإلغاء.
Future<Med?> showMedicationSheet(BuildContext context, Patient patient, List<Med> current) {
  return showModalBottomSheet<Med>(
    context: context,
    isScrollControlled: true,
    builder: (sheet) {
      Drug? drug;
      String? freq;
      var days = 7;
      var acknowledged = false;
      final note = TextEditingController();
      return StatefulBuilder(
        builder: (_, setState) {
          final check = drug == null ? null : checkMed(patient, drug!, inPrescription: current);
          final blocked = check?.allergy != null && !acknowledged;
          final ready = drug != null && freq != null && !blocked;
          return SafeArea(
            child: Padding(
              padding: EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6 + MediaQuery.of(sheet).viewInsets.bottom),
              child: SingleChildScrollView(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Semantics(header: true, child: const Text('إضافة دواء', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
                    const SizedBox(height: CcSpace.s4),
                    const Text('الدواء', style: TextStyle(fontWeight: FontWeight.w700)),
                    const SizedBox(height: CcSpace.s2),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        for (final d in drugs)
                          ChoiceChip(
                            label: Text(d.name),
                            selected: drug == d,
                            onSelected: (_) => setState(() {
                              drug = d;
                              acknowledged = false;
                            }),
                          ),
                      ],
                    ),
                    if (check?.allergy != null) ...[
                      const SizedBox(height: CcSpace.s4),
                      CcNotice('تحذير: المريض لديه حساسية من ${check!.allergy}، وهذا الدواء من العائلة نفسها.'),
                      const SizedBox(height: CcSpace.s2),
                      CheckboxListTile(
                        value: acknowledged,
                        onChanged: (v) => setState(() => acknowledged = v ?? false),
                        controlAffinity: ListTileControlAffinity.leading,
                        contentPadding: EdgeInsets.zero,
                        activeColor: CcColors.redText,
                        title: const Text('راجعت الحساسية وأتحمل مسؤولية وصف الدواء', style: TextStyle(fontSize: 14)),
                      ),
                    ],
                    if (check?.duplicate == true) ...[
                      const SizedBox(height: CcSpace.s3),
                      Container(
                        padding: const EdgeInsets.all(CcSpace.s3),
                        decoration: BoxDecoration(color: CcColors.orange50, borderRadius: BorderRadius.circular(CcRadius.md)),
                        child: const Row(children: [
                          Icon(Icons.info_outline_rounded, color: CcColors.amberText, size: 20),
                          SizedBox(width: CcSpace.s2),
                          Expanded(child: Text('هذا الدواء موجود ضمن أدوية المريض الحالية أو الوصفة.', style: TextStyle(color: CcColors.amberText, fontWeight: FontWeight.w600, fontSize: 13))),
                        ]),
                      ),
                    ],
                    const SizedBox(height: CcSpace.s4),
                    const Text('عدد المرات', style: TextStyle(fontWeight: FontWeight.w700)),
                    const SizedBox(height: CcSpace.s2),
                    Wrap(spacing: 8, runSpacing: 8, children: [for (final f in frequencies) ChoiceChip(label: Text(f), selected: freq == f, onSelected: (_) => setState(() => freq = f))]),
                    const SizedBox(height: CcSpace.s4),
                    Row(
                      children: [
                        const Expanded(child: Text('مدة العلاج', style: TextStyle(fontWeight: FontWeight.w700))),
                        IconButton.outlined(tooltip: 'تقليل المدة', onPressed: days > 1 ? () => setState(() => days--) : null, icon: const Icon(Icons.remove_rounded)),
                        SizedBox(width: 72, child: Text('$days ${days <= 10 ? 'أيام' : 'يومًا'}', textAlign: TextAlign.center, style: const TextStyle(fontWeight: FontWeight.w800))),
                        IconButton.outlined(tooltip: 'زيادة المدة', onPressed: days < 90 ? () => setState(() => days++) : null, icon: const Icon(Icons.add_rounded)),
                      ],
                    ),
                    const SizedBox(height: CcSpace.s3),
                    CcTextField(label: 'تعليمات (اختياري)', controller: note, hint: 'مثال: بعد الأكل'),
                    const SizedBox(height: CcSpace.s5),
                    CcButton(
                      label: check?.allergy != null ? 'إضافة رغم التحذير' : 'إضافة للوصفة',
                      variant: check?.allergy != null ? CcButtonVariant.danger : CcButtonVariant.primary,
                      onPressed: ready ? () => Navigator.of(sheet).pop(Med(drug: drug!, frequency: freq!, days: days, note: note.text.trim())) : null,
                    ),
                    const SizedBox(height: CcSpace.s2),
                    CcButton(label: 'إلغاء', variant: CcButtonVariant.ghost, onPressed: () => Navigator.of(sheet).pop()),
                  ],
                ),
              ),
            ),
          );
        },
      );
    },
  );
}
