import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../domain/models.dart';
import '../../../presentation/app_providers.dart';

// ─── Providers ────────────────────────────────────────────────────────────────

/// Fetches the most recent 100 timeline events for trade-map visualization.
final tradeMapTimelineProvider =
    FutureProvider<List<RuntimeTimelineItem>>((ref) {
  return ref.watch(brainApiProvider).getTimelineEvents(take: 100);
});

// ─── Data models ─────────────────────────────────────────────────────────────

/// Summarises the engine state for a single display cycle, extracted from
/// timeline events.  All trading logic stays in the backend; this object
/// only reflects what the engine already decided.
class _CycleView {
  const _CycleView({
    this.cycleId,
    this.session,
    this.currentPrice,
    this.pretableLevel,
    this.pretableRiskScore,
    this.pretableRiskFlags = const [],
    this.patterns = const [],
    this.pendingOrders = const [],
    this.decisionRail,
    this.decisionEntry,
    this.decisionTp,
    this.decisionGrams,
    this.decisionExpiry,
    this.rotationMode,
    this.impulseExhaustionLevel,
    this.dynamicSessionModifier,
    this.waterfallRisk,
    this.candidateRegime,
    this.candidateStructureValid,
    this.candidateRiskLevel,
    this.candidateExecutionMode,
    this.freeAed,
    this.existingGoldGrams,
    this.maxBuyableGrams,
    this.openExposureGrams,
    this.pendingExposureGrams,
    this.microModeActive,
    this.latestSlipSummary,
    this.realizedProfitTodayAed,
    this.openOrdersCount,
    this.finalDecision,
    this.finalDecisionReason,
    this.capturedAt,
  });

  final String? cycleId;
  final String? session;
  final double? currentPrice;

  /// PRETABLE level: SAFE | CAUTION | BLOCK
  final String? pretableLevel;
  final double? pretableRiskScore;
  final List<String> pretableRiskFlags;

  /// Pattern Detector results
  final List<_PatternView> patterns;

  /// Pending orders from the engine
  final List<_OrderView> pendingOrders;

  /// Decision Engine output
  final String? decisionRail;
  final double? decisionEntry;
  final double? decisionTp;
  final double? decisionGrams;
  final DateTime? decisionExpiry;
  final String? rotationMode;
  final String? impulseExhaustionLevel;
  final double? dynamicSessionModifier;
  final String? waterfallRisk;

  /// Candidate panel fields
  final String? candidateRegime;
  final bool? candidateStructureValid;
  final String? candidateRiskLevel;
  final String? candidateExecutionMode;

  /// Capital panel fields
  final double? freeAed;
  final double? existingGoldGrams;
  final double? maxBuyableGrams;
  final double? openExposureGrams;
  final double? pendingExposureGrams;
  final bool? microModeActive;

  /// Ledger/slips panel fields
  final String? latestSlipSummary;
  final double? realizedProfitTodayAed;
  final int? openOrdersCount;

  final String? finalDecision;
  final String? finalDecisionReason;
  final DateTime? capturedAt;
}

class _PatternView {
  const _PatternView({
    required this.patternType,
    required this.subtype,
    required this.confidence,
    required this.entrySafety,
    required this.waterfallRisk,
    required this.failThreatened,
    required this.recommendedAction,
  });

  final String patternType;
  final String subtype;
  final double confidence;
  final String entrySafety;
  final String waterfallRisk;
  final bool failThreatened;
  final String recommendedAction;
}

class _OrderView {
  const _OrderView({
    required this.type,
    required this.price,
    required this.tp,
    this.expiry,
    required this.gramsEquivalent,
  });

  final String type;
  final double price;
  final double tp;
  final DateTime? expiry;
  final double gramsEquivalent;
}

// ─── Engine output extraction ─────────────────────────────────────────────────

