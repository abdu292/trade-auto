import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../presentation/app_providers.dart';

String _sessionPhase(String session, DateTime? ksaTime) {
  if (ksaTime == null) return '';
  final h = ksaTime.hour;
  switch (session) {
    case 'JAPAN':
      if (h >= 3 && h < 5) return 'EARLY';
      if (h >= 5 && h < 8) return 'MID';
      if (h >= 8 && h < 10) return 'PEAK';
      return 'LATE';
    case 'INDIA':
      if (h >= 7 && h < 9) return 'EARLY';
      if (h >= 9 && h < 11) return 'MID';
      if (h >= 11 && h < 14) return 'PEAK';
      return 'LATE';
    case 'LONDON':
      if (h >= 10 && h < 12) return 'EARLY';
      if (h >= 12 && h < 14) return 'MID';
      if (h >= 14 && h < 17) return 'PEAK';
      return 'LATE';
    case 'NY':
      if (h >= 15 && h < 16) return 'EARLY';
      if (h >= 16 && h < 18) return 'MID';
      if (h >= 18 && h < 22) return 'PEAK';
      return 'LATE';
    default:
      return '';
  }
}

class DashboardScreen extends ConsumerWidget {
  const DashboardScreen({super.key, required this.isEmergencyPaused});

  final bool isEmergencyPaused;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final colorScheme = Theme.of(context).colorScheme;
    final health = ref.watch(healthProvider);
    final ledger = ref.watch(ledgerProvider);
    final runtime = ref.watch(runtimeStatusProvider);
    final aiHealth = ref.watch(aiHealthStatusProvider);
    final notifications = ref.watch(notificationsProvider);
    final kpi = ref.watch(kpiProvider);
    final goldDashboard = ref.watch(goldDashboardProvider);
    final hazardWindows = ref.watch(hazardWindowsProvider);

    Future<void> refresh() async {
      ref
        ..invalidate(healthProvider)
        ..invalidate(ledgerProvider)
        ..invalidate(runtimeStatusProvider)
        ..invalidate(aiHealthStatusProvider)
        ..invalidate(notificationsProvider)
        ..invalidate(kpiProvider)
        ..invalidate(hazardWindowsProvider);
    }

