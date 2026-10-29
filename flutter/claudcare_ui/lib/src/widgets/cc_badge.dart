import 'package:flutter/material.dart';
import '../tokens.dart';

enum CcTone { blue, green, orange, red, gray }

/// شارة حالة: النص دائمًا مع اللون (لا نعتمد على اللون وحده).
class CcBadge extends StatelessWidget {
  const CcBadge(this.label, {super.key, this.tone = CcTone.blue});

  final String label;
  final CcTone tone;

  @override
  Widget build(BuildContext context) {
    final (bg, fg) = switch (tone) {
      CcTone.blue => (CcColors.blue50, CcColors.blue),
      CcTone.green => (CcColors.green50, CcColors.greenText),
      CcTone.orange => (CcColors.orange50, CcColors.amberText),
      CcTone.red => (CcColors.red50, CcColors.redText),
      CcTone.gray => (const Color(0xFFF1F3F7), CcColors.slate),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
      decoration: BoxDecoration(color: bg, borderRadius: const BorderRadius.all(CcRadius.pill)),
      // نصوص لاتينية فقط (مثل فصيلة الدم A+) تُعرض من اليسار حتى لا ينقلب الرمز
      child: Text(label, textDirection: RegExp(r'[\u0600-\u06FF]').hasMatch(label) ? null : TextDirection.ltr, style: TextStyle(color: fg, fontSize: 12, fontWeight: FontWeight.w700)),
    );
  }
}
