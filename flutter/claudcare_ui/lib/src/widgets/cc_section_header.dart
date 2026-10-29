import 'package:flutter/material.dart';

class CcSectionHeader extends StatelessWidget {
  const CcSectionHeader(this.title, {super.key, this.actionLabel, this.onAction});

  final String title;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(child: Semantics(header: true, child: Text(title, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800)))),
        if (actionLabel != null) TextButton(onPressed: onAction, child: Text(actionLabel!, style: const TextStyle(fontWeight: FontWeight.w700))),
      ],
    );
  }
}
