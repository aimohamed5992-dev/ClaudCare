import 'package:flutter/material.dart';
import '../tokens.dart';
import '../widgets/cc_button.dart';
import '../widgets/cc_text_field.dart';
import 'cc_auth_scaffold.dart';

/// حساب تجريبي يملأ حقلي الدخول.
class CcDemoAccount {
  const CcDemoAccount(this.label, this.identifier, this.password);
  final String label;
  final String identifier;
  final String password;
}

/// شاشة تسجيل الدخول. [onLogin] تعيد null عند النجاح أو رسالة الخطأ.
class CcLoginScreen extends StatefulWidget {
  const CcLoginScreen({
    super.key,
    required this.title,
    required this.subtitle,
    required this.identifierLabel,
    required this.validateIdentifier,
    required this.onLogin,
    required this.onForgot,
    this.identifierHint,
    this.identifierIcon = Icons.person_outline_rounded,
    this.identifierKeyboard = TextInputType.text,
    this.onRegister,
    this.registerPrompt = 'ليس لديك حساب؟',
    this.registerLabel = 'إنشاء حساب',
    this.header,
    this.showBiometric = false,
    this.footer,
    this.demoAccounts = const [],
  });

  final String title;
  final String subtitle;
  final String identifierLabel;
  final String? identifierHint;
  final IconData identifierIcon;
  final TextInputType identifierKeyboard;
  final String? Function(String?) validateIdentifier;
  final Future<String?> Function(String identifier, String password) onLogin;
  final VoidCallback onForgot;
  final VoidCallback? onRegister;
  final String registerPrompt;
  final String registerLabel;
  final Widget? header;
  final bool showBiometric;
  final String? footer;

  /// حسابات تجربة تُعبّأ بلمسة (للتطوير فقط؛ مرّر قائمة فارغة في الإنتاج).
  final List<CcDemoAccount> demoAccounts;

  @override
  State<CcLoginScreen> createState() => _CcLoginScreenState();
}

class _CcLoginScreenState extends State<CcLoginScreen> {
  final _id = TextEditingController();
  final _pw = TextEditingController();
  String? _idError, _pwError, _serverError;
  bool _loading = false, _submitted = false;

  @override
  void dispose() {
    _id.dispose();
    _pw.dispose();
    super.dispose();
  }

  bool _validate() {
    setState(() {
      _idError = widget.validateIdentifier(_id.text);
      _pwError = _pw.text.isEmpty ? 'أدخل كلمة المرور' : (_pw.text.length < 6 ? 'كلمة المرور 6 أحرف على الأقل' : null);
    });
    return _idError == null && _pwError == null;
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    setState(() {
      _submitted = true;
      _serverError = null;
    });
    if (!_validate()) return;
    setState(() => _loading = true);
    final err = await widget.onLogin(_id.text.trim(), _pw.text);
    if (!mounted) return;
    setState(() {
      _loading = false;
      _serverError = err;
    });
  }

  @override
  Widget build(BuildContext context) {
    return CcAuthScaffold(
      title: widget.title,
      subtitle: widget.subtitle,
      header: widget.header,
      showBack: false,
      children: [
        if (_serverError != null) ...[CcNotice(_serverError!), const SizedBox(height: CcSpace.s4)],
        CcTextField(
          label: widget.identifierLabel,
          controller: _id,
          hint: widget.identifierHint,
          icon: widget.identifierIcon,
          keyboardType: widget.identifierKeyboard,
          textInputAction: TextInputAction.next,
          error: _idError,
          ltr: true,
          onChanged: (_) => _submitted ? _validate() : null,
        ),
        const SizedBox(height: CcSpace.s4),
        CcTextField(
          label: 'كلمة المرور',
          controller: _pw,
          icon: Icons.lock_outline_rounded,
          obscure: true,
          textInputAction: TextInputAction.done,
          error: _pwError,
          ltr: true,
          onChanged: (_) => _submitted ? _validate() : null,
          onSubmitted: (_) => _submit(),
        ),
        Align(
          alignment: AlignmentDirectional.centerEnd,
          child: TextButton(onPressed: widget.onForgot, child: const Text('نسيت كلمة المرور؟', style: TextStyle(fontWeight: FontWeight.w700))),
        ),
        const SizedBox(height: CcSpace.s2),
        CcButton(label: 'تسجيل الدخول', loading: _loading, onPressed: _submit),
        if (widget.showBiometric) ...[
          const SizedBox(height: CcSpace.s3),
          CcButton(
            label: 'الدخول بالبصمة',
            icon: Icons.fingerprint_rounded,
            variant: CcButtonVariant.ghost,
            onPressed: () => ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('الدخول بالبصمة غير متاح في النسخة التجريبية'))),
          ),
        ],
        if (widget.onRegister != null) ...[
          const SizedBox(height: CcSpace.s6),
          Wrap(
            alignment: WrapAlignment.center,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(widget.registerPrompt, style: const TextStyle(color: CcColors.muted)),
              TextButton(onPressed: widget.onRegister, child: Text(widget.registerLabel, style: const TextStyle(fontWeight: FontWeight.w800))),
            ],
          ),
        ],
        if (widget.demoAccounts.isNotEmpty) ...[
          const SizedBox(height: CcSpace.s4),
          const Text('حسابات التجربة (اضغط للتعبئة)', textAlign: TextAlign.center, style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: CcColors.muted)),
          const SizedBox(height: CcSpace.s2),
          Wrap(
            alignment: WrapAlignment.center,
            spacing: CcSpace.s2,
            children: [
              for (final a in widget.demoAccounts)
                ActionChip(
                  label: Text('${a.label} · ${a.identifier}', textDirection: TextDirection.ltr),
                  onPressed: () => setState(() {
                    _id.text = a.identifier;
                    _pw.text = a.password;
                    _serverError = null;
                  }),
                ),
            ],
          ),
        ],
        if (widget.footer != null) ...[
          const SizedBox(height: CcSpace.s4),
          Text(widget.footer!, textAlign: TextAlign.center, style: const TextStyle(fontSize: 13, color: CcColors.muted)),
        ],
      ],
    );
  }
}
