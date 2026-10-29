import 'package:flutter/material.dart';

/// ألوان الهوية (مأخوذة من الشعار ومطابقة للنسخة الويب). الخلفية بيضاء دائمًا.
abstract final class CcColors {
  static const blue = Color(0xFF0B55B5);
  static const blue600 = Color(0xFF0A4A9E);
  static const sky = Color(0xFF0A98F5);
  static const orange = Color(0xFFF58A25);
  static const slate = Color(0xFF3F4B5F);

  static const blue50 = Color(0xFFEEF5FF);
  static const blue100 = Color(0xFFDBE9FF);
  static const orange50 = Color(0xFFFFF4EA);

  static const bg = Color(0xFFFFFFFF);
  static const line = Color(0xFFE6EAF0);
  static const lineStrong = Color(0xFFCFD6E2);
  static const muted = Color(0xFF5B6679);

  static const green = Color(0xFF16A34A);
  static const green50 = Color(0xFFE8F7EE);
  static const greenText = Color(0xFF11803A);
  static const red = Color(0xFFDC2626);
  static const red50 = Color(0xFFFDECEC);
  static const redText = Color(0xFFC81E1E);
  static const amberText = Color(0xFFA85A0C);
}

abstract final class CcSpace {
  static const double s1 = 4, s2 = 8, s3 = 12, s4 = 16, s5 = 20, s6 = 24, s8 = 32, s10 = 40;
}

abstract final class CcRadius {
  static const double sm = 8, md = 12, lg = 16, xl = 22;
  static const pill = Radius.circular(999);
}

abstract final class CcShadow {
  static final card = [
    BoxShadow(color: CcColors.blue.withValues(alpha: 0.10), blurRadius: 24, offset: const Offset(0, 10), spreadRadius: -12),
  ];
}

/// اسم الخط كما يُسجَّل في التطبيقات التي تعتمد على هذه الحزمة.
const kCcFont = 'packages/claudcare_ui/Cairo';
