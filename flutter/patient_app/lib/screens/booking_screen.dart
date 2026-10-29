import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/appointments.dart';
import '../data/doctors.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';

const _reasons = ['كشف جديد', 'متابعة', 'نتائج تحاليل', 'ألم أو أعراض', 'تجديد وصفة'];
const _payments = ['بطاقة (مدى/فيزا)', 'Apple Pay', 'الدفع في العيادة'];
const insuranceCopay = 0.2;

/// حجز موعد: النوع والوقت ← البيانات ← المراجعة والدفع ← النجاح.
class BookingScreen extends StatefulWidget {
  const BookingScreen({super.key, required this.doctorId, this.rescheduleApptId});
  final int doctorId;
  final int? rescheduleApptId;

  @override
  State<BookingScreen> createState() => _BookingScreenState();
}

class _BookingScreenState extends State<BookingScreen> {
  static const _titles = ['الوقت', 'البيانات', 'الدفع'];
  int _step = 0;
  VisitType _type = VisitType.clinic;
  int? _day, _slot;
  String _who = 'أنا';
  String? _reason;
  final _notes = TextEditingController();
  bool _insured = true;
  String _payment = _payments[0];
  final _errors = <String, String?>{};
  int? _bookedId;
  bool _busy = false;

  Doctor get _doctor => doctorById(widget.doctorId);

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadAvailability());
  }

  /// الخانات الحقيقية من الخادم لنوع الزيارة الحالي (عند الاتصال).
  Future<void> _loadAvailability() async {
    final remote = AppScope.storeOf(context).remote;
    if (remote == null) return;
    try {
      await remote.loadAvailability(widget.doctorId, _type);
    } on ApiException catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
    }
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _notes.dispose();
    super.dispose();
  }

  int get _base => priceFor(_doctor, _type);
  int get _coverage => _insured ? (_base * (1 - insuranceCopay)).round() : 0;
  int get _due => _base - _coverage;

  bool _validate() {
    final e = <String, String?>{};
    if (_step == 0) {
      e['time'] = (_day == null || _slot == null) ? 'اختر اليوم والوقت' : null;
    } else if (_step == 1) {
      e['reason'] = _reason == null ? 'اختر سبب الزيارة' : null;
    }
    setState(() => _errors
      ..clear()
      ..addAll(e));
    return e.values.every((v) => v == null);
  }

  Future<void> _next() async {
    if (!_validate()) return;
    if (_step < 2) {
      setState(() => _step++);
      return;
    }
    final store = AppScope.storeOf(context);
    setState(() => _busy = true);
    final reason = [_reason!, if (_notes.text.trim().isNotEmpty) _notes.text.trim()].join(' – ');
    final int id;
    if (store.remote != null) {
      try {
        id = await store.appointments.bookRemote(doctor: _doctor, day: _day!, minutes: _slot!, type: _type, forWhom: _who, reason: reason, payment: _payment, insured: _insured);
      } on ApiException catch (e) {
        if (!mounted) return;
        setState(() => _busy = false);
        // الخانة أُخذت للتو: نعيد التحميل ونعود لاختيار الوقت
        if (e.code == 'slot_taken' || e.code == 'slot_invalid') {
          setState(() {
            _step = 0;
            _slot = null;
          });
          _loadAvailability();
        }
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
        return;
      }
    } else {
      await Future<void>.delayed(const Duration(milliseconds: 900));
      if (!mounted) return;
      id = store.appointments.book(doctor: _doctor, day: _day!, minutes: _slot!, type: _type, forWhom: _who, reason: reason, price: _due, paid: _payment != _payments[2] && _due > 0);
    }
    if (!mounted) return;
    setState(() {
      _busy = false;
      _bookedId = id;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_bookedId != null) return _Success(apptId: _bookedId!);
    final d = _doctor;
    final store = AppScope.storeOf(context);

    return Scaffold(
      appBar: AppBar(title: const Text('حجز موعد'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => _step > 0 ? setState(() => _step--) : Navigator.of(context).maybePop())),
      body: Column(
        children: [
          _DoctorStrip(d: d),
          _Steps(step: _step, titles: _titles),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s3, CcSpace.s5, CcSpace.s8),
              child: switch (_step) {
                0 => _stepTime(d, store),
                1 => _stepDetails(),
                _ => _stepPay(d),
              },
            ),
          ),
        ],
      ),
      bottomNavigationBar: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s3),
          child: CcButton(label: _step == 2 ? (_due == 0 ? 'تأكيد الحجز' : 'تأكيد الحجز والدفع') : 'التالي', icon: _step == 2 ? Icons.check_rounded : null, loading: _busy, onPressed: _next),
        ),
      ),
    );
  }

  Widget _stepTime(Doctor d, PatientStore store) {
    final days = store.appointments.bookableDays(d);
    final base = DateTime.now();
    final slots = _day == null ? <int>[] : store.appointments.slotsFor(d, _day!);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (d.videoPrice != null) ...[
          const CcSectionHeader('نوع الزيارة'),
          const SizedBox(height: CcSpace.s3),
          Row(children: [
            for (final t in VisitType.values)
              Expanded(
                child: Padding(
                  padding: EdgeInsetsDirectional.only(end: t == VisitType.clinic ? CcSpace.s3 : 0),
                  child: _TypeTile(icon: t.isVideo ? Icons.videocam_outlined : Icons.local_hospital_outlined, title: t.label, price: priceFor(d, t), selected: _type == t, onTap: () {
                    setState(() {
                      _type = t;
                      _day = _slot = null;
                    });
                    _loadAvailability();
                  }),
                ),
              ),
          ]),
          const SizedBox(height: CcSpace.s6),
        ],
        const CcSectionHeader('اختر اليوم'),
        const SizedBox(height: CcSpace.s3),
        if (days.isEmpty)
          const Text('لا توجد مواعيد متاحة خلال أسبوعين.', style: TextStyle(color: CcColors.muted))
        else
          SizedBox(
            height: 108,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              itemCount: days.length,
              separatorBuilder: (_, __) => const SizedBox(width: 8),
              itemBuilder: (_, i) {
                final off = days[i];
                final date = base.add(Duration(days: off));
                final sel = _day == off;
                return Semantics(
                  button: true,
                  selected: sel,
                  label: dayLabel(date, base),
                  excludeSemantics: true,
                  child: InkWell(
                    borderRadius: BorderRadius.circular(CcRadius.lg),
                    onTap: () => setState(() {
                      _day = off;
                      _slot = null;
                      _errors['time'] = null;
                    }),
                    child: Container(
                      width: 66,
                      decoration: BoxDecoration(color: sel ? CcColors.blue : Colors.white, borderRadius: BorderRadius.circular(CcRadius.lg), border: Border.all(color: sel ? CcColors.blue : CcColors.line)),
                      child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
                        Text(off == 0 ? 'اليوم' : weekdayName(date), style: TextStyle(fontSize: 12, color: sel ? Colors.white70 : CcColors.muted)),
                        Text('${date.day}', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800, color: sel ? Colors.white : CcColors.slate)),
                        Text(months[date.month - 1], style: TextStyle(fontSize: 11, color: sel ? Colors.white70 : CcColors.muted)),
                      ]),
                    ),
                  ),
                );
              },
            ),
          ),
        const SizedBox(height: CcSpace.s6),
        const CcSectionHeader('اختر الوقت'),
        const SizedBox(height: CcSpace.s3),
        if (_day == null)
          const Text('اختر يومًا أولًا لعرض الأوقات المتاحة.', style: TextStyle(color: CcColors.muted))
        else
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [for (final s in slots) ChoiceChip(label: Text(fmtTime(s)), selected: _slot == s, onSelected: (_) => setState(() {
                  _slot = s;
                  _errors['time'] = null;
                }))],
          ),
        if (_errors['time'] != null) ...[const SizedBox(height: CcSpace.s3), CcNotice(_errors['time']!)],
      ],
    );
  }

  Widget _stepDetails() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const CcSectionHeader('لمن الموعد؟'),
          const SizedBox(height: CcSpace.s3),
          Wrap(spacing: 8, runSpacing: 8, children: [for (final f in AppScope.storeOf(context).family.bookingNames) ChoiceChip(label: Text(f), selected: _who == f, onSelected: (_) => setState(() => _who = f))]),
          const SizedBox(height: CcSpace.s6),
          const CcSectionHeader('سبب الزيارة'),
          const SizedBox(height: CcSpace.s3),
          Wrap(spacing: 8, runSpacing: 8, children: [
            for (final r in _reasons)
              ChoiceChip(label: Text(r), selected: _reason == r, onSelected: (_) => setState(() {
                    _reason = r;
                    _errors['reason'] = null;
                  })),
          ]),
          if (_errors['reason'] != null) ...[const SizedBox(height: CcSpace.s3), CcNotice(_errors['reason']!)],
          const SizedBox(height: CcSpace.s5),
          CcTextField(label: 'ملاحظات للطبيب (اختياري)', controller: _notes, maxLength: 200),
        ],
      );

  Widget _stepPay(Doctor d) {
    final base = DateTime.now();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        CcCard(
          color: CcColors.blue50,
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [CcAvatar(name: d.name, asset: d.avatar, size: 44), const SizedBox(width: CcSpace.s3), Expanded(child: Text(d.name, style: const TextStyle(fontWeight: FontWeight.w800))), CcBadge(_type.label)]),
            const SizedBox(height: CcSpace.s3),
            _row('الموعد', '${dayLabel(base.add(Duration(days: _day!)), base)} · ${fmtTime(_slot!)}'),
            _row('المريض', _who),
            _row('السبب', _reason!),
          ]),
        ),
        const SizedBox(height: CcSpace.s5),
        SwitchListTile(
          value: _insured,
          onChanged: (v) => setState(() => _insured = v),
          activeTrackColor: CcColors.blue,
          contentPadding: EdgeInsets.zero,
          title: const Text('استخدام التأمين الصحي', style: TextStyle(fontWeight: FontWeight.w700)),
          subtitle: const Text('التعاونية – الفئة الذهبية (تحمّل المريض 20%)', style: TextStyle(fontSize: 12)),
        ),
        const SizedBox(height: CcSpace.s3),
        CcCard(
          child: Column(children: [
            _row('سعر الكشف', '$_base ر.س'),
            if (_insured) _row('يغطيه التأمين', '$_coverage ر.س', color: CcColors.greenText),
            const Divider(),
            _row('المستحق', '$_due ر.س', bold: true),
          ]),
        ),
        const SizedBox(height: CcSpace.s5),
        if (_due > 0) ...[
          const CcSectionHeader('طريقة الدفع'),
          const SizedBox(height: CcSpace.s2),
          for (final p in _payments)
            // ignore: deprecated_member_use
            RadioListTile<String>(value: p, groupValue: _payment, onChanged: (v) => setState(() => _payment = v ?? _payment), title: Text(p, style: const TextStyle(fontWeight: FontWeight.w700)), contentPadding: EdgeInsets.zero, activeColor: CcColors.blue),
        ] else
          const CcNotice('الكشف مغطى بالكامل ولا يلزم دفع.', error: false),
        const SizedBox(height: CcSpace.s3),
        const Text('يمكن إلغاء الموعد مجانًا قبل 24 ساعة من موعده.', style: TextStyle(fontSize: 12, color: CcColors.muted)),
      ],
    );
  }

  Widget _row(String k, String v, {bool bold = false, Color? color}) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(children: [
          Text(k, style: const TextStyle(color: CcColors.muted)),
          const Spacer(),
          Flexible(child: Text(v, textAlign: TextAlign.end, style: TextStyle(fontWeight: bold ? FontWeight.w800 : FontWeight.w700, fontSize: bold ? 17 : 14, color: color))),
        ]),
      );
}

