/// «الآن» بالدقائق من منتصف الليل. الافتراضي ثابت 09:15 للبيانات التجريبية والاختبارات،
/// ويُستبدل بالساعة الحقيقية عند الاتصال بالخادم ([useRealClock]).
const demoNowMinutes = 9 * 60 + 15;

int Function() _nowMinutes = () => demoNowMinutes;
int get nowMinutes => _nowMinutes();
void useRealClock() => _nowMinutes = () {
      final n = DateTime.now();
      return n.hour * 60 + n.minute;
    };
void useDemoClock() => _nowMinutes = () => demoNowMinutes;

DateTime get today {
  final n = DateTime.now();
  return DateTime(n.year, n.month, n.day);
}

/// عدد الأيام من اليوم إلى تاريخ yyyy-MM-dd (سالب للماضي).
int dayOffsetOf(String iso) {
  final p = iso.split('-').map(int.parse).toList();
  return DateTime(p[0], p[1], p[2]).difference(today).inDays;
}

/// عكس [dayOffsetOf]: yyyy-MM-dd لليوم المنسوب إلى اليوم الحالي.
String isoOfOffset(int offset) {
  final d = today.add(Duration(days: offset));
  return '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';
}
