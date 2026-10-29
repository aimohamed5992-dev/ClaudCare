import 'package:flutter/material.dart';
import '../tokens.dart';
import '../widgets/cc_button.dart';
import '../widgets/cc_text_field.dart';
import 'cc_auth_scaffold.dart';
import 'cc_otp_screen.dart';
import 'validators.dart';

/// استعادة كلمة المرور: معرّف الحساب ← رمز التحقق ← كلمة مرور جديدة ← نجاح.
class CcForgotScreen extends StatefulWidget {
  const CcForgotScreen({
    super.key,
    required this.identifierLabel,
    required this.validateIdentifier,
    required this.maskDestination,
    this.identifierIcon = Icons.person_outline_rounded,
    this.identifierKeyboard = TextInputType.text,
    this.otpCode = '123456',
    this.onRequest,
    this.onVerifyCode,
    this.onResend,
    this.onReset,
  });

  final String identifierLabel;
  final String? Function(String?) validateIdentifier;
  final String Function(String identifier) maskDestination;
  final IconData identifierIcon;
  final TextInputType identifierKeyboard;
  final String otpCode;

  /// ربط الخادم (اختياري؛ بدونها تعمل المحاكاة): إرسال الرمز، التحقق منه، إعادة الإرسال، ثم حفظ كلمة المرور.
  /// الدوال التي تعيد نصًا تعيد رسالة الخطأ أو null عند النجاح، ويُعاد إلى النص الذي يعرضه الحقل.
  final Future<String?> Function(String identifier)? onRequest;
  final Future<String?> Function(String code)? onVerifyCode;
  final Future<void> Function()? onResend;
  final Future<String?> Function(String newPassword)? onReset;

  @override
  State<CcForgotScreen> createState() => _CcForgotScreenState();
}

class _CcForgotScreenState extends State<CcForgotScreen> {
  final _id = TextEditingController();
  String? _error;
  bool _loading = false;

  @override
  void dispose() {
    _id.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final e = widget.validateIdentifier(_id.text);
    setState(() => _error = e);
    if (e != null) return;
    setState(() => _loading = true);
    final remote = widget.onRequest;
    String? serverError;
    if (remote != null) {
      serverError = await remote(_id.text.trim());
    } else {
      await Future<void>.delayed(const Duration(milliseconds: 700));
    }
    if (!mounted) return;
    setState(() {
      _loading = false;
      _error = serverError;
    });
    if (serverError != null) return;
    Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => CcOtpScreen(
        destination: widget.maskDestination(_id.text.trim()),
        demoHint: remote == null ? 'للتجربة: الرمز ${widget.otpCode}' : null,
        onResend: widget.onResend ?? () async {},
        onVerify: widget.onVerifyCode ??
            (code) async {
              await Future<void>.delayed(const Duration(milliseconds: 500));
              return code == widget.otpCode ? null : 'الرمز غير صحيح، حاول مرة أخرى';
            },
        onVerified: (ctx) => Navigator.of(ctx).push(MaterialPageRoute<void>(builder: (_) => CcResetPasswordScreen(onSave: widget.onReset))),
      ),
    ));
  }

  @override
  Widget build(BuildContext context) {
    return CcAuthScaffold(
      title: 'استعادة كلمة المرور',
      subtitle: 'أدخل بيانات حسابك وسنرسل لك رمز تحقق لإعادة تعيين كلمة المرور.',
      children: [
        CcTextField(
          label: widget.identifierLabel,
          controller: _id,
          icon: widget.identifierIcon,
          keyboardType: widget.identifierKeyboard,
          error: _error,
          ltr: true,
          textInputAction: TextInputAction.done,
          onSubmitted: (_) => _send(),
        ),
        const SizedBox(height: CcSpace.s6),
        CcButton(label: 'إرسال الرمز', loading: _loading, onPressed: _send),
      ],
    );
  }
}

class CcResetPasswordScreen extends StatefulWidget {
  const CcResetPasswordScreen({super.key, this.onSave});