_CycleView _buildCycleView(
  List<RuntimeTimelineItem> events,
  List<TradeItem> trades,
  RuntimeStatus? runtime,
  RuntimeSettings? runtimeSettings,
  LedgerState? ledger,
  KpiStats? kpi,
  List<NotificationFeedItem> notifications,
) {
  // Find the most recent PRETABLE result
  final pretableEvent =
      events.lastWhereOrNull((e) => e.eventType == 'PRETABLE_RESULT');

  // Find the most recent pattern detector results
  final patternEvent =
      events.lastWhereOrNull((e) => e.eventType == 'PATTERN_DETECTOR_RESULTS');

  // Find the most recent market snapshot for price
  final snapshotEvent = events
      .lastWhereOrNull((e) => e.eventType == 'MT5_MARKET_SNAPSHOT_RECEIVED');

  // Find the most recent decision evaluation
  final decisionEvent =
      events.lastWhereOrNull((e) => e.eventType == 'DECISION_EVALUATED');

  // Find the most recent final decision
  final finalDecisionEvent =
      events.lastWhereOrNull((e) => e.eventType == 'FINAL_DECISION');

  // Find the most recent candidate context from study logging
  final candidateEvent =
      events.lastWhereOrNull((e) => e.eventType == 'STUDY_CANDIDATE_LOG');

  // Find the most recent capital utilization check
  final capitalEvent =
      events.lastWhereOrNull((e) => e.eventType == 'CAPITAL_UTILIZATION_CHECK');

  // Find the most recent rotation optimizer result
  final rotationEvent =
      events.lastWhereOrNull((e) => e.eventType == 'ROTATION_OPTIMIZER');

  String? pretableLevel;
  double? pretableRiskScore;
  List<String> pretableFlags = [];
  String? impulseLevel;
  double? dynamicModifier;

  if (pretableEvent != null) {
    final p = pretableEvent.payload;
    pretableLevel = _s(p, 'riskLevel');
    pretableRiskScore = _d(p, 'riskScore');
    final rawFlags = p['riskFlags'];
    if (rawFlags is List) {
      pretableFlags = rawFlags.map((e) => e.toString()).toList();
    }
    impulseLevel = _s(p, 'impulseExhaustionLevel');
    dynamicModifier = _d(p, 'dynamicSessionModifier');
  }

  List<_PatternView> patterns = [];
  if (patternEvent != null) {
    final raw = patternEvent.payload['patterns'];
    if (raw is List) {
      patterns = raw.whereType<Map>().map((m) {
        final map = m.map((k, v) => MapEntry(k.toString(), v));
        return _PatternView(
          patternType: _s(map, 'patternType'),
          subtype: _s(map, 'subtype'),
          confidence: _d(map, 'confidence'),
          entrySafety: _s(map, 'entrySafety'),
          waterfallRisk: _s(map, 'waterfallRisk'),
          failThreatened: map['failThreatened'] == true,
          recommendedAction: _s(map, 'recommendedAction'),
        );
      }).toList();
    }
  }

  double? currentPrice;
  String? session;
  if (snapshotEvent != null) {
    final p = snapshotEvent.payload;
    currentPrice = _d(p, 'ask') > 0 ? _d(p, 'ask') : _d(p, 'bid');
    session = _s(p, 'session');
  }

  String? rail;
  double? entry;
  double? tp;
  double? grams;
  DateTime? expiry;
  String? waterfallRisk;
  if (decisionEvent != null) {
    final p = decisionEvent.payload;
    if (_s(p, 'isTradeAllowed') == 'true' || p['isTradeAllowed'] == true) {
      rail = _s(p, 'rail');
      entry = _d(p, 'entry');
      tp = _d(p, 'tp');
      grams = _d(p, 'grams');
      expiry = _dt(p, 'expiryUtc');
    }
    waterfallRisk = _s(p, 'waterfallRisk');
  }

  String? finalDecision;
  String? finalDecisionReason;
  if (finalDecisionEvent != null) {
    final p = finalDecisionEvent.payload;
    finalDecision = _s(p, 'finalDecision');
    finalDecisionReason = _s(p, 'primaryReason').isNotEmpty
        ? _s(p, 'primaryReason')
        : _s(p, 'reason');
  }

  String? rotationMode;
  if (rotationEvent != null) {
    rotationMode = _s(rotationEvent.payload, 'mode');
  }

  String? regime;
  bool? structureValid;
  String? candidateRiskLevel;
  String? candidateExecutionMode;
  if (candidateEvent != null) {
    final p = candidateEvent.payload;
    regime = _s(p, 'rotationRegime');
    structureValid = p['structureValid'] == true;
    candidateRiskLevel = _s(p, 'pretableRiskLevel');
    candidateExecutionMode = _s(p, 'rotationMode');
  }

  double? maxBuyableGrams;
  if (capitalEvent != null) {
    maxBuyableGrams = _d(capitalEvent.payload, 'maxLegalGrams');
  }

  final runtimePending =
      runtime?.pendingOrders ?? const <PendingOrderSnapshot>[];
  final runtimeOpen = runtime?.openPositions ?? const <OpenPositionSnapshot>[];
  final openExposureGrams = runtimeOpen.fold<double>(
    0,
    (sum, p) => sum + p.volumeGramsEquivalent,
  );
  final pendingExposureGrams = runtimePending.fold<double>(
    0,
    (sum, p) => sum + p.volumeGramsEquivalent,
  );

  final latestSlip = notifications
      .firstWhereOrNull((n) => n.title.toUpperCase().contains('SLIP'));

  // Prefer runtime pending orders because they include grams-equivalent.
  var orders = runtimePending
      .map((o) => _OrderView(
            type: o.type,
            price: o.price,
            tp: o.tp,
            expiry: o.expiry,
            gramsEquivalent: o.volumeGramsEquivalent,
          ))
      .toList();

  if (orders.isEmpty) {
    orders = trades
        .where((t) => t.status == 'Pending' || t.status == 'PENDING')
        .map((t) => _OrderView(
              type: t.rail,
              price: t.entry,
              tp: t.tp,
              expiry: t.expiryUtc,
              gramsEquivalent: 0,
            ))
        .toList();
  }

  final capturedAt = pretableEvent?.createdAtUtc ??
      decisionEvent?.createdAtUtc ??
      snapshotEvent?.createdAtUtc;

  return _CycleView(
    cycleId: pretableEvent?.cycleId,
    session: session,
    currentPrice: currentPrice,
    pretableLevel: pretableLevel,
    pretableRiskScore: pretableRiskScore,
    pretableRiskFlags: pretableFlags,
    patterns: patterns,
    pendingOrders: orders,
    decisionRail: rail,
    decisionEntry: entry,
    decisionTp: tp,
    decisionGrams: grams,
    decisionExpiry: expiry,
    rotationMode: rotationMode,
    impulseExhaustionLevel: impulseLevel,
    dynamicSessionModifier: dynamicModifier,
    waterfallRisk: waterfallRisk,
    candidateRegime: regime,
    candidateStructureValid: structureValid,
    candidateRiskLevel: candidateRiskLevel,
    candidateExecutionMode: candidateExecutionMode,
    freeAed: ledger?.cashAed,
    existingGoldGrams: ledger?.goldGrams,
    maxBuyableGrams: maxBuyableGrams,
    openExposureGrams: openExposureGrams,
    pendingExposureGrams: pendingExposureGrams,
    microModeActive: runtimeSettings?.microRotationEnabled,
    latestSlipSummary: latestSlip?.message,
    realizedProfitTodayAed: kpi?.todayProfitAed,
    openOrdersCount: runtimePending.length,
    finalDecision: finalDecision,
    finalDecisionReason: finalDecisionReason,
    capturedAt: capturedAt,
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

/// A) Trade Map — live graphical display showing engine state.
/// UI rule: no trading logic here — only visualises engine output.
class TradeMapScreen extends ConsumerWidget {
  const TradeMapScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final timelineAsync = ref.watch(tradeMapTimelineProvider);
    final tradesAsync = ref.watch(activeTradesProvider);
    final runtimeAsync = ref.watch(runtimeStatusProvider);
    final runtimeSettingsAsync = ref.watch(runtimeSettingsProvider);
    final ledgerAsync = ref.watch(ledgerProvider);
    final kpiAsync = ref.watch(kpiProvider);
    final notificationsAsync = ref.watch(notificationsProvider);

    return RefreshIndicator(
      onRefresh: () async {
        ref.invalidate(tradeMapTimelineProvider);
        ref.invalidate(activeTradesProvider);
      },
      child: timelineAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text('Error: $e')),
        data: (events) {
          final trades = tradesAsync.maybeWhen(
            data: (t) => t,
            orElse: () => <TradeItem>[],
          );
          final runtime = runtimeAsync.maybeWhen(
            data: (v) => v,
            orElse: () => null,
          );
          final runtimeSettings = runtimeSettingsAsync.maybeWhen(
            data: (v) => v,
            orElse: () => null,
          );
          final ledger = ledgerAsync.maybeWhen(
            data: (v) => v,
            orElse: () => null,
          );
          final kpi = kpiAsync.maybeWhen(
            data: (v) => v,
            orElse: () => null,
          );
          final notifications = notificationsAsync.maybeWhen(
            data: (v) => v,
            orElse: () => <NotificationFeedItem>[],
          );
          final cycle = _buildCycleView(
            events,
            trades,
            runtime,
            runtimeSettings,
            ledger,
            kpi,
            notifications,
          );
          return _TradeMapBody(cycle: cycle);
        },
      ),
    );
  }
}

