import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/network/api_client.dart';
import '../features/dashboard/presentation/dashboard_screen.dart';
import '../features/live_feed/presentation/live_feed_screen.dart';
import '../features/more/presentation/more_screen.dart';
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
                          if (value == null) return;
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
      await ref
          .read(selectedApiEnvironmentProvider.notifier)
          .setEnvironment(tempEnvironment);
      _invalidateAll();
    }
  }

  void _invalidateAll() {
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

  @override
  Widget build(BuildContext context) {
    final apiEnvironment = ref.watch(selectedApiEnvironmentProvider);
    final effectiveApiBaseUrl = ref.watch(effectiveApiBaseUrlProvider);

    final navItems = [
      (
        label: 'Dashboard',
        icon: Icons.dashboard_outlined,
        selectedIcon: Icons.dashboard,
        screen: DashboardScreen(isEmergencyPaused: _isEmergencyPaused),
      ),
      (
        label: 'Monitor',
        icon: Icons.stream_outlined,
        selectedIcon: Icons.stream,
        screen: const LiveFeedScreen(),
      ),
      (
        label: 'Trades',
        icon: Icons.swap_horiz_outlined,
        selectedIcon: Icons.swap_horiz,
        screen: const TradesScreen(),
      ),
      (
        label: 'More',
        icon: Icons.apps_outlined,
        selectedIcon: Icons.apps,
        screen: const MoreScreen(),
      ),
    ];

    final screens = navItems.map((item) => item.screen).toList();

    final destinations = navItems
        .map(
          (item) => NavigationDestination(
            icon: Icon(item.icon),
            selectedIcon: Icon(item.selectedIcon),
            label: item.label,
          ),
        )
        .toList(growable: false);

    final isCompact = MediaQuery.sizeOf(context).width < 900;

    return Scaffold(
      appBar: AppBar(
        title: Text(navItems[_index].label),
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
            onPressed: _invalidateAll,
            tooltip: 'Refresh all',
            icon: const Icon(Icons.refresh),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: FilledButton(
              onPressed: () =>
                  setState(() => _isEmergencyPaused = !_isEmergencyPaused),
              style: _isEmergencyPaused
                  ? FilledButton.styleFrom(
                      backgroundColor:
                          Theme.of(context).colorScheme.errorContainer,
                      foregroundColor:
                          Theme.of(context).colorScheme.onErrorContainer,
                    )
                  : null,
              child: Text(_isEmergencyPaused ? 'Resume' : 'Emergency Pause'),
            ),
          ),
        ],
      ),
      body: Column(
        children: [
          // Emergency pause banner
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
                              'Emergency pause enabled — manual actions only.',
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
                        destinations: navItems
                            .map(
                              (item) => NavigationRailDestination(
                                icon: Icon(item.icon),
                                selectedIcon: Icon(item.selectedIcon),
                                label: Text(item.label),
                              ),
                            )
                            .toList(growable: false),
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