class _DoctorStrip extends StatelessWidget {
  const _DoctorStrip({required this.d});
  final Doctor d;
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s2),
        child: Row(children: [
          CcAvatar(name: d.name, asset: d.avatar, size: 44),
          const SizedBox(width: CcSpace.s3),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(d.name, style: const TextStyle(fontWeight: FontWeight.w800)), Text(d.spec.label, style: const TextStyle(color: CcColors.blue, fontSize: 12, fontWeight: FontWeight.w700))])),
          CcRating(value: d.rating),
        ]),
      );
}

class _Steps extends StatelessWidget {
  const _Steps({required this.step, required this.titles});
  final int step;
  final List<String> titles;
  @override
  Widget build(BuildContext context) => Semantics(
        label: 'الخطوة ${step + 1} من ${titles.length}: ${titles[step]}',
        child: ExcludeSemantics(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s2),
            child: Row(children: [
              for (var i = 0; i < titles.length; i++)
                Expanded(
                  child: Padding(
                    padding: EdgeInsetsDirectional.only(end: i == titles.length - 1 ? 0 : 6),
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Container(height: 5, decoration: BoxDecoration(color: i <= step ? CcColors.blue : CcColors.line, borderRadius: const BorderRadius.all(CcRadius.pill))),
                      const SizedBox(height: 4),
                      Text(titles[i], style: TextStyle(fontSize: 12, fontWeight: i == step ? FontWeight.w800 : FontWeight.w600, color: i == step ? CcColors.blue : CcColors.muted)),
                    ]),
                  ),
                ),
            ]),
          ),
        ),
      );
}

