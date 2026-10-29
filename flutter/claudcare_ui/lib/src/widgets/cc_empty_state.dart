import 'package:flutter/material.dart';
import '../tokens.dart';

/// حالة فارغة: صورة (اختيارية) أو أيقونة + عنوان + وصف + زر اختياري.
class CcEmptyState extends StatelessWidget {
  const CcEmptyState({super.key, required this.title, this.text, this.image, this.icon = Icons.inbox_outlined, this.action});

  final String title;
  final String? text;
  final String? image;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(CcSpace.s6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (image != null) Image.asset(image!, height: 160, fit: BoxFit.contain, excludeFromSemantics: true) else Icon(icon, size: 56, color: CcColors.lineStrong),
            const SizedBox(height: CcSpace.s4),
            Text(title, textAlign: TextAlign.center, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
            if (text != null) ...[const SizedBox(height: CcSpace.s2), Text(text!, textAlign: TextAlign.center, style: const TextStyle(color: CcColors.muted, height: 1.6))],
            if (action != null) ...[const SizedBox(height: CcSpace.s4), action!],
          ],
        ),
      ),
    );
  }
}
