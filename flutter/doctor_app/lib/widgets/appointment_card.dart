import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/appointments.dart';
import '../utils/format.dart';

/// بطاقة موعد: الوقت على جنب + المريض + السبب + الحالة.
class AppointmentCard extends StatelessWidget {
  const AppointmentCard({super.key, required this.appt, required this.onTap, this.compact = false});

  final Appointment appt;
  final VoidCallback onTap;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final muted = !appt.active;
    return Semantics(
      button: true,
      label: '${appt.patient}، ${fmtTime(appt.minutes)}، ${appt.reason}، ${appt.status.label}',
      excludeSemantics: true,
      child: CcCard(
        onTap: onTap,
        padding: const EdgeInsets.all(CcSpace.s3),
        child: Opacity(
          opacity: muted && appt.status == ApptStatus.cancelled ? 0.65 : 1,
          child: Row(
            children: [
              SizedBox(
                width: 54,
                child: Column(
                  children: [
                    Text(fmtTime(appt.minutes).split(' ').first, style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w800, color: CcColors.blue)),
                    Text(fmtTime(appt.minutes).split(' ').last, style: const TextStyle(fontSize: 12, color: CcColors.muted)),
                  ],
                ),
              ),
              Container(width: 1, height: 56, color: CcColors.line),
              const SizedBox(width: CcSpace.s3),
              CcAvatar(name: appt.patient, asset: appt.avatar, size: 42),
              const SizedBox(width: CcSpace.s3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(appt.patient, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
                    const SizedBox(height: 2),
                    Row(
                      children: [
                        if (appt.type.isVideo) const Padding(padding: EdgeInsetsDirectional.only(end: 4), child: Icon(Icons.videocam_outlined, size: 16, color: CcColors.sky)),
                        Expanded(child: Text(appt.reason, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13, color: CcColors.muted))),
                      ],
                    ),
                    const SizedBox(height: 6),
                    CcBadge(appt.status.label, tone: appt.status.tone),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
