import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/network/api_client.dart';
import '../features/dashboard/presentation/dashboard_screen.dart';
import '../features/ledger/presentation/ledger_screen.dart';
import '../features/live_feed/presentation/live_feed_screen.dart';
import '../features/risk/presentation/risk_control_screen.dart';
import '../features/sessions/presentation/session_overview_screen.dart';
import '../features/trades/presentation/trades_screen.dart';
import 'app_providers.dart';

class AppShell extends ConsumerStatefulWidget {
  const AppShell({super.key});

  @override
  ConsumerState<AppShell> createState() => _AppShellState();
}

class _AppShellState extends ConsumerState<AppShell> {
  int _index = 0;
  bool _isEmergencyPaused = false;

  static const _navigationLabels = [
    'Live Feed',
    'Dashboard',
    'Trades',
    'Sessions',
    'Risk',
    'Ledger',
  ];

  Future<void> _openEnvironmentDialog(BuildContext context) async {
    final selected = ref.read(selectedApiEnvironmentProvider);
    final effectiveBase = ref.read(effectiveApiBaseUrlProvider);

    ApiEnvironment tempEnvironment = selected;

    final saved = await showDialog<bool>(
      context: context,
      builder: (dialogContext) {
        return StatefulBuilder(
          builder: (context, setDialogState) {
            return AlertDialog(
              title: const Text('API Environment'),
              content: SizedBox(
                width: 520,
                child: SingleChildScrollView(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      DropdownButtonFormField<ApiEnvironment>(
                        initialValue: tempEnvironment,
                        decoration: const InputDecoration(
                          labelText: 'Environment',
                        ),
                        items: const [
                          DropdownMenuItem(
                            value: ApiEnvironment.production,
                            child: Text('Production (default)'),
                          ),
                          DropdownMenuItem(
                            value: ApiEnvironment.local,
                            child: Text('Local'),
                          ),
                        ],
                        onChanged: (value) {
                          if (value == null) {
                            return;
                          }
                          setDialogState(() => tempEnvironment = value);
                        },
                      ),
                      const SizedBox(height: 12),
                      Text(
                        'Current effective URL: $effectiveBase',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ),
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(false),
                  child: const Text('Cancel'),
                ),
                FilledButton(
                  onPressed: () => Navigator.of(dialogContext).pop(true),
                  child: const Text('Save'),
                ),
              ],
            );
          },
        );
      },
    );

    if (saved == true) {
      ref.read(selectedApiEnvironmentProvider.notifier).state = tempEnvironment;

      ref
        ..invalidate(healthProvider)
        ..invalidate(ledgerProvider)
        ..invalidate(notificationsProvider)
        ..invalidate(approvalsProvider)
        ..invalidate(riskProfilesProvider)
        ..invalidate(hazardWindowsProvider)
        ..invalidate(activeTradesProvider)
        ..invalidate(signalsProvider)
        ..invalidate(timelineProvider)
        ..invalidate(sessionsProvider)
        ..invalidate(runtimeStatusProvider)
        ..invalidate(kpiProvider);
    }
  }

