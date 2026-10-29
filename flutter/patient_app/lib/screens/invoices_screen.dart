import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/invoices.dart';
import '../state/app_scope.dart';

const _methods = ['بطاقة (مدى/فيزا)', 'Apple Pay', 'تحويل بنكي'];

class InvoicesScreen extends StatefulWidget {
  const InvoicesScreen({super.key});
  @override
  State<InvoicesScreen> createState() => _InvoicesScreenState();
}

class _InvoicesScreenState extends State<InvoicesScreen> {
  InvStatus? _f;

  @override
  Widget build(BuildContext context) {
    final c = AppScope.storeOf(context).invoices;
    final items = c.all.where((i) => _f == null || i.status == _f).toList();

    return Scaffold(
      appBar: AppBar(title: const Text('فواتيري'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: ListView(
        padding: const EdgeInsets.all(CcSpace.s5),
        children: [
          CcCard(
            color: c.totalDue > 0 ? CcColors.orange50 : CcColors.green50,
            child: Row(children: [
              Icon(c.totalDue > 0 ? Icons.receipt_long_rounded : Icons.check_circle_rounded, color: c.totalDue > 0 ? CcColors.amberText : CcColors.greenText, size: 32),
              const SizedBox(width: CcSpace.s3),
              Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(c.totalDue > 0 ? 'المستحق عليك' : 'لا مستحقات', style: const TextStyle(color: CcColors.muted)), if (c.totalDue > 0) Text('${c.totalDue} ر.س', style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w800))])),
              if (c.unpaid.isNotEmpty) Text('${c.unpaid.length} ${c.unpaid.length <= 2 ? 'فاتورة' : 'فواتير'}', style: const TextStyle(fontWeight: FontWeight.w700)),
            ]),
          ),
          const SizedBox(height: CcSpace.s4),
          Wrap(spacing: 8, runSpacing: 8, children: [
            ChoiceChip(label: const Text('الكل'), selected: _f == null, onSelected: (_) => setState(() => _f = null)),
            for (final s in InvStatus.values) ChoiceChip(label: Text(s.label), selected: _f == s, onSelected: (_) => setState(() => _f = s)),
          ]),
          const SizedBox(height: CcSpace.s3),
          if (items.isEmpty) const CcEmptyState(icon: Icons.receipt_long_outlined, title: 'لا فواتير'),
          for (final i in items) Padding(padding: const EdgeInsets.only(bottom: CcSpace.s3), child: _InvoiceTile(i: i)),
        ],
      ),
    );
  }
}

class _InvoiceTile extends StatelessWidget {
  const _InvoiceTile({required this.i});
  final Invoice i;

  @override
  Widget build(BuildContext context) {
    final tone = switch (i.status) { InvStatus.unpaid => CcTone.orange, InvStatus.paid => CcTone.green, InvStatus.covered => CcTone.blue };
    return Semantics(
      button: true,
      label: '${i.title}، ${i.date}، ${i.status.label}، ${i.due == 0 ? 'لا مبلغ مستحق' : '${i.due} ريال'}',
      excludeSemantics: true,
      child: CcCard(
        onTap: () => showInvoiceSheet(context, i.id),
        padding: const EdgeInsets.all(CcSpace.s3),
        child: Row(children: [
          Container(width: 44, height: 44, decoration: BoxDecoration(color: CcColors.blue50, borderRadius: BorderRadius.circular(CcRadius.md)), child: const Icon(Icons.receipt_long_outlined, color: CcColors.blue)),
          const SizedBox(width: CcSpace.s3),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(i.title, style: const TextStyle(fontWeight: FontWeight.w800)), Text('${i.no} · ${i.date}', textDirection: TextDirection.rtl, style: const TextStyle(fontSize: 12, color: CcColors.muted))])),
          Column(crossAxisAlignment: CrossAxisAlignment.end, children: [Text(i.due == 0 ? '—' : '${i.due} ر.س', style: const TextStyle(fontWeight: FontWeight.w800)), const SizedBox(height: 4), CcBadge(i.status.label, tone: tone)]),
        ]),
      ),
    );
  }
}

