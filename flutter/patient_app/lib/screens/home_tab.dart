import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/doctors.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';
import '../data/appointments.dart';
import 'doctor_profile_screen.dart';
import 'invoices_screen.dart';
import 'medications_screen.dart';
import 'messages_screen.dart';
import 'notifications_screen.dart';

class HomeTab extends StatelessWidget {
  const HomeTab({super.key});

  void _push(BuildContext c, Widget w) => Navigator.of(c).push(MaterialPageRoute<void>(builder: (_) => w));

  @override
  Widget build(BuildContext context) {
    final s = AppScope.storeOf(context);
    final next = s.appointments.next;
    final top = searchDoctors().take(5).toList();

    return ListView(
      padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s8),
      children: [
        Row(
          children: [
            CcAvatar(name: s.name, asset: 'assets/images/avatars/patient-2.webp', size: 48),
            const SizedBox(width: CcSpace.s3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('صباح الخير', style: TextStyle(color: CcColors.muted, fontSize: 13)),
                  Semantics(header: true, child: Text(s.name, style: const TextStyle(fontSize: 19, fontWeight: FontWeight.w800))),
                ],
              ),
            ),
            IconButton.outlined(
              tooltip: s.conversations.unreadTotal == 0 ? 'الرسائل' : 'الرسائل، ${s.conversations.unreadTotal} غير مقروءة',
              onPressed: () => _push(context, const MessagesScreen()),
              icon: Badge(isLabelVisible: s.conversations.unreadTotal > 0, label: Text('${s.conversations.unreadTotal}'), backgroundColor: CcColors.orange, child: const Icon(Icons.chat_bubble_outline_rounded)),
              style: IconButton.styleFrom(side: const BorderSide(color: CcColors.line)),
            ),
            const SizedBox(width: 6),
            IconButton.outlined(
              tooltip: s.notifications.unreadCount == 0 ? 'الإشعارات' : 'الإشعارات، ${s.notifications.unreadCount} غير مقروءة',
              onPressed: () => _push(context, const NotificationsScreen()),
              icon: Badge(isLabelVisible: s.notifications.unreadCount > 0, label: Text('${s.notifications.unreadCount}'), backgroundColor: CcColors.orange, child: const Icon(Icons.notifications_none_rounded)),
              style: IconButton.styleFrom(side: const BorderSide(color: CcColors.line)),
            ),
          ],
        ),
        const SizedBox(height: CcSpace.s5),
        Semantics(
          button: true,
          label: 'ابحث عن طبيب أو تخصص',
          excludeSemantics: true,
          child: InkWell(
            borderRadius: BorderRadius.circular(CcRadius.md),
            onTap: () {
              s.specialtyFilter.value = null;
              s.tab.value = 1;
              s.focusSearch.value++;
            },
            child: Container(
              height: 52,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              decoration: BoxDecoration(borderRadius: BorderRadius.circular(CcRadius.md), border: Border.all(color: CcColors.lineStrong)),
              child: const Row(children: [Icon(Icons.search_rounded, color: CcColors.muted), SizedBox(width: 12), Expanded(child: Text('ابحث عن طبيب أو تخصص', maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(color: CcColors.muted)))]),
            ),
          ),
        ),
        const SizedBox(height: CcSpace.s5),
        const _Banners(),
        const SizedBox(height: CcSpace.s6),
        const CcSectionHeader('التخصصات'),
        const SizedBox(height: CcSpace.s3),
        SizedBox(
          height: 100,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: specialties.length,
            separatorBuilder: (_, __) => const SizedBox(width: CcSpace.s4),
            itemBuilder: (_, i) {
              final sp = specialties[i];
              return Semantics(
                button: true,
                label: 'تخصص ${sp.label}',
                excludeSemantics: true,
                child: InkWell(
                  borderRadius: BorderRadius.circular(CcRadius.lg),
                  onTap: () {
                    s.specialtyFilter.value = sp.key;
                    s.tab.value = 1;
                  },
                  child: SizedBox(
                    width: 78,
                    child: Column(children: [
                      Container(width: 64, height: 64, padding: const EdgeInsets.all(12), decoration: BoxDecoration(color: CcColors.blue50, borderRadius: BorderRadius.circular(CcRadius.lg)), child: Image.asset(sp.icon, fit: BoxFit.contain)),
                      const SizedBox(height: 6),
                      Text(sp.label, textAlign: TextAlign.center, maxLines: 2, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700, height: 1.2)),
                    ]),
                  ),
                ),
              );
            },
          ),
        ),
        const SizedBox(height: CcSpace.s6),
        CcSectionHeader('موعدك القادم', actionLabel: next == null ? null : 'مواعيدي', onAction: () => s.tab.value = 2),
        const SizedBox(height: CcSpace.s3),
        if (next == null)
          CcCard(
            color: CcColors.blue50,
            child: Row(children: [
              const Icon(Icons.event_available_outlined, color: CcColors.blue, size: 32),
              const SizedBox(width: CcSpace.s3),
              const Expanded(child: Text('لا مواعيد قادمة. احجز موعدك الأول الآن.', style: TextStyle(fontWeight: FontWeight.w700))),
              CcButton(label: 'احجز', expand: false, onPressed: () => s.tab.value = 1),
            ]),
          )
        else
          _NextAppointment(appt: next),
        const SizedBox(height: CcSpace.s6),
        GridView.count(
          crossAxisCount: 4,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: CcSpace.s3,
          crossAxisSpacing: CcSpace.s3,
          childAspectRatio: 0.82,
          children: [
            _Quick(icon: Icons.calendar_month_outlined, label: 'احجز موعدًا', onTap: () => s.tab.value = 1),
            _Quick(icon: Icons.medication_outlined, label: 'أدويتي', onTap: () => _push(context, const MedicationsScreen())),
            _Quick(icon: Icons.science_outlined, label: 'نتائجي', onTap: () => s.tab.value = 3),
            _Quick(icon: Icons.receipt_long_outlined, label: 'فواتيري', onTap: () => _push(context, const InvoicesScreen())),
          ],
        ),
        const SizedBox(height: CcSpace.s6),
        CcSectionHeader('أطباء مميزون', actionLabel: 'عرض الكل', onAction: () {
          s.specialtyFilter.value = null;
          s.tab.value = 1;
        }),
        const SizedBox(height: CcSpace.s3),
        SizedBox(
          height: 176,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: top.length,
            separatorBuilder: (_, __) => const SizedBox(width: CcSpace.s3),
            itemBuilder: (_, i) => _FeaturedDoctor(d: top[i]),
          ),
        ),
      ],
    );
  }
}

