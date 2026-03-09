import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../features/dashboard/presentation/dashboard_screen.dart';
import '../features/live_feed/presentation/live_feed_screen.dart';
import '../features/more/presentation/more_screen.dart';
import '../features/trades/presentation/trades_screen.dart';
import '../features/live_feed/presentation/filter_dialog.dart';
import 'app_providers.dart';

class AppShell extends ConsumerStatefulWidget {
  const AppShell({super.key});

  @override
  ConsumerState<AppShell> createState() => _AppShellState();
}

class _AppShellState extends ConsumerState<AppShell> {
  int _index = 0;


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
    final emergencyPaused = ref.watch(emergencyPauseProvider);
    final runtimeSettings = ref.watch(runtimeSettingsProvider);
    final showPause = emergencyPaused &&
        runtimeSettings.maybeWhen(
            data: (s) => s.autoTradeEnabled, orElse: () => false);

    final navItems = [
      (
        label: 'Dashboard',
        icon: Icons.dashboard_outlined,
        selectedIcon: Icons.dashboard,
        screen: DashboardScreen(isEmergencyPaused: showPause),
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
          IconButton(
            onPressed: _invalidateAll,
            tooltip: 'Refresh all',
            icon: const Icon(Icons.refresh),
          ),
          if (_index == 1) // "Monitor" tab
            IconButton(
              icon: const Icon(Icons.filter_list),
              tooltip: 'Filter live feed',
              onPressed: () {
                showDialog(
                  context: context,
                  builder: (ctx) => const FilterDialog(),
                );
              },
            ),
        ],
      ),
      body: Column(
        children: [
          // Emergency pause banner - now driven by provider and auto-trade
          Consumer(builder: (context, ref, _) {
            final paused = ref.watch(emergencyPauseProvider);
            final runtimeSettings = ref.watch(runtimeSettingsProvider);
            final showPause = paused &&
                runtimeSettings.maybeWhen(
                    data: (s) => s.autoTradeEnabled, orElse: () => false);
            return AnimatedSize(
              duration: const Duration(milliseconds: 200),
              curve: Curves.easeOut,
              child: showPause
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
            );
          }),

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
