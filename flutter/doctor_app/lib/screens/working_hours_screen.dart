import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/profile.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';

class WorkingHoursScreen extends StatelessWidget {
  const WorkingHoursScreen({super.key});

  Future<int?> _pick(BuildContext context, int initial, String help) async {
    final t = await showTimePicker(context: context, initialTime: TimeOfDay(hour: initial ~/ 60, minute: initial % 60), helpText: help, builder: (c, w) => MediaQuery(data: MediaQuery.of(c).copyWith(alwaysUse24HourFormat: false), child: w!));
    return t == null ? null : t.hour * 60 + t.minute;
  }

  Future<void> _edit(BuildContext context, WorkingHoursController c, int i) async {
    final d = c.days[i];
    final from = await _pick(context, d.from, 'بداية الدوام');
    if (from == null || !context.mounted) return;
    final to = await _pick(context, d.to, 'نهاية الدوام');
    if (to == null || !context.mounted) return;
    final err = c.setRange(i, from, to);
    final m = ScaffoldMessenger.of(context);
    m.hideCurrentSnackBar();
    m.showSnackBar(SnackBar(content: Text(err ?? 'تم تحديث دوام ${d.name}')));
  }

  @override
  Widget build(BuildContext context) {
    AppScope.of(context);
    final c = AppScope.storeOf(context).hours;
    return Scaffold(
      appBar: AppBar(title: const Text('أوقات العمل'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: ListView(
        padding: const EdgeInsets.all(CcSpace.s5),
        children: [
          Text('${c.activeDays} أيام عمل في الأسبوع. يظهر المرضى خانات الحجز ضمن هذه الأوقات فقط.', style: const TextStyle(color: CcColors.muted, height: 1.6)),
          const SizedBox(height: CcSpace.s4),
          for (var i = 0; i < c.days.length; i++)
            Padding(
              padding: const EdgeInsets.only(bottom: CcSpace.s2),
              child: CcCard(
                padding: const EdgeInsets.symmetric(horizontal: CcSpace.s4, vertical: CcSpace.s2),
                child: Row(children: [
                  Expanded(
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text(c.days[i].name, style: const TextStyle(fontWeight: FontWeight.w800)),
                      Text(c.days[i].on ? fmtRange(c.days[i].from, c.days[i].to - c.days[i].from) : 'إجازة', style: TextStyle(fontSize: 13, color: c.days[i].on ? CcColors.muted : CcColors.redText)),
                    ]),
                  ),
                  if (c.days[i].on) IconButton(tooltip: 'تعديل دوام ${c.days[i].name}', icon: const Icon(Icons.edit_outlined, color: CcColors.blue), onPressed: () => _edit(context, c, i)),
                  Semantics(label: 'دوام ${c.days[i].name}', child: Switch(value: c.days[i].on, onChanged: (v) => c.toggle(i, v), activeTrackColor: CcColors.blue)),
                ]),
              ),
            ),
        ],
      ),
    );
  }
}