class _Banners extends StatefulWidget {
  const _Banners();
  @override
  State<_Banners> createState() => _BannersState();
}

class _BannersState extends State<_Banners> {
  static const _items = [
    ('assets/images/home/promo-checkup.webp', 'الفحص الشامل', 'اطمئن على صحتك بباقة فحوصات كاملة'),
    ('assets/images/home/promo-teleconsult.webp', 'استشارة عن بُعد', 'تحدّث مع طبيبك من بيتك بمكالمة مرئية'),
  ];
  int _i = 0;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        SizedBox(
          height: 150,
          child: PageView.builder(
            itemCount: _items.length,
            onPageChanged: (i) => setState(() => _i = i),
            itemBuilder: (_, i) {
              final (img, title, sub) = _items[i];
              return Padding(
                padding: const EdgeInsets.symmetric(horizontal: 2),
                child: ClipRRect(
                  borderRadius: BorderRadius.circular(CcRadius.xl),
                  child: Stack(
                    fit: StackFit.expand,
                    children: [
                      Image.asset(img, fit: BoxFit.cover, excludeFromSemantics: true),
                      // تدرج أبيض خلف النص (الجهة اليسرى في RTL حيث توجد المساحة الفارغة)
                      const DecoratedBox(decoration: BoxDecoration(gradient: LinearGradient(begin: Alignment.centerLeft, end: Alignment.center, colors: [Color(0xF2FFFFFF), Color(0x00FFFFFF)]))),
                      Align(
                        alignment: AlignmentDirectional.centerEnd,
                        child: Padding(
                          padding: const EdgeInsets.all(CcSpace.s4),
                          child: SizedBox(
                            width: 150,
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(title, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16, color: CcColors.blue)),
                                const SizedBox(height: 4),
                                Text(sub, style: const TextStyle(fontSize: 12, color: CcColors.slate, height: 1.4)),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
        const SizedBox(height: CcSpace.s3),
        CcPageDots(count: _items.length, index: _i),
      ],
    );
  }
}

class _NextAppointment extends StatelessWidget {
  const _NextAppointment({required this.appt});
  final PatientAppt appt;

  @override
  Widget build(BuildContext context) {
    final s = AppScope.storeOf(context);
    final d = doctorById(appt.doctorId);
    final now = DateTime.now();
    final date = now.add(Duration(days: appt.day));
    return CcCard(
      onTap: () => s.tab.value = 2,
      padding: const EdgeInsets.all(CcSpace.s4),
      child: Column(
        children: [
          Row(children: [
            CcAvatar(name: d.name, asset: d.avatar, size: 52),
            const SizedBox(width: CcSpace.s3),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(d.name, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)), Text(d.spec.label, style: const TextStyle(color: CcColors.blue, fontWeight: FontWeight.w700, fontSize: 13))])),
            CcBadge(appt.day == 0 ? 'اليوم' : 'بعد ${appt.day} ${appt.day <= 10 ? 'أيام' : 'يومًا'}', tone: CcTone.green),
          ]),
          const SizedBox(height: CcSpace.s3),
          Container(
            padding: const EdgeInsets.all(CcSpace.s3),
            decoration: BoxDecoration(color: CcColors.blue50, borderRadius: BorderRadius.circular(CcRadius.md)),
            child: Row(children: [
              const Icon(Icons.event_rounded, size: 18, color: CcColors.blue),
              const SizedBox(width: 6),
              Expanded(child: Text('${dayLabel(date, now)} · ${fmtTime(appt.minutes)}', style: const TextStyle(fontWeight: FontWeight.w700, color: CcColors.blue))),
              Text(appt.type.label, style: const TextStyle(fontSize: 12, color: CcColors.muted)),
            ]),
          ),
        ],
      ),
    );
  }
}

class _Quick extends StatelessWidget {
  const _Quick({required this.icon, required this.label, required this.onTap});
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Semantics(
        button: true,
        label: label,
        excludeSemantics: true,
        child: InkWell(
          borderRadius: BorderRadius.circular(CcRadius.lg),
          onTap: onTap,
          child: Container(
            decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(CcRadius.lg), border: Border.all(color: CcColors.line)),
            child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
              Icon(icon, color: CcColors.blue, size: 28),
              const SizedBox(height: 6),
              Text(label, textAlign: TextAlign.center, style: const TextStyle(fontSize: 11.5, fontWeight: FontWeight.w700, height: 1.2)),
            ]),
          ),
        ),
      );
}

class _FeaturedDoctor extends StatelessWidget {
  const _FeaturedDoctor({required this.d});
  final Doctor d;

  @override
  Widget build(BuildContext context) => SizedBox(
        width: 150,
        child: Semantics(
          button: true,
          label: '${d.name}، ${d.spec.label}، تقييم ${d.rating}',
          excludeSemantics: true,
          child: CcCard(
            onTap: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => DoctorProfileScreen(doctorId: d.id))),
            padding: const EdgeInsets.all(CcSpace.s3),
            child: Column(children: [
              CcAvatar(name: d.name, asset: d.avatar, size: 64),
              const SizedBox(height: CcSpace.s2),
              Text(d.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 14)),
              Text(d.spec.label, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(color: CcColors.blue, fontSize: 12, fontWeight: FontWeight.w700)),
              const Spacer(),
              CcRating(value: d.rating),
            ]),
          ),
        ),
      );
}
