import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/appointments.dart';
import '../data/doctors.dart';
import '../screens/doctor_profile_screen.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';

const cancelReasons = ['ظرف طارئ', 'تعارض مع موعد آخر', 'حجزت بالخطأ', 'تحسّنت حالتي', 'سبب آخر'];

void toast(BuildContext c, String msg, {VoidCallback? undo}) {
  final m = ScaffoldMessenger.of(c);
  m.hideCurrentSnackBar();
  m.showSnackBar(SnackBar(content: Text(msg), action: undo == null ? null : SnackBarAction(label: 'تراجع', textColor: Colors.white, onPressed: undo)));
}

/// تفاصيل موعد مع الإجراءات المناسبة لحالته.
Future<void> showApptSheet(BuildContext context, int id) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (sheet) {
      final c = AppScope.storeOf(context).appointments;
      return ListenableBuilder(listenable: c, builder: (_, __) => _Detail(appt: c.byId(id), host: context, sheet: sheet));
    },
  );
}

class _Detail extends StatelessWidget {
  const _Detail({required this.appt, required this.host, required this.sheet});
  final PatientAppt appt;
  final BuildContext host, sheet;

  @override
  Widget build(BuildContext context) {
    final d = doctorById(appt.doctorId);
    final now = DateTime.now();
    final date = now.add(Duration(days: appt.day));
    final upcoming = appt.status == ApptStatus.upcoming;
    final canCall = upcoming && appt.type.isVideo && appt.hoursUntil <= 0.17 && appt.hoursUntil >= -0.5;

    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(children: [
              CcAvatar(name: d.name, asset: d.avatar, size: 60),
              const SizedBox(width: CcSpace.s4),
              Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Semantics(header: true, child: Text(d.name, style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w800))), Text(d.spec.label, style: const TextStyle(color: CcColors.blue, fontWeight: FontWeight.w700))])),
              CcBadge(appt.status.label, tone: switch (appt.status) { ApptStatus.upcoming => CcTone.green, ApptStatus.done => CcTone.gray, ApptStatus.cancelled => CcTone.red }),
            ]),
            const SizedBox(height: CcSpace.s5),
            _Row(Icons.event_rounded, 'الموعد', '${dayLabel(date, now)} · ${fmtTime(appt.minutes)}'),
            _Row(appt.type.isVideo ? Icons.videocam_outlined : Icons.local_hospital_outlined, 'النوع', appt.type.label),
            _Row(Icons.person_outline_rounded, 'المريض', appt.forWhom),
            _Row(Icons.notes_rounded, 'السبب', appt.reason.isEmpty ? '—' : appt.reason),
            _Row(Icons.payments_outlined, 'المبلغ', appt.price == 0 ? 'مغطى بالتأمين' : '${appt.price} ر.س · ${appt.paid ? 'مدفوع' : 'يُدفع في العيادة'}'),
            if (appt.cancelReason != null) _Row(Icons.info_outline_rounded, 'سبب الإلغاء', appt.cancelReason!),
            if (appt.rating != null) _Row(Icons.star_rounded, 'تقييمك', '${appt.rating} من 5${appt.ratingComment == null || appt.ratingComment!.isEmpty ? '' : ' – ${appt.ratingComment}'}'),
            const SizedBox(height: CcSpace.s5),
            if (upcoming && appt.type.isVideo) ...[
              CcButton(
                label: canCall ? 'ابدأ المكالمة' : 'المكالمة تُفتح قبل الموعد بـ 10 دقائق',
                icon: Icons.videocam_rounded,
                onPressed: canCall ? () => toast(host, 'المكالمات المرئية غير متاحة في النسخة التجريبية') : null,
              ),
              const SizedBox(height: CcSpace.s3),
            ],
            if (upcoming) ...[
              if (!appt.type.isVideo) ...[
                CcButton(label: 'الاتجاهات إلى العيادة', icon: Icons.directions_outlined, variant: CcButtonVariant.soft, onPressed: () => toast(host, 'الخرائط غير متاحة في النسخة التجريبية')),
                const SizedBox(height: CcSpace.s3),
              ],
              CcButton(
                label: 'إعادة جدولة',
                icon: Icons.event_repeat_rounded,
                variant: CcButtonVariant.ghost,
                onPressed: () async {
                  Navigator.of(sheet).pop();
                  await showRescheduleSheet(host, appt.id);
                },
              ),
              const SizedBox(height: CcSpace.s3),
              CcButton(
                label: 'إلغاء الموعد',
                variant: CcButtonVariant.danger,
                onPressed: () async {
                  Navigator.of(sheet).pop();
                  await showCancelSheet(host, appt.id);
                },
              ),
            ],
            if (appt.status == ApptStatus.done) ...[
              if (appt.rating == null) ...[
                CcButton(
                  label: 'قيّم الزيارة',
                  icon: Icons.star_outline_rounded,
                  onPressed: () async {
                    Navigator.of(sheet).pop();
                    await showRateSheet(host, appt.id);
                  },
                ),
                const SizedBox(height: CcSpace.s3),
              ],
              CcButton(
                label: 'احجز موعدًا آخر',
                variant: CcButtonVariant.soft,
                onPressed: () {
                  Navigator.of(sheet).pop();
                  Navigator.of(host).push(MaterialPageRoute<void>(builder: (_) => DoctorProfileScreen(doctorId: d.id)));
                },
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row(this.icon, this.label, this.value);
  final IconData icon;
  final String label, value;
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Icon(icon, size: 20, color: CcColors.muted),
          const SizedBox(width: CcSpace.s3),
          SizedBox(width: 78, child: Text(label, style: const TextStyle(color: CcColors.muted))),
          Expanded(child: Text(value, style: const TextStyle(fontWeight: FontWeight.w700))),
        ]),
      );
}

