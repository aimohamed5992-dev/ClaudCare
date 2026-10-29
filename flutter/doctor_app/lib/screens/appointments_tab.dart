import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/appointments.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';
import '../widgets/appointment_card.dart';
import '../widgets/appointment_sheets.dart';

enum _Filter {
  all('الكل'),
  pending('بانتظار التأكيد'),
  confirmed('مؤكدة'),
  done('منتهية'),
  cancelled('ملغاة');

  const _Filter(this.label);
  final String label;

  bool test(Appointment a) => switch (this) {
        _Filter.all => true,
        _Filter.pending => a.status == ApptStatus.pending,
        _Filter.confirmed => a.status == ApptStatus.confirmed || a.status == ApptStatus.arrived || a.status == ApptStatus.inProgress,
        _Filter.done => a.status == ApptStatus.done,
        _Filter.cancelled => a.status == ApptStatus.cancelled,
      };
}

class AppointmentsTab extends StatefulWidget {
  const AppointmentsTab({super.key});
  @override
  State<AppointmentsTab> createState() => _AppointmentsTabState();
}

class _AppointmentsTabState extends State<AppointmentsTab> {
  int _day = 0;
  _Filter _filter = _Filter.all;

  @override
  Widget build(BuildContext context) {
    final c = AppScope.of(context);
    final base = DateTime.now();
    final items = c.forDay(_day).where(_filter.test).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s3),
          child: Semantics(header: true, child: const Text('المواعيد', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800))),
        ),
        SizedBox(
          height: 92,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: CcSpace.s5),
            itemCount: 7,
            separatorBuilder: (_, __) => const SizedBox(width: 8),
            itemBuilder: (_, i) {
              final d = base.add(Duration(days: i));
              final selected = i == _day;
              final count = c.forDay(i).where((a) => a.status != ApptStatus.cancelled).length;
              return Semantics(
                button: true,
                selected: selected,
                label: '${dayLabel(d, base)}، $count مواعيد',
                excludeSemantics: true,
                child: InkWell(
                  borderRadius: BorderRadius.circular(CcRadius.lg),
                  onTap: () => setState(() => _day = i),
                  child: Container(
                    width: 62,
                    decoration: BoxDecoration(
                      color: selected ? CcColors.blue : Colors.white,
                      borderRadius: BorderRadius.circular(CcRadius.lg),
                      border: Border.all(color: selected ? CcColors.blue : CcColors.line),
                    ),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Text(weekdayName(d), style: TextStyle(fontSize: 12, color: selected ? Colors.white70 : CcColors.muted)),
                        Text('${d.day}', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800, color: selected ? Colors.white : CcColors.slate)),
                        Text('$count', style: TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: selected ? Colors.white : CcColors.blue)),
                      ],
                    ),
                  ),
                ),
              );
            },
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
            itemBuilder: (_, i) {
              final f = _Filter.values[i];
              return ChoiceChip(label: Text(f.label), selected: _filter == f, onSelected: (_) => setState(() => _filter = f));
            },
          ),
        ),
        const SizedBox(height: CcSpace.s2),
        Expanded(
          child: items.isEmpty
              ? CcEmptyState(
                  image: 'assets/images/empty/empty-appointments.webp',
                  title: 'لا توجد مواعيد',
                  text: _filter == _Filter.all ? 'يومك خالٍ من المواعيد.' : 'لا مواعيد بهذه الحالة في ${dayLabel(base.add(Duration(days: _day)), base)}.',
                )
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s8),
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: CcSpace.s3),
                  itemBuilder: (_, i) => AppointmentCard(appt: items[i], onTap: () => showAppointmentSheet(context, items[i].id)),
                ),
        ),
      ],
    );
  }
}
