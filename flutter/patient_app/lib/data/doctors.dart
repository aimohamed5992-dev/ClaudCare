import 'package:flutter/foundation.dart';
import 'remote.dart';

class Specialty {
  const Specialty(this.key, this.label, this.icon);
  final String key, label, icon;
}

final List<Specialty> specialties = [
  Specialty('cardiology', 'القلب', 'assets/images/specialties/cardiology.webp'),
  Specialty('pediatrics', 'الأطفال', 'assets/images/specialties/pediatrics.webp'),
  Specialty('dermatology', 'الجلدية', 'assets/images/specialties/dermatology.webp'),
  Specialty('orthopedics', 'العظام', 'assets/images/specialties/orthopedics.webp'),
  Specialty('neurology', 'الأعصاب', 'assets/images/specialties/neurology.webp'),
  Specialty('obgyn', 'النساء والولادة', 'assets/images/specialties/obgyn.webp'),
  Specialty('ophthalmology', 'العيون', 'assets/images/specialties/ophthalmology.webp'),
  Specialty('ent', 'أنف وأذن وحنجرة', 'assets/images/specialties/ent.webp'),
  Specialty('dentistry', 'الأسنان', 'assets/images/specialties/dentistry.webp'),
  Specialty('nutrition', 'التغذية', 'assets/images/specialties/nutrition.webp'),
];

Specialty specialtyOf(String key) => specialties.firstWhere((s) => s.key == key, orElse: () => Specialty(key, key, 'assets/images/specialties/cardiology.webp'));

class Review {
  const Review(this.name, this.rating, this.text, this.when);
  final String name, text, when;
  final int rating;
}

class Doctor {
  const Doctor({
    required this.id,
    required this.name,
    required this.specialty,
    required this.avatar,
    required this.rating,
    required this.reviewsCount,
    required this.years,
    required this.patients,
    required this.price,
    required this.nextSlot,
    required this.about,
    this.branch = 'الفرع الرئيسي',
    this.videoPrice,
    this.days = const ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس'],
    this.hours = '08:00 ص – 04:00 م',
    this.languages = const ['العربية', 'الإنجليزية'],
    this.reviews = const [],
  });

  final int id;
  final String name, specialty, avatar, nextSlot, about, branch, hours;
  final double rating;
  final int reviewsCount, years, patients, price;
  final int? videoPrice;
  final List<String> days, languages;
  final List<Review> reviews;

  Specialty get spec => specialtyOf(specialty);

  /// تفاصيل الملف الشخصي القادمة من الخادم (النبذة والأيام والتقييمات…).
  Doctor withDetail({required String about, required String branch, required List<String> days, required String hours, required List<String> languages, required int patients, required List<Review> reviews}) =>
      Doctor(id: id, name: name, specialty: specialty, avatar: avatar, rating: rating, reviewsCount: reviewsCount, years: years, patients: patients, price: price, nextSlot: nextSlot, about: about,
          branch: branch, videoPrice: videoPrice, days: days, hours: hours, languages: languages, reviews: reviews);
}

const _a = 'assets/images/avatars/';

