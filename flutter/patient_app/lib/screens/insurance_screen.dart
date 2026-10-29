import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/profile.dart';
import '../state/app_scope.dart';
import '../utils/format.dart';

class InsuranceScreen extends StatelessWidget {
  const InsuranceScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final p = AppScope.storeOf(context).profile;
    final remain = insurance.limit - insurance.used;
    final pct = insurance.used / insurance.limit;
    return Scaffold(
      appBar: AppBar(title: const Text('التأمين الصحي'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: ListView(
        padding: const EdgeInsets.all(CcSpace.s5),
        children: [
          // بطاقة التأمين
          Container(
            padding: const EdgeInsets.all(CcSpace.s5),
            decoration: BoxDecoration(gradient: const LinearGradient(colors: [CcColors.blue, CcColors.sky], begin: Alignment.topRight, end: Alignment.bottomLeft), borderRadius: BorderRadius.circular(CcRadius.xl), boxShadow: CcShadow.card),
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(children: [
                Expanded(child: Text(insurance.insurer, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 18))),
                Container(padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4), decoration: BoxDecoration(color: Colors.white24, borderRadius: BorderRadius.circular(99)), child: Text('الفئة ${insurance.tier}', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 12))),
              ]),
              const SizedBox(height: CcSpace.s6),
              Text(isolateLtr(insurance.policy), style: const TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.w800, letterSpacing: 1)),
              const SizedBox(height: CcSpace.s3),
              Wrap(alignment: WrapAlignment.spaceBetween, spacing: CcSpace.s3, children: [Text(p.name, style: const TextStyle(color: Color(0xFFDCEBFF))), Text('ينتهي ${insurance.expiry}', style: const TextStyle(color: Color(0xFFDCEBFF), fontSize: 12))]),
            ]),
          ),
          const SizedBox(height: CcSpace.s5),
          CcCard(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              const Text('الحد السنوي', style: TextStyle(fontWeight: FontWeight.w800)),
              const SizedBox(height: CcSpace.s3),
              ClipRRect(borderRadius: BorderRadius.circular(6), child: LinearProgressIndicator(value: pct, minHeight: 10, backgroundColor: CcColors.line, color: pct > 0.85 ? CcColors.orange : CcColors.blue, semanticsLabel: 'المستهلك من الحد السنوي', semanticsValue: '${(pct * 100).round()}%')),
              const SizedBox(height: CcSpace.s2),
              Wrap(alignment: WrapAlignment.spaceBetween, spacing: CcSpace.s3, children: [Text('المستهلك ${insurance.used} ر.س', style: const TextStyle(fontSize: 12, color: CcColors.muted)), Text('المتبقي $remain ر.س', style: const TextStyle(fontWeight: FontWeight.w800, color: CcColors.greenText))]),
            ]),
          ),
          const SizedBox(height: CcSpace.s3),
          CcCard(child: Column(children: [
            _row('نسبة التحمّل', insurance.copay),
            _row('الحد السنوي', '${insurance.limit} ر.س'),
            _row('رقم الهوية', isolateLtr(p.nationalId)),
          ])),
          const SizedBox(height: CcSpace.s5),
          CcButton(label: 'عرض بطاقة التأمين للطباعة', icon: Icons.print_outlined, variant: CcButtonVariant.ghost, onPressed: () => ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('الطباعة غير متاحة في النسخة التجريبية')))),
        ],
      ),
    );
  }

  Widget _row(String k, String v) => Padding(padding: const EdgeInsets.symmetric(vertical: 6), child: Row(children: [Text(k, style: const TextStyle(color: CcColors.muted)), const Spacer(), Text(v, style: const TextStyle(fontWeight: FontWeight.w800))]));
}
