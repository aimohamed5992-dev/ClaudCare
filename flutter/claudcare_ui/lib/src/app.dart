import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'theme.dart';

/// غلاف MaterialApp موحّد: عربي RTL وسمة ClaudCare.
class CcApp extends StatelessWidget {
  const CcApp({super.key, required this.title, required this.home, this.routes = const {}, this.navigatorKey});

  final String title;
  final Widget home;
  final Map<String, WidgetBuilder> routes;
  final GlobalKey<NavigatorState>? navigatorKey;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: title,
      debugShowCheckedModeBanner: false,
      navigatorKey: navigatorKey,
      theme: ccTheme(),
      locale: const Locale('ar'),
      supportedLocales: const [Locale('ar')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      home: home,
      routes: routes,
    );
  }
}