// ─── Body ─────────────────────────────────────────────────────────────────────

class _TradeMapBody extends StatelessWidget {
  const _TradeMapBody({required this.cycle});

  final _CycleView cycle;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _PriceHeader(cycle: cycle),
        const SizedBox(height: 12),
        _LiveCandidateCard(cycle: cycle),
        const SizedBox(height: 12),
        _PretableCard(cycle: cycle),
        const SizedBox(height: 12),
        if (cycle.patterns.isNotEmpty) ...[
          _PatternsCard(patterns: cycle.patterns),
          const SizedBox(height: 12),
        ],
        _PriceLevelMap(cycle: cycle),
        const SizedBox(height: 12),
        if (cycle.pendingOrders.isNotEmpty) ...[
          _PendingOrdersCard(orders: cycle.pendingOrders),
          const SizedBox(height: 12),
        ],
        _RiskFlagsCard(cycle: cycle),
        const SizedBox(height: 12),
        _CapitalPanelCard(cycle: cycle),
        const SizedBox(height: 12),
        _LedgerSlipsCard(cycle: cycle),
        const SizedBox(height: 12),
        _DecisionCard(cycle: cycle),
        const SizedBox(height: 24),
      ],
    );
  }
}

// ─── Price header ─────────────────────────────────────────────────────────────

class _PriceHeader extends StatelessWidget {
  const _PriceHeader({required this.cycle});

