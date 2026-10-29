import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/profile.dart';
import '../state/app_scope.dart';

const _relations = ['ابني', 'ابنتي', 'زوجي', 'زوجتي', 'والدي', 'والدتي'];

class FamilyScreen extends StatelessWidget {
  const FamilyScreen({super.key});

  Future<void> _add(BuildContext context) => showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        builder: (sheet) => _AddMemberSheet(onAdd: (m) => AppScope.storeOf(context).family.add(m)),
      );

  @override
  Widget build(BuildContext context) {
    final c = AppScope.storeOf(context).family;
    final items = c.all;
    return Scaffold(
      appBar: AppBar(title: const Text('أفراد العائلة'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      floatingActionButton: FloatingActionButton.extended(onPressed: () => _add(context), backgroundColor: CcColors.blue, foregroundColor: Colors.white, icon: const Icon(Icons.add_rounded), label: const Text('إضافة فرد', style: TextStyle(fontWeight: FontWeight.w800))),
      body: items.isEmpty
          ? const CcEmptyState(icon: Icons.family_restroom_rounded, title: 'لم تضف أحدًا بعد', text: 'أضف أفراد عائلتك لتحجز لهم المواعيد من حسابك.')
          : ListView.separated(
              padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, 96),
              itemCount: items.length,
              separatorBuilder: (_, __) => const SizedBox(height: CcSpace.s2),
              itemBuilder: (_, i) {
                final m = items[i];
                return CcCard(
                  padding: const EdgeInsets.symmetric(horizontal: CcSpace.s4, vertical: CcSpace.s2),
                  child: Row(children: [
                    CcAvatar(name: m.name, size: 44),
                    const SizedBox(width: CcSpace.s3),
                    Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(m.name, style: const TextStyle(fontWeight: FontWeight.w800)), Text('${m.relation} · ${DateTime.now().year - m.birthYear} سنة', style: const TextStyle(fontSize: 12, color: CcColors.muted))])),
                    IconButton(
                      tooltip: 'حذف ${m.name}',
                      icon: const Icon(Icons.delete_outline_rounded, color: CcColors.redText),
                      onPressed: () {
                        c.removeAt(i);
                        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('حُذف ${m.name}'), action: SnackBarAction(label: 'تراجع', textColor: Colors.white, onPressed: () => c.add(m))));
                      },
                    ),
                  ]),
                );
              },
            ),
    );
  }
}

/// نموذج الإضافة: يملك متحكماته ويتخلص منها مع إغلاق الورقة (بعد انتهاء حركة الإغلاق).
class _AddMemberSheet extends StatefulWidget {
  const _AddMemberSheet({required this.onAdd});
  final void Function(FamilyMember) onAdd;
  @override
  State<_AddMemberSheet> createState() => _AddMemberSheetState();
}

class _AddMemberSheetState extends State<_AddMemberSheet> {
  final _name = TextEditingController();
  final _year = TextEditingController();
  var _relation = _relations.first;
  final _errors = <String, String?>{};

  @override
  void dispose() {
    _name.dispose();
    _year.dispose();
    super.dispose();
  }

  void _submit() {
    final y = int.tryParse(ccNormalizeDigits(_year.text.trim()));
    setState(() {
      _errors['name'] = _name.text.trim().length < 2 ? 'أدخل الاسم' : null;
      _errors['year'] = (y == null || y < 1900 || y > DateTime.now().year) ? 'أدخل سنة ميلاد صحيحة' : null;
    });
    if (_errors.values.any((e) => e != null)) return;
    widget.onAdd(FamilyMember(_name.text.trim(), _relation, y!));
    Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: SingleChildScrollView(
        padding: EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6 + MediaQuery.of(context).viewInsets.bottom),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, mainAxisSize: MainAxisSize.min, children: [
          Semantics(header: true, child: const Text('إضافة فرد من العائلة', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
          const SizedBox(height: CcSpace.s4),
          CcTextField(label: 'الاسم', controller: _name, error: _errors['name']),
          const SizedBox(height: CcSpace.s3),
          const Text('صلة القرابة', style: TextStyle(fontWeight: FontWeight.w700)),
          const SizedBox(height: CcSpace.s2),
          Wrap(spacing: 8, runSpacing: 8, children: [for (final r in _relations) ChoiceChip(label: Text(r), selected: _relation == r, onSelected: (_) => setState(() => _relation = r))]),
          const SizedBox(height: CcSpace.s3),
          CcTextField(label: 'سنة الميلاد', controller: _year, keyboardType: TextInputType.number, maxLength: 4, ltr: true, error: _errors['year']),
          const SizedBox(height: CcSpace.s4),
          CcButton(label: 'إضافة', onPressed: _submit),
        ]),
      ),
    );
  }
}
