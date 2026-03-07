import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../domain/models.dart';
import '../../../presentation/app_providers.dart';

// ─── Filter categories ────────────────────────────────────────────────────────
enum _FeedFilter {
  all,
  tradeSignals,
  noTrade,
  market,
  ai,
}

extension _FeedFilterLabel on _FeedFilter {
  String get label => switch (this) {
        _FeedFilter.all => 'All',
        _FeedFilter.tradeSignals => 'Trades',
        _FeedFilter.noTrade => 'No-Trade',
        _FeedFilter.market => 'Market',
        _FeedFilter.ai => 'AI',
      };
}

// ─── Event classifier ─────────────────────────────────────────────────────────
enum _EventCategory { trade, noTrade, market, ai, cycle, info }

_EventCategory _classifyEvent(RuntimeTimelineItem item) {
  final t = item.eventType;
  if (t == 'TRADE_ROUTED' ||
      (t == 'FINAL_DECISION' &&
          (item.payload['finalDecision'] == 'TRADE' ||
              item.payload['finalDecision'] == 'TRADE_ARMED'))) {
    return _EventCategory.trade;
  }
  if (t == 'RULE_ENGINE_ABORT' ||
      t == 'AI_CONSENSUS_FAILED' ||
      t == 'AI_SKIPPED_RULE_ENGINE_ABORT' ||
      t == 'CYCLE_ABORTED' ||
      (t == 'FINAL_DECISION' && item.payload['finalDecision'] == 'NO_TRADE') ||
      t == 'REPLAY_CYCLE_NO_TRADE') {
    return _EventCategory.noTrade;
  }
  if (t == 'MT5_MARKET_SNAPSHOT_RECEIVED' ||
      t == 'MARKET_REGIME_DETECTED' ||
      t == 'MT5_PENDING_TRADE_DEQUEUED' ||
      t == 'MT5_TRADE_STATUS_RECEIVED') {
    return _EventCategory.market;
  }
  if (t.startsWith('AI_') ||
      t == 'TELEGRAM_INTERPRETED' ||
      t == 'NEWS_CHECK') {
    return _EventCategory.ai;
  }
  if (t == 'CYCLE_STARTED' ||
      t == 'REPLAY_CYCLE_STARTED' ||
      t == 'REPLAY_TRADE_ARMED') {
    return _EventCategory.cycle;
  }
  return _EventCategory.info;
}

bool _matchesFilter(RuntimeTimelineItem item, _FeedFilter filter) {
  if (filter == _FeedFilter.all) return true;
  final cat = _classifyEvent(item);
  return switch (filter) {
    _FeedFilter.tradeSignals =>
      cat == _EventCategory.trade || cat == _EventCategory.cycle,
    _FeedFilter.noTrade => cat == _EventCategory.noTrade,
    _FeedFilter.market => cat == _EventCategory.market,
    _FeedFilter.ai => cat == _EventCategory.ai,
    _FeedFilter.all => true,
  };
}

