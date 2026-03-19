import 'package:flutter/material.dart';
import 'workout_timer_page.dart';

void main() {
  runApp(const WorkoutTimerApp());
}

class WorkoutTimerApp extends StatelessWidget {
  const WorkoutTimerApp({super.key});

  @override
  Widget build(BuildContext context) {
    const primary = Color(0xFFFF5722);

    final darkColorScheme = ColorScheme.fromSeed(
      seedColor: primary,
      brightness: Brightness.dark,
      primary: primary,
    );

    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'Training Timer MD3',
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: darkColorScheme,
        brightness: Brightness.dark,
        fontFamily: 'Inter',
        visualDensity: VisualDensity.standard,
        materialTapTargetSize: MaterialTapTargetSize.padded,
        sliderTheme: const SliderThemeData(
          trackHeight: 4,
          thumbShape: RoundSliderThumbShape(enabledThumbRadius: 10),
          overlayShape: RoundSliderOverlayShape(overlayRadius: 20),
        ),
        segmentedButtonTheme: SegmentedButtonThemeData(
          style: ButtonStyle(
            minimumSize: WidgetStateProperty.all(const Size(48, 48)),
          ),
        ),
        filledButtonTheme: FilledButtonThemeData(
          style: ButtonStyle(
            minimumSize: WidgetStateProperty.all(const Size.fromHeight(48)),
          ),
        ),
        elevatedButtonTheme: ElevatedButtonThemeData(
          style: ButtonStyle(
            minimumSize: WidgetStateProperty.all(const Size.fromHeight(48)),
          ),
        ),
      ),
      home: const WorkoutTimerPage(),
    );
  }
}