  final _CycleView cycle;

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final price = cycle.currentPrice;

    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('XAUUSD.gram',
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                        color: cs.onSurfaceVariant,
                      )),
              Text(
                price != null ? '\$${price.toStringAsFixed(2)}' : '—',
                style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                      fontWeight: FontWeight.bold,
                    ),
              ),
            ],
          ),
        ),
        if (cycle.session != null)
          Chip(
            label: Text(cycle.session!),
            backgroundColor: cs.secondaryContainer,
            labelStyle: TextStyle(color: cs.onSecondaryContainer),
          ),
        const SizedBox(width: 8),
        if (cycle.capturedAt != null)
          Text(
            _timeAgo(cycle.capturedAt!),
            style: Theme.of(context)
                .textTheme
                .bodySmall
                ?.copyWith(color: cs.outline),
          ),
      ],
    );
  }
}

// ─── Live candidate panel ───────────────────────────────────────────────────

class _LiveCandidateCard extends StatelessWidget {
  const _LiveCandidateCard({required this.cycle});

  final _CycleView cycle;

  @override
  Widget build(BuildContext context) {
    final regime = cycle.candidateRegime ?? 'UNKNOWN';
    final structure = cycle.candidateStructureValid;
    final risk = cycle.candidateRiskLevel ?? cycle.pretableLevel ?? 'UNKNOWN';
    final mode =
        cycle.candidateExecutionMode ?? cycle.rotationMode ?? 'UNKNOWN';

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.hub_outlined, size: 18),
                const SizedBox(width: 8),
                Text('Live Candidate',
                    style: Theme.of(context)
                        .textTheme
                        .titleSmall
                        ?.copyWith(fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 10),
            _kv(context, 'structureValid',
                structure == null ? '—' : (structure ? 'true' : 'false')),
            _kv(context, 'regime', regime),
            _kv(context, 'PRETABLE riskLevel', risk),
            _kv(context, 'executionMode', mode),
            _kv(context, 'orderType', cycle.decisionRail ?? '—'),
            _kv(context, 'entryPrice',
                cycle.decisionEntry == null ? '—' : _usd(cycle.decisionEntry!)),
            _kv(context, 'tpPrice',
                cycle.decisionTp == null ? '—' : _usd(cycle.decisionTp!)),
            _kv(context, 'expiry',
                cycle.decisionExpiry?.toLocal().toString() ?? '—'),
          ],
        ),
      ),
    );
  }
}

// ─── PRETABLE card ────────────────────────────────────────────────────────────

class _PretableCard extends StatelessWidget {
  const _PretableCard({required this.cycle});

  final _CycleView cycle;

  @override
  Widget build(BuildContext context) {
    final level = cycle.pretableLevel ?? 'UNKNOWN';
    final color = _pretableColor(context, level);
    final score = cycle.pretableRiskScore;

    return Card(
      color: color.withOpacity(0.10),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: color.withOpacity(0.40), width: 1.5),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(_pretableIcon(level), color: color, size: 20),
                const SizedBox(width: 8),
                Text(
                  'PRETABLE',
                  style: Theme.of(context)
                      .textTheme
                      .titleSmall
                      ?.copyWith(color: color, fontWeight: FontWeight.bold),
                ),
                const Spacer(),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
                  decoration: BoxDecoration(
                    color: color,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    level,
                    style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 12),
                  ),
                ),
              ],
            ),
            if (score != null) ...[
              const SizedBox(height: 8),
              _RiskScoreBar(score: score, color: color),
              Text(
                'Risk score: ${(score * 100).toStringAsFixed(0)}%',
                style: Theme.of(context)
                    .textTheme
                    .bodySmall
                    ?.copyWith(color: color),
              ),
            ],
            if (cycle.rotationMode != null) ...[
              const SizedBox(height: 4),
              Text(
                'Mode: ${cycle.rotationMode}',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _RiskScoreBar extends StatelessWidget {
  const _RiskScoreBar({required this.score, required this.color});

  final double score;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 2),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(4),
        child: LinearProgressIndicator(
          value: score.clamp(0, 1),
          backgroundColor: color.withOpacity(0.15),
          valueColor: AlwaysStoppedAnimation<Color>(color),
          minHeight: 6,
        ),
      ),
    );
  }
}

// ─── Patterns card ────────────────────────────────────────────────────────────

class _PatternsCard extends StatelessWidget {
  const _PatternsCard({required this.patterns});

  final List<_PatternView> patterns;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.radar, size: 18),
                const SizedBox(width: 8),
                Text('Pattern Detector',
                    style: Theme.of(context)
                        .textTheme
                        .titleSmall
                        ?.copyWith(fontWeight: FontWeight.bold)),
                const Spacer(),
                Text(
                  '${patterns.length} pattern${patterns.length == 1 ? '' : 's'}',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ),
            const SizedBox(height: 10),
            ...patterns.map((p) => _PatternTile(pattern: p)),
          ],
        ),
      ),
    );
  }
}

class _PatternTile extends StatelessWidget {
  const _PatternTile({required this.pattern});

  final _PatternView pattern;