    return RefreshIndicator(
      onRefresh: refresh,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          // Status banner
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  CircleAvatar(
                    radius: 18,
                    backgroundColor: isEmergencyPaused
                        ? colorScheme.errorContainer
                        : colorScheme.primaryContainer,
                    child: Icon(
                      isEmergencyPaused
                          ? Icons.pause_circle_filled
                          : Icons.play_circle_fill,
                      color: isEmergencyPaused
                          ? colorScheme.error
                          : colorScheme.primary,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      isEmergencyPaused
                          ? 'Emergency pause is ON'
                          : 'Automation is active',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // A) Capital Dashboard — physical ledger truth
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Capital Dashboard',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  ledger.when(
                    data: (state) => Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        _MetricChip(
                            label: 'Cash',
                            value: 'AED ${state.cashAed.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Gold',
                            value:
                                '${state.goldGrams.toStringAsFixed(2)}g = AED ${state.goldAedEquivalent.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Net Equity',
                            value:
                                'AED ${state.netEquityAed.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Purchase Power',
                            value:
                                'AED ${state.purchasePowerAed.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Deployable',
                            value:
                                'AED ${state.deployableCashAed.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Deployed',
                            value:
                                'AED ${state.deployedAed.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Positions',
                            value:
                                'AED ${state.openPositionsAed.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Pending Reserved',
                            value:
                                'AED ${state.pendingReservedAed.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Exposure %',
                            value:
                                '${state.openExposurePercent.toStringAsFixed(1)}%'),
                        _MetricChip(
                            label: 'Open Buys',
                            value: state.openBuyCount.toString()),
                      ],
                    ),
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('Ledger error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // A2) Gold Engine Factor State (spec v7 §10.B)
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: goldDashboard.when(
                data: (dash) {
                  final panel = dash.factorStatePanel;
                  if (panel == null) {
                    return const Text(
                        'Gold Engine factor state not available yet.');
                  }
                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Gold Engine Factor State',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      const SizedBox(height: 8),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          _MetricChip(
                              label: 'Legality',
                              value: panel.legalityState.toUpperCase()),
                          _MetricChip(
                              label: 'Bias',
                              value: panel.biasState.toUpperCase()),
                          _MetricChip(
                              label: 'Path',
                              value: panel.pathState.toUpperCase()),
                          _MetricChip(
                              label: 'Overextension',
                              value: panel.overextensionState.toUpperCase()),
                          _MetricChip(
                              label: 'Waterfall',
                              value: panel.waterfallRisk.toUpperCase()),
                          _MetricChip(
                              label: 'Session',
                              value:
                                  '${panel.session} · ${panel.sessionPhase}'),
                          _MetricChip(
                              label: 'Exec Mode',
                              value: dash.executionMode.toUpperCase()),
                        ],
                      ),
                    ],
                  );
                },
                loading: () => const LinearProgressIndicator(),
                error: (e, _) => Text('Factor state error: $e'),
              ),
            ),
          ),
          const SizedBox(height: 12),

          // B) Quick Decision Panel
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Quick Decision',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  runtime.when(
                    data: (rt) {
                      final isSellSignal = rt.telegramState == 'STRONG_SELL' ||
                          rt.telegramState == 'SELL';
                      final hazardActive = rt.activeBlockedHazardWindows > 0;

                      String railAStatus;
                      String railBStatus;
                      Color railAColor;
                      Color railBColor;

                      if (hazardActive) {
                        railAStatus = 'BLOCKED';
                        railBStatus = 'BLOCKED';
                        railAColor = colorScheme.error;
                        railBColor = colorScheme.error;
                      } else if (rt.panicSuspected || isSellSignal) {
                        railAStatus = 'DEEP-ONLY';
                        railBStatus = 'BLOCKED';
                        railAColor = colorScheme.tertiary;
                        railBColor = colorScheme.error;
                      } else {
                        railAStatus = 'ALLOWED';
                        railBStatus = 'ALLOWED';
                        railAColor = colorScheme.primary;
                        railBColor = colorScheme.primary;
                      }

                      final isHealthy = health.valueOrNull ?? false;
                      final tableReady = isHealthy &&
                          !rt.panicSuspected &&
                          !hazardActive &&
                          !isSellSignal &&
                          rt.approvalQueueDepth > 0;

                      final noTrade =
                          rt.panicSuspected || hazardActive || isSellSignal;

                      final phase = _sessionPhase(rt.session, rt.ksaTime);

                      // Next hazard window – show start time (static, no stale countdown)
                      final nextHazardText = hazardWindows.whenOrNull(
                        data: (windows) {
                          final now = DateTime.now().toUtc();
                          final upcoming = windows
                              .where((w) =>
                                  w.isActive &&
                                  w.isBlocked &&
                                  w.startUtc.isAfter(now))
                              .toList();
                          if (upcoming.isEmpty) return null;
                          upcoming.sort(
                              (a, b) => a.startUtc.compareTo(b.startUtc));
                          final next = upcoming.first;
                          final ksaStart = next.startUtc
                              .toLocal()
                              .add(const Duration(hours: 3));
                          final hh = ksaStart.hour.toString().padLeft(2, '0');
                          final mm = ksaStart.minute.toString().padLeft(2, '0');
                          return '⚠️ Next hazard: ${next.title} at $hh:$mm KSA';
                        },
                      );

                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: [
                              _MetricChip(
                                  label: 'Session',
                                  value: phase.isNotEmpty
                                      ? '${rt.session} · $phase'
                                      : rt.session),
                              _MetricChip(
                                  label: 'Mode', value: rt.macroBias),
                              _MetricChip(
                                  label: 'Regime',
                                  value: rt.institutionalBias),
                              _MetricChip(
                                  label: 'Waterfall Risk',
                                  value: rt.positioningFlag),
                              _MetricChip(
                                  label: 'CB Flow',
                                  value: rt.cbFlowFlag),
                              _MetricChip(
                                  label: 'Telegram',
                                  value: rt.telegramState),
                            ],
                          ),
                          const SizedBox(height: 8),
                          Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.symmetric(
                                    horizontal: 10, vertical: 4),
                                decoration: BoxDecoration(
                                  color: railAColor.withAlpha(30),
                                  borderRadius: BorderRadius.circular(8),
                                  border:
                                      Border.all(color: railAColor, width: 1),
                                ),
                                child: Text(
                                  'Rail-A: $railAStatus',
                                  style: TextStyle(
                                      color: railAColor,
                                      fontWeight: FontWeight.bold),
                                ),
                              ),
                              const SizedBox(width: 8),
                              Container(
                                padding: const EdgeInsets.symmetric(
                                    horizontal: 10, vertical: 4),
                                decoration: BoxDecoration(
                                  color: railBColor.withAlpha(30),
                                  borderRadius: BorderRadius.circular(8),
                                  border:
                                      Border.all(color: railBColor, width: 1),
                                ),
                                child: Text(
                                  'Rail-B: $railBStatus',
                                  style: TextStyle(
                                      color: railBColor,
                                      fontWeight: FontWeight.bold),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 8),
                          if (hazardActive)
                            Text(
                              '⏱ ${rt.activeBlockedHazardWindows} hazard window(s) active',
                              style: TextStyle(color: colorScheme.error),
                            ),
                          if (nextHazardText != null) ...[
                            const SizedBox(height: 4),
                            Text(
                              nextHazardText,
                              style: TextStyle(color: colorScheme.tertiary),
                            ),
                          ],
                          const SizedBox(height: 4),
                          Container(
                            width: double.infinity,
                            padding: const EdgeInsets.symmetric(
                                horizontal: 12, vertical: 8),
                            decoration: BoxDecoration(
                              color: tableReady
                                  ? colorScheme.primaryContainer
                                  : colorScheme.errorContainer,
                              borderRadius: BorderRadius.circular(8),
                            ),
                            child: Text(
                              tableReady
                                  ? '✅ TABLE READY — ${rt.approvalQueueDepth} approval(s) pending'
                                  : noTrade
                                      ? '🛑 CAPITAL PROTECTED / NO TRADE'
                                      : '⏳ WAITING — no pending approvals',
                              style: TextStyle(
                                fontWeight: FontWeight.bold,
                                color: tableReady
                                    ? colorScheme.onPrimaryContainer
                                    : colorScheme.onErrorContainer,
                              ),
                            ),
                          ),
                        ],
                      );
                    },
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('Runtime error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // E) Compounding Tracker
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Compounding Tracker (4x)',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  kpi.when(
                    data: (stats) {
                      final c = stats.compounding;
                      final progress = (c.multiple / 4.0).clamp(0.0, 1.0);
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          _RowLabel(
                              label: 'Starting Investment',
                              value:
                                  'AED ${c.startingInvestmentAed.toStringAsFixed(2)}'),
                          _RowLabel(
                              label: 'Current Equity',
                              value:
                                  'AED ${c.currentEquityAed.toStringAsFixed(2)}'),
                          _RowLabel(
                              label: 'Multiple',
                              value: '${c.multiple.toStringAsFixed(2)}x'),
                          const SizedBox(height: 8),
                          LinearProgressIndicator(
                            value: progress,
                            backgroundColor:
                                colorScheme.surfaceContainerHighest,
                            color: c.milestoneReached
                                ? colorScheme.tertiary
                                : colorScheme.primary,
                            minHeight: 10,
                            borderRadius: BorderRadius.circular(6),
                          ),
                          const SizedBox(height: 6),
                          if (c.milestoneReached)
                            Text(
                              '🎉 4x REACHED — Ready to Pull Original Capital',
                              style: TextStyle(
                                  color: colorScheme.tertiary,
                                  fontWeight: FontWeight.bold),
                            )
                          else
                            Text(
                              '4x Target: AED ${c.neededForFourXAed.toStringAsFixed(2)} remaining',
                              style:
                                  Theme.of(context).textTheme.bodySmall,
                            ),
                        ],
                      );
                    },
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('KPI error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // F) Today's Performance
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    "Today's Performance",
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 8),
                  kpi.when(
                    data: (stats) => Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        _MetricChip(label: 'Date', value: stats.todayKsaDate),
                        _MetricChip(
                            label: 'Profit',
                            value:
                                'AED ${stats.todayProfitAed.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Rotations',
                            value: stats.todayRotations.toString()),
                        _MetricChip(
                            label: 'Avg/Rotation',
                            value:
                                'AED ${stats.todayAvgProfitAed.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Hit Rate',
                            value:
                                '${(stats.todayHitRate * 100).toStringAsFixed(1)}%'),
                      ],
                    ),
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('KPI error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // C) AI Providers Status
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('AI Providers',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  aiHealth.when(
                    data: (status) {
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: [
                              _MetricChip(
                                  label: 'Analyzers',
                                  value: status.analyzerCount.toString()),
                              _MetricChip(
                                  label: 'All 4 Active',
                                  value: status.coverage.allFourEnabled
                                      ? 'YES'
                                      : 'NO'),
                              _MetricChip(
                                  label: 'OpenAI',
                                  value:
                                      status.coverage.openai ? 'ON' : 'OFF'),
                              _MetricChip(
                                  label: 'Gemini',
                                  value:
                                      status.coverage.gemini ? 'ON' : 'OFF'),
                              _MetricChip(
                                  label: 'Grok',
                                  value: status.coverage.grok ? 'ON' : 'OFF'),
                              _MetricChip(
                                  label: 'Perplexity',
                                  value: status.coverage.perplexity
                                      ? 'ON'
                                      : 'OFF'),
                            ],
                          ),
                          if (status.analyzers.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            Text(
                                'Active analyzers: ${status.analyzers.join(', ')}'),
                          ],
                          if (status.parityBlockers.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            Text(
                              'Parity blockers: ${status.parityBlockers.join(' | ')}',
                              style: Theme.of(context)
                                  .textTheme
                                  .bodySmall
                                  ?.copyWith(color: colorScheme.error),
                            ),
                          ],
                        ],
                      );
                    },
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('AI health error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // D) System Health + Spread/Tick stats
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('System Health',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  health.when(
                    data: (ok) => Chip(
                      backgroundColor: ok
                          ? colorScheme.primaryContainer
                          : colorScheme.errorContainer,
                      avatar: Icon(ok ? Icons.check_circle : Icons.error),
                      label: Text(ok ? 'Backend healthy' : 'Backend unhealthy'),
                    ),
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('Error: $error'),
                  ),
                  const SizedBox(height: 8),
                  runtime.when(
                    data: (state) => Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        _MetricChip(label: 'Symbol', value: state.symbol),
                        _MetricChip(
                            label: 'Bid',
                            value: state.bid.toStringAsFixed(2)),
                        _MetricChip(
                            label: 'Ask',
                            value: state.ask.toStringAsFixed(2)),
                        _MetricChip(
                            label: 'Spread',
                            value: state.spread.toStringAsFixed(3)),
                        _MetricChip(
                            label: 'Spread Median 60m',
                            value: state.spreadMedian60m.toStringAsFixed(3)),
                        _MetricChip(
                            label: 'Execution Mode',
                            value: state.executionMode.toUpperCase()),
                        _MetricChip(
                            label: 'Macro Age (m)',
                            value: state.macroCacheAgeMinutes.toString()),
                        _MetricChip(
                            label: 'MT5 Time',
                            value: state.mt5ServerTime != null
                                ? state.mt5ServerTime!
                                    .toLocal()
                                    .toString()
                                    .substring(11, 19)
                                : 'N/A'),
                        _MetricChip(
                            label: 'Ticks/min',
                            value: (state.tickRatePer30s * 2)
                                .toStringAsFixed(1)),
                        _MetricChip(
                            label: 'MT5 Feed',
                            value: state.mt5ServerTime != null &&
                                    !state.freezeGapDetected
                                ? 'CONNECTED'
                                : 'DEGRADED'),
                      ],
                    ),
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('Runtime error: $error'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // G) Notifications
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Notifications',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 8),
                  notifications.when(
                    data: (items) {
                      if (items.isEmpty) {
                        return const Text('No notifications yet.');
                      }
                      return Column(
                        children: items
                            .take(10)
                            .map(
                              (item) => ListTile(
                                dense: true,
                                contentPadding: EdgeInsets.zero,
                                leading: Container(
                                  width: 28,
                                  height: 28,
                                  decoration: BoxDecoration(
                                    color: colorScheme.secondaryContainer,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: const Icon(Icons.notifications,
                                      size: 18),
                                ),
                                title: Text(item.title),
                                subtitle: Text(
                                    '${item.channel} • ${item.message}'),
                              ),
                            )
                            .toList(),
                      );
                    },
                    loading: () => const LinearProgressIndicator(),
                    error: (error, _) => Text('Notifications error: $error'),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _RowLabel extends StatelessWidget {
  const _RowLabel({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label),
          Text(value, style: const TextStyle(fontWeight: FontWeight.bold)),
        ],
      ),
    );
  }
}

class _MetricChip extends StatelessWidget {
  const _MetricChip({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Chip(
      visualDensity: VisualDensity.compact,
      label: Text('$label: $value'),
    );
  }
}

class _AnimatedCard extends StatelessWidget {
  const _AnimatedCard({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOut,
      child: Card(child: child),
    );
  }
}