Future<void> showInvoiceSheet(BuildContext context, int id) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (sheet) {
      final c = AppScope.storeOf(context).invoices;
      return ListenableBuilder(listenable: c, builder: (_, __) => _InvoiceDetail(i: c.byId(id), host: context, sheet: sheet));
    },
  );
}

class _InvoiceDetail extends StatelessWidget {
  const _InvoiceDetail({required this.i, required this.host, required this.sheet});
  final Invoice i;
  final BuildContext host, sheet;

  @override
  Widget build(BuildContext context) {
    Widget row(String k, String v, {bool bold = false, Color? color}) => Padding(padding: const EdgeInsets.symmetric(vertical: 4), child: Row(children: [Text(k, style: const TextStyle(color: CcColors.muted)), const Spacer(), Text(v, style: TextStyle(fontWeight: bold ? FontWeight.w800 : FontWeight.w700, fontSize: bold ? 17 : 14, color: color))]));
    return SafeArea(
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, mainAxisSize: MainAxisSize.min, children: [
          Semantics(header: true, child: Text(i.title, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
          Text('${i.no} · ${i.date}', style: const TextStyle(color: CcColors.muted)),
          const SizedBox(height: CcSpace.s4),
          for (final it in i.items) row(it.name, '${it.amount} ر.س'),
          const Divider(),
          row('الإجمالي', '${i.total} ر.س'),
          row('يغطيه التأمين', '${i.covered} ر.س', color: CcColors.greenText),
          row('المستحق', '${i.due} ر.س', bold: true),
          if (i.paidWith != null) ...[const SizedBox(height: CcSpace.s2), CcNotice('تم السداد عبر ${i.paidWith}', error: false)],
          const SizedBox(height: CcSpace.s5),
          if (i.status == InvStatus.unpaid) CcButton(label: 'ادفع ${i.due} ر.س', icon: Icons.lock_outline_rounded, onPressed: () async {
            Navigator.of(sheet).pop();
            await _pay(host, i);
          }),
          if (i.status == InvStatus.unpaid) const SizedBox(height: CcSpace.s3),
          CcButton(label: 'تنزيل الفاتورة PDF', icon: Icons.download_rounded, variant: CcButtonVariant.ghost, onPressed: () => ScaffoldMessenger.of(host).showSnackBar(const SnackBar(content: Text('التنزيل غير متاح في النسخة التجريبية')))),
        ]),
      ),
    );
  }
}

Future<void> _pay(BuildContext context, Invoice i) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (sheet) {
      var method = _methods.first;
      var busy = false;
      return StatefulBuilder(
        builder: (_, setState) => SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(CcSpace.s6, 0, CcSpace.s6, CcSpace.s6),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, mainAxisSize: MainAxisSize.min, children: [
              Semantics(header: true, child: const Text('طريقة الدفع', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800))),
              const SizedBox(height: CcSpace.s2),
              for (final m in _methods)
                // ignore: deprecated_member_use
                RadioListTile<String>(value: m, groupValue: method, onChanged: (v) => setState(() => method = v ?? method), title: Text(m, style: const TextStyle(fontWeight: FontWeight.w700)), contentPadding: EdgeInsets.zero, activeColor: CcColors.blue),
              const SizedBox(height: CcSpace.s3),
              CcButton(
                label: 'ادفع ${i.due} ر.س',
                loading: busy,
                onPressed: () async {
                  setState(() => busy = true);
                  await Future<void>.delayed(const Duration(milliseconds: 800));
                  if (!context.mounted) return;
                  AppScope.storeOf(context).invoices.pay(i.id, method);
                  Navigator.of(sheet).pop();
                  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('تم دفع ${i.due} ر.س بنجاح')));
                },
              ),
            ]),
          ),
        ),
      );
    },
  );
}