  @override
  Widget build(BuildContext context) {
    final color = _patternColor(context, pattern.patternType);
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: color.withOpacity(0.08),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: color.withOpacity(0.30)),
      ),
      child: Row(
        children: [
          Container(
            width: 4,
            height: 36,
            decoration: BoxDecoration(
              color: color,
              borderRadius: BorderRadius.circular(2),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  pattern.patternType.replaceAll('_', ' '),
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                        fontWeight: FontWeight.bold,
                        color: color,
                      ),
                ),
                Text(
                  pattern.subtype,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: Theme.of(context).colorScheme.onSurfaceVariant,
                      ),
                ),
              ],
            ),
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              _SafetyBadge(safety: pattern.entrySafety),
              const SizedBox(height: 4),
              Text(
                '${(pattern.confidence * 100).toStringAsFixed(0)}%',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
          ),
          if (pattern.failThreatened) ...[
            const SizedBox(width: 6),
            const Tooltip(
              message: 'FAIL level threatened',
              child: Icon(Icons.warning_amber_rounded,
                  size: 16, color: Colors.orange),
            ),
          ],
        ],
      ),
    );
  }
}

class _SafetyBadge extends StatelessWidget {
  const _SafetyBadge({required this.safety});

  final String safety;

  @override
  Widget build(BuildContext context) {
    final (text, color) = switch (safety.toUpperCase()) {
      'SAFE' => ('SAFE', Colors.green),
      'CAUTION' => ('CAUTION', Colors.amber.shade700),
      'BLOCKED' || 'BLOCK' => ('BLOCKED', Colors.red),
      _ => (safety, Colors.grey),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withOpacity(0.15),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        text,
        style: Theme.of(context)
            .textTheme
            .labelSmall
            ?.copyWith(color: color, fontWeight: FontWeight.bold),
      ),
    );
  }
}

// ─── Price level map ──────────────────────────────────────────────────────────

/// Visual representation of price levels — entry, TP, and current price —
/// as a horizontal bar chart without any trading logic.
class _PriceLevelMap extends StatelessWidget {
  const _PriceLevelMap({required this.cycle});

  final _CycleView cycle;

  @override
  Widget build(BuildContext context) {
    final current = cycle.currentPrice;
    final entry = cycle.decisionEntry;
    final tp = cycle.decisionTp;
    final rail = cycle.decisionRail;

    if (current == null || entry == null || tp == null) {
      return const SizedBox.shrink();
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.bar_chart, size: 18),
                const SizedBox(width: 8),
                Text('Price Levels',
                    style: Theme.of(context)
                        .textTheme
                        .titleSmall
                        ?.copyWith(fontWeight: FontWeight.bold)),
                const Spacer(),
                if (rail != null)
                  Chip(
                    label: Text(rail,
                        style: const TextStyle(
                            fontSize: 11, fontWeight: FontWeight.bold)),
                    backgroundColor: rail == 'BUY_STOP'
                        ? Colors.blue.shade50
                        : Colors.green.shade50,
                    side: BorderSide(
                      color: rail == 'BUY_STOP'
                          ? Colors.blue.shade300
                          : Colors.green.shade300,
                    ),
                    padding: EdgeInsets.zero,
                  ),
              ],
            ),
            const SizedBox(height: 12),
            _PriceLevelRow(
              label: 'TP',
              price: tp,
              current: current,
              color: Colors.green.shade600,
              icon: Icons.flag_outlined,
            ),
            const SizedBox(height: 6),
            _PriceLevelRow(
              label: 'Current',
              price: current,
              current: current,
              color: Theme.of(context).colorScheme.primary,
              icon: Icons.circle,
              isCurrentPrice: true,
            ),
            const SizedBox(height: 6),
            _PriceLevelRow(
              label: 'Entry',
              price: entry,
              current: current,
              color: Colors.blue.shade600,
              icon: Icons.arrow_forward_outlined,
            ),
            const SizedBox(height: 10),
            _PriceLevelVisualBar(
              entry: entry,
              tp: tp,
              current: current,
              rail: rail ?? 'BUY_LIMIT',
            ),
            if (cycle.decisionGrams != null)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(
                  'Size: ${cycle.decisionGrams!.toStringAsFixed(2)} g',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: Theme.of(context).colorScheme.onSurfaceVariant,
                      ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _PriceLevelRow extends StatelessWidget {
  const _PriceLevelRow({
    required this.label,
    required this.price,
    required this.current,
    required this.color,
    required this.icon,
    this.isCurrentPrice = false,
  });

  final String label;
  final double price;
  final double current;
  final Color color;
  final IconData icon;
  final bool isCurrentPrice;

  @override
  Widget build(BuildContext context) {
    final diff = price - current;
    final diffStr = diff >= 0
        ? '+\$${diff.toStringAsFixed(2)}'
        : '-\$${diff.abs().toStringAsFixed(2)}';

    return Row(
      children: [
        Icon(icon, size: 14, color: color),
        const SizedBox(width: 6),
        SizedBox(
          width: 52,
          child: Text(
            label,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
                  color: color,
                  fontWeight: FontWeight.w600,
                ),
          ),
        ),
        Text(
          '\$${price.toStringAsFixed(2)}',
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                fontWeight:
                    isCurrentPrice ? FontWeight.bold : FontWeight.normal,
              ),
        ),
        const SizedBox(width: 6),
        if (!isCurrentPrice)
          Text(
            diffStr,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color:
                      diff >= 0 ? Colors.green.shade600 : Colors.red.shade600,
                ),
          ),
      ],
    );
  }
}

