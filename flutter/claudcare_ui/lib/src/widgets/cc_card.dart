import 'package:flutter/material.dart';
import '../tokens.dart';

/// بطاقة بيضاء بحد رفيع. إن مُرّر [onTap] تصبح قابلة للنقر مع تأثير لمس.
class CcCard extends StatelessWidget {
  const CcCard({super.key, required this.child, this.onTap, this.padding = const EdgeInsets.all(CcSpace.s4), this.color = Colors.white});

  final Widget child;
  final VoidCallback? onTap;
  final EdgeInsetsGeometry padding;
  final Color color;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(CcRadius.lg);
    return Material(
      color: color,
      shape: RoundedRectangleBorder(borderRadius: radius, side: const BorderSide(color: CcColors.line)),
      child: InkWell(
        borderRadius: radius,
        onTap: onTap,
        child: Padding(padding: padding, child: child),
      ),
    );
  }
}
