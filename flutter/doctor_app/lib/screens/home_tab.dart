import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/appointments.dart';
import '../state/app_scope.dart';
import 'notifications_screen.dart';
import '../utils/format.dart';
import '../widgets/appointment_card.dart';
import '../widgets/appointment_sheets.dart';

class HomeTab extends StatelessWidget {
  const HomeTab({super.key, required this.onOpenAppointments});
  final VoidCallback onOpenAppointments;

  @override
  Widget build(BuildContext context) {
    final store = AppScope.storeOf(context);
    final c = AppScope.of(context);
    final name = store.profile.name;
    final unread = store.notifications.unreadCount;
    final today = c.forDay(0);
    final upcoming = today.where((a) => a.active).take(3).toList();
    final focus = c.focus;
    final now = DateTime.now();

    return ListView(
      padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s8),
      children: [
        Row(
          children: [
            CcAvatar(name: name, asset: 'assets/images/avatars/doc-f1.webp', size: 48),
            const SizedBox(width: CcSpace.s3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('صباح الخير', style: const TextStyle(color: CcColors.muted, fontSize: 13)),
                  Semantics(header: true, child: Text(name, style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w800))),
                ],
              ),
            ),
            IconButton.outlined(
              tooltip: unread == 0 ? 'الإشعارات' : 'الإشعارات، $unread غير مقروءة',
              onPressed: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const NotificationsScreen())),
              icon: Badge(isLabelVisible: unread > 0, label: Text('$unread'), backgroundColor: CcColors.orange, child: const Icon(Icons.notifications_none_rounded)),
              style: IconButton.styleFrom(side: const BorderSide(color: CcColors.line)),
            ),
          ],
        ),
        const SizedBox(height: CcSpace.s2),
        Text('${weekdayName(now)} ${now.day} ${months[now.month - 1]}', style: const TextStyle(color: CcColors.muted)),
        const SizedBox(height: CcSpace.s5),
        if (focus != null) _FocusCard(appt: focus) else const _DoneCard(),
        const SizedBox(height: CcSpace.s5),
        GridView.count(
          crossAxisCount: 2,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: CcSpace.s3,
          crossAxisSpacing: CcSpace.s3,
          childAspectRatio: 2.3,
          children: [
            CcStatTile(icon: Icons.event_note_rounded, value: '${c.countToday((a) => a.status != ApptStatus.cancelled)}', label: 'مواعيد اليوم'),
            CcStatTile(icon: Icons.hourglass_top_rounded, value: '${c.countToday((a) => a.status == ApptStatus.pending)}', label: 'بانتظار التأكيد', tone: CcTone.orange),
            CcStatTile(icon: Icons.videocam_outlined, value: '${c.countToday((a) => a.type.isVideo && a.active)}', label: 'استشارات عن بُعد', tone: CcTone.blue),
            CcStatTile(icon: Icons.check_circle_outline_rounded, value: '${c.countToday((a) => a.status == ApptStatus.done)}', label: 'منتهية', tone: CcTone.green),
          ],
        ),
        const SizedBox(height: CcSpace.s6),
        CcSectionHeader('مواعيد اليوم', actionLabel: 'عرض الكل', onAction: onOpenAppointments),
        const SizedBox(height: CcSpace.s3),
        if (upcoming.isEmpty)
          const Padding(padding: EdgeInsets.all(CcSpace.s6), child: Center(child: Text('لا توجد مواعيد نشطة اليوم', style: TextStyle(color: CcColors.muted))))
        else
          for (final a in upcoming) Padding(padding: const EdgeInsets.only(bottom: CcSpace.s3), child: AppointmentCard(appt: a, onTap: () => showAppointmentSheet(context, a.id))),
        const SizedBox(height: CcSpace.s4),
        const CcSectionHeader('تنبيهات'),
        const SizedBox(height: CcSpace.s3),
        const _AlertTile(icon: Icons.science_outlined, tone: CcTone.blue, title: 'نتيجة تحليل جاهزة', text: 'محمد علي – صورة دم كاملة'),
        const SizedBox(height: CcSpace.s3),
        const _AlertTile(icon: Icons.warning_amber_rounded, tone: CcTone.orange, title: 'قيمة خارج المعدل', text: 'عبدالله ناصر – كرياتينين مرتفع'),
      ],
    );
  }
}

class _FocusCard extends StatelessWidget {
  const _FocusCard({required this.appt});
  final Appointment appt;

  @override
  Widget build(BuildContext context) {
    final label = switch (appt.status) {
      ApptStatus.inProgress => 'الكشف الحالي',
      ApptStatus.arrived => 'بانتظارك في العيادة',
      _ => 'الموعد القادم',
    };
    return Container(
      padding: const EdgeInsets.all(CcSpace.s5),
      decoration: BoxDecoration(
        gradient: const LinearGradient(colors: [CcColors.blue, CcColors.blue600], begin: Alignment.topRight, end: Alignment.bottomLeft),
        borderRadius: BorderRadius.circular(CcRadius.xl),
        boxShadow: CcShadow.card,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(color: Color(0xFFCFE3FF), fontWeight: FontWeight.w700, fontSize: 13)),
          const SizedBox(height: CcSpace.s3),
          Row(
            children: [
              CcAvatar(name: appt.patient, asset: appt.avatar, size: 52),
              const SizedBox(width: CcSpace.s3),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(appt.patient, style: const TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.w800)),
                    Text('${appt.reason} · ${fmtTime(appt.minutes)}', style: const TextStyle(color: Color(0xFFCFE3FF), fontSize: 13)),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: CcSpace.s4),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: () => showAppointmentSheet(context, appt.id),
              style: FilledButton.styleFrom(backgroundColor: Colors.white, foregroundColor: CcColors.blue, minimumSize: const Size(48, 48), shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(CcRadius.md)), textStyle: const TextStyle(fontFamily: kCcFont, fontWeight: FontWeight.w800, fontSize: 15)),
              child: const Text('عرض التفاصيل'),
            ),
          ),
        ],
      ),
    );
  }
}

class _DoneCard extends StatelessWidget {
  const _DoneCard();
  @override
  Widget build(BuildContext context) => const CcCard(
        color: CcColors.green50,
        child: Row(children: [
          Icon(Icons.check_circle_rounded, color: CcColors.greenText),
          SizedBox(width: CcSpace.s3),
          Expanded(child: Text('أنهيت كل مواعيد اليوم. أحسنت!', style: TextStyle(fontWeight: FontWeight.w700, color: CcColors.greenText))),
        ]),
      );
}

class _AlertTile extends StatelessWidget {
  const _AlertTile({required this.icon, required this.tone, required this.title, required this.text});
  final IconData icon;
  final CcTone tone;
  final String title, text;

  @override
  Widget build(BuildContext context) {
    final fg = tone == CcTone.orange ? CcColors.amberText : CcColors.blue;
    final bg = tone == CcTone.orange ? CcColors.orange50 : CcColors.blue50;
    return CcCard(
      padding: const EdgeInsets.all(CcSpace.s3),
      child: Row(
        children: [
          Container(width: 40, height: 40, decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(CcRadius.md)), child: Icon(icon, color: fg)),
          const SizedBox(width: CcSpace.s3),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [Text(title, style: const TextStyle(fontWeight: FontWeight.w800)), Text(text, style: const TextStyle(fontSize: 13, color: CcColors.muted))],
            ),
          ),
        ],
      ),
    );
  }
}
