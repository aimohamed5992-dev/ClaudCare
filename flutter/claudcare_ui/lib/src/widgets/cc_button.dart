import 'package:flutter/material.dart';
import '../tokens.dart';

enum CcButtonVariant { primary, soft, ghost, danger }

/// زر موحّد. [loading] يعطّل الزر ويعرض مؤشرًا. الحد الأدنى للارتفاع 48 للمس المريح.
class CcButton extends StatelessWidget {
  const CcButton({
    super.key,
    required this.label,
    this.onPressed,
    this.variant = CcButtonVariant.primary,
    this.icon,
    this.loading = false,
    this.expand = true,
  });

  final String label;
  final VoidCallback? onPressed;
  final CcButtonVariant variant;
  final IconData? icon;
  final bool loading;
  final bool expand;

  @override
  Widget build(BuildContext context) {
    final (bg, fg, side) = switch (variant) {
      CcButtonVariant.primary => (CcColors.blue, Colors.white, BorderSide.none),
      CcButtonVariant.soft => (CcColors.blue50, CcColors.blue, BorderSide.none),
      CcButtonVariant.ghost => (Colors.white, CcColors.slate, const BorderSide(color: CcColors.lineStrong)),
      CcButtonVariant.danger => (CcColors.red50, CcColors.redText, BorderSide.none),
    };
    final enabled = onPressed != null && !loading;
    final child = loading
        ? SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: fg))
        : Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (icon != null) ...[Icon(icon, size: 20), const SizedBox(width: 8)],
              Flexible(child: Text(label, overflow: TextOverflow.ellipsis)),
            ],
          );
    final button = FilledButton(
      onPressed: enabled ? onPressed : null,
      style: FilledButton.styleFrom(
        backgroundColor: bg,
        foregroundColor: fg,
        disabledBackgroundColor: variant == CcButtonVariant.primary ? CcColors.blue.withValues(alpha: 0.35) : CcColors.line,
        disabledForegroundColor: variant == CcButtonVariant.primary ? Colors.white : CcColors.muted,
        minimumSize: const Size(48, 52),
        padding: const EdgeInsets.symmetric(horizontal: 20),
        textStyle: const TextStyle(fontFamily: kCcFont, fontSize: 16, fontWeight: FontWeight.w700),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(CcRadius.md), side: side),
        elevation: 0,
      ),
      child: child,
    );
    return Semantics(
      button: true,
      enabled: enabled,
      label: loading ? '$label، جارٍ التنفيذ' : label,
      excludeSemantics: true,
      child: expand ? SizedBox(width: double.infinity, child: button) : button,
    );
  }
}
