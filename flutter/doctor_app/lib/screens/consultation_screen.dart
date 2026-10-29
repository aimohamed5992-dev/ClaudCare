import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/appointments.dart';
import '../data/patients.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';
import '../widgets/medication_sheet.dart';

/// الكشف الطبي في ثلاث خطوات: المعاينة ← التشخيص والفحوصات ← الوصفة.
class ConsultationScreen extends StatefulWidget {
  const ConsultationScreen({super.key, required this.patientId, this.apptId});
  final int patientId;
  final int? apptId;

  @override
  State<ConsultationScreen> createState() => _ConsultationScreenState();
}

String _medsLabel(int n) => switch (n) { 0 => 'بدون أدوية', 1 => 'دواء واحد', 2 => 'دواءان', <= 10 => '$n أدوية', _ => '$n دواءً' };

class _Summary {
  const _Summary(this.diagnosis, this.meds, this.tests, this.followUp);
  final String diagnosis, followUp;
  final int meds, tests;
}

class _ConsultationScreenState extends State<ConsultationScreen> {
  static const _titles = ['المعاينة', 'التشخيص', 'الوصفة'];

  final _complaint = TextEditingController();
  final _sys = TextEditingController();
  final _dia = TextEditingController();
  final _hr = TextEditingController();
  final _temp = TextEditingController();
  final _spo2 = TextEditingController();
  final _diagnosis = TextEditingController();
  final _notes = TextEditingController();
  final _tests = <String>{};
  final _meds = <Med>[];
  String _followUp = followUps[2];

  int _step = 0;
  final _errors = <String, String?>{};
  _Summary? _done;

  List<TextEditingController> get _all => [_complaint, _sys, _dia, _hr, _temp, _spo2, _diagnosis, _notes];
  bool get _dirty => _all.any((c) => c.text.isNotEmpty) || _tests.isNotEmpty || _meds.isNotEmpty;

  bool _wasDirty = false;

  @override
  void initState() {
    super.initState();
    // نحمّل الملف الكامل (الحساسية والأدوية الحالية) قبل أن يصف الطبيب أي دواء
    WidgetsBinding.instance.addPostFrameCallback((_) => AppScope.storeOf(context).remote?.ensurePatientDetail(widget.patientId));
    // PopScope يحتاج إعادة بناء عند أول إدخال/آخر مسح فقط
    for (final c in _all) {
      c.addListener(_syncDirty);
    }
  }

  void _syncDirty() {
    if (_dirty != _wasDirty && mounted) setState(() => _wasDirty = _dirty);
  }

  @override
  void dispose() {
    for (final c in _all) {
      c.removeListener(_syncDirty);
      c.dispose();
    }
    super.dispose();
  }

  String? _range(TextEditingController c, num min, num max, String label) {
    final t = ccNormalizeDigits(c.text.trim());
    if (t.isEmpty) return null;
    final v = num.tryParse(t);
    return (v == null || v < min || v > max) ? '$label غير معقول' : null;
  }

  bool _validate() {
    final e = <String, String?>{};
    if (_step == 0) {
      e['complaint'] = _complaint.text.trim().length < 3 ? 'اكتب الشكوى الرئيسية' : null;
      e['sys'] = _range(_sys, 60, 260, 'الضغط');
      e['dia'] = _range(_dia, 30, 160, 'الضغط');
      e['hr'] = _range(_hr, 30, 220, 'النبض');
      e['temp'] = _range(_temp, 34, 42, 'الحرارة');
      e['spo2'] = _range(_spo2, 70, 100, 'التشبع');
      if ((_sys.text.isEmpty) != (_dia.text.isEmpty)) e['dia'] = 'أدخل الانقباضي والانبساطي معًا';
    } else if (_step == 1) {
      e['diagnosis'] = _diagnosis.text.trim().length < 3 ? 'اكتب التشخيص' : null;
    }
    setState(() => _errors
      ..clear()
      ..addAll(e));
    return e.values.every((v) => v == null);
  }

  void _next() {
    if (!_validate()) return;
    if (_step < 2) {
      setState(() => _step++);
    } else {
      _finish();
    }
  }

  bool _saving = false;

