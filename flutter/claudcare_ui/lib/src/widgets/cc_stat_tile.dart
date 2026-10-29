import 'package:flutter/material.dart';
import '../tokens.dart';
import 'cc_badge.dart';

/// بطاقة إحصاء صغيرة: أيقونة + رقم + وصف.
class CcStatTile extends StatelessWidget {
  const CcStatTile({super.key, required this.icon, required this.value, required this.label, this.tone = CcTone.blue});

  final IconData icon;
  final String value;
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
    return Semantics(
      label: '$label: $value',
      child: ExcludeSemantics(
        child: Container(
          padding: const EdgeInsets.all(CcSpace.s3),
          decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(CcRadius.lg), border: Border.all(color: CcColors.line)),
          child: Row(
            children: [
              Container(width: 40, height: 40, decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(CcRadius.md)), child: Icon(icon, color: fg, size: 22)),
              const SizedBox(width: CcSpace.s3),
              Expanded(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    FittedBox(fit: BoxFit.scaleDown, alignment: AlignmentDirectional.centerStart, child: Text(value, maxLines: 1, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800, height: 1.15))),
                    Text(label, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12, color: CcColors.muted, height: 1.2)),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