class _TypeTile extends StatelessWidget {
  const _TypeTile({required this.icon, required this.title, required this.price, required this.selected, required this.onTap});
  final IconData icon;
  final String title;
  final int price;
  final bool selected;
  final VoidCallback onTap;
  @override
  Widget build(BuildContext context) => Semantics(
        button: true,
        selected: selected,
        label: '$title، $price ريال',
        excludeSemantics: true,
        child: InkWell(
          borderRadius: BorderRadius.circular(CcRadius.lg),
          onTap: onTap,
          child: Container(
            padding: const EdgeInsets.all(CcSpace.s3),
            decoration: BoxDecoration(color: selected ? CcColors.blue50 : Colors.white, borderRadius: BorderRadius.circular(CcRadius.lg), border: Border.all(color: selected ? CcColors.blue : CcColors.line, width: selected ? 2 : 1)),
            child: Column(children: [Icon(icon, color: CcColors.blue), const SizedBox(height: 4), Text(title, textAlign: TextAlign.center, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)), Text('$price ر.س', style: const TextStyle(color: CcColors.muted, fontSize: 12))]),
          ),
        ),
      );
}

class _Success extends StatelessWidget {
  const _Success({required this.apptId});
  final int apptId;

  @override
  Widget build(BuildContext context) {
    final store = AppScope.storeOf(context);
    final a = store.appointments.byId(apptId);
    final d = doctorById(a.doctorId);
    final base = DateTime.now();
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(CcSpace.s6),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Image.asset('assets/images/illustrations/booking-success.webp', height: 220, fit: BoxFit.contain, excludeFromSemantics: true),
              const SizedBox(height: CcSpace.s4),
              Semantics(header: true, child: const Text('تم تأكيد حجزك', textAlign: TextAlign.center, style: TextStyle(fontSize: 26, fontWeight: FontWeight.w800))),
              const SizedBox(height: CcSpace.s2),
              Text('${d.name} · ${dayLabel(base.add(Duration(days: a.day)), base)} · ${fmtTime(a.minutes)}', textAlign: TextAlign.center, style: const TextStyle(color: CcColors.muted, height: 1.6)),
              const SizedBox(height: CcSpace.s2),
              CcBadge(a.price == 0 ? 'مغطى بالتأمين' : (a.paid ? 'تم دفع ${a.price} ر.س' : 'الدفع في العيادة: ${a.price} ر.س'), tone: CcTone.green),
              const SizedBox(height: CcSpace.s8),
              CcButton(
                label: 'عرض مواعيدي',
                onPressed: () {
                  store.tab.value = 2;
                  Navigator.of(context).popUntil((r) => r.isFirst);
                },
              ),
              const SizedBox(height: CcSpace.s3),
              CcButton(label: 'إضافة إلى التقويم', icon: Icons.event_outlined, variant: CcButtonVariant.ghost, onPressed: () => ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('إضافة التقويم غير متاحة في النسخة التجريبية')))),
              const SizedBox(height: CcSpace.s3),
              TextButton(
                onPressed: () {
                  store.tab.value = 0;
                  Navigator.of(context).popUntil((r) => r.isFirst);
                },
                child: const Text('العودة للرئيسية', style: TextStyle(fontWeight: FontWeight.w700)),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
