import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/doctors.dart';
import '../state/app_scope.dart';
import 'booking_screen.dart';

class DoctorProfileScreen extends StatelessWidget {
  const DoctorProfileScreen({super.key, required this.doctorId});
  final int doctorId;

  @override
  Widget build(BuildContext context) {
    final d = doctorById(doctorId);
    AppScope.storeOf(context).remote?.ensureDoctorDetail(doctorId);
    final fav = AppScope.storeOf(context).favorites;
    final isFav = fav.has(d.id);

    return Scaffold(
      appBar: AppBar(
        title: const Text('ملف الطبيب'),
        leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop()),
        actions: [
          IconButton(
            tooltip: isFav ? 'إزالة من المفضلة' : 'إضافة للمفضلة',
            onPressed: () => fav.toggle(d.id),
            icon: Icon(isFav ? Icons.favorite_rounded : Icons.favorite_border_rounded, color: isFav ? CcColors.redText : CcColors.slate),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s8),
        children: [
          Row(
            children: [
              CcAvatar(name: d.name, asset: d.avatar, size: 84),
              const SizedBox(width: CcSpace.s4),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Semantics(header: true, child: Text(d.name, style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800))),
                    Text(d.spec.label, style: const TextStyle(color: CcColors.blue, fontWeight: FontWeight.w700, fontSize: 15)),
                    const SizedBox(height: 4),
                    Text('مستشفى ClaudCare · ${d.branch}', style: const TextStyle(fontSize: 12, color: CcColors.muted)),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: CcSpace.s5),
          Row(
            children: [
              Expanded(child: _Stat(icon: Icons.star_rounded, value: d.rating.toStringAsFixed(1), label: '${d.reviewsCount} تقييم', color: CcColors.orange)),
              const SizedBox(width: CcSpace.s3),
              Expanded(child: _Stat(icon: Icons.workspace_premium_outlined, value: '${d.years}', label: 'سنة خبرة', color: CcColors.blue)),
              const SizedBox(width: CcSpace.s3),
              Expanded(child: _Stat(icon: Icons.people_alt_outlined, value: '${d.patients}+', label: 'مريض', color: CcColors.greenText)),
            ],
          ),
          const SizedBox(height: CcSpace.s6),
          const CcSectionHeader('نبذة'),
          const SizedBox(height: CcSpace.s2),
          Text(d.about, style: const TextStyle(color: CcColors.muted, height: 1.8)),
          const SizedBox(height: CcSpace.s6),
          const CcSectionHeader('الخدمات والأسعار'),
          const SizedBox(height: CcSpace.s3),
          _Price(icon: Icons.local_hospital_outlined, title: 'كشف في العيادة', price: d.price),
          if (d.videoPrice != null) ...[const SizedBox(height: CcSpace.s2), _Price(icon: Icons.videocam_outlined, title: 'استشارة عن بُعد', price: d.videoPrice!)],
          const SizedBox(height: CcSpace.s6),
          const CcSectionHeader('أوقات الدوام'),
          const SizedBox(height: CcSpace.s3),
          Wrap(spacing: 8, runSpacing: 8, children: [for (final day in d.days) CcBadge(day)]),
          const SizedBox(height: CcSpace.s2),
          Text(d.hours, style: const TextStyle(color: CcColors.muted)),
          const SizedBox(height: CcSpace.s6),
          const CcSectionHeader('اللغات'),
          const SizedBox(height: CcSpace.s2),
          Text(d.languages.join(' · '), style: const TextStyle(color: CcColors.muted)),
          const SizedBox(height: CcSpace.s6),
          CcSectionHeader('التقييمات', actionLabel: d.reviews.isEmpty ? null : '${d.reviewsCount} تقييم'),
          const SizedBox(height: CcSpace.s3),
          if (d.reviews.isEmpty) const Text('لا توجد تقييمات مكتوبة بعد.', style: TextStyle(color: CcColors.muted)),
          for (final r in d.reviews)
            Padding(
              padding: const EdgeInsets.only(bottom: CcSpace.s3),
              child: CcCard(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Row(children: [
                    Expanded(child: Text(r.name, style: const TextStyle(fontWeight: FontWeight.w800))),
                    CcRating(value: r.rating.toDouble()),
                  ]),
                  const SizedBox(height: 4),
                  Text(r.text, style: const TextStyle(color: CcColors.muted, height: 1.6)),
                  const SizedBox(height: 4),
                  Text(r.when, style: const TextStyle(fontSize: 11, color: CcColors.muted)),
                ]),
              ),
            ),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Container(
          padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s3, CcSpace.s5, CcSpace.s3),
          decoration: const BoxDecoration(border: Border(top: BorderSide(color: CcColors.line))),
          child: Row(
            children: [
              Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [const Text('الكشف', style: TextStyle(fontSize: 12, color: CcColors.muted)), Text('${d.price} ر.س', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800))],
              ),
              const SizedBox(width: CcSpace.s5),
              Expanded(
                child: CcButton(
                  label: 'احجز موعدًا',
                  icon: Icons.event_available_rounded,
                  onPressed: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => BookingScreen(doctorId: d.id))),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.icon, required this.value, required this.label, required this.color});
  final IconData icon;
  final String value, label;
  final Color color;

  @override
  Widget build(BuildContext context) => Semantics(
        label: '$label: $value',
        excludeSemantics: true,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: CcSpace.s3),
          decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(CcRadius.lg), border: Border.all(color: CcColors.line)),
          child: Column(children: [Icon(icon, color: color), const SizedBox(height: 2), Text(value, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 17)), Text(label, style: const TextStyle(fontSize: 11, color: CcColors.muted))]),
        ),
      );
}

class _Price extends StatelessWidget {
  const _Price({required this.icon, required this.title, required this.price});
  final IconData icon;
  final String title;
  final int price;

  @override
  Widget build(BuildContext context) => CcCard(
        padding: const EdgeInsets.symmetric(horizontal: CcSpace.s4, vertical: CcSpace.s3),
        child: Row(children: [
          Icon(icon, color: CcColors.blue),
          const SizedBox(width: CcSpace.s3),
          Expanded(child: Text(title, style: const TextStyle(fontWeight: FontWeight.w700))),
          Text('$price ر.س', style: const TextStyle(fontWeight: FontWeight.w800)),
        ]),
      );
}
