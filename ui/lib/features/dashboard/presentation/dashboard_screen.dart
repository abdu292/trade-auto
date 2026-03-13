import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../domain/models.dart';
import '../../../presentation/app_providers.dart';

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
    final marketState = ref.watch(marketStateProvider);
    final hazardWindows = ref.watch(hazardWindowsProvider);
    final liveFeed = ref.watch(timelineProvider);

    Future<void> refresh() async {
      ref
        ..invalidate(healthProvider)
        ..invalidate(ledgerProvider)
        ..invalidate(runtimeStatusProvider)
        ..invalidate(aiHealthStatusProvider)
        ..invalidate(notificationsProvider)
        ..invalidate(kpiProvider)
        ..invalidate(hazardWindowsProvider)
        ..invalidate(goldDashboardProvider)
        ..invalidate(marketStateProvider)
        ..invalidate(timelineProvider);
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

          // A) Capital Dashboard — physical ledger (source of truth) and MT5 execution (separate)
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Capital Dashboard',
                      style: Theme.of(context).textTheme.titleMedium),
                  const SizedBox(height: 12),
                  Text('Physical ledger (source of truth)',
                      style: Theme.of(context).textTheme.titleSmall),
                  const SizedBox(height: 6),
                  ledger.when(
                    data: (state) => Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        _MetricChip(
                            label: 'Cash (AED)',
                            value: state.cashAed.toStringAsFixed(2)),
                        _MetricChip(
                            label: 'Gold (g)',
                            value: state.goldGrams.toStringAsFixed(2)),
                        _MetricChip(
                            label: 'Gold ≈ AED',
                            value: state.goldAedEquivalent.toStringAsFixed(2)),
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
                  const SizedBox(height: 16),
                  Text('MT5 execution account (for reference only)',
                      style: Theme.of(context).textTheme.titleSmall),
                  const SizedBox(height: 6),
                  runtime.when(
                    data: (rt) => Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        _MetricChip(
                            label: 'MT5 Balance',
                            value: 'AED ${rt.balance.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'MT5 Equity',
                            value: 'AED ${rt.equity.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Free Margin',
                            value: 'AED ${rt.freeMargin.toStringAsFixed(2)}'),
                        _MetricChip(
                            label: 'Bid / Ask',
                            value:
                                '${rt.bid.toStringAsFixed(2)} / ${rt.ask.toStringAsFixed(2)}'),
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
                              value: panel.pathStateDisplay.toUpperCase()),
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
                          // Spec v8 §11 — Rotation Efficiency state
                          _MetricChip(
                              label: 'Efficiency',
                              value: panel.efficiencyState.toUpperCase()),
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

          // A2a2) Path Map card — market bias, current path state (ladder), next likely move, nearest legal entry zone, why blocked/armed
          goldDashboard.when(
            data: (dash) {
              final pathMap = dash.pathMap;
              if (pathMap == null) return const SizedBox.shrink();
              return _AnimatedCard(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Path Map',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                      Text(
                        'Market bias, current path state, next likely move, nearest legal entry zone, why blocked/armed.',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: colorScheme.onSurfaceVariant,
                            ),
                      ),
                      const SizedBox(height: 12),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          _MetricChip(
                            label: 'Market Bias',
                            value: pathMap.marketBias.toUpperCase(),
                          ),
                          _MetricChip(
                            label: 'Path State',
                            value: pathMap.currentPathState.toUpperCase(),
                          ),
                          _MetricChip(
                            label: 'Next Likely Move',
                            value: pathMap.nextLikelyMove,
                          ),
                          if (pathMap.nearestLegalEntryZone != null)
                            _MetricChip(
                              label: 'Nearest Legal Entry Zone',
                              value: pathMap.nearestLegalEntryZone!.toStringAsFixed(2),
                            ),
                          if (pathMap.whyBlockedOrArmed != null &&
                              pathMap.whyBlockedOrArmed!.isNotEmpty)
                            _MetricChip(
                              label: 'Why Blocked / Armed',
                              value: pathMap.whyBlockedOrArmed!,
                            ),
                        ],
                      ),
                    ],
                  ),
                ),
              );
            },
            loading: () => const SizedBox.shrink(),
            error: (_, __) => const SizedBox.shrink(),
          ),
          const SizedBox(height: 12),

          // A2b) Rates / session range — always from market state (independent of regime/trade). Where rates are heading.
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Rates & session range',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Text(
                    'Market state (always shown). Session high/low and current price. Pull to refresh.',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: colorScheme.onSurfaceVariant,
                        ),
                  ),
                  const SizedBox(height: 12),
                  marketState.when(
                    data: (ms) {
                      final current = (ms.bid + ms.ask) / 2;
                      return goldDashboard.when(
                        data: (dash) {
                          final chart = dash.tradeMapChart;
                          final sessionHigh = ms.sessionHigh > 0 ? ms.sessionHigh : chart.sessionHigh;
                          final sessionLow = ms.sessionLow > 0 ? ms.sessionLow : chart.sessionLow;
                          if (sessionHigh <= 0 || sessionLow <= 0) {
                            return Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: [
                                _MetricChip(
                                    label: 'Bid / Ask',
                                    value:
                                        '${ms.bid.toStringAsFixed(2)} / ${ms.ask.toStringAsFixed(2)}'),
                                _MetricChip(
                                    label: 'Session', value: '${ms.session} · ${ms.sessionPhase}'),
                              ],
                            );
                          }
                          return _RatesSessionChart(
                            sessionHigh: sessionHigh,
                            sessionLow: sessionLow,
                            currentPrice: current,
                            bases: chart.bases,
                            pendingBuyLimit: chart.pendingBuyLimit,
                            pendingBuyStop: chart.pendingBuyStop,
                          );
                        },
                        loading: () => _RatesFallback(ms),
                        error: (_, __) => _RatesFallback(ms),
                      );
                    },
                    loading: () => runtime.when(
                      data: (rt) => _MetricChip(
                          label: 'Bid / Ask',
                          value:
                              '${rt.bid.toStringAsFixed(2)} / ${rt.ask.toStringAsFixed(2)}'),
                      loading: () => const LinearProgressIndicator(),
                      error: (_, __) => const LinearProgressIndicator(),
                    ),
                    error: (_, __) => runtime.when(
                      data: (rt) => _MetricChip(
                          label: 'Bid / Ask',
                          value:
                              '${rt.bid.toStringAsFixed(2)} / ${rt.ask.toStringAsFixed(2)}'),
                      loading: () => const LinearProgressIndicator(),
                      error: (e, _) => Text('Market state error: $e'),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // A2c) Market Direction / Heading Panel — shows where rates are heading
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Market Direction / Heading',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Text(
                    'Current bias, next path, and nearest legal trade zone',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: colorScheme.onSurfaceVariant,
                        ),
                  ),
                  const SizedBox(height: 12),
                  liveFeed.when(
                    data: (events) {
                      if (events.isEmpty) {
                        return const Text('Direction data unavailable');
                      }
                      // Find latest ANALYZE_STARTED or PATH_PROJECTION event
                      final analyzeEvent = events.firstWhere(
                        (e) => e.eventType == 'ANALYZE_STARTED' ||
                            e.eventType == 'STATE_06B_PATH_PROJECTION',
                        orElse: () => events.first,
                      );
                      
                      final payload = analyzeEvent.payload;
                      final pathBias = payload?['pathBias']?.toString() ?? 
                          payload?['nextLikelyPath']?.toString() ?? 'UNKNOWN';
                      final patternType = payload?['patternType']?.toString() ?? 'NONE';
                      final bottomType = payload?['bottomType']?.toString() ?? 'NONE';
                      final compressionState = payload?['compressionState']?.toString() ?? 'UNKNOWN';
                      final expansionState = payload?['expansionState']?.toString() ?? 'UNKNOWN';
                      final nearestMagnet = payload?['nearestMagnet'];
                      final primaryTradeConcept = payload?['primaryTradeConcept']?.toString() ?? 'STANDARD';
                      
                      // Determine state: attack / reject / reclaim / stall
                      String stateLabel = 'STALL';
                      Color stateColor = colorScheme.onSurfaceVariant;
                      if (compressionState == 'COMPRESSION') {
                        stateLabel = 'COMPRESSION';
                        stateColor = colorScheme.primary;
                      } else if (expansionState == 'EXPANSION') {
                        stateLabel = 'EXPANSION';
                        stateColor = colorScheme.tertiary;
                      }
                      if (patternType.contains('RECLAIM') || bottomType.contains('RECLAIM')) {
                        stateLabel = 'RECLAIM';
                        stateColor = colorScheme.primary;
                      }
                      if (patternType.contains('WATERFALL')) {
                        stateLabel = 'ATTACK';
                        stateColor = colorScheme.error;
                      }
                      
                      final nearestMagnetValue = nearestMagnet != null
                          ? (nearestMagnet is num
                              ? nearestMagnet.toStringAsFixed(2)
                              : nearestMagnet.toString())
                          : 'N/A';
                      
                      return Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          _MetricChip(
                            label: 'Path Bias',
                            value: pathBias,
                          ),
                          Chip(
                            visualDensity: VisualDensity.compact,
                            label: Text('State: $stateLabel', style: TextStyle(color: stateColor)),
                          ),
                          _MetricChip(
                            label: 'Pattern',
                            value: patternType,
                          ),
                          _MetricChip(
                            label: 'Nearest Zone',
                            value: nearestMagnetValue,
                          ),
                          _MetricChip(
                            label: 'Concept',
                            value: primaryTradeConcept,
                          ),
                        ],
                      );
                    },
                    loading: () => const LinearProgressIndicator(),
                    error: (_, __) => const Text('Direction data unavailable'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),

          // B) Engine Status — Rail permissions, hazard, TABLE/approval (doc: Final Decision, Rail permissions)
          _AnimatedCard(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Engine Status',
                      style: Theme.of(context).textTheme.titleMedium),
                  Text(
                    'Rail permissions, hazard windows, and TABLE/approval state. Pull to refresh for latest.',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: colorScheme.onSurfaceVariant,
                        ),
                  ),
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
                                value: rt.session,
                              ),
                              if (rt.ksaTime != null)
                                _MetricChip(
                                  label: 'KSA Time',
                                  value:
                                      '${rt.ksaTime!.hour.toString().padLeft(2, '0')}:${rt.ksaTime!.minute.toString().padLeft(2, '0')}',
                                ),
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

          // E) Today's Performance
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