// ─── Human-readable descriptions ─────────────────────────────────────────────
String _describeEvent(RuntimeTimelineItem item) {
  final p = item.payload;
  String s(String key) => p[key]?.toString() ?? '';
  String n(String key) {
    final v = p[key];
    if (v == null) return '';
    if (v is num) return v.toStringAsFixed(2);
    return v.toString();
  }

  String withReason(String base, String reason) =>
      reason.isNotEmpty ? '$base: $reason' : base;

  switch (item.eventType) {
    case 'MT5_MARKET_SNAPSHOT_RECEIVED':
      final session = s('session');
      final phase = s('sessionPhase');
      final sessionLabel =
          session.isNotEmpty ? ' · Session: $session $phase' : '';
      return 'MT5 market snapshot received$sessionLabel';

    case 'CYCLE_STARTED':
    case 'REPLAY_CYCLE_STARTED':
      final symbol = s('symbol').isNotEmpty ? s('symbol') : 'XAUUSD';
      return 'New decision cycle started for $symbol';

    case 'MARKET_REGIME_DETECTED':
      final regime = s('regime');
      final tradeable =
          s('isTradeable') == 'true' ? '✅ Tradeable' : '🚫 Not tradeable';
      final reason = s('reason');
      final base = 'Market regime: $regime — $tradeable';
      return reason.isNotEmpty ? '$base · $reason' : base;

    case 'RULE_ENGINE_SETUP_CANDIDATE':
      final regimeLabel = s('marketRegime');
      final abortReason = s('abortReason');
      var desc = 'Rule engine ✅ — Setup candidate found';
      if (regimeLabel.isNotEmpty) desc += ' · Regime: $regimeLabel';
      if (abortReason.isNotEmpty) desc += ' · $abortReason';
      return desc;

    case 'RULE_ENGINE_ABORT':
      return withReason(
          'Rule engine 🚫 — No setup basis', s('abortReason'));

    case 'AI_SKIPPED_RULE_ENGINE_ABORT':
      return 'AI analysis skipped — rule engine already blocked this cycle';

    case 'NEWS_CHECK':
      final result = s('result').isNotEmpty ? s('result') : 'checked';
      final reason = s('reason');
      return reason.isNotEmpty
          ? 'News filter: $result — $reason'
          : 'News filter: $result';

    case 'CYCLE_ABORTED':
      final reason = s('reason').isNotEmpty ? s('reason') : s('abortReason');
      return withReason('Cycle aborted', reason);

    case 'AI_ANALYZE_REQUEST':
      return 'Sending market data to AI engine for analysis…';

    case 'TELEGRAM_INTERPRETED':
      final stance = s('stance');
      return stance.isNotEmpty
          ? 'Telegram signals interpreted — stance: $stance'
          : 'Telegram signals interpreted';

    case 'AI_PRE_STAGE_COMPLETED':
      return 'AI pre-stage completed — provider votes collected';

    case 'AI_COMMITTEE_EVALUATED':
      return 'AI committee evaluated — checking agreement across providers';

    case 'AI_VALIDATION_EVALUATED':
      return 'AI validation stage — checking signal consistency';

    case 'AI_ANALYZE_RESPONSE':
      final rail = s('rail').isNotEmpty ? s('rail') : '—';
      final disagreement = s('disagreementReason');
      final base = 'AI responded: Rail $rail';
      return disagreement.isNotEmpty ? '$base · Note: $disagreement' : base;

    case 'AI_ANALYZE_FAILED':
      return withReason('AI analysis failed', s('reason'));

    case 'AI_CONSENSUS_FAILED':
      return withReason(
          'AI consensus failed 🚫 — not enough agreement to trade',
          s('reason'));

    case 'TRADE_SCORE_CALCULATION':
      final total = n('totalScore');
      final tier = s('decisionTier');
      var desc = 'Trade score: $total/100 (Tier: $tier)';
      final structure = n('structureScore');
      final momentum = n('momentumScore');
      final aiScore = n('aiScore');
      if (structure.isNotEmpty) desc += ' · Structure: $structure';
      if (momentum.isNotEmpty) desc += ' · Momentum: $momentum';
      if (aiScore.isNotEmpty) desc += ' · AI: $aiScore';
      return desc;

    case 'DECISION_EVALUATED':
      final status = s('status').isNotEmpty
          ? s('status')
          : s('isTradeAllowed') == 'true'
              ? 'TRADE ALLOWED'
              : 'BLOCKED';
      final rail = s('rail');
      final reason = s('reason');
      var desc = 'Decision engine: $status';
      if (rail.isNotEmpty) desc += ' · Rail: $rail';
      if (reason.isNotEmpty) desc += ' · $reason';
      return desc;

    case 'TRADE_ROUTED':
      final rail = s('rail');
      final entry = s('entry');
      var desc = 'Trade routed to execution queue ✅';
      if (rail.isNotEmpty) desc += ' · Rail: $rail';
      if (entry.isNotEmpty) desc += ' · Entry: $entry';
      return desc;

    case 'FINAL_DECISION':
      final decision = s('finalDecision');
      final reason =
          s('primaryReason').isNotEmpty ? s('primaryReason') : s('reason');
      if (decision == 'NO_TRADE') {
        return withReason('🚫 NO TRADE — Capital protected', reason);
      } else if (decision.isNotEmpty) {
        return withReason('✅ TRADE APPROVED — $decision', reason);
      }
      return withReason('Final decision recorded', reason);

    case 'MT5_PENDING_TRADE_DEQUEUED':
      return 'MT5 EA pulled pending trade from queue for execution';

    case 'MT5_TRADE_STATUS_RECEIVED':
      final status = s('status');
      return status.isNotEmpty
          ? 'MT5 execution status received: $status'
          : 'MT5 execution status received';

    case 'REPLAY_TRADE_ARMED':
      return 'Replay: Trade armed for this cycle';

    case 'REPLAY_CYCLE_NO_TRADE':
      return 'Replay: No trade this cycle';

    case 'REPLAY_AI_RESPONSE':
      return 'Replay: AI responded for this cycle';

    case 'REPLAY_NEWS_CHECK':
      return 'Replay: News check completed';

    default:
      final reason = s('reason');
      final words = item.eventType
          .replaceAll('_', ' ')
          .toLowerCase()
          .split(' ')
          .map((w) => w.isNotEmpty
              ? '${w[0].toUpperCase()}${w.substring(1)}'
              : w)
          .join(' ');
      return reason.isNotEmpty ? '$words — $reason' : words;
  }
}

