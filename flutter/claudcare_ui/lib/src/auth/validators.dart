/// تحويل الأرقام العربية/الفارسية إلى لاتينية قبل التحقق.
String ccNormalizeDigits(String s) {
  const ar = '٠١٢٣٤٥٦٧٨٩', fa = '۰۱۲۳۴۵۶۷۸۹';
  final b = StringBuffer();
  for (final r in s.runes) {
    final c = String.fromCharCode(r);
    final i = ar.contains(c) ? ar.indexOf(c) : fa.indexOf(c);
    b.write(i >= 0 ? '$i' : c);
  }
  return b.toString();
}

String? ccRequired(String? v, String label) => (v == null || v.trim().isEmpty) ? 'أدخل $label' : null;

String? ccEmail(String? v) {
  final s = (v ?? '').trim();
  if (s.isEmpty) return 'أدخل البريد الإلكتروني';
  return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]{2,}$').hasMatch(s) ? null : 'صيغة البريد غير صحيحة';
}

/// جوال سعودي: 05XXXXXXXX أو +9665XXXXXXXX
String? ccPhone(String? v) {
  final s = ccNormalizeDigits((v ?? '').replaceAll(RegExp(r'[\s\-]'), ''));
  if (s.isEmpty) return 'أدخل رقم الجوال';
  return RegExp(r'^(05\d{8}|\+?9665\d{8})$').hasMatch(s) ? null : 'رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام';
}

/// هوية وطنية/إقامة: 10 أرقام تبدأ بـ 1 أو 2
String? ccNationalId(String? v) {
  final s = ccNormalizeDigits((v ?? '').trim());
  if (s.isEmpty) return 'أدخل رقم الهوية';
  return RegExp(r'^[12]\d{9}$').hasMatch(s) ? null : 'رقم الهوية 10 أرقام ويبدأ بـ 1 أو 2';
}

/// كلمة مرور جديدة: 8 أحرف على الأقل، فيها حرف ورقم.
String? ccNewPassword(String? v) {
  final s = v ?? '';
  if (s.isEmpty) return 'أدخل كلمة المرور';
  if (s.length < 8) return 'كلمة المرور 8 أحرف على الأقل';
  if (!RegExp(r'[A-Za-z؀-ۿ]').hasMatch(s) || !RegExp(r'\d').hasMatch(s)) return 'يجب أن تحتوي على حروف وأرقام';
  return null;
}

/// قوة كلمة المرور من 0 إلى 4.
int ccPasswordStrength(String s) {
  var n = 0;
  if (s.length >= 8) n++;
  if (s.length >= 12) n++;
  if (RegExp(r'[A-Za-z؀-ۿ]').hasMatch(s) && RegExp(r'\d').hasMatch(s)) n++;
  if (RegExp(r'[^A-Za-z0-9؀-ۿ]').hasMatch(s) || (RegExp(r'[A-Z]').hasMatch(s) && RegExp(r'[a-z]').hasMatch(s))) n++;
  return n;
}
