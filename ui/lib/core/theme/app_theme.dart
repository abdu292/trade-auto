import 'package:flutter/material.dart';

class AppTheme {
  static ThemeData light() {
    final base = ThemeData(
      useMaterial3: true,
      colorScheme: ColorScheme.fromSeed(seedColor: Colors.indigo),
    );

    return base.copyWith(
      cardTheme: const CardThemeData(margin: EdgeInsets.zero),
      inputDecorationTheme: const InputDecorationTheme(
        filled: true,
        border: OutlineInputBorder(),
      ),
      navigationBarTheme: NavigationBarThemeData(
        indicatorColor: base.colorScheme.primaryContainer,
      ),
    );
  }

  static ThemeData dark() {
    return ThemeData(
      useMaterial3: true,
      colorScheme: ColorScheme.fromSeed(
          seedColor: Colors.indigo, brightness: Brightness.dark),
      cardTheme: const CardThemeData(margin: EdgeInsets.zero),
    );
  }
}