  Future<void> _finish() async {
    final appts = AppScope.of(context);
    final patients = AppScope.patientsOf(context);
    final p = patients.byId(widget.patientId);
    final remote = AppScope.storeOf(context).remote;
    if (remote != null) {
      if (_saving) return;
      setState(() => _saving = true);
      int? n(TextEditingController c) => c.text.trim().isEmpty ? null : int.tryParse(ccNormalizeDigits(c.text.trim()));
      try {
        await remote.saveVisit(
          patientId: p.id,
          appointmentId: widget.apptId,
          complaint: _complaint.text.trim(),
          sys: n(_sys), dia: n(_dia), hr: n(_hr), spo2: n(_spo2),
          temp: _temp.text.trim().isEmpty ? null : num.tryParse(ccNormalizeDigits(_temp.text.trim())),
          diagnosis: _diagnosis.text.trim(),
          notes: _notes.text.trim().isEmpty ? null : _notes.text.trim(),
          followUp: _followUp,
          tests: _tests,
          meds: _meds,
          patient: p,
        );
      } on ApiException catch (e) {
        if (!mounted) return;
        setState(() => _saving = false);
        // تعارض حساسية أو حالة موعد: نبقي الطبيب في الشاشة ليعدّل
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.code == 'allergy_conflict' ? 'تعارض دواء مع حساسية المريض، راجع الوصفة.' : e.userMessage)));
        return;
      }
      if (!mounted) return;
      if (widget.apptId != null) appts.finish(widget.apptId!);
      setState(() {
        _saving = false;
        _done = _Summary(_diagnosis.text.trim(), _meds.length, _tests.length, _followUp);
      });
      return;
    }
    final now = DateTime.now();
    final date = '${now.day} ${months[now.month - 1]}';
    final note = [_complaint.text.trim(), if (_notes.text.trim().isNotEmpty) _notes.text.trim()].join(' · ');
    patients.addVisit(
      p.id,
      Visit(date: date, diagnosis: _diagnosis.text.trim(), note: note),
      rx: _meds.isEmpty ? null : Rx(no: patients.nextRxNo(), date: date, meds: [for (final m in _meds) m.text]),
    );
    if (widget.apptId != null) {
      final a = appts.byId(widget.apptId!);
      if (a.status != ApptStatus.done) appts.finish(a.id);
    }
    setState(() => _done = _Summary(_diagnosis.text.trim(), _meds.length, _tests.length, _followUp));
  }

  Future<bool> _confirmExit() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('تجاهل هذا الكشف؟'),
        content: const Text('ستُفقد البيانات التي أدخلتها ولن يُحفظ الكشف.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(c).pop(false), child: const Text('متابعة الكشف')),
          TextButton(onPressed: () => Navigator.of(c).pop(true), style: TextButton.styleFrom(foregroundColor: CcColors.redText), child: const Text('تجاهل')),
        ],
      ),
    );
    return ok ?? false;
  }

  @override
  Widget build(BuildContext context) {
    final p = AppScope.patientsOf(context).byId(widget.patientId);
    if (_done != null) return _SuccessView(p: p, s: _done!);

    return PopScope(
      canPop: !_dirty,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        final nav = Navigator.of(context);
        if (await _confirmExit()) nav.pop();
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text('كشف ${p.name}'),
          leading: IconButton(tooltip: 'إغلاق', icon: const Icon(Icons.close_rounded), onPressed: () => Navigator.of(context).maybePop()),
        ),
        body: Column(
          children: [
            _Steps(step: _step, titles: _titles),
            if (p.allergies.isNotEmpty)
              Container(
                margin: const EdgeInsets.fromLTRB(CcSpace.s5, 0, CcSpace.s5, CcSpace.s2),
                padding: const EdgeInsets.symmetric(horizontal: CcSpace.s3, vertical: CcSpace.s2),
                decoration: BoxDecoration(color: CcColors.red50, borderRadius: BorderRadius.circular(CcRadius.md)),
                child: Row(children: [
                  const Icon(Icons.warning_amber_rounded, size: 18, color: CcColors.redText),
                  const SizedBox(width: CcSpace.s2),
                  Expanded(child: Text('حساسية: ${p.allergies.join('، ')}', style: const TextStyle(color: CcColors.redText, fontWeight: FontWeight.w700, fontSize: 13))),
                ]),
              ),
            Expanded(
              child: SingleChildScrollView(
                keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
                padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s3, CcSpace.s5, CcSpace.s8),
                child: switch (_step) {
                  0 => _stepExam(),
                  1 => _stepDiagnosis(p),
                  _ => _stepRx(p),
                },
              ),
            ),
          ],
        ),
        bottomNavigationBar: SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s3),
            child: Row(
              children: [
                if (_step > 0) ...[Expanded(flex: 2, child: CcButton(label: 'السابق', variant: CcButtonVariant.ghost, onPressed: () => setState(() => _step--))), const SizedBox(width: CcSpace.s3)],
                Expanded(flex: 3, child: CcButton(label: _step == 2 ? 'إنهاء الكشف' : 'التالي', icon: _step == 2 ? Icons.check_rounded : null, onPressed: _next)),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _stepExam() => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CcTextField(label: 'الشكوى الرئيسية', controller: _complaint, hint: 'مثال: ألم في الصدر منذ يومين', error: _errors['complaint'], textInputAction: TextInputAction.next),
          const SizedBox(height: CcSpace.s5),
          const CcSectionHeader('العلامات الحيوية'),
          const SizedBox(height: CcSpace.s3),
          Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Expanded(child: CcTextField(label: 'الضغط الانقباضي', controller: _sys, keyboardType: TextInputType.number, ltr: true, error: _errors['sys'])),
            const SizedBox(width: CcSpace.s3),
            Expanded(child: CcTextField(label: 'الانبساطي', controller: _dia, keyboardType: TextInputType.number, ltr: true, error: _errors['dia'])),
          ]),
          const SizedBox(height: CcSpace.s3),
          Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Expanded(child: CcTextField(label: 'النبض (/د)', controller: _hr, keyboardType: TextInputType.number, ltr: true, error: _errors['hr'])),
            const SizedBox(width: CcSpace.s3),
            Expanded(child: CcTextField(label: 'الحرارة °م', controller: _temp, keyboardType: const TextInputType.numberWithOptions(decimal: true), ltr: true, error: _errors['temp'])),
          ]),
          const SizedBox(height: CcSpace.s3),
          CcTextField(label: 'تشبع الأكسجين %', controller: _spo2, keyboardType: TextInputType.number, ltr: true, error: _errors['spo2']),
        ],
      );

  Widget _stepDiagnosis(Patient p) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CcTextField(label: 'التشخيص', controller: _diagnosis, hint: 'اكتب أو اختر من الاقتراحات', error: _errors['diagnosis']),
          if (p.conditions.isNotEmpty) ...[
            const SizedBox(height: CcSpace.s2),
            Wrap(spacing: 8, runSpacing: 4, children: [
              for (final c in p.conditions) ActionChip(label: Text(c), onPressed: () => setState(() => _diagnosis.text = c)),
            ]),
          ],
          const SizedBox(height: CcSpace.s5),
          const CcSectionHeader('طلب فحوصات'),
          const SizedBox(height: CcSpace.s3),
          Wrap(spacing: 8, runSpacing: 8, children: [
            for (final t in testOptions) FilterChip(label: Text(t), selected: _tests.contains(t), showCheckmark: true, onSelected: (v) => setState(() => v ? _tests.add(t) : _tests.remove(t))),
          ]),
          const SizedBox(height: CcSpace.s5),
          CcTextField(label: 'ملاحظات الطبيب (اختياري)', controller: _notes),
        ],
      );

  Widget _stepRx(Patient p) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          CcSectionHeader('الوصفة', actionLabel: _meds.isEmpty ? null : _medsLabel(_meds.length)),
          const SizedBox(height: CcSpace.s3),
          if (_meds.isEmpty)
            const CcCard(child: Text('لم تُضف أدوية بعد. يمكنك إنهاء الكشف دون وصفة.', style: TextStyle(color: CcColors.muted)))
          else
            for (var i = 0; i < _meds.length; i++)
              Padding(
                padding: const EdgeInsets.only(bottom: CcSpace.s2),
                child: CcCard(
                  padding: const EdgeInsets.fromLTRB(CcSpace.s3, CcSpace.s2, CcSpace.s2, CcSpace.s2),
                  child: Row(children: [
                    const Icon(Icons.medication_outlined, color: CcColors.sky),
                    const SizedBox(width: CcSpace.s3),
                    Expanded(child: Text(_meds[i].text, style: const TextStyle(fontWeight: FontWeight.w700, height: 1.5))),
                    IconButton(tooltip: 'حذف ${_meds[i].drug.name}', icon: const Icon(Icons.delete_outline_rounded, color: CcColors.redText), onPressed: () => setState(() => _meds.removeAt(i))),
                  ]),
                ),
              ),
          const SizedBox(height: CcSpace.s3),
          CcButton(
            label: 'إضافة دواء',
            icon: Icons.add_rounded,
            variant: CcButtonVariant.soft,
            onPressed: () async {
              final m = await showMedicationSheet(context, p, _meds);
              if (m != null) setState(() => _meds.add(m));
            },
          ),
          const SizedBox(height: CcSpace.s6),
          const CcSectionHeader('موعد المتابعة'),
          const SizedBox(height: CcSpace.s3),
          Wrap(spacing: 8, runSpacing: 8, children: [for (final f in followUps) ChoiceChip(label: Text(f), selected: _followUp == f, onSelected: (_) => setState(() => _followUp = f))]),
          const SizedBox(height: CcSpace.s6),
          CcCard(
            color: CcColors.blue50,
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const Text('ملخص الكشف', style: TextStyle(fontWeight: FontWeight.w800)),
              const SizedBox(height: CcSpace.s2),
              Text('التشخيص: ${_diagnosis.text.trim()}'),
              Text('الفحوصات المطلوبة: ${_tests.isEmpty ? 'لا يوجد' : _tests.join('، ')}'),
              Text('الأدوية: ${_meds.isEmpty ? 'لا يوجد' : _medsLabel(_meds.length)}'),
            ]),
          ),
        ],
      );
}

