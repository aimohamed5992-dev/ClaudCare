import 'package:flutter/material.dart';
import '../tokens.dart';

/// حقل نصي بعنوان فوقه ورسالة خطأ/تلميح. [obscure] يضيف زر إظهار/إخفاء.
class CcTextField extends StatefulWidget {
  const CcTextField({
    super.key,
    required this.label,
    this.controller,
    this.hint,
    this.error,
    this.helper,
    this.icon,
    this.obscure = false,
    this.keyboardType,
    this.textInputAction,
    this.onChanged,
    this.onSubmitted,
    this.maxLength,
    this.textAlign = TextAlign.start,
    this.ltr = false,
  });

  final String label;
  final TextEditingController? controller;
  final String? hint;
  final String? error;
  final String? helper;
  final IconData? icon;
  final bool obscure;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;
  final int? maxLength;
  final TextAlign textAlign;
  /// للأرقام والبريد: اتجاه الكتابة من اليسار.
  final bool ltr;

  @override
  State<CcTextField> createState() => _CcTextFieldState();
}

class _CcTextFieldState extends State<CcTextField> {
  late bool _hidden = widget.obscure;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(widget.label, style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
        const SizedBox(height: 6),
        TextField(
          controller: widget.controller,
          obscureText: _hidden,
          keyboardType: widget.keyboardType,
          textInputAction: widget.textInputAction,
          onChanged: widget.onChanged,
          onSubmitted: widget.onSubmitted,
          maxLength: widget.maxLength,
          textAlign: widget.ltr ? TextAlign.right : widget.textAlign,
          textDirection: widget.ltr ? TextDirection.ltr : null,
          decoration: InputDecoration(
            hintText: widget.hint,
            errorText: widget.error,
            helperText: widget.helper,
            counterText: '',
            prefixIcon: widget.icon == null ? null : Icon(widget.icon, color: CcColors.muted),
            suffixIcon: widget.obscure
                ? IconButton(
                    tooltip: _hidden ? 'إظهار' : 'إخفاء',
                    onPressed: () => setState(() => _hidden = !_hidden),
                    icon: Icon(_hidden ? Icons.visibility_outlined : Icons.visibility_off_outlined, color: CcColors.muted),
                  )
                : null,
          ),
        ),
      ],
    );
  }
}
