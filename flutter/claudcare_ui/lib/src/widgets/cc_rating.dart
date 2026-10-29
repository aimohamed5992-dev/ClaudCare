import 'package:flutter/material.dart';
import '../tokens.dart';

/// نجمة + التقييم + (عدد المراجعات).
class CcRating extends StatelessWidget {
  const CcRating({super.key, required this.value, this.count});
  final double value;
  final int? count;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'التقييم ${value.toStringAsFixed(1)} من 5${count == null ? '' : '، $count مراجعة'}',
      excludeSemantics: true,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.star_rounded, size: 18, color: CcColors.orange),
          const SizedBox(width: 2),
          Text(value.toStringAsFixed(1), style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 13)),
          if (count != null) Text(' ($count)', style: const TextStyle(fontSize: 12, color: CcColors.muted)),
        ],
      ),
    );
  }
}
