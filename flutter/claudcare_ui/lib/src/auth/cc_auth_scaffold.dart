import 'package:flutter/material.dart';
import '../tokens.dart';

/// هيكل شاشات الدخول: زر رجوع اختياري، عنوان، وصف، ومحتوى قابل للتمرير يتحمل لوحة المفاتيح.
class CcAuthScaffold extends StatelessWidget {
  const CcAuthScaffold({super.key, required this.title, required this.children, this.subtitle, this.header, this.showBack = true});

  final String title;
  final String? subtitle;
  final Widget? header;
  final List<Widget> children;
  final bool showBack;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      appBar: showBack && Navigator.of(context).canPop()
          ? AppBar(leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop()))
          : null,
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
              padding: const EdgeInsets.fromLTRB(CcSpace.s6, CcSpace.s4, CcSpace.s6, CcSpace.s8),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (header != null) ...[Center(child: header!), const SizedBox(height: CcSpace.s5)],
                  Semantics(header: true, child: Text(title, style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w800, height: 1.3))),
                  if (subtitle != null) ...[
                    const SizedBox(height: CcSpace.s2),
                    Text(subtitle!, style: const TextStyle(fontSize: 15, color: CcColors.muted, height: 1.7)),
                  ],
                  const SizedBox(height: CcSpace.s6),
                  ...children,
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// شريط رسالة خطأ/نجاح مُعلَن لقارئ الشاشة.
class CcNotice extends StatelessWidget {
  const CcNotice(this.message, {super.key, this.error = true});
  final String message;
  final bool error;

  @override
  Widget build(BuildContext context) {
    final fg = error ? CcColors.redText : CcColors.greenText;
    return Semantics(
      liveRegion: true,
      child: Container(
        padding: const EdgeInsets.all(CcSpace.s3),
        decoration: BoxDecoration(color: error ? CcColors.red50 : CcColors.green50, borderRadius: BorderRadius.circular(CcRadius.md)),
        child: Row(
          children: [
            Icon(error ? Icons.error_outline_rounded : Icons.check_circle_outline_rounded, color: fg, size: 20),
            const SizedBox(width: CcSpace.s2),
            Expanded(child: Text(message, style: TextStyle(color: fg, fontWeight: FontWeight.w600, fontSize: 14))),
          ],
        ),
      ),
    );
  }
}
