import 'package:flutter/material.dart';
import '../tokens.dart';

/// مؤشر صفحات للـ onboarding: النقطة الحالية ممتدة.
class CcPageDots extends StatelessWidget {
  const CcPageDots({super.key, required this.count, required this.index});

  final int count;
  final int index;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'الصفحة ${index + 1} من $count',
      child: ExcludeSemantics(
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (var i = 0; i < count; i++)
              AnimatedContainer(
                duration: const Duration(milliseconds: 250),
                curve: Curves.easeOut,
                margin: const EdgeInsets.symmetric(horizontal: 3),
                width: i == index ? 26 : 8,
                height: 8,
                decoration: BoxDecoration(
                  color: i == index ? CcColors.blue : CcColors.lineStrong,
                  borderRadius: const BorderRadius.all(CcRadius.pill),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