/// When we have market state but dashboard is loading/error: show chart from market state only.
Widget _RatesFallback(MarketState ms) {
  final current = (ms.bid + ms.ask) / 2;
  if (ms.sessionHigh <= 0 || ms.sessionLow <= 0) {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        _MetricChip(
            label: 'Bid / Ask',
            value: '${ms.bid.toStringAsFixed(2)} / ${ms.ask.toStringAsFixed(2)}'),
        _MetricChip(label: 'Session', value: '${ms.session} · ${ms.sessionPhase}'),
      ],
    );
  }
  return _RatesSessionChart(
    sessionHigh: ms.sessionHigh,
    sessionLow: ms.sessionLow,
    currentPrice: current,
    bases: const [],
    pendingBuyLimit: const [],
    pendingBuyStop: const [],
  );
}

/// Compact chart: session high/low range with current price and optional bases/pending levels.
class _RatesSessionChart extends StatelessWidget {
  const _RatesSessionChart({
    required this.sessionHigh,
    required this.sessionLow,
    required this.currentPrice,
    this.bases = const [],
    this.pendingBuyLimit = const [],
    this.pendingBuyStop = const [],
  });

  final double sessionHigh;
  final double sessionLow;
  final double currentPrice;
  final List<double> bases;
  final List<PendingLevelSummary> pendingBuyLimit;
  final List<PendingLevelSummary> pendingBuyStop;

