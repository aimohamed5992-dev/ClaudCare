// ignore_for_file: text_direction_code_point_in_literal
const weekdays = ['الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت', 'الأحد'];
const months = ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'];

/// 570 → «09:30 ص»
String fmtTime(int minutes) {
  final h = minutes ~/ 60, m = minutes % 60;
  final h12 = h % 12 == 0 ? 12 : h % 12;
  return '${h12.toString().padLeft(2, '0')}:${m.toString().padLeft(2, '0')} ${h < 12 ? 'ص' : 'م'}';
}

String fmtRange(int start, int duration) => '${fmtTime(start)} – ${fmtTime(start + duration)}';

String weekdayName(DateTime d) => weekdays[d.weekday - 1];

/// اليوم / غدًا / اسم اليوم مع التاريخ
String dayLabel(DateTime d, DateTime today) {
  final diff = DateTime(d.year, d.month, d.day).difference(DateTime(today.year, today.month, today.day)).inDays;
  if (diff == 0) return 'اليوم';
  if (diff == 1) return 'غدًا';
  return '${weekdayName(d)} ${d.day} ${months[d.month - 1]}';
}

/// يعزل نصًا لاتينيًا (مثل A+ أو SCFHS-123) داخل جملة عربية حتى لا ينقلب ترتيبه.
String isolateLtr(String s) => '\u2066$s\u2069';

/// 12 أكتوبر
String fmtShortDate(DateTime d) => '${d.day} ${months[d.month - 1]}';

/// yyyy-MM-dd ← «12 أكتوبر»
String fmtIsoShort(String iso) {
  final p = iso.split('-').map(int.parse).toList();
  return fmtShortDate(DateTime(p[0], p[1], p[2]));
}

/// وقت نسبي مختصر: «قبل 20 دقيقة» / «أمس 06:00 م» / «12 أكتوبر».
String relativeTime(DateTime when, DateTime now) {
  final diff = now.difference(when);
  if (diff.inMinutes < 1) return 'الآن';
  if (diff.inMinutes < 60) return 'قبل ${diff.inMinutes} دقيقة';
  if (diff.inHours < 24 && when.day == now.day) return 'قبل ${diff.inHours} ساعة';
  final t = fmtTime(when.hour * 60 + when.minute);
  final yesterday = DateTime(now.year, now.month, now.day).subtract(const Duration(days: 1));
  if (DateTime(when.year, when.month, when.day) == yesterday) return 'أمس $t';
  return fmtShortDate(when);
}