class _PriceLevelVisualBar extends StatelessWidget {
  const _PriceLevelVisualBar({
    required this.entry,
    required this.tp,
    required this.current,
    required this.rail,
  });

  final double entry;
  final double tp;
  final double current;
  final String rail;

  @override
  Widget build(BuildContext context) {
    final min = math.min(entry, math.min(tp, current)) - 2;
    final max = math.max(entry, math.max(tp, current)) + 2;
    final range = max - min;
    if (range <= 0) return const SizedBox.shrink();

    double pos(double v) => ((v - min) / range).clamp(0, 1);

    return LayoutBuilder(builder: (context, constraints) {
      final width = constraints.maxWidth;

      Widget marker(double price, Color color, String label) {
        final left = pos(price) * width;
        return Positioned(
          left: left.clamp(0, width - 2),
          top: 0,
          bottom: 0,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Tooltip(
                message: '$label: \$${price.toStringAsFixed(2)}',
                child: Container(
                  width: 2,
                  height: 24,
                  color: color,
                ),
              ),
            ],
          ),
        );
      }

      return Container(
        height: 32,
        child: Stack(
          children: [
            Positioned.fill(
              child: Container(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    colors: [
                      Colors.blue.shade50,
                      Colors.green.shade50,
                    ],
                  ),
                  borderRadius: BorderRadius.circular(4),
                  border: Border.all(
                    color: Theme.of(context).colorScheme.outlineVariant,
                  ),
                ),
              ),
            ),
            marker(entry, Colors.blue.shade600, 'Entry'),
            marker(tp, Colors.green.shade600, 'TP'),
            marker(current, Theme.of(context).colorScheme.primary, 'Current'),
          ],
        ),
      );
    });
  }
}

// ─── Pending orders card ──────────────────────────────────────────────────────

class _PendingOrdersCard extends StatelessWidget {
  const _PendingOrdersCard({required this.orders});

  final List<_OrderView> orders;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.pending_actions, size: 18),
                const SizedBox(width: 8),
                Text('Pending Orders',
                    style: Theme.of(context)
                        .textTheme
                        .titleSmall
                        ?.copyWith(fontWeight: FontWeight.bold)),
                const Spacer(),
                Badge(
                  label: Text('${orders.length}'),
                  child: const Icon(Icons.schedule, size: 16),
                ),
              ],
            ),
            const SizedBox(height: 10),
            ...orders.map((o) => _OrderTile(order: o)),
          ],
        ),
      ),
    );
  }
}

class _OrderTile extends StatelessWidget {
  const _OrderTile({required this.order});

  final _OrderView order;

  @override
  Widget build(BuildContext context) {
    final isStop = order.type.contains('STOP');
    final color = isStop ? Colors.blue.shade600 : Colors.green.shade600;
    final now = DateTime.now();
    String expiryLabel = '';
    if (order.expiry != null) {
      final remaining = order.expiry!.difference(now);
      if (remaining.isNegative) {
        expiryLabel = 'Expired';
      } else if (remaining.inMinutes < 60) {
        expiryLabel = '${remaining.inMinutes}m left';
      } else {
        expiryLabel =
            '${remaining.inHours}h ${remaining.inMinutes.remainder(60)}m';
      }
    }

    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: color.withOpacity(0.07),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: color.withOpacity(0.25)),
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
            decoration: BoxDecoration(
              color: color,
              borderRadius: BorderRadius.circular(4),
            ),
            child: Text(
              order.type,
              style: const TextStyle(
                  color: Colors.white,
                  fontSize: 11,
                  fontWeight: FontWeight.bold),
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Tooltip(
                  message:
                      'orderType=${order.type}\nentryPrice=${order.price.toStringAsFixed(2)}\ntpPrice=${order.tp.toStringAsFixed(2)}\nexpiry=${order.expiry?.toLocal() ?? 'N/A'}\ngrams=${order.gramsEquivalent.toStringAsFixed(2)}',
                  child: Text(
                    'Entry: \$${order.price.toStringAsFixed(2)}  TP: \$${order.tp.toStringAsFixed(2)}',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  'Size: ${order.gramsEquivalent.toStringAsFixed(2)} g',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: Theme.of(context).colorScheme.onSurfaceVariant,
                      ),
                ),
              ],
            ),
          ),
          if (expiryLabel.isNotEmpty)
            Chip(
              label: Text(expiryLabel, style: const TextStyle(fontSize: 11)),
              padding: EdgeInsets.zero,
              backgroundColor:
                  expiryLabel == 'Expired' ? Colors.red.shade50 : null,
            ),
        ],
      ),
    );
  }
}

