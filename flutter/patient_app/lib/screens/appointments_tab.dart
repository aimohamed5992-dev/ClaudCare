import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/appointments.dart';
import '../data/doctors.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';
import '../widgets/appointment_sheets.dart';

class AppointmentsTab extends StatefulWidget {
  const AppointmentsTab({super.key});
  @override
  State<AppointmentsTab> createState() => _AppointmentsTabState();
}

class _AppointmentsTabState extends State<AppointmentsTab> {
  ApptStatus _tab = ApptStatus.upcoming;

  @override
  Widget build(BuildContext context) {
    final store = AppScope.storeOf(context);
    final c = store.appointments;
    final items = c.byStatus(_tab);
    final base = DateTime.now();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s3),
          child: Semantics(header: true, child: const Text('مواعيدي', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800))),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: CcSpace.s5),
          child: Wrap(spacing: 8, runSpacing: 8, children: [
            for (final s in ApptStatus.values)
              ChoiceChip(label: Text('${switch (s) { ApptStatus.upcoming => 'القادمة', ApptStatus.done => 'السابقة', ApptStatus.cancelled => 'الملغاة' }} (${c.byStatus(s).length})'), selected: _tab == s, onSelected: (_) => setState(() => _tab = s)),
          ]),
        ),
        const SizedBox(height: CcSpace.s3),
        Expanded(
          child: items.isEmpty
              ? CcEmptyState(
                  image: 'assets/images/empty/empty-appointments.webp',
                  title: _tab == ApptStatus.upcoming ? 'لا مواعيد قادمة' : 'لا شيء هنا',
                  text: _tab == ApptStatus.upcoming ? 'احجز موعدك مع أحد أطبائنا.' : null,
                  action: _tab == ApptStatus.upcoming ? CcButton(label: 'ابحث عن طبيب', expand: false, onPressed: () => store.tab.value = 1) : null,
                )
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s8),
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: CcSpace.s3),
                  itemBuilder: (_, i) {
                    final a = items[i];
                    final d = doctorById(a.doctorId);
                    final date = base.add(Duration(days: a.day));
                    return Semantics(
                      button: true,
                      label: '${d.name}، ${dayLabel(date, base)}، ${fmtTime(a.minutes)}، ${a.type.label}، ${a.status.label}',
                      excludeSemantics: true,
                      child: CcCard(
                        onTap: () => showApptSheet(context, a.id),
                        padding: const EdgeInsets.all(CcSpace.s3),
                        child: Column(children: [
                          Row(children: [
                            CcAvatar(name: d.name, asset: d.avatar, size: 52),
                            const SizedBox(width: CcSpace.s3),
                            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(d.name, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)), Text(d.spec.label, style: const TextStyle(color: CcColors.blue, fontWeight: FontWeight.w700, fontSize: 12))])),
                            if (a.type.isVideo) const Icon(Icons.videocam_outlined, color: CcColors.sky),
                          ]),
                          const SizedBox(height: CcSpace.s3),
                          Container(
                            padding: const EdgeInsets.all(CcSpace.s3),
                            decoration: BoxDecoration(color: _tab == ApptStatus.upcoming ? CcColors.blue50 : const Color(0xFFF6F7F9), borderRadius: BorderRadius.circular(CcRadius.md)),
                            child: Row(children: [
                              const Icon(Icons.event_rounded, size: 18, color: CcColors.muted),
                              const SizedBox(width: 6),
                              Expanded(child: Text('${dayLabel(date, base)} · ${fmtTime(a.minutes)}', style: const TextStyle(fontWeight: FontWeight.w700))),
                              if (_tab == ApptStatus.done && a.rating == null) const CcBadge('بانتظار تقييمك', tone: CcTone.orange) else if (_tab == ApptStatus.done) CcRating(value: a.rating!.toDouble()) else Text(a.forWhom, style: const TextStyle(fontSize: 12, color: CcColors.muted)),
                            ]),
                          ),
                        ]),
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }
}