// ─── Supplemental detail lines ────────────────────────────────────────────────
List<String> _detailLines(RuntimeTimelineItem item) {
  final p = item.payload;
  final lines = <String>[];

  String? _tryStr(String key) {
    final v = p[key];
    if (v == null || v.toString().isEmpty) return null;
    return v.toString();
  }

  // For FINAL_DECISION show entry/TP if present
  if (item.eventType == 'TRADE_ROUTED' ||
      (item.eventType == 'FINAL_DECISION' &&
          p['finalDecision'] != 'NO_TRADE')) {
    final entry = _tryStr('entry');
    final tp = _tryStr('tp');
    final grams = _tryStr('grams');
    final rail = _tryStr('rail');
    if (entry != null) lines.add('Entry: $entry');
    if (tp != null) lines.add('TP: $tp');
    if (grams != null) lines.add('Grams: $grams');
    if (rail != null) lines.add('Rail: $rail');
  }

  // For DECISION_EVALUATED show key decision fields
  if (item.eventType == 'DECISION_EVALUATED') {
    final waterfallRisk = _tryStr('waterfallRisk');
    final mode = _tryStr('mode');
    final cause = _tryStr('cause');
    if (waterfallRisk != null) lines.add('Waterfall risk: $waterfallRisk');
    if (mode != null) lines.add('Mode: $mode');
    if (cause != null) lines.add('Cause: $cause');
  }

  // Source & stage always shown if non-trivial
  if (item.stage.isNotEmpty && item.stage != 'info') {
    lines.add('Stage: ${item.stage}');
  }

  return lines;
}

// ─── Time helpers ─────────────────────────────────────────────────────────────
String _ksaTime(DateTime utc) {
  final ksa = utc.add(const Duration(hours: 3));
  final h = ksa.hour.toString().padLeft(2, '0');
  final m = ksa.minute.toString().padLeft(2, '0');
  final s = ksa.second.toString().padLeft(2, '0');
  return '$h:$m:$s KSA';
}

String _relativeTime(DateTime utc) {
  final now = DateTime.now().toUtc();
  final diff = now.difference(utc);
  if (diff.inSeconds < 60) return '${diff.inSeconds}s ago';
  if (diff.inMinutes < 60) return '${diff.inMinutes}m ago';
  return '${diff.inHours}h ago';
}

// ─── Color helpers ────────────────────────────────────────────────────────────
Color _categoryColor(BuildContext context, _EventCategory cat) {
  final cs = Theme.of(context).colorScheme;
  return switch (cat) {
    _EventCategory.trade => Colors.green.shade700,
    _EventCategory.noTrade => cs.error,
    _EventCategory.market => cs.primary,
    _EventCategory.ai => Colors.purple.shade600,
    _EventCategory.cycle => cs.secondary,
    _EventCategory.info => cs.onSurfaceVariant,
  };
}

IconData _categoryIcon(_EventCategory cat) => switch (cat) {
      _EventCategory.trade => Icons.trending_up,
      _EventCategory.noTrade => Icons.block,
      _EventCategory.market => Icons.show_chart,
      _EventCategory.ai => Icons.psychology,
      _EventCategory.cycle => Icons.refresh,
      _EventCategory.info => Icons.info_outline,
    };