  /// حفظ كلمة المرور على الخادم؛ تعيد رسالة الخطأ أو null.
  final Future<String?> Function(String newPassword)? onSave;
  @override
  State<CcResetPasswordScreen> createState() => _CcResetPasswordScreenState();
}

class _CcResetPasswordScreenState extends State<CcResetPasswordScreen> {
  final _pw = TextEditingController();
  final _confirm = TextEditingController();
  String? _pwError, _confirmError;
  bool _loading = false, _done = false;

  @override
  void dispose() {
    _pw.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() {
      _pwError = ccNewPassword(_pw.text);
      _confirmError = _confirm.text.isEmpty ? 'أعد كتابة كلمة المرور' : (_confirm.text != _pw.text ? 'كلمتا المرور غير متطابقتين' : null);
    });
    if (_pwError != null || _confirmError != null) return;
    setState(() => _loading = true);
    String? serverError;
    if (widget.onSave != null) {
      serverError = await widget.onSave!(_pw.text);
    } else {
      await Future<void>.delayed(const Duration(milliseconds: 700));
    }
    if (!mounted) return;
    setState(() {
      _loading = false;
      _done = serverError == null;
      if (serverError != null) _pwError = serverError;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_done) {
      return CcAuthScaffold(
        title: 'تم تغيير كلمة المرور',
        subtitle: 'يمكنك الآن تسجيل الدخول بكلمة المرور الجديدة.',
        showBack: false,
        header: Container(
          width: 96,
          height: 96,
          decoration: const BoxDecoration(color: CcColors.green50, shape: BoxShape.circle),
          child: const Icon(Icons.check_rounded, size: 52, color: CcColors.greenText, semanticLabel: 'تم'),
        ),
        children: [CcButton(label: 'العودة لتسجيل الدخول', onPressed: () => Navigator.of(context).popUntil((r) => r.isFirst))],
      );
    }
    return CcAuthScaffold(
      title: 'كلمة مرور جديدة',
      subtitle: 'اختر كلمة مرور قوية: 8 أحرف على الأقل وتحتوي على حروف وأرقام.',
      children: [
        CcTextField(label: 'كلمة المرور الجديدة', controller: _pw, obscure: true, icon: Icons.lock_outline_rounded, error: _pwError, ltr: true, onChanged: (_) => setState(() {}), textInputAction: TextInputAction.next),
        const SizedBox(height: CcSpace.s2),
        CcStrengthMeter(password: _pw.text),
        const SizedBox(height: CcSpace.s4),
        CcTextField(label: 'تأكيد كلمة المرور', controller: _confirm, obscure: true, icon: Icons.lock_outline_rounded, error: _confirmError, ltr: true, onSubmitted: (_) => _save()),
        const SizedBox(height: CcSpace.s6),
        CcButton(label: 'حفظ كلمة المرور', loading: _loading, onPressed: _save),
      ],
    );
  }
}

/// مقياس قوة كلمة المرور: أربع شرائح مع نص (لا نعتمد على اللون وحده).
class CcStrengthMeter extends StatelessWidget {
  const CcStrengthMeter({super.key, required this.password});
  final String password;

  @override
  Widget build(BuildContext context) {
    if (password.isEmpty) return const SizedBox(height: 18);
    final s = ccPasswordStrength(password).clamp(1, 4);
    const labels = ['', 'ضعيفة', 'مقبولة', 'جيدة', 'قوية'];
    final color = [CcColors.lineStrong, CcColors.redText, CcColors.orange, CcColors.sky, CcColors.green][s];
    return Semantics(
      label: 'قوة كلمة المرور: ${labels[s]}',
      child: ExcludeSemantics(
        child: Row(
          children: [
            for (var i = 1; i <= 4; i++)
              Expanded(child: Container(height: 5, margin: const EdgeInsetsDirectional.only(end: 4), decoration: BoxDecoration(color: i <= s ? color : CcColors.line, borderRadius: const BorderRadius.all(CcRadius.pill)))),
            const SizedBox(width: CcSpace.s2),
            Text(labels[s], style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700, color: s == 1 ? CcColors.redText : CcColors.slate)),
          ],
        ),
      ),
    );
  }
}
