import 'package:flutter/material.dart';
import '../tokens.dart';
import '../widgets/cc_button.dart';
import '../widgets/cc_text_field.dart';
import 'cc_auth_scaffold.dart';
import 'cc_forgot_flow.dart';
import 'validators.dart';

/// تغيير كلمة المرور من الإعدادات. للتجربة: الحالية `wrongpw` تُظهر الخطأ.
class CcChangePasswordScreen extends StatefulWidget {
  const CcChangePasswordScreen({super.key});
  @override
  State<CcChangePasswordScreen> createState() => _CcChangePasswordScreenState();
}

class _CcChangePasswordScreenState extends State<CcChangePasswordScreen> {
  final _cur = TextEditingController();
  final _new = TextEditingController();
  final _confirm = TextEditingController();
  final _errors = <String, String?>{};
  bool _loading = false;

  @override
  void dispose() {
    for (final c in [_cur, _new, _confirm]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    setState(() {
      _errors['cur'] = _cur.text.isEmpty ? 'أدخل كلمة المرور الحالية' : null;
      _errors['new'] = ccNewPassword(_new.text) ?? (_new.text == _cur.text ? 'يجب أن تختلف عن الحالية' : null);
      _errors['confirm'] = _confirm.text != _new.text ? 'كلمتا المرور غير متطابقتين' : null;
    });
    if (_errors.values.any((e) => e != null)) return;
    setState(() => _loading = true);
    await Future<void>.delayed(const Duration(milliseconds: 700));
    if (!mounted) return;
    if (_cur.text == 'wrongpw') {
      setState(() {
        _loading = false;
        _errors['cur'] = 'كلمة المرور الحالية غير صحيحة';
      });
      return;
    }
    Navigator.of(context).pop();
    ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم تغيير كلمة المرور')));
  }

  @override
  Widget build(BuildContext context) {
    return CcAuthScaffold(
      title: 'تغيير كلمة المرور',
      subtitle: 'للتجربة: كلمة المرور الحالية wrongpw تُظهر الخطأ.',
      children: [
        CcTextField(label: 'كلمة المرور الحالية', controller: _cur, obscure: true, ltr: true, error: _errors['cur']),
        const SizedBox(height: CcSpace.s4),
        CcTextField(label: 'كلمة المرور الجديدة', controller: _new, obscure: true, ltr: true, error: _errors['new'], onChanged: (_) => setState(() {})),
        const SizedBox(height: CcSpace.s2),
        CcStrengthMeter(password: _new.text),
        const SizedBox(height: CcSpace.s4),
        CcTextField(label: 'تأكيد كلمة المرور', controller: _confirm, obscure: true, ltr: true, error: _errors['confirm']),
        const SizedBox(height: CcSpace.s6),
        CcButton(label: 'حفظ', loading: _loading, onPressed: _save),
      ],
    );
  }
}