// ─── Risk flags card ──────────────────────────────────────────────────────────

class _RiskFlagsCard extends StatelessWidget {
  const _RiskFlagsCard({required this.cycle});

  final _CycleView cycle;

  @override
  Widget build(BuildContext context) {
    final flags = cycle.pretableRiskFlags;
    final impulse = cycle.impulseExhaustionLevel;
    final modifier = cycle.dynamicSessionModifier;
    final wf = cycle.waterfallRisk;

    // Include the doc-required risk flags (refinement spec §H)
    final allFlags = <String>{...flags};
    if (impulse == 'BLOCK') allFlags.add('IMPULSE_EXHAUSTION');
    if (impulse == 'CAUTION') allFlags.add('IMPULSE_EXHAUSTION');
    if (wf == 'HIGH') allFlags.add('WATERFALL_RISK');

    if (allFlags.isEmpty && modifier == null) {
      return const SizedBox.shrink();
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.shield_outlined, size: 18),
                const SizedBox(width: 8),
                Text('Risk Flags',
                    style: Theme.of(context)
                        .textTheme
                        .titleSmall
                        ?.copyWith(fontWeight: FontWeight.bold)),
                if (modifier != null) ...[
                  const Spacer(),
                  Text(
                    'Session modifier: ${(modifier * 100).toStringAsFixed(0)}%',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: Theme.of(context).colorScheme.onSurfaceVariant,
                        ),
                  ),
                ],
              ],
            ),
            const SizedBox(height: 8),
            if (allFlags.isEmpty)
              Text('No active risk flags',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: Colors.green.shade600,
                      ))
            else
              Wrap(
                spacing: 6,
                runSpacing: 6,
                children: allFlags.map((f) => _RiskFlagChip(flag: f)).toList(),
              ),
          ],
        ),
      ),
    );
  }
}

// ─── Capital panel ──────────────────────────────────────────────────────────

class _CapitalPanelCard extends StatelessWidget {
  const _CapitalPanelCard({required this.cycle});

  final _CycleView cycle;

  @override
  Widget build(BuildContext context) {
    final exposure =
        (cycle.openExposureGrams ?? 0) + (cycle.pendingExposureGrams ?? 0);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.account_balance_wallet_outlined, size: 18),
                const SizedBox(width: 8),
                Text('Capital Panel',
                    style: Theme.of(context)
                        .textTheme
                        .titleSmall
                        ?.copyWith(fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 10),
            _kv(context, 'free AED available',
                cycle.freeAed == null ? '—' : _aed(cycle.freeAed!)),
            _kv(
                context,
                'existing gold held',
                cycle.existingGoldGrams == null
                    ? '—'
                    : '${cycle.existingGoldGrams!.toStringAsFixed(2)} g'),
            _kv(
                context,
                'max grams currently buyable',
                cycle.maxBuyableGrams == null
                    ? '—'
                    : '${cycle.maxBuyableGrams!.toStringAsFixed(2)} g'),
            _kv(context, 'current exposure grams',
                '${exposure.toStringAsFixed(2)} g'),
            _kv(
                context,
                'micro mode active',
                cycle.microModeActive == null
                    ? '—'
                    : (cycle.microModeActive! ? 'true' : 'false')),
          ],
        ),
      ),
    );
  }
}

// ─── Ledger/slips panel ─────────────────────────────────────────────────────

class _LedgerSlipsCard extends StatelessWidget {
  const _LedgerSlipsCard({required this.cycle});

  final _CycleView cycle;

  @override
  Widget build(BuildContext context) {
    final openExposure = cycle.openExposureGrams ?? 0;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.receipt_long_outlined, size: 18),
                const SizedBox(width: 8),
                Text('Ledger / SLIPS',
                    style: Theme.of(context)
                        .textTheme
                        .titleSmall
                        ?.copyWith(fontWeight: FontWeight.bold)),
              ],
            ),
            const SizedBox(height: 10),
            _kv(
                context,
                'latest slip summary',
                cycle.latestSlipSummary?.trim().isNotEmpty == true
                    ? cycle.latestSlipSummary!
                    : 'No recent slip'),
            _kv(
                context,
                'realized AED profit today',
                cycle.realizedProfitTodayAed == null
                    ? '—'
                    : _aed(cycle.realizedProfitTodayAed!)),
            _kv(context, 'open orders count',
                (cycle.openOrdersCount ?? 0).toString()),
            _kv(context, 'open exposure grams',
                '${openExposure.toStringAsFixed(2)} g'),
          ],
        ),
      ),
    );
  }
}

class _RiskFlagChip extends StatelessWidget {
  const _RiskFlagChip({required this.flag});

  final String flag;