List<Doctor> doctors = const [
  Doctor(
    id: 1, name: 'د. منى القاسم', specialty: 'cardiology', avatar: '${_a}doc-f1.webp', rating: 4.9, reviewsCount: 312, years: 12, patients: 2400, price: 300, videoPrice: 200, nextSlot: 'اليوم 04:30 م',
    about: 'استشارية أمراض القلب والأوعية الدموية، متخصصة في ارتفاع ضغط الدم وقصور القلب والوقاية من أمراض الشرايين.',
    reviews: [Review('محمد ع.', 5, 'شرحت حالتي بوضوح وبصبر. أنصح بها بشدة.', 'قبل أسبوع'), Review('سارة أ.', 5, 'دقيقة ومنظمة والانتظار كان قصيرًا.', 'قبل 3 أسابيع'), Review('عبدالله ن.', 4, 'تعامل ممتاز، لكن الموعد تأخر 15 دقيقة.', 'قبل شهر')],
  ),
  Doctor(
    id: 2, name: 'د. عمر الشامي', specialty: 'neurology', avatar: '${_a}doc-m1.webp', rating: 4.8, reviewsCount: 204, years: 15, patients: 3100, price: 350, videoPrice: 250, nextSlot: 'غدًا 10:00 ص',
    about: 'استشاري أمراض الدماغ والأعصاب، خبرة واسعة في الصداع النصفي والصرع واضطرابات النوم.',
    reviews: [Review('نورة س.', 5, 'خبير ومطمئن في تعامله.', 'قبل أسبوعين')],
  ),
  Doctor(
    id: 3, name: 'د. ريم العتيبي', specialty: 'pediatrics', avatar: '${_a}doc-f2.webp', rating: 4.9, reviewsCount: 458, years: 9, patients: 5200, price: 250, videoPrice: 150, nextSlot: 'اليوم 05:15 م',
    about: 'أخصائية طب الأطفال وحديثي الولادة، تهتم بالتطعيمات والنمو والتغذية السليمة.',
    reviews: [Review('هند م.', 5, 'ابني ارتاح لها كثيرًا.', 'قبل 5 أيام'), Review('ياسر ع.', 5, 'صبورة وتشرح للأهل بالتفصيل.', 'قبل شهر')],
  ),
  Doctor(
    id: 4, name: 'د. هالة منصور', specialty: 'dermatology', avatar: '${_a}doc-f3.webp', rating: 4.7, reviewsCount: 176, years: 8, patients: 1900, price: 280, videoPrice: 180, nextSlot: 'بعد غد 11:30 ص',
    about: 'أخصائية الأمراض الجلدية والتجميل الطبي، متخصصة في حب الشباب والأكزيما وأمراض الشعر.',
  ),
  Doctor(
    id: 5, name: 'د. زياد النجار', specialty: 'orthopedics', avatar: '${_a}doc-m2.webp', rating: 4.8, reviewsCount: 233, years: 14, patients: 2800, price: 320, nextSlot: 'غدًا 02:00 م',
    about: 'استشاري جراحة العظام والمفاصل، متخصص في إصابات الملاعب وتنظير الركبة والكتف.',
    days: ['الأحد', 'الاثنين', 'الأربعاء', 'الخميس'],
  ),
  Doctor(
    id: 6, name: 'د. سامي الراشد', specialty: 'ophthalmology', avatar: '${_a}doc-m3.webp', rating: 4.6, reviewsCount: 142, years: 10, patients: 2200, price: 260, videoPrice: 160, nextSlot: 'اليوم 06:00 م',
    about: 'أخصائي طب وجراحة العيون، يعالج المياه البيضاء وجفاف العين وأخطاء الانكسار.',
  ),
  Doctor(
    id: 7, name: 'د. لينا الحربي', specialty: 'obgyn', avatar: '${_a}doc-f2.webp', rating: 4.9, reviewsCount: 389, years: 13, patients: 4100, price: 330, videoPrice: 220, nextSlot: 'الأحد 09:00 ص',
    about: 'استشارية النساء والولادة، متخصصة في متابعة الحمل وعلاج العقم والمناظير النسائية.',
  ),
  Doctor(
    id: 8, name: 'د. ياسر العمر', specialty: 'ent', avatar: '${_a}doc-m1.webp', rating: 4.5, reviewsCount: 98, years: 7, patients: 1300, price: 240, nextSlot: 'غدًا 12:30 م',
    about: 'أخصائي أنف وأذن وحنجرة، يعالج التهاب اللوزتين والجيوب الأنفية واضطرابات السمع.',
  ),
];

Doctor doctorById(int id) => doctors.firstWhere((d) => d.id == id, orElse: () => Doctor(id: id, name: 'طبيب', specialty: 'cardiology', avatar: '${_a}doc-m1.webp', rating: 0, reviewsCount: 0, years: 0, patients: 0, price: 0, nextSlot: '', about: ''));

enum DoctorSort {
  rating('الأعلى تقييمًا'),
  price('الأقل سعرًا'),
  experience('الأكثر خبرة');

  const DoctorSort(this.label);
  final String label;
}

/// بحث وتصفية الأطباء (منطق خالص قابل للاختبار).
List<Doctor> searchDoctors({String query = '', String? specialty, DoctorSort sort = DoctorSort.rating, bool favoritesOnly = false, Set<int> favorites = const {}}) {
  final q = query.trim();
  final l = doctors.where((d) {
    if (specialty != null && d.specialty != specialty) return false;
    if (favoritesOnly && !favorites.contains(d.id)) return false;
    return q.isEmpty || d.name.contains(q) || d.spec.label.contains(q) || d.about.contains(q);
  }).toList();
  l.sort((a, b) => switch (sort) {
        DoctorSort.rating => b.rating.compareTo(a.rating),
        DoctorSort.price => a.price.compareTo(b.price),
        DoctorSort.experience => b.years.compareTo(a.years),
      });
  return l;
}

class FavoritesController extends ChangeNotifier {
  FavoritesController({Set<int>? initial}) : _ids = initial ?? {1};
  final Set<int> _ids;
  PatientRemote? remote;
  Set<int> get ids => Set.unmodifiable(_ids);
  bool has(int id) => _ids.contains(id);
  void replaceAll(Iterable<int> ids) {
    _ids
      ..clear()
      ..addAll(ids);
    notifyListeners();
  }

  void toggle(int id) {
    final adding = !_ids.contains(id);
    adding ? _ids.add(id) : _ids.remove(id);
    notifyListeners();
    remote?.setFavorite(id, adding);
  }
}
