import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/appointments.dart';
import '../screens/consultation_screen.dart';
import '../screens/patient_record_screen.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';

const cancelReasons = ['ظرف طارئ', 'إجازة', 'تعارض في المواعيد', 'بناءً على طلب المريض'];

void _toast(BuildContext context, String msg, {VoidCallback? undo}) {
  final m = ScaffoldMessenger.of(context);
  m.hideCurrentSnackBar();
  m.showSnackBar(SnackBar(content: Text(msg), action: undo == null ? null : SnackBarAction(label: 'تراجع', textColor: Colors.white, onPressed: undo)));
}

void _openConsult(BuildContext host, Appointment a) {
  final p = AppScope.patientsOf(host).forAppointment(a);
  if (p == null) {
    _toast(host, 'لا يوجد ملف طبي لهذا المريض');
    return;
  }
  Navigator.of(host).push(MaterialPageRoute<void>(builder: (_) => ConsultationScreen(patientId: p.id, apptId: a.id)));
}

/// تفاصيل الموعد مع الإجراءات المناسبة لحالته.
Future<void> showAppointmentSheet(BuildContext context, int id) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (sheetCtx) {
      final scope = AppScope.of(context);
      return ListenableBuilder(
        listenable: scope,
        builder: (_, __) => _DetailBody(appt: scope.byId(id), controller: scope, host: context, sheet: sheetCtx),
      );
    },
  );
}

class _DetailBody extends StatelessWidget {
  const _DetailBody({required this.appt, required this.controller, required this.host, required this.sheet});
  final Appointment appt;
  final AppointmentsController controller;
  final BuildContext host; // السياق الذي يعرض الـ SnackBar بعد الإغلاق
  final BuildContext sheet;

  @override
  Widget build(BuildContext context) {
    final today = appt.day == 0;
    final actions = <Widget>[];
    void close() => Navigator.of(sheet).pop();

    switch (appt.status) {
      case ApptStatus.pending:
        actions.addAll([
          CcButton(
            label: 'تأكيد الموعد',
            icon: Icons.check_rounded,
            onPressed: () {
              controller.confirm(appt.id);
              close();
              _toast(host, 'تم تأكيد موعد ${appt.patient}', undo: () => controller.restore(appt.id, ApptStatus.pending));
            },
          ),
        ]);
      case ApptStatus.confirmed:
        if (today && appt.type.isVideo) {
          actions.add(CcButton(label: 'بدء المكالمة والكشف', icon: Icons.videocam_rounded, onPressed: () {
            controller.start(appt.id);
            close();
            _openConsult(host, appt);
          }));
        }
      case ApptStatus.arrived:
        actions.add(CcButton(label: 'بدء الكشف', icon: Icons.play_arrow_rounded, onPressed: () {
          controller.start(appt.id);
          close();
          _openConsult(host, appt);
        }));
      case ApptStatus.inProgress:
        actions.add(CcButton(label: 'متابعة الكشف', icon: Icons.edit_note_rounded, onPressed: () {
          close();
          _openConsult(host, appt);
        }));
      case ApptStatus.done || ApptStatus.cancelled:
        break;
    }

    actions.add(CcButton(
      label: 'عرض الملف الطبي',
      icon: Icons.folder_open_rounded,
      variant: CcButtonVariant.ghost,
      onPressed: () {
        final p = AppScope.patientsOf(host).forAppointment(appt);
        close();
        if (p != null) Navigator.of(host).push(MaterialPageRoute<void>(builder: (_) => PatientRecordScreen(patientId: p.id)));
      },
    ));

    if (appt.status == ApptStatus.pending || appt.status == ApptStatus.confirmed || appt.status == ApptStatus.arrived) {
      actions.add(CcButton(
        label: 'إعادة جدولة',
        icon: Icons.event_repeat_rounded,
        variant: CcButtonVariant.soft,
        onPressed: () async {
          close();
          await showRescheduleSheet(host, appt.id);
        },
      ));
    }
    if (appt.status == ApptStatus.pending || appt.status == ApptStatus.confirmed) {
      actions.add(CcButton(
        label: appt.status == ApptStatus.pending ? 'رفض الموعد' : 'إلغاء الموعد',
        variant: CcButtonVariant.danger,
        onPressed: () async {
          close();
          await showCancelSheet(host, appt.id);
        },
      ));
    }

    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                CcAvatar(name: appt.patient, asset: appt.avatar, size: 60),
                const SizedBox(width: CcSpace.s4),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Semantics(header: true, child: Text(appt.patient, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
                      Text('${appt.age} سنة', style: const TextStyle(color: CcColors.muted)),
                    ],
                  ),
                ),
                CcBadge(appt.status.label, tone: appt.status.tone),
              ],
            ),
            const SizedBox(height: CcSpace.s5),
            _Row(icon: Icons.schedule_rounded, label: 'الموعد', value: '${dayLabel(DateTime.now().add(Duration(days: appt.day)), DateTime.now())} · ${fmtRange(appt.minutes, appt.duration)}'),
            _Row(icon: appt.type.isVideo ? Icons.videocam_outlined : Icons.local_hospital_outlined, label: 'النوع', value: appt.type.label),
            _Row(icon: Icons.notes_rounded, label: 'سبب الزيارة', value: appt.reason),
            if (appt.cancelReason != null) _Row(icon: Icons.info_outline_rounded, label: 'سبب الإلغاء', value: appt.cancelReason!),
            if (actions.isNotEmpty) const SizedBox(height: CcSpace.s5),
            for (final a in actions) ...[a, const SizedBox(height: CcSpace.s3)],
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.icon, required this.label, required this.value});
  final IconData icon;
  final String label, value;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, size: 20, color: CcColors.muted),
            const SizedBox(width: CcSpace.s3),
            SizedBox(width: 84, child: Text(label, style: const TextStyle(color: CcColors.muted))),
            Expanded(child: Text(value, style: const TextStyle(fontWeight: FontWeight.w700))),
          ],
        ),
      );
}

