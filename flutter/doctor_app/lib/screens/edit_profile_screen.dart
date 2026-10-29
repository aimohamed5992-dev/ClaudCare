import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/profile.dart';
import '../state/app_scope.dart';

class EditProfileScreen extends StatefulWidget {
  const EditProfileScreen({super.key});
  @override
  State<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends State<EditProfileScreen> {
  late final DoctorProfile _p = AppScope.storeOf(context).profile;
  late final _name = TextEditingController(text: _p.name);
  late final _phone = TextEditingController(text: _p.phone);
  late final _email = TextEditingController(text: _p.email);
  late final _bio = TextEditingController(text: _p.bio);
  late String _specialty = _p.specialty;
  final _errors = <String, String?>{};

  @override
  void dispose() {
    for (final c in [_name, _phone, _email, _bio]) {
      c.dispose();
    }
    super.dispose();
  }

  void _save() {
    final words = _name.text.trim().split(RegExp(r'\s+')).where((w) => w.length > 1).length;
    setState(() {
      _errors['name'] = words >= 2 ? null : 'أدخل اسمك الثنائي على الأقل';
      _errors['phone'] = ccPhone(_phone.text);
      _errors['email'] = ccEmail(_email.text);
      _errors['bio'] = _bio.text.length > 200 ? 'الحد الأقصى 200 حرف' : null;
    });
    if (_errors.values.any((e) => e != null)) return;
    _p.update(name: _name.text.trim(), specialty: _specialty, phone: ccNormalizeDigits(_phone.text.trim()), email: _email.text.trim(), bio: _bio.text.trim());
    Navigator.of(context).pop();
    ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('تم حفظ الملف الشخصي')));
  }

  @override
  Widget build(BuildContext context) {
    const gap = SizedBox(height: CcSpace.s4);
    return CcAuthScaffold(
      title: 'تعديل الملف الشخصي',
      children: [
        Center(child: Stack(children: [CcAvatar(name: _name.text, asset: 'assets/images/avatars/doc-f1.webp', size: 88), Positioned(bottom: 0, left: 0, child: Container(padding: const EdgeInsets.all(6), decoration: const BoxDecoration(color: CcColors.blue, shape: BoxShape.circle), child: const Icon(Icons.camera_alt_outlined, size: 16, color: Colors.white)))])),
        const SizedBox(height: CcSpace.s5),
        CcTextField(label: 'الاسم', controller: _name, error: _errors['name'], onChanged: (_) => setState(() {})),
        gap,
        const Text('التخصص', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 14)),
        const SizedBox(height: 6),
        DropdownButtonFormField<String>(
          isExpanded: true,
          value: _specialty,
          items: [for (final s in DoctorProfile.specialties) DropdownMenuItem(value: s, child: Text(s))],
          onChanged: (v) => setState(() => _specialty = v ?? _specialty),
          decoration: const InputDecoration(),
        ),
        gap,
        CcTextField(label: 'رقم الجوال', controller: _phone, keyboardType: TextInputType.phone, ltr: true, error: _errors['phone']),
        gap,
        CcTextField(label: 'البريد الإلكتروني', controller: _email, keyboardType: TextInputType.emailAddress, ltr: true, error: _errors['email']),
        gap,
        CcTextField(label: 'نبذة', controller: _bio, error: _errors['bio'], helper: 'حتى 200 حرف'),
        const SizedBox(height: CcSpace.s6),
        CcButton(label: 'حفظ التعديلات', onPressed: _save),
      ],
    );
  }
}
