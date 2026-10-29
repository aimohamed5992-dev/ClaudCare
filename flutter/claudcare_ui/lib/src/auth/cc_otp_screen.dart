import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../tokens.dart';
import '../widgets/cc_button.dart';
import 'cc_auth_scaffold.dart';
import 'validators.dart';

/// إدخال رمز التحقق (OTP): حقل واحد حقيقي (يدعم اللصق والإكمال التلقائي) يُعرض كمربعات.
/// [onVerify] تعيد null عند النجاح أو رسالة الخطأ، ثم تُستدعى [onVerified].
class CcOtpScreen extends StatefulWidget {
  const CcOtpScreen({
    super.key,
    required this.destination,
    required this.onVerify,
    required this.onVerified,
    this.onResend,
    this.length = 6,
    this.resendSeconds = 30,
    this.title = 'رمز التحقق',
    this.demoHint,
  });

  final String destination;
  final Future<String?> Function(String code) onVerify;
  final void Function(BuildContext context) onVerified;
  final Future<void> Function()? onResend;
  final int length;
  final int resendSeconds;
  final String title;
  final String? demoHint;

  @override
  State<CcOtpScreen> createState() => _CcOtpScreenState();
}

class _CcOtpScreenState extends State<CcOtpScreen> {
  final _ctrl = TextEditingController();
  final _focus = FocusNode();
  Timer? _timer;
  late int _left = widget.resendSeconds;
  String? _error;
  bool _loading = false;

  @override
  void initState() {
    super.initState();
    _startTimer();
    _ctrl.addListener(() => setState(() {}));
  }

  void _startTimer() {
    _timer?.cancel();
    _left = widget.resendSeconds;
    _timer = Timer.periodic(const Duration(seconds: 1), (t) {
      if (_left <= 1) {
        t.cancel();
      }
      if (mounted) setState(() => _left = (_left - 1).clamp(0, 999));
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _ctrl.dispose();
    _focus.dispose();
    super.dispose();
  }

  Future<void> _verify() async {
    final code = _ctrl.text;
    if (code.length != widget.length) {
      setState(() => _error = 'أدخل الرمز كاملًا (${widget.length} أرقام)');
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    final err = await widget.onVerify(code);
    if (!mounted) return;
    if (err == null) {
      setState(() => _loading = false);
      widget.onVerified(context);
    } else {
      setState(() {
        _loading = false;
        _error = err;
      });
      _ctrl.clear();
      _focus.requestFocus();
    }
  }

  Future<void> _resend() async {
    await widget.onResend?.call();
    if (!mounted) return;
    _ctrl.clear();
    setState(() => _error = null);
    _startTimer();
    ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('أُرسل رمز جديد')));
  }

  @override
  Widget build(BuildContext context) {
    final code = _ctrl.text;
    return CcAuthScaffold(
      title: widget.title,
      subtitle: 'أرسلنا رمزًا من ${widget.length} أرقام إلى ${widget.destination}',
      children: [
        if (_error != null) ...[CcNotice(_error!), const SizedBox(height: CcSpace.s4)],
        Semantics(
          textField: true,
          label: 'رمز التحقق، ${code.length} من ${widget.length} أرقام',
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: () => _focus.requestFocus(),
            child: Stack(
              alignment: Alignment.center,
              children: [
                // الحقل الحقيقي شفاف فوق المربعات
                Positioned.fill(
                  child: Opacity(
                    opacity: 0.01,
                    child: TextField(
                      controller: _ctrl,
                      focusNode: _focus,
                      autofocus: true,
                      keyboardType: TextInputType.number,
                      autofillHints: const [AutofillHints.oneTimeCode],
                      maxLength: widget.length,
                      showCursor: false,
                      enableInteractiveSelection: false,
                      inputFormatters: [
                        TextInputFormatter.withFunction((o, n) => n.copyWith(text: ccNormalizeDigits(n.text).replaceAll(RegExp(r'\D'), ''))),
                      ],
                      onChanged: (v) {
                        if (_error != null) setState(() => _error = null);
                        if (v.length == widget.length) _verify();
                      },
                      decoration: const InputDecoration(counterText: '', border: InputBorder.none),
                    ),
                  ),
                ),
                IgnorePointer(
                  child: Directionality(
                    textDirection: TextDirection.ltr,
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [for (var i = 0; i < widget.length; i++) _Box(char: i < code.length ? code[i] : '', active: i == code.length && _focus.hasFocus, error: _error != null)],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: CcSpace.s5),
        Center(
          child: _left > 0
              ? Text('إعادة الإرسال بعد 00:${_left.toString().padLeft(2, '0')}', style: const TextStyle(color: CcColors.muted))
              : TextButton(onPressed: _resend, child: const Text('إعادة إرسال الرمز', style: TextStyle(fontWeight: FontWeight.w800))),
        ),
        const SizedBox(height: CcSpace.s4),
        CcButton(label: 'تحقق', loading: _loading, onPressed: code.length == widget.length ? _verify : null),
        if (widget.demoHint != null) ...[
          const SizedBox(height: CcSpace.s4),
          Text(widget.demoHint!, textAlign: TextAlign.center, style: const TextStyle(fontSize: 13, color: CcColors.muted)),
        ],
      ],
    );
  }
}

class _Box extends StatelessWidget {
  const _Box({required this.char, required this.active, required this.error});
  final String char;
  final bool active, error;

  @override
  Widget build(BuildContext context) {
    final border = error ? CcColors.redText : (active ? CcColors.sky : (char.isNotEmpty ? CcColors.blue : CcColors.lineStrong));
    return AnimatedContainer(
      duration: const Duration(milliseconds: 120),
      width: 46,
      height: 56,
      alignment: Alignment.center,
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(CcRadius.md), border: Border.all(color: border, width: active ? 2 : 1.4)),
      child: Text(char, style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w800)),
    );
  }
}