  @override
  Widget build(BuildContext context) {
    final apiEnvironment = ref.watch(selectedApiEnvironmentProvider);
    final effectiveApiBaseUrl = ref.watch(effectiveApiBaseUrlProvider);

    final screens = <Widget>[
      const LiveFeedScreen(),
      DashboardScreen(isEmergencyPaused: _isEmergencyPaused),
      const TradesScreen(),
      const SessionOverviewScreen(),
      const RiskControlScreen(),
      const LedgerScreen(),
    ];

    final destinations = const <NavigationDestination>[
      NavigationDestination(
          icon: Icon(Icons.stream_outlined),
          selectedIcon: Icon(Icons.stream),
          label: 'Live Feed'),
      NavigationDestination(
          icon: Icon(Icons.dashboard_outlined),
          selectedIcon: Icon(Icons.dashboard),
          label: 'Dashboard'),
      NavigationDestination(
          icon: Icon(Icons.swap_horiz_outlined),
          selectedIcon: Icon(Icons.swap_horiz),
          label: 'Trades'),
      NavigationDestination(
          icon: Icon(Icons.schedule_outlined),
          selectedIcon: Icon(Icons.schedule),
          label: 'Sessions'),
      NavigationDestination(
          icon: Icon(Icons.shield_outlined),
          selectedIcon: Icon(Icons.shield),
          label: 'Risk'),
      NavigationDestination(
          icon: Icon(Icons.account_balance_outlined),
          selectedIcon: Icon(Icons.account_balance),
          label: 'Ledger'),
    ];

    Future<void> refreshEverything() async {
      ref
        ..invalidate(healthProvider)
        ..invalidate(ledgerProvider)
        ..invalidate(notificationsProvider)
        ..invalidate(approvalsProvider)
        ..invalidate(riskProfilesProvider)
        ..invalidate(hazardWindowsProvider)
        ..invalidate(activeTradesProvider)
        ..invalidate(signalsProvider)
        ..invalidate(timelineProvider)
        ..invalidate(sessionsProvider)
        ..invalidate(runtimeStatusProvider)
        ..invalidate(kpiProvider);
    }

    final isCompact = MediaQuery.sizeOf(context).width < 900;

    return Scaffold(
      appBar: AppBar(
        title: Text(_navigationLabels[_index]),
        actions: [
          Center(
            child: Padding(
              padding: const EdgeInsets.only(right: 8),
              child: Tooltip(
                message: effectiveApiBaseUrl,
                child: Chip(
                  label: Text(
                    apiEnvironment == ApiEnvironment.production
                        ? 'Production'
                        : 'Local',
                  ),
                ),
              ),
            ),
          ),
          IconButton(
            onPressed: () => _openEnvironmentDialog(context),
            tooltip: 'API Environment',
            icon: const Icon(Icons.cloud_sync_outlined),
          ),
          IconButton(
            onPressed: refreshEverything,
            tooltip: 'Refresh',
            icon: const Icon(Icons.refresh),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: FilledButton(
              onPressed: () =>
                  setState(() => _isEmergencyPaused = !_isEmergencyPaused),
              child: Text(_isEmergencyPaused ? 'Resume' : 'Emergency Pause'),
            ),
          ),
        ],
      ),
      body: Column(
        children: [
          AnimatedSize(
            duration: const Duration(milliseconds: 200),
            curve: Curves.easeOut,
            child: _isEmergencyPaused
                ? Material(
                    color: Theme.of(context).colorScheme.errorContainer,
                    child: const Padding(
                      padding:
                          EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                      child: Row(
                        children: [
                          Icon(Icons.pause_circle_filled),
                          SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              'Emergency pause enabled: manual actions only.',
                            ),
                          ),
                        ],
                      ),
                    ),
                  )
                : const SizedBox.shrink(),
          ),
          Expanded(
            child: isCompact
                ? AnimatedSwitcher(
                    duration: const Duration(milliseconds: 220),
                    switchInCurve: Curves.easeOut,
                    switchOutCurve: Curves.easeIn,
                    child: KeyedSubtree(
                      key: ValueKey(_index),
                      child: screens[_index],
                    ),
                  )
                : Row(
                    children: [
                      NavigationRail(
                        selectedIndex: _index,
                        onDestinationSelected: (value) =>
                            setState(() => _index = value),
                        labelType: NavigationRailLabelType.all,
                        destinations: const [
                          NavigationRailDestination(
                              icon: Icon(Icons.stream),
                              label: Text('Live Feed')),
                          NavigationRailDestination(
                              icon: Icon(Icons.dashboard),
                              label: Text('Dashboard')),
                          NavigationRailDestination(
                              icon: Icon(Icons.swap_horiz),
                              label: Text('Trades')),
                          NavigationRailDestination(
                              icon: Icon(Icons.schedule),
                              label: Text('Sessions')),
                          NavigationRailDestination(
                              icon: Icon(Icons.shield), label: Text('Risk')),
                          NavigationRailDestination(
                              icon: Icon(Icons.account_balance),
                              label: Text('Ledger')),
                        ],
                      ),
                      const VerticalDivider(width: 1),
                      Expanded(
                        child: AnimatedSwitcher(
                          duration: const Duration(milliseconds: 220),
                          switchInCurve: Curves.easeOut,
                          switchOutCurve: Curves.easeIn,
                          child: KeyedSubtree(
                            key: ValueKey(_index),
                            child: screens[_index],
                          ),
                        ),
                      ),
                    ],
                  ),
          ),
        ],
      ),
      bottomNavigationBar: isCompact
          ? NavigationBar(
              selectedIndex: _index,
              destinations: destinations,
              onDestinationSelected: (value) => setState(() => _index = value),
            )
          : null,
    );
  }
}
