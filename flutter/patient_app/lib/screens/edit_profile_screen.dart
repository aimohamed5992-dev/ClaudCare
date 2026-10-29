import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../state/app_scope.dart';

class EditProfileScreen extends StatefulWidget {
  const EditProfileScreen({super.key});
  @override
  State<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends State<EditProfileScreen> {
  late final _p = AppScope.storeOf(context).profile;
  late final _name = TextEditingController(text: _p.name);
  late final _phone = TextEditingController(text: _p.phone);
  late final _email = TextEditingController(text: _p.email);
  late final _year = TextEditingController(text: '${_p.birthYear}');
  final _errors = <String, String?>{};

  @override
  void dispose() {
    for (final c in [_name, _phone, _email, _year]) {
      c.dispose();
    }
    super.dispose();
  }

  void _save() {
    final words = _name.text.trim().split(RegExp(r'\s+')).where((w) => w.length > 1).length;
    final y = int.tryParse(ccNormalizeDigits(_year.text.trim()));
    setState(() {
      _errors['name'] = words >= 2 ? null : 'أدخل اسمك الثنائي على الأقل';
      _errors['phone'] = ccPhone(_phone.text);
      _errors['email'] = ccEmail(_email.text);
      _errors['year'] = (y == null || y < 1900 || y > DateTime.now().year) ? 'أدخل سنة ميلاد صحيحة' : null;
    });
    if (_errors.values.any((e) => e != null)) return;
    _p.update(name: _name.text.trim(), phone: ccNormalizeDigits(_phone.text.trim()), email: _email.text.trim(), birthYear: y);
    Navigator.of(context).pop();
    ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم حفظ الملف الشخصي')));
  }

  @override
  Widget build(BuildContext context) {
    const gap = SizedBox(height: CcSpace.s4);
    return CcAuthScaffold(
      title: 'تعديل الملف الشخصي',
      children: [
        CcTextField(label: 'الاسم الكامل', controller: _name, error: _errors['name']),
        gap,
        CcTextField(label: 'رقم الجوال', controller: _phone, keyboardType: TextInputType.phone, ltr: true, error: _errors['phone']),
        gap,
        CcTextField(label: 'البريد الإلكتروني', controller: _email, keyboardType: TextInputType.emailAddress, ltr: true, error: _errors['email']),
        gap,
        CcTextField(label: 'سنة الميلاد', controller: _year, keyboardType: TextInputType.number, maxLength: 4, ltr: true, error: _errors['year']),
        gap,
        CcTextField(label: 'رقم الهوية', controller: TextEditingController(text: _p.nationalId), ltr: true, helper: 'لا يمكن تعديل رقم الهوية'),
        const SizedBox(height: CcSpace.s6),
        CcButton(label: 'حفظ التعديلات', onPressed: _save),
      ],
    );
  }
}