// ─── Screen ───────────────────────────────────────────────────────────────────
class LiveFeedScreen extends ConsumerStatefulWidget {
  const LiveFeedScreen({super.key});

  @override
  ConsumerState<LiveFeedScreen> createState() => _LiveFeedScreenState();
}

class _LiveFeedScreenState extends ConsumerState<LiveFeedScreen> {
  _FeedFilter _filter = _FeedFilter.all;
  Timer? _autoRefreshTimer;

  @override
  void initState() {
    super.initState();
    // Auto-refresh every 15 seconds so new events appear without scrolling
    _autoRefreshTimer =
        Timer.periodic(const Duration(seconds: 15), (_) {
      if (mounted) ref.invalidate(timelineProvider);
    });
  }

  @override
  void dispose() {
    _autoRefreshTimer?.cancel();
    super.dispose();
  }

  Future<void> _refresh() async {
    ref.invalidate(timelineProvider);
  }

  @override
  Widget build(BuildContext context) {
    final timelineAsync = ref.watch(timelineProvider);

    return RefreshIndicator(
      onRefresh: _refresh,
      child: CustomScrollView(
        slivers: [
          // Filter chips
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 12, 12, 4),
              child: SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: [
                    for (final filter in _FeedFilter.values)
                      Padding(
                        padding: const EdgeInsets.only(right: 6),
                        child: FilterChip(
                          label: Text(filter.label),
                          selected: _filter == filter,
                          onSelected: (_) =>
                              setState(() => _filter = filter),
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),

          // Legend
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
              child: Wrap(
                spacing: 12,
                runSpacing: 4,
                children: [
                  _LegendChip(
                    color: Colors.green.shade700,
                    icon: Icons.trending_up,
                    label: 'Trade',
                  ),
                  _LegendChip(
                    color: Theme.of(context).colorScheme.error,
                    icon: Icons.block,
                    label: 'No-Trade',
                  ),
                  _LegendChip(
                    color: Theme.of(context).colorScheme.primary,
                    icon: Icons.show_chart,
                    label: 'Market',
                  ),
                  _LegendChip(
                    color: Colors.purple.shade600,
                    icon: Icons.psychology,
                    label: 'AI',
                  ),
                  _LegendChip(
                    color: Theme.of(context).colorScheme.secondary,
                    icon: Icons.refresh,
                    label: 'Cycle',
                  ),
                ],
              ),
            ),
          ),

          // Event list
          timelineAsync.when(
            loading: () => const SliverFillRemaining(
              child: Center(child: CircularProgressIndicator()),
            ),
            error: (e, _) => SliverFillRemaining(
              child: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.wifi_off, size: 48),
                    const SizedBox(height: 12),
                    Text(
                      'Could not load live feed',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      e.toString(),
                      style: Theme.of(context).textTheme.bodySmall,
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 16),
                    FilledButton.icon(
                      onPressed: _refresh,
                      icon: const Icon(Icons.refresh),
                      label: const Text('Retry'),
                    ),
                  ],
                ),
              ),
            ),
            data: (events) {
              final filtered = events
                  .where((e) => _matchesFilter(e, _filter))
                  .toList();

              if (filtered.isEmpty) {
                return SliverFillRemaining(
                  child: Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.stream,
                          size: 56,
                          color: Theme.of(context)
                              .colorScheme
                              .onSurfaceVariant
                              .withOpacity(0.4),
                        ),
                        const SizedBox(height: 12),
                        Text(
                          'No events yet',
                          style: Theme.of(context)
                              .textTheme
                              .titleMedium
                              ?.copyWith(
                                color: Theme.of(context)
                                    .colorScheme
                                    .onSurfaceVariant,
                              ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          'Pull to refresh or wait for the next MT5 tick cycle.',
                          style: Theme.of(context).textTheme.bodySmall,
                          textAlign: TextAlign.center,
                        ),
                      ],
                    ),
                  ),
                );
              }

              return SliverPadding(
                padding: const EdgeInsets.fromLTRB(12, 0, 12, 24),
                sliver: SliverList.separated(
                  itemCount: filtered.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 6),
                  itemBuilder: (context, index) {
                    return _EventCard(item: filtered[index]);
                  },
                ),
              );
            },
          ),
        ],
      ),
    );
  }
}

