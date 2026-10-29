import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/doctors.dart';
import '../state/app_scope.dart';
import '../widgets/doctor_card.dart';

class DoctorsTab extends StatefulWidget {
  const DoctorsTab({super.key});
  @override
  State<DoctorsTab> createState() => _DoctorsTabState();
}

class _DoctorsTabState extends State<DoctorsTab> {
  final _q = TextEditingController();
  final _focus = FocusNode();
  DoctorSort _sort = DoctorSort.rating;
  bool _favOnly = false;
  PatientStore? _store;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final s = AppScope.storeOf(context);
    if (_store != s) {
      _store?.specialtyFilter.removeListener(_rebuild);
      _store?.focusSearch.removeListener(_requestFocus);
      _store = s
        ..specialtyFilter.addListener(_rebuild)
        ..focusSearch.addListener(_requestFocus);
    }
  }

  void _rebuild() => setState(() {});
  void _requestFocus() => _focus.requestFocus();

  @override
  void dispose() {
    _store?.specialtyFilter.removeListener(_rebuild);
    _store?.focusSearch.removeListener(_requestFocus);
    _q.dispose();
    _focus.dispose();
    super.dispose();
  }

  Future<void> _pickSort() async {
    final r = await showModalBottomSheet<DoctorSort>(
      context: context,
      builder: (c) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          const Padding(padding: EdgeInsets.all(CcSpace.s4), child: Text('ترتيب حسب', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800))),
          for (final s in DoctorSort.values)
            ListTile(title: Text(s.label, style: const TextStyle(fontWeight: FontWeight.w700)), trailing: s == _sort ? const Icon(Icons.check_rounded, color: CcColors.blue) : null, onTap: () => Navigator.of(c).pop(s)),
          const SizedBox(height: CcSpace.s2),
        ]),
      ),
    );
    if (r != null) setState(() => _sort = r);
  }

  @override
  Widget build(BuildContext context) {
    final store = AppScope.storeOf(context);
    final spec = store.specialtyFilter.value;
    final items = searchDoctors(query: _q.text, specialty: spec, sort: _sort, favoritesOnly: _favOnly, favorites: store.favorites.ids);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s3),
          child: Semantics(header: true, child: const Text('الأطباء', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800))),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: CcSpace.s5),
          child: TextField(
            controller: _q,
            focusNode: _focus,
            onChanged: (_) => setState(() {}),
            textInputAction: TextInputAction.search,
            decoration: InputDecoration(
              hintText: 'ابحث عن طبيب أو تخصص',
              prefixIcon: const Icon(Icons.search_rounded, color: CcColors.muted),
              suffixIcon: _q.text.isEmpty ? null : IconButton(tooltip: 'مسح البحث', icon: const Icon(Icons.close_rounded), onPressed: () => setState(_q.clear)),
            ),
          ),
        ),
        const SizedBox(height: CcSpace.s3),
        SizedBox(
          height: 44,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: CcSpace.s5),
            children: [
              ChoiceChip(label: const Text('الكل'), selected: spec == null, onSelected: (_) => store.specialtyFilter.value = null),
              for (final s in specialties) ...[const SizedBox(width: 8), ChoiceChip(label: Text(s.label), selected: spec == s.key, onSelected: (_) => store.specialtyFilter.value = s.key)],
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, 0),
          child: Wrap(
            spacing: 8,
            runSpacing: 4,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text('${items.length} طبيبًا', style: const TextStyle(color: CcColors.muted, fontWeight: FontWeight.w600)),
              FilterChip(label: const Text('المفضلة'), avatar: Icon(_favOnly ? Icons.favorite_rounded : Icons.favorite_border_rounded, size: 18, color: CcColors.redText), selected: _favOnly, onSelected: (v) => setState(() => _favOnly = v)),
              ActionChip(label: Text(_sort.label), avatar: const Icon(Icons.sort_rounded, size: 18), onPressed: _pickSort),
            ],
          ),
        ),
        Expanded(
          child: items.isEmpty
              ? CcEmptyState(
                  image: 'assets/images/empty/empty-search.webp',
                  title: _favOnly ? 'لا أطباء في المفضلة' : 'لا نتائج',
                  text: _favOnly ? 'اضغط على القلب بجانب الطبيب لإضافته.' : 'جرّب كلمة بحث أخرى أو غيّر التخصص.',
                  action: CcButton(label: 'مسح التصفية', expand: false, variant: CcButtonVariant.soft, onPressed: () {
                    _q.clear();
                    _favOnly = false;
                    store.specialtyFilter.value = null;
                    setState(() {});
                  }),
                )
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s3, CcSpace.s5, CcSpace.s8),
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: CcSpace.s3),
                  itemBuilder: (_, i) => DoctorCard(doctor: items[i]),
                ),
        ),
      ],
    );
  }
}
