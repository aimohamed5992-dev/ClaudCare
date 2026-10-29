import 'package:flutter/material.dart';
import 'tokens.dart';

/// سمة التطبيق: Material 3 بخلفية بيضاء وخط Cairo.
ThemeData ccTheme() {
  const scheme = ColorScheme.light(
    primary: CcColors.blue,
    onPrimary: Colors.white,
    secondary: CcColors.orange,
    surface: Colors.white,
    onSurface: CcColors.slate,
    error: CcColors.redText,
    outline: CcColors.lineStrong,
  );

  OutlineInputBorder border(Color c, [double w = 1]) => OutlineInputBorder(
        borderRadius: BorderRadius.circular(CcRadius.md),
        borderSide: BorderSide(color: c, width: w),
      );

  final base = ThemeData(useMaterial3: true, colorScheme: scheme, fontFamily: kCcFont);
  return base.copyWith(
    scaffoldBackgroundColor: CcColors.bg,
    canvasColor: CcColors.bg,
    dividerColor: CcColors.line,
    textTheme: base.textTheme.apply(bodyColor: CcColors.slate, displayColor: CcColors.slate),
    appBarTheme: const AppBarTheme(
      backgroundColor: Colors.white,
      foregroundColor: CcColors.slate,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: true,
      titleTextStyle: TextStyle(fontFamily: kCcFont, fontSize: 18, fontWeight: FontWeight.w700, color: CcColors.slate),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      enabledBorder: border(CcColors.lineStrong),
      focusedBorder: border(CcColors.sky, 2),
      errorBorder: border(CcColors.redText),
      focusedErrorBorder: border(CcColors.redText, 2),
      hintStyle: const TextStyle(color: CcColors.muted),
    ),
    navigationBarTheme: NavigationBarThemeData(
      backgroundColor: Colors.white,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      height: 68,
      indicatorColor: CcColors.blue50,
      labelTextStyle: WidgetStateProperty.resolveWith((s) => TextStyle(
            fontFamily: kCcFont,
            fontSize: 12,
            fontWeight: s.contains(WidgetState.selected) ? FontWeight.w800 : FontWeight.w600,
            color: s.contains(WidgetState.selected) ? CcColors.blue : CcColors.muted,
          )),
      iconTheme: WidgetStateProperty.resolveWith((s) => IconThemeData(size: 24, color: s.contains(WidgetState.selected) ? CcColors.blue : CcColors.muted)),
    ),
    chipTheme: ChipThemeData(
      backgroundColor: Colors.white,
      selectedColor: CcColors.blue50,
      side: const BorderSide(color: CcColors.lineStrong),
      labelStyle: const TextStyle(fontFamily: kCcFont, fontWeight: FontWeight.w700, color: CcColors.slate),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
      showCheckmark: false,
    ),
    bottomSheetTheme: const BottomSheetThemeData(
      backgroundColor: Colors.white,
      surfaceTintColor: Colors.transparent,
      showDragHandle: true,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(24))),
    ),
    snackBarTheme: SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      backgroundColor: CcColors.slate,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(CcRadius.md)),
    ),
  );
}
