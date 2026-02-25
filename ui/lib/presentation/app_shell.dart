import 'package:flutter/material.dart';

import '../features/dashboard/presentation/dashboard_screen.dart';
import '../features/risk/presentation/risk_control_screen.dart';
import '../features/sessions/presentation/session_overview_screen.dart';
import '../features/strategies/presentation/strategy_control_screen.dart';
import '../features/trades/presentation/trades_screen.dart';

class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _index = 0;
  bool _isEmergencyPaused = false;

  @override
  Widget build(BuildContext context) {
    final screens = [
      DashboardScreen(isEmergencyPaused: _isEmergencyPaused),
      const StrategyControlScreen(),
      const RiskControlScreen(),
      const TradesScreen(),
      const SessionOverviewScreen(),
    ];

    return Scaffold(
      appBar: AppBar(
        title: const Text('Trade Auto'),
        actions: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: FilledButton(
              onPressed: () => setState(() => _isEmergencyPaused = !_isEmergencyPaused),
              child: Text(_isEmergencyPaused ? 'Resume' : 'Emergency Pause'),
            ),
          ),
        ],
      ),
      body: Row(
        children: [
          NavigationRail(
            selectedIndex: _index,
            onDestinationSelected: (value) => setState(() => _index = value),
            labelType: NavigationRailLabelType.all,
            destinations: const [
              NavigationRailDestination(icon: Icon(Icons.dashboard), label: Text('Dashboard')),
              NavigationRailDestination(icon: Icon(Icons.tune), label: Text('Strategies')),
              NavigationRailDestination(icon: Icon(Icons.shield), label: Text('Risk')),
              NavigationRailDestination(icon: Icon(Icons.swap_horiz), label: Text('Trades')),
              NavigationRailDestination(icon: Icon(Icons.schedule), label: Text('Sessions')),
            ],
          ),
          const VerticalDivider(width: 1),
          Expanded(child: screens[_index]),
        ],
      ),
    );
  }
}
