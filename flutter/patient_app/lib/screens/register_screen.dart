import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../shell/patient_shell.dart';
import '../state/app_scope.dart';
import 'session.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});
  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _name = TextEditingController();
  final _phone = TextEditingController();
  final _nid = TextEditingController();
  final _pw = TextEditingController();
  final _confirm = TextEditingController();
  final _errors = <String, String?>{};
  bool _terms = false, _loading = false;

  @override
  void dispose() {
    for (final c in [_name, _phone, _nid, _pw, _confirm]) {
      c.dispose();
    }
    super.dispose();
  }

  bool _validate() {
    final name = _name.text.trim();
    setState(() {
      _errors['name'] = name.split(RegExp(r'\s+')).where((w) => w.length > 1).length >= 2 ? null : 'أدخل اسمك الثنائي على الأقل';
      _errors['phone'] = ccPhone(_phone.text);
      _errors['nid'] = ccNationalId(_nid.text);
      _errors['pw'] = ccNewPassword(_pw.text);
      _errors['confirm'] = _confirm.text.isEmpty ? 'أعد كتابة كلمة المرور' : (_confirm.text != _pw.text ? 'كلمتا المرور غير متطابقتين' : null);
      _errors['terms'] = _terms ? null : 'يجب الموافقة على الشروط للمتابعة';
    });
    return _errors.values.every((e) => e == null);
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    if (!_validate()) return;
    setState(() => _loading = true);
    final remote = AppScope.storeOf(context).remote;
    OtpChallenge? challenge;
    if (remote != null) {
      try {
        challenge = await remote.auth.register(
          fullName: _name.text.trim(),
          phone: ccNormalizeDigits(_phone.text.trim()),
          nationalId: ccNormalizeDigits(_nid.text.trim()),
          password: _pw.text,
        );
      } on ApiException catch (e) {
        if (!mounted) return;
        setState(() {
          _loading = false;
          _errors['phone'] = e.code == 'phone_taken' || e.errors.containsKey('Phone') || e.errors.containsKey('phone') ? e.userMessage : _errors['phone'];
          _errors['nid'] = e.code == 'national_id_taken' || e.errors.containsKey('NationalId') || e.errors.containsKey('nationalId') ? e.userMessage : _errors['nid'];
        });
        if (_errors['phone'] == null && _errors['nid'] == null) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.userMessage)));
        return;
      }
    } else {
      await Future<void>.delayed(const Duration(milliseconds: 800));
    }
    if (!mounted) return;
    setState(() => _loading = false);
    final name = _name.text.trim();
    Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => CcOtpScreen(
        destination: challenge?.destination ?? maskPhone(_phone.text),
        demoHint: remote == null ? 'للتجربة: الرمز 123456' : null,
        onResend: () async {
          if (remote != null && challenge != null) {
            try {
              challenge = await remote.auth.resend(challenge!.token);
            } on ApiException catch (_) {}
          }
        },
        onVerify: (code) async {
          if (remote != null) {
            try {
              remote.user = await remote.auth.verifyLogin(challenge!.token, code);
              await remote.loadAll();
              return null;
            } on ApiException catch (e) {
              return e.userMessage;
            }
          }
          await Future<void>.delayed(const Duration(milliseconds: 600));
          return code == '123456' ? null : 'الرمز غير صحيح، حاول مرة أخرى';
        },
        onVerified: (ctx) async {
          await patientSession.signIn(name);
          if (!ctx.mounted) return;
          Navigator.of(ctx).pushAndRemoveUntil(ccFadeRoute(PatientShell(name: name)), (_) => false);
        },
      ),
    ));
  }

  @override
  Widget build(BuildContext context) {
    const gap = SizedBox(height: CcSpace.s4);
    return CcAuthScaffold(
      title: 'إنشاء حساب جديد',
      subtitle: 'أدخل بياناتك لإنشاء ملفك الطبي. سنرسل رمز تحقق إلى جوالك.',
      children: [
        CcTextField(label: 'الاسم الكامل', controller: _name, icon: Icons.person_outline_rounded, error: _errors['name'], textInputAction: TextInputAction.next),
        gap,
        CcTextField(label: 'رقم الجوال', controller: _phone, hint: '05XXXXXXXX', icon: Icons.phone_iphone_rounded, keyboardType: TextInputType.phone, ltr: true, error: _errors['phone'], textInputAction: TextInputAction.next),
        gap,
        CcTextField(label: 'رقم الهوية / الإقامة', controller: _nid, icon: Icons.badge_outlined, keyboardType: TextInputType.number, maxLength: 10, ltr: true, error: _errors['nid'], textInputAction: TextInputAction.next),
        gap,
        CcTextField(label: 'كلمة المرور', controller: _pw, obscure: true, icon: Icons.lock_outline_rounded, ltr: true, error: _errors['pw'], onChanged: (_) => setState(() {}), textInputAction: TextInputAction.next),
        const SizedBox(height: CcSpace.s2),
        CcStrengthMeter(password: _pw.text),
        gap,
        CcTextField(label: 'تأكيد كلمة المرور', controller: _confirm, obscure: true, icon: Icons.lock_outline_rounded, ltr: true, error: _errors['confirm']),
        gap,
        CheckboxListTile(
          value: _terms,
          onChanged: (v) => setState(() {
            _terms = v ?? false;
            if (_terms) _errors['terms'] = null;
          }),
          controlAffinity: ListTileControlAffinity.leading,
          contentPadding: EdgeInsets.zero,
          activeColor: CcColors.blue,
          title: const Text('أوافق على الشروط وسياسة الخصوصية', style: TextStyle(fontSize: 14)),
          subtitle: _errors['terms'] == null ? null : Text(_errors['terms']!, style: const TextStyle(color: CcColors.redText, fontSize: 12)),
        ),
        const SizedBox(height: CcSpace.s4),
        CcButton(label: 'إنشاء الحساب', loading: _loading, onPressed: _submit),
      ],
    );
  }
}