class _Steps extends StatelessWidget {
  const _Steps({required this.step, required this.titles});
  final int step;
  final List<String> titles;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'الخطوة ${step + 1} من ${titles.length}: ${titles[step]}',
      child: ExcludeSemantics(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s3),
          child: Row(
            children: [
              for (var i = 0; i < titles.length; i++)
                Expanded(
                  child: Padding(
                    padding: EdgeInsetsDirectional.only(end: i == titles.length - 1 ? 0 : 6),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Container(height: 5, decoration: BoxDecoration(color: i <= step ? CcColors.blue : CcColors.line, borderRadius: const BorderRadius.all(CcRadius.pill))),
                        const SizedBox(height: 4),
                        Text(titles[i], style: TextStyle(fontSize: 12, fontWeight: i == step ? FontWeight.w800 : FontWeight.w600, color: i == step ? CcColors.blue : CcColors.muted)),
                      ],
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _SuccessView extends StatelessWidget {
  const _SuccessView({required this.p, required this.s});
  final Patient p;
  final _Summary s;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(CcSpace.s6),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Center(
                child: Container(
                  width: 96,
                  height: 96,
                  decoration: const BoxDecoration(color: CcColors.green50, shape: BoxShape.circle),
                  child: const Icon(Icons.check_rounded, size: 52, color: CcColors.greenText, semanticLabel: 'تم'),
                ),
              ),
              const SizedBox(height: CcSpace.s5),
              Semantics(header: true, child: const Text('تم حفظ الكشف', textAlign: TextAlign.center, style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800))),
              const SizedBox(height: CcSpace.s2),
              Text(p.name, textAlign: TextAlign.center, style: const TextStyle(color: CcColors.muted, fontSize: 16)),
              const SizedBox(height: CcSpace.s6),
              CcCard(
                child: Column(children: [
                  _line('التشخيص', s.diagnosis),
                  _line('الوصفة', _medsLabel(s.meds)),
                  _line('فحوصات مطلوبة', '${s.tests}'),
                  _line('المتابعة', s.followUp),
                ]),
              ),
              const SizedBox(height: CcSpace.s6),
              CcButton(label: 'العودة', onPressed: () => Navigator.of(context).popUntil((r) => r.isFirst)),
            ],
          ),
        ),
      ),
    );
  }

  Widget _line(String k, String v) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Row(children: [Text(k, style: const TextStyle(color: CcColors.muted)), const Spacer(), Flexible(child: Text(v, textAlign: TextAlign.end, style: const TextStyle(fontWeight: FontWeight.w800)))]),
      );
}