/// إلغاء/رفض الموعد: سبب إلزامي.
Future<void> showCancelSheet(BuildContext context, int id) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (sheet) {
      String? reason;
      return StatefulBuilder(
        builder: (_, setState) => SafeArea(
          child: Padding(
            padding: EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6 + MediaQuery.of(sheet).viewInsets.bottom),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Semantics(header: true, child: const Text('سبب الإلغاء', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
                const SizedBox(height: CcSpace.s2),
                const Text('سيُبلَّغ المريض بالإلغاء وسببه.', style: TextStyle(color: CcColors.muted)),
                const SizedBox(height: CcSpace.s4),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [for (final r in cancelReasons) ChoiceChip(label: Text(r), selected: reason == r, onSelected: (_) => setState(() => reason = r))],
                ),
                const SizedBox(height: CcSpace.s6),
                CcButton(
                  label: 'تأكيد الإلغاء',
                  variant: CcButtonVariant.danger,
                  onPressed: reason == null
                      ? null
                      : () {
                          final c = AppScope.of(context);
                          final name = c.byId(id).patient;
                          c.cancel(id, reason!);
                          Navigator.of(sheet).pop();
                          _toast(context, 'أُلغي موعد $name');
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

/// إعادة الجدولة: اختيار اليوم ثم خانة فارغة.
Future<void> showRescheduleSheet(BuildContext context, int id) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (sheet) {
      final c = AppScope.of(context);
      final appt = c.byId(id);
      var day = appt.day;
      int? slot;
      final base = DateTime.now();
      return StatefulBuilder(
        builder: (_, setState) {
          final slots = c.freeSlots(day, appt.duration, excludeId: id);
          return SafeArea(
            child: Padding(
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
                      itemCount: 7,
                      separatorBuilder: (_, __) => const SizedBox(width: 8),
                      itemBuilder: (_, i) => ChoiceChip(
                        label: Text(dayLabel(base.add(Duration(days: i)), base)),
                        selected: day == i,
                        onSelected: (_) => setState(() {
                          day = i;
                          slot = null;
                        }),
                      ),
                    ),
                  ),
                  const SizedBox(height: CcSpace.s4),
                  if (slots.isEmpty)
                    const Padding(padding: EdgeInsets.symmetric(vertical: CcSpace.s6), child: Center(child: Text('لا توجد خانات متاحة في هذا اليوم', style: TextStyle(color: CcColors.muted))))
                  else
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxHeight: 220),
                      child: SingleChildScrollView(
                        child: Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: [for (final s in slots) ChoiceChip(label: Text(fmtTime(s)), selected: slot == s, onSelected: (_) => setState(() => slot = s))],
                        ),
                      ),
                    ),
                  const SizedBox(height: CcSpace.s6),
                  CcButton(
                    label: 'تأكيد الموعد الجديد',
                    onPressed: slot == null
                        ? null
                        : () {
                            c.reschedule(id, day, slot!);
                            Navigator.of(sheet).pop();
                            _toast(context, 'نُقل موعد ${appt.patient} إلى ${dayLabel(base.add(Duration(days: day)), base)} ${fmtTime(slot!)}');
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
