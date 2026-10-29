import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/doctors.dart';
import '../screens/doctor_profile_screen.dart';
import '../state/app_scope.dart';

/// بطاقة طبيب في القوائم: صورة + اسم + تخصص + تقييم + سعر + أقرب موعد + مفضلة.
class DoctorCard extends StatelessWidget {
  const DoctorCard({super.key, required this.doctor});
  final Doctor doctor;

  @override
  Widget build(BuildContext context) {
    final d = doctor;
    final fav = AppScope.storeOf(context).favorites;
    final isFav = fav.has(d.id);
    return Semantics(
      container: true,
      label: '${d.name}، ${d.spec.label}، تقييم ${d.rating}، الكشف ${d.price} ريال، أقرب موعد ${d.nextSlot}',
      child: CcCard(
        onTap: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => DoctorProfileScreen(doctorId: d.id))),
        padding: const EdgeInsets.all(CcSpace.s3),
        child: Column(
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                CcAvatar(name: d.name, asset: d.avatar, size: 64),
                const SizedBox(width: CcSpace.s3),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(d.name, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                      Text(d.spec.label, style: const TextStyle(color: CcColors.blue, fontWeight: FontWeight.w700, fontSize: 13)),
                      const SizedBox(height: 4),
                      Wrap(crossAxisAlignment: WrapCrossAlignment.center, spacing: CcSpace.s3, children: [CcRating(value: d.rating, count: d.reviewsCount), Text('${d.years} سنة خبرة', style: const TextStyle(fontSize: 12, color: CcColors.muted))]),
                    ],
                  ),
                ),
                IconButton(
                  tooltip: isFav ? 'إزالة ${d.name} من المفضلة' : 'إضافة ${d.name} للمفضلة',
                  onPressed: () => fav.toggle(d.id),
                  icon: Icon(isFav ? Icons.favorite_rounded : Icons.favorite_border_rounded, color: isFav ? CcColors.redText : CcColors.muted),
                ),
              ],
            ),
            const SizedBox(height: CcSpace.s3),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: CcSpace.s3, vertical: CcSpace.s2),
              decoration: BoxDecoration(color: CcColors.blue50, borderRadius: BorderRadius.circular(CcRadius.md)),
              child: Row(
                children: [
                  const Icon(Icons.schedule_rounded, size: 18, color: CcColors.blue),
                  const SizedBox(width: 6),
                  Expanded(child: Text('أقرب موعد: ${d.nextSlot}', style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: CcColors.blue))),
                  Text('${d.price} ر.س', style: const TextStyle(fontWeight: FontWeight.w800)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