// ─── Event card ───────────────────────────────────────────────────────────────
class _EventCard extends StatelessWidget {
  const _EventCard({required this.item});

  final RuntimeTimelineItem item;

  @override
  Widget build(BuildContext context) {
    final cat = _classifyEvent(item);
    final color = _categoryColor(context, cat);
    final icon = _categoryIcon(cat);
    final description = _describeEvent(item);
    final details = _detailLines(item);
    final cs = Theme.of(context).colorScheme;

    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Color stripe
            Container(width: 4, color: color),
            // Content
            Expanded(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Header row: icon + description + time
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Icon(icon, size: 16, color: color),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            description,
                            style: Theme.of(context)
                                .textTheme
                                .bodyMedium
                                ?.copyWith(fontWeight: FontWeight.w500),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.end,
                          children: [
                            Text(
                              _ksaTime(item.createdAtUtc),
                              style: Theme.of(context)
                                  .textTheme
                                  .labelSmall
                                  ?.copyWith(
                                    color: cs.onSurfaceVariant,
                                  ),
                            ),
                            Text(
                              _relativeTime(item.createdAtUtc),
                              style: Theme.of(context)
                                  .textTheme
                                  .labelSmall
                                  ?.copyWith(
                                    color: cs.onSurfaceVariant,
                                    fontSize: 10,
                                  ),
                            ),
                          ],
                        ),
                      ],
                    ),

                    // Detail lines
                    if (details.isNotEmpty) ...[
                      const SizedBox(height: 6),
                      Wrap(
                        spacing: 8,
                        runSpacing: 4,
                        children: [
                          for (final line in details)
                            _DetailBadge(text: line, baseColor: color),
                        ],
                      ),
                    ],

                    // Meta row: symbol + source + cycleId
                    const SizedBox(height: 6),
                    Row(
                      children: [
                        if (item.symbol.isNotEmpty) ...[
                          Icon(Icons.currency_exchange,
                              size: 12,
                              color: cs.onSurfaceVariant),
                          const SizedBox(width: 3),
                          Text(
                            item.symbol,
                            style: Theme.of(context)
                                .textTheme
                                .labelSmall
                                ?.copyWith(color: cs.onSurfaceVariant),
                          ),
                          const SizedBox(width: 8),
                        ],
                        if (item.source.isNotEmpty) ...[
                          Icon(Icons.source_outlined,
                              size: 12,
                              color: cs.onSurfaceVariant),
                          const SizedBox(width: 3),
                          Text(
                            item.source,
                            style: Theme.of(context)
                                .textTheme
                                .labelSmall
                                ?.copyWith(color: cs.onSurfaceVariant),
                          ),
                          const SizedBox(width: 8),
                        ],
                        if (item.cycleId case final cycleId?
                            when cycleId.isNotEmpty) ...[
                          Icon(Icons.loop,
                              size: 12,
                              color: cs.onSurfaceVariant),
                          const SizedBox(width: 3),
                          Flexible(
                            child: Text(
                              'cycle: ${cycleId.length > 8 ? cycleId.substring(0, 8) : cycleId}',
                              style: Theme.of(context)
                                  .textTheme
                                  .labelSmall
                                  ?.copyWith(
                                    color: cs.onSurfaceVariant,
                                    fontFamily: 'monospace',
                                  ),
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

// ─── Detail badge ─────────────────────────────────────────────────────────────
class _DetailBadge extends StatelessWidget {
  const _DetailBadge({required this.text, required this.baseColor});

  final String text;
  final Color baseColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
      decoration: BoxDecoration(
        color: baseColor.withOpacity(0.12),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: baseColor.withOpacity(0.25)),
      ),
      child: Text(
        text,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: baseColor,
              fontWeight: FontWeight.w600,
            ),
      ),
    );
  }
}

// ─── Legend chip ─────────────────────────────────────────────────────────────
class _LegendChip extends StatelessWidget {
  const _LegendChip({
    required this.color,
    required this.icon,
    required this.label,
  });

  final Color color;
  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 12, color: color),
        const SizedBox(width: 3),
        Text(
          label,
          style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: color,
                fontWeight: FontWeight.w500,
              ),
        ),
      ],
    );
  }
}