Future<void> showCancelSheet(BuildContext context, int id) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (sheet) {
      String? reason;
      final a = AppScope.storeOf(context).appointments.byId(id);
      final late = a.hoursUntil < 24;
      return StatefulBuilder(
        builder: (_, setState) => SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Semantics(header: true, child: const Text('إلغاء الموعد', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
                const SizedBox(height: CcSpace.s3),
                CcNotice(late ? 'تبقّى أقل من 24 ساعة على الموعد، وقد تُطبَّق رسوم إلغاء 50%.' : 'الإلغاء مجاني حتى 24 ساعة قبل الموعد.', error: late),
                const SizedBox(height: CcSpace.s4),
                const Text('سبب الإلغاء', style: TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: CcSpace.s2),
                Wrap(spacing: 8, runSpacing: 8, children: [for (final r in cancelReasons) ChoiceChip(label: Text(r), selected: reason == r, onSelected: (_) => setState(() => reason = r))]),
                const SizedBox(height: CcSpace.s6),
                CcButton(
                  label: 'تأكيد الإلغاء',
                  variant: CcButtonVariant.danger,
                  onPressed: reason == null
                      ? null
                      : () {
                          final c = AppScope.storeOf(context).appointments;
                          final prev = c.byId(id);
                          c.cancel(id, reason!);
                          Navigator.of(sheet).pop();
                          toast(context, 'أُلغي الموعد', undo: () => c.restore(id, prev));
                        },
                ),
                const SizedBox(height: CcSpace.s2),
                CcButton(label: 'رجوع', variant: CcButtonVariant.ghost, onPressed: () => Navigator.of(sheet).pop()),
              ],
            ),
          ),
        ),
      );
    },
  );
}

Future<void> showRescheduleSheet(BuildContext context, int id) async {
  final store = AppScope.storeOf(context);
  if (store.remote != null) {
    final a = store.appointments.byId(id);
    try {
      await store.remote!.loadAvailability(a.doctorId, a.type);
    } on ApiException catch (_) {}
    if (!context.mounted) return;
  }
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (sheet) {
      final c = AppScope.storeOf(context).appointments;
      final a = c.byId(id);
      final d = doctorById(a.doctorId);
      final days = c.bookableDays(d);
      final base = DateTime.now();
      var day = days.contains(a.day) ? a.day : (days.isEmpty ? a.day : days.first);
      int? slot;
      return StatefulBuilder(
        builder: (_, setState) {
          final slots = c.slotsFor(d, day, excludeApptId: id);
          return SafeArea(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Semantics(header: true, child: const Text('إعادة جدولة الموعد', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
                  const SizedBox(height: CcSpace.s4),
                  SizedBox(
                    height: 44,
                    child: ListView.separated(
                      scrollDirection: Axis.horizontal,
                      itemCount: days.length,
                      separatorBuilder: (_, __) => const SizedBox(width: 8),
                      itemBuilder: (_, i) => ChoiceChip(label: Text(dayLabel(base.add(Duration(days: days[i])), base)), selected: day == days[i], onSelected: (_) => setState(() {
                            day = days[i];
                            slot = null;
                          })),
                    ),
                  ),
                  const SizedBox(height: CcSpace.s4),
                  if (slots.isEmpty) const Padding(padding: EdgeInsets.all(CcSpace.s4), child: Center(child: Text('لا أوقات متاحة في هذا اليوم', style: TextStyle(color: CcColors.muted)))) else Wrap(spacing: 8, runSpacing: 8, children: [for (final s in slots) ChoiceChip(label: Text(fmtTime(s)), selected: slot == s, onSelected: (_) => setState(() => slot = s))]),
                  const SizedBox(height: CcSpace.s6),
                  CcButton(
                    label: 'تأكيد الموعد الجديد',
                    onPressed: slot == null
                        ? null
                        : () {
                            c.reschedule(id, day, slot!);
                            Navigator.of(sheet).pop();
                            toast(context, 'نُقل موعدك إلى ${dayLabel(base.add(Duration(days: day)), base)} ${fmtTime(slot!)}');
                          },
                  ),
                  const SizedBox(height: CcSpace.s2),
                  CcButton(label: 'رجوع', variant: CcButtonVariant.ghost, onPressed: () => Navigator.of(sheet).pop()),
                ],
              ),
            ),
          );
        },
      );
    },
  );
}

Future<void> showRateSheet(BuildContext context, int id) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (sheet) {
      var stars = 0;
      final note = TextEditingController();
      return StatefulBuilder(
        builder: (_, setState) => SafeArea(
          child: Padding(
            padding: EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6 + MediaQuery.of(sheet).viewInsets.bottom),
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Semantics(header: true, child: const Text('كيف كانت زيارتك؟', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
                  const SizedBox(height: CcSpace.s4),
                  Row(mainAxisAlignment: MainAxisAlignment.center, children: [
                    for (var i = 1; i <= 5; i++)
                      IconButton(
                        tooltip: '$i من 5',
                        iconSize: 38,
                        onPressed: () => setState(() => stars = i),
                        icon: Icon(i <= stars ? Icons.star_rounded : Icons.star_outline_rounded, color: CcColors.orange),
                      ),
                  ]),
                  const SizedBox(height: CcSpace.s3),
                  CcTextField(label: 'تعليق (اختياري)', controller: note, maxLength: 200),
                  const SizedBox(height: CcSpace.s4),
                  CcButton(
                    label: 'إرسال التقييم',
                    onPressed: stars == 0
                        ? null
                        : () {
                            AppScope.storeOf(context).appointments.rate(id, stars, note.text.trim());
                            Navigator.of(sheet).pop();
                            toast(context, 'شكرًا لتقييمك');
                          },
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    },
  );
}
