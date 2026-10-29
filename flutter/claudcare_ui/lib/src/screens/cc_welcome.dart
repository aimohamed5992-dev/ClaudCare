import 'package:flutter/material.dart';
import '../tokens.dart';
import '../widgets/cc_button.dart';

/// شاشة الترحيب بعد الـ onboarding: صورة + عنوان + زر الدخول (وزر ثانوي اختياري).
class CcWelcome extends StatelessWidget {
  const CcWelcome({
    super.key,
    required this.image,
    required this.title,
    required this.subtitle,
    required this.primaryLabel,
    required this.onPrimary,
    this.secondaryLabel,
    this.onSecondary,
    this.footer,
  });

  final String image;
  final String title;
  final String subtitle;
  final String primaryLabel;
  final VoidCallback onPrimary;
  final String? secondaryLabel;
  final VoidCallback? onSecondary;
  final String? footer;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, box) => SingleChildScrollView(
            padding: const EdgeInsets.all(CcSpace.s6),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: box.maxHeight - CcSpace.s6 * 2),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Image.asset(image, height: (box.maxHeight * 0.4).clamp(150.0, 360.0), fit: BoxFit.contain, excludeFromSemantics: true),
                  const SizedBox(height: CcSpace.s6),
                  Semantics(header: true, child: Text(title, textAlign: TextAlign.center, style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w800, height: 1.3))),
                  const SizedBox(height: CcSpace.s3),
                  Text(subtitle, textAlign: TextAlign.center, style: const TextStyle(fontSize: 16, color: CcColors.muted, height: 1.7)),
                  const SizedBox(height: CcSpace.s8),
                  CcButton(label: primaryLabel, onPressed: onPrimary),
                  if (secondaryLabel != null) ...[
                    const SizedBox(height: CcSpace.s3),
                    CcButton(label: secondaryLabel!, variant: CcButtonVariant.ghost, onPressed: onSecondary),
                  ],
                  if (footer != null) ...[
                    const SizedBox(height: CcSpace.s5),
                    Text(footer!, textAlign: TextAlign.center, style: const TextStyle(fontSize: 13, color: CcColors.muted)),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
