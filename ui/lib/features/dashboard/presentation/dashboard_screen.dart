import 'package:flutter/material.dart';

class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key, required this.isEmergencyPaused});

  final bool isEmergencyPaused;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text('DashboardScreen'),
              const SizedBox(height: 8),
              Text('Emergency Pause: ${isEmergencyPaused ? 'ON' : 'OFF'}'),
            ],
          ),
        ),
      ),
    );
  }
}
