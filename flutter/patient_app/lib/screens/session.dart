import 'package:claudcare_ui/claudcare_ui.dart';

const patientSession = SessionStore('patient.session.name');

/// حساب تجريبي: أي بيانات صحيحة الشكل تُقبل، عدا كلمة المرور `wrongpw` لتجربة رسالة الخطأ.
const demoPatientName = 'ريم العلي';

/// قناع الجوال للعرض: 05•• ••• 67
String maskPhone(String phone) {
  final d = ccNormalizeDigits(phone).replaceAll(RegExp(r'\D'), '');
  return d.length >= 2 ? '05•• ••• ${d.substring(d.length - 2)}' : 'جوالك المسجّل';
}