  @override
  Widget build(BuildContext context) {
    final range = sessionHigh - sessionLow;
    if (range <= 0) {
      return Text(
        'Session: \$${sessionLow.toStringAsFixed(2)} – \$${sessionHigh.toStringAsFixed(2)} · Now: \$${currentPrice.toStringAsFixed(2)}',
        style: Theme.of(context).textTheme.bodyMedium,
      );
    }
    final padding = range * 0.02;
    final min = sessionLow - padding;
    final max = sessionHigh + padding;
    final span = max - min;
    double pos(double v) => ((v - min) / span).clamp(0.0, 1.0);

    return LayoutBuilder(builder: (context, constraints) {
      final width = constraints.maxWidth;
      final theme = Theme.of(context);
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Text(
                'Session high ',
                style: theme.textTheme.labelSmall,
              ),
              Text(
                '\$${sessionHigh.toStringAsFixed(2)}',
                style: theme.textTheme.labelSmall?.copyWith(
                    fontWeight: FontWeight.w600, color: Colors.green.shade700),
              ),
              const SizedBox(width: 12),
              Text(
                'Current ',
                style: theme.textTheme.labelSmall,
              ),
              Text(
                '\$${currentPrice.toStringAsFixed(2)}',
                style: theme.textTheme.labelSmall?.copyWith(
                    fontWeight: FontWeight.bold,
                    color: theme.colorScheme.primary),
              ),
              const SizedBox(width: 12),
              Text(
                'Session low ',
                style: theme.textTheme.labelSmall,
              ),
              Text(
                '\$${sessionLow.toStringAsFixed(2)}',
                style: theme.textTheme.labelSmall?.copyWith(
                    fontWeight: FontWeight.w600, color: Colors.red.shade700),
              ),
            ],
          ),
          const SizedBox(height: 10),
          SizedBox(
            height: 36,
            child: Stack(
              children: [
                Positioned.fill(
                  child: Container(
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(6),
                      gradient: LinearGradient(
                        begin: Alignment.centerLeft,
                        end: Alignment.centerRight,
                        colors: [
                          Colors.red.shade100,
                          Colors.orange.shade100,
                          Colors.green.shade100,
                        ],
                      ),
                      border: Border.all(
                          color: theme.colorScheme.outline.withOpacity(0.3)),
                    ),
                  ),
                ),
                ...bases.take(5).map((base) {
                  final left = pos(base) * width;
                  return Positioned(
                    left: (left - 1).clamp(0.0, width - 2),
                    top: 4,
                    bottom: 4,
                    child: Container(
                      width: 2,
                      decoration: BoxDecoration(
                        color: theme.colorScheme.outline.withOpacity(0.7),
                        borderRadius: BorderRadius.circular(1),
                      ),
                    ),
                  );
                }),
                ...pendingBuyLimit.take(2).map((p) {
                  final left = pos(p.price) * width;
                  return Positioned(
                    left: (left - 1).clamp(0.0, width - 2),
                    top: 2,
                    child: Icon(Icons.arrow_downward,
                        size: 14, color: Colors.blue.shade700),
                  );
                }),
                ...pendingBuyStop.take(2).map((p) {
                  final left = pos(p.price) * width;
                  return Positioned(
                    left: (left - 1).clamp(0.0, width - 2),
                    top: 2,
                    child: Icon(Icons.arrow_upward,
                        size: 14, color: Colors.green.shade700),
                  );
                }),
                Positioned(
                  left: (pos(currentPrice) * width - 6).clamp(0.0, width - 12),
                  top: 0,
                  bottom: 0,
                  child: Center(
                    child: Container(
                      width: 12,
                      height: 12,
                      decoration: BoxDecoration(
                        color: theme.colorScheme.primary,
                        shape: BoxShape.circle,
                        border: Border.all(
                            color: theme.colorScheme.surface, width: 2),
                        boxShadow: [
                          BoxShadow(
                            color: theme.colorScheme.primary.withOpacity(0.5),
                            blurRadius: 4,
                            offset: const Offset(0, 1),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      );
    });
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