  @override
  Widget build(BuildContext context) {
    final color = _riskFlagColor(flag);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: color.withOpacity(0.12),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: color.withOpacity(0.40)),
      ),
      child: Text(
        flag,
        style: TextStyle(
          color: color,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

// ─── Decision card ────────────────────────────────────────────────────────────

class _DecisionCard extends StatelessWidget {
  const _DecisionCard({required this.cycle});

  final _CycleView cycle;

  @override
  Widget build(BuildContext context) {
    final decision = cycle.finalDecision;
    final reason = cycle.finalDecisionReason;
    if (decision == null) return const SizedBox.shrink();

    final isNo = decision == 'NO_TRADE';
    final color = isNo
        ? Theme.of(context).colorScheme.error
        : Theme.of(context).colorScheme.primary;

    return Card(
      color: color.withOpacity(0.06),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: color.withOpacity(0.30), width: 1),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  isNo ? Icons.block_outlined : Icons.check_circle_outline,
                  size: 18,
                  color: color,
                ),
                const SizedBox(width: 8),
                Text(
                  'Final Decision',
                  style: Theme.of(context)
                      .textTheme
                      .titleSmall
                      ?.copyWith(fontWeight: FontWeight.bold, color: color),
                ),
                const Spacer(),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 10, vertical: 3),
                  decoration: BoxDecoration(
                    color: color,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    decision,
                    style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 12),
                  ),
                ),
              ],
            ),
            if (reason != null && reason.isNotEmpty) ...[
              const SizedBox(height: 6),
              Text(
                reason,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

Color _pretableColor(BuildContext context, String level) => switch (level) {
      'SAFE' => Colors.green.shade600,
      'CAUTION' => Colors.amber.shade700,
      'BLOCK' => Colors.red.shade600,
      _ => Theme.of(context).colorScheme.outline,
    };

IconData _pretableIcon(String level) => switch (level) {
      'SAFE' => Icons.check_circle_outline,
      'CAUTION' => Icons.warning_amber_outlined,
      'BLOCK' => Icons.cancel_outlined,
      _ => Icons.help_outline,
    };

Color _patternColor(BuildContext context, String type) =>
    switch (type.toUpperCase()) {
      'WATERFALL_RISK' => Colors.red.shade600,
      'CONTINUATION_BREAKOUT' => Colors.green.shade600,
      'RANGE_RELOAD' || 'LIQUIDITY_SWEEP' => Colors.blue.shade600,
      'FALSE_BREAKOUT' || 'SESSION_TRANSITION_TRAP' => Colors.amber.shade700,
      _ => Theme.of(context).colorScheme.secondary,
    };

Color _riskFlagColor(String flag) => switch (flag) {
      'WATERFALL_RISK' || 'PATTERN_WATERFALL_HIGH' => Colors.red.shade600,
      'IMPULSE_EXHAUSTION' ||
      'IMPULSE_EXHAUSTION_BLOCK' ||
      'IMPULSE_EXHAUSTION_CAUTION' =>
        Colors.orange.shade700,
      'MA_STRETCH' || 'ATR_EXPANSION' => Colors.deepOrange.shade600,
      'MOMENTUM_WEAK' || 'ADR_EXHAUSTED' => Colors.orange.shade600,
      'SESSION_TRANSITION' => Colors.amber.shade700,
      'SPREAD_INSTABILITY' || 'PANIC_SUSPECTED' => Colors.red.shade400,
      _ => Colors.blueGrey.shade600,
    };

String _timeAgo(DateTime dt) {
  final diff = DateTime.now().difference(dt);
  if (diff.inSeconds < 60) return '${diff.inSeconds}s ago';
  if (diff.inMinutes < 60) return '${diff.inMinutes}m ago';
  return '${diff.inHours}h ago';
}

double _d(Map<String, dynamic> map, String key) {
  final v = map[key];
  if (v is num) return v.toDouble();
  if (v is String) return double.tryParse(v) ?? 0;
  return 0;
}

String _s(Map<String, dynamic> map, String key) => map[key]?.toString() ?? '';

DateTime? _dt(Map<String, dynamic> map, String key) {
  final raw = map[key];
  if (raw == null) return null;
  if (raw is DateTime) return raw;
  if (raw is String && raw.isNotEmpty) {
    return DateTime.tryParse(raw);
  }
  return null;
}

Widget _kv(BuildContext context, String key, String value) => Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: RichText(
        text: TextSpan(
          style: Theme.of(context).textTheme.bodySmall?.copyWith(fontSize: 12),
          children: [
            TextSpan(
              text: '$key: ',
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            TextSpan(text: value),
          ],
        ),
      ),
    );

String _aed(double value) => '${value.toStringAsFixed(2)} AED';

String _usd(double value) => '\$${value.toStringAsFixed(2)}';

extension _ListExt<T> on List<T> {
  T? firstWhereOrNull(bool Function(T) test) {
    for (final e in this) {
      if (test(e)) return e;
    }
    return null;
  }

  T? lastWhereOrNull(bool Function(T) test) {
    T? result;
    for (final e in this) {
      if (test(e)) result = e;
    }
    return result;
  }
}
