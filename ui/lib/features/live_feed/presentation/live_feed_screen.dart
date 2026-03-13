import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../domain/models.dart';
import '../../../presentation/app_providers.dart';
import 'filter_settings.dart';

// date filtering logic and label have been moved to filter_settings.dart

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
      t == 'BLOCKED_VALID_SETUP_CANDIDATE' ||
      t == 'SYMBOL_EXPOSURE_REJECTED' ||
      (t == 'FINAL_DECISION' && item.payload['finalDecision'] == 'NO_TRADE') ||
      t == 'REPLAY_CYCLE_NO_TRADE') {
    return _EventCategory.noTrade;
  }
  if (t == 'MT5_MARKET_SNAPSHOT_RECEIVED' ||
      t == 'MARKET_REGIME_DETECTED' ||
      t == 'PATTERN_DETECTOR_RESULTS' ||
      t == 'MT5_PENDING_TRADE_DEQUEUED' ||
      t == 'MT5_TRADE_STATUS_RECEIVED' ||
      t == 'CAPITAL_UTILIZATION_CHECK') {
    return _EventCategory.market;
  }
  if (t.startsWith('AI_') || t == 'TELEGRAM_INTERPRETED' || t == 'NEWS_CHECK') {
    return _EventCategory.ai;
  }
  if (t == 'CYCLE_STARTED' ||
      t == 'REPLAY_CYCLE_STARTED' ||
      t == 'REPLAY_TRADE_ARMED') {
    return _EventCategory.cycle;
  }
  return _EventCategory.info;
}

// filtering logic is now handled by the filter settings provider

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
      final symbol = s('symbol').isNotEmpty ? s('symbol') : 'XAUUSD.gram';
      return 'New decision cycle started for $symbol';

    case 'MARKET_REGIME_DETECTED':
      final regime = s('regime');
      final tradeable =
          s('isTradeable') == 'true' ? '✅ Tradeable' : '🚫 Not tradeable';
      final reason = s('reason');
      final base = 'Market regime: $regime — $tradeable';
      return reason.isNotEmpty ? '$base · $reason' : base;

    case 'PATTERN_DETECTOR_RESULTS':
      final patternCount = p['patternCount'];
      final countLabel = patternCount != null ? '$patternCount' : '?';
      final patterns = p['patterns'];
      if (patterns is List && patterns.isNotEmpty) {
        final types = patterns
            .whereType<Map>()
            .map((pat) => pat['patternType']?.toString() ?? '')
            .where((t) => t.isNotEmpty)
            .toList();
        final typeLabel = types.take(3).join(', ');
        final moreLabel = types.length > 3 ? ' +${types.length - 3} more' : '';
        return 'Pattern detector: $countLabel pattern(s) found — $typeLabel$moreLabel';
      }
      return 'Pattern detector: $countLabel pattern(s) detected';

    case 'BLOCKED_VALID_SETUP_CANDIDATE':
      final session = s('session');
      final cause = s('cause');
      final score = n('tradeScore');
      var blockedDesc = '⚠️ Blocked valid setup (study candidate)';
      if (session.isNotEmpty) blockedDesc += ' · Session: $session';
      if (score.isNotEmpty) blockedDesc += ' · Score: $score';
      if (cause.isNotEmpty) blockedDesc += ' · Cause: $cause';
      return blockedDesc;

    case 'RULE_ENGINE_SETUP_CANDIDATE':
      final regimeLabel = s('marketRegime');
      final abortReason = s('abortReason');
      var desc = 'Rule engine ✅ — Setup candidate found';
      if (regimeLabel.isNotEmpty) desc += ' · Regime: $regimeLabel';
      if (abortReason.isNotEmpty) desc += ' · $abortReason';
      return desc;

    case 'RULE_ENGINE_ABORT':
      return withReason('Rule engine 🚫 — No setup basis', s('abortReason'));

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
      final committeeSource = s('committeeSource');
      final agreementCount = n('agreementCount');
      final base2 = 'AI committee evaluated';
      if (committeeSource == 'lead') return '$base2 — lead model decision';
      if (committeeSource == 'full_failover')
        return '$base2 — full failover succeeded ✅';
      if (agreementCount.isNotEmpty)
        return '$base2 · agreement=$agreementCount';
      return base2;

    case 'AI_COMMITTEE_FAILOVER_TRIGGERED':
      final reason = s('reason');
      return reason.isNotEmpty
          ? 'AI lead committee failed — retrying with full analyzer set · $reason'
          : 'AI lead committee failed — retrying with full analyzer set';

    case 'AI_COMMITTEE_FAILOVER_SUCCEEDED':
      final fAgreement = n('agreementCount');
      return fAgreement.isNotEmpty
          ? 'AI failover succeeded ✅ — agreement=$fAgreement'
          : 'AI failover succeeded ✅ — full analyzer set produced a signal';

    case 'AI_COMMITTEE_FAILOVER_FAILED':
      final ffReason = s('reason');
      return ffReason.isNotEmpty
          ? 'AI failover also failed ⚠️ — using fallback simulation · $ffReason'
          : 'AI failover also failed ⚠️ — using fallback simulation';

    case 'AI_FALLBACK_TRIGGERED':
      final fbReason = s('reason');
      return fbReason.isNotEmpty
          ? 'AI fallback simulation triggered — no live AI signal · $fbReason'
          : 'AI fallback simulation triggered — no live AI signal';

    case 'AI_SIGNAL_FALLBACK_USED':
      return '⚠️ AI signal: deterministic fallback used (all AI providers unavailable this cycle)';

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

    case 'STUDY_REFINEMENT_STARTED':
      final failures = n('consecutiveWaterfallFailures');
      return failures.isNotEmpty
          ? '🔬 Study & self-crosscheck started — analyzing $failures waterfall failure(s) with all AI models'
          : '🔬 Study & self-crosscheck started — full AI review in progress';

    case 'STUDY_REFINEMENT_RESULT':
      final bottomVerdict = s('bottomPermissionVerdict');
      final waterfallVerdict = s('waterfallVerdict');
      final adjustments = s('ruleAdjustments');
      var studyDesc = '🔬 Study complete';
      if (bottomVerdict.isNotEmpty)
        studyDesc += ' · BottomPerm: $bottomVerdict';
      if (waterfallVerdict.isNotEmpty)
        studyDesc += ' · Waterfall: $waterfallVerdict';
      if (adjustments.isNotEmpty && adjustments != '[]')
        studyDesc += ' · Adjustments suggested';
      return studyDesc;

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

    case 'CAPITAL_UTILIZATION_CHECK':
      final status = s('orderStatus');
      final attempted = n('attemptedGrams');
      final approved = n('approvedGrams');
      final maxLegal = n('maxLegalGrams');
      if (status == 'REJECTED') {
        return '🚫 Capital gate REJECTED — insufficient cash (max ${maxLegal}g allowed)';
      }
      if (status == 'RESIZE_REQUIRED') {
        return '⚠️ Capital gate RESIZE — ${attempted}g → ${approved}g (max legal: ${maxLegal}g)';
      }
      return '✅ Capital gate APPROVED — ${approved}g within allowed capital';

    case 'SYMBOL_EXPOSURE_REJECTED':
      final open = n('openPositionGrams');
      final proposed = n('proposedGrams');
      final total = n('totalProjectedExposure');
      final maxExp = n('maxSymbolExposureGrams');
      return '🚫 Exposure gate REJECTED — ${open}g open + ${proposed}g proposed = ${total}g exceeds max ${maxExp}g';

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
          .map((w) =>
              w.isNotEmpty ? '${w[0].toUpperCase()}${w.substring(1)}' : w)
          .join(' ');
      final base = reason.isNotEmpty ? '$words — $reason' : words;
      final summary = _shortPayloadSummary(item.eventType, item.payload, maxParts: 3);
      return summary.isEmpty ? base : '$base · $summary';
  }
}

String _shortPayloadSummary(String eventType, Map<String, dynamic> payload, {int maxParts = 3}) {
  final preferred = _preferredKeys[eventType];
  final keys = preferred != null
      ? preferred.where(payload.containsKey).take(maxParts + 2)
      : payload.keys.where((k) => !_skipPayloadKeys.contains(k)).take(maxParts + 2);
  final parts = <String>[];
  for (final key in keys) {
    final v = payload[key];
    if (v == null || (v is String && v.isEmpty)) continue;
    if (parts.length >= maxParts) break;
    parts.add(_formatPayloadValue(v));
  }
  return parts.join(' · ');
}

// ─── Dynamic payload → detail lines (single path for all event types) ───────────
const _maxDetailLines = 12;
const _maxValueLength = 36;
const _skipPayloadKeys = {'id', 'cycleId', 'tradeId', 'snapshotHash'};

/// Preferred key order per event type (first keys shown first; rest appended).
/// Omit event type to use payload key order for that event.
final _preferredKeys = <String, List<String>>{
  'PRETABLE_RESULT': [
    'riskLevel', 'riskScore', 'riskFlags', 'sizeModifier', 'session',
    'pretableReason', 'impulseExhaustionLevel', 'liquiditySweepConfirmed',
    'rotationRegime', 'dynamicSessionModifier', 'patternCount',
  ],
  'DECISION_EVALUATED': [
    'status', 'rail', 'reason', 'waterfallRisk', 'mode', 'cause',
    'bottomPermissionVerdict', 'bottomPermissionMode',
  ],
  'FINAL_DECISION': [
    'finalDecision', 'primaryReason', 'reason', 'entry', 'tp', 'grams', 'rail',
  ],
  'TRADE_ROUTED': ['rail', 'entry', 'tp', 'grams', 'orderStatus'],
  'CAPITAL_UTILIZATION_CHECK': [
    'orderStatus', 'approvedGrams', 'maxLegalGrams', 'attemptedGrams',
    'requiredAed', 'allowedCapitalAed', 'cashAed',
  ],
  'SYMBOL_EXPOSURE_REJECTED': [
    'exposureSource', 'ledgerNetEquityAed', 'openPositionGrams',
    'proposedGrams', 'totalProjectedExposure', 'maxSymbolExposureGrams',
    'rejectionReason',
  ],
  'MARKET_REGIME_DETECTED': ['regime', 'isTradeable', 'reason'],
  'PATTERN_DETECTOR_RESULTS': ['patternCount', 'patterns'],
  'AI_ANALYZE_RESPONSE': ['rail', 'disagreementReason'],
  'MT5_MARKET_SNAPSHOT_RECEIVED': ['session', 'sessionPhase', 'bid', 'ask'],
  'CYCLE_STARTED': ['symbol'],
  'CYCLE_ABORTED': ['reason', 'regime', 'regimeReason', 'snapshotSession', 'mappedSession'],
  'BLOCKED_VALID_SETUP_CANDIDATE': ['cause', 'tradeScore', 'session'],
  'RULE_ENGINE_ABORT': ['abortReason'],
  'STUDY_REFINEMENT_RESULT': [
    'bottomPermissionVerdict', 'waterfallVerdict', 'ruleAdjustments',
  ],
  'TRADE_SCORE_CALCULATION': [
    'totalScore', 'decisionTier', 'structureScore', 'momentumScore', 'aiScore',
  ],
};

String _formatPayloadValue(dynamic v) {
  if (v == null) return '—';
  if (v is bool) return v ? 'Yes' : 'No';
  if (v is num) return v is int ? '$v' : (v as double).toStringAsFixed(2);
  if (v is List) {
    if (v.isEmpty) return '—';
    if (v.length <= 3 && v.every((e) => e is num || e is String)) {
      final s = v.join(', ');
      return s.length <= _maxValueLength ? s : '${s.substring(0, _maxValueLength - 3)}…';
    }
    return '${v.length} item${v.length == 1 ? '' : 's'}';
  }
  final s = v.toString();
  return s.length <= _maxValueLength ? s : '${s.substring(0, _maxValueLength - 1)}…';
}

String _payloadKeyToLabel(String key) {
  const knownLabels = {
    'riskLevel': 'Risk level',
    'riskScore': 'Risk score',
    'riskFlags': 'Risk flags',
    'sizeModifier': 'Size modifier',
    'pretableReason': 'Reason',
    'impulseExhaustionLevel': 'Impulse level',
    'liquiditySweepConfirmed': 'Liquidity sweep',
    'rotationRegime': 'Rotation regime',
    'dynamicSessionModifier': 'Session modifier',
    'dynamicSessionWaterfallCap': 'Waterfall cap',
    'patternCount': 'Patterns',
    'patternTypes': 'Pattern types',
    'openPositionGrams': 'Open (g)',
    'proposedGrams': 'Proposed (g)',
    'totalProjectedExposure': 'Total exposure (g)',
    'maxSymbolExposureGrams': 'Max allowed (g)',
    'rejectionReason': 'Reason',
    'ledgerNetEquityAed': 'Ledger equity (AED)',
    'exposureSource': 'Source',
    'orderStatus': 'Status',
    'approvedGrams': 'Approved (g)',
    'maxLegalGrams': 'Max legal (g)',
    'attemptedGrams': 'Attempted (g)',
    'requiredAed': 'Required (AED)',
    'allowedCapitalAed': 'Allowed (AED)',
    'cashAed': 'Cash (AED)',
    'finalDecision': 'Decision',
    'primaryReason': 'Reason',
    'bottomPermissionVerdict': 'Bottom verdict',
    'bottomPermissionMode': 'Bottom mode',
    'waterfallRisk': 'Waterfall risk',
    'totalScore': 'Score',
    'decisionTier': 'Tier',
    'structureScore': 'Structure',
    'momentumScore': 'Momentum',
    'aiScore': 'AI score',
  };
  if (knownLabels.containsKey(key)) return knownLabels[key]!;
  final withSpaces = key.replaceAllMapped(RegExp(r'([A-Z])'), (m) => ' ${m[1]!.toLowerCase()}').replaceAll('_', ' ').trim();
  if (withSpaces.isEmpty) return key;
  return withSpaces.split(RegExp(r'\s+')).map((w) => w.isNotEmpty ? '${w[0].toUpperCase()}${w.substring(1)}' : w).join(' ');
}

List<String> _payloadToDetailLines(String eventType, Map<String, dynamic> payload) {
  final lines = <String>[];
  final preferred = _preferredKeys[eventType];
  final keys = preferred != null
      ? [...preferred.where(payload.containsKey), ...payload.keys.where((k) => !_skipPayloadKeys.contains(k) && !preferred.contains(k))]
      : payload.keys.where((k) => !_skipPayloadKeys.contains(k)).toList();

  for (final key in keys) {
    if (lines.length >= _maxDetailLines) break;
    final v = payload[key];
    if (v == null || (v is String && v.isEmpty)) continue;
    final label = _payloadKeyToLabel(key);
    final value = _formatPayloadValue(v);
    lines.add('$label: $value');
  }
  return lines;
}

List<String> _detailLines(RuntimeTimelineItem item) {
  final lines = _payloadToDetailLines(item.eventType, item.payload);
  if (item.stage.isNotEmpty && item.stage != 'info' && !lines.any((l) => l.toLowerCase().startsWith('stage:'))) {
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
  Timer? _autoRefreshTimer;
  DateTime? _lastRefreshedAt;

  @override
  void initState() {
    super.initState();
    _lastRefreshedAt = DateTime.now().toUtc();
    // Auto-refresh every 15 seconds so new events appear without scrolling
    _autoRefreshTimer = Timer.periodic(const Duration(seconds: 15), (_) {
      if (mounted) {
        setState(() => _lastRefreshedAt = DateTime.now().toUtc());
        ref.invalidate(timelineProvider);
      }
    });
  }

  @override
  void dispose() {
    _autoRefreshTimer?.cancel();
    super.dispose();
  }

  Future<void> _refresh() async {
    setState(() => _lastRefreshedAt = DateTime.now().toUtc());
    ref.invalidate(timelineProvider);
  }

  /// Exports the full log text for a single event (for AI/support analysis).
  String _exportEventLog(RuntimeTimelineItem item) {
    final buf = StringBuffer();
    buf.writeln('--- Event ---');
    buf.writeln('Type: ${item.eventType}');
    buf.writeln('Time (UTC): ${item.createdAtUtc.toIso8601String()}');
    buf.writeln('Time (KSA): ${_ksaTime(item.createdAtUtc)}');
    buf.writeln('Stage: ${item.stage}');
    buf.writeln('Source: ${item.source}');
    if (item.symbol.isNotEmpty) buf.writeln('Symbol: ${item.symbol}');
    if (item.cycleId != null) buf.writeln('Cycle ID: ${item.cycleId}');
    if (item.tradeId != null) buf.writeln('Trade ID: ${item.tradeId}');
    buf.writeln('Payload:');
    for (final entry in item.payload.entries) {
      buf.writeln('  ${entry.key}: ${entry.value}');
    }
    buf.writeln('Summary: ${_describeEvent(item)}');
    return buf.toString();
  }

  /// Exports all currently filtered events as a bulk log text.
  String _exportBulkLog(List<RuntimeTimelineItem> events) {
    final buf = StringBuffer();
    final settings = ref.read(liveFeedFilterProvider);
    buf.writeln('=== Live Feed Export ===');
    buf.writeln('Filter: ${settings.dateFilter.label(DateTime.now().toUtc())}');
    if (settings.sessions.isNotEmpty) {
      buf.writeln('Sessions: ${settings.sessions.join(", ")}');
    }
    buf.writeln('Exported at: ${DateTime.now().toUtc().toIso8601String()}');
    buf.writeln('Event count: ${events.length}');
    buf.writeln();
    for (final event in events) {
      buf.writeln(_exportEventLog(event));
    }
    return buf.toString();
  }

  Future<void> _copyEventToClipboard(RuntimeTimelineItem item) async {
    final text = _exportEventLog(item);
    await Clipboard.setData(ClipboardData(text: text));
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Event log copied to clipboard'),
          duration: Duration(seconds: 2),
        ),
      );
    }
  }

  Future<void> _copyBulkLog(List<RuntimeTimelineItem> events) async {
    final text = _exportBulkLog(events);
    await Clipboard.setData(ClipboardData(text: text));
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('${events.length} events copied to clipboard'),
          duration: const Duration(seconds: 2),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final timelineAsync = ref.watch(timelineProvider);
    final cs = Theme.of(context).colorScheme;

    // Refreshed-at display time in KSA
    final refreshedKsa = _lastRefreshedAt != null
        ? _lastRefreshedAt!.add(const Duration(hours: 3))
        : null;
    final refreshedLabel = refreshedKsa != null
        ? '${refreshedKsa.hour.toString().padLeft(2, '0')}:${refreshedKsa.minute.toString().padLeft(2, '0')}:${refreshedKsa.second.toString().padLeft(2, '0')} KSA'
        : '';

    return RefreshIndicator(
      onRefresh: _refresh,
      child: CustomScrollView(
        slivers: [
          // Filters summary and export on a single row
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  Expanded(
                    child: Consumer(builder: (context, ref, _) {
                      final settings = ref.watch(liveFeedFilterProvider);
                      final chips = <Widget>[];
                      chips.add(Chip(
                        label: Text(
                            settings.dateFilter.label(DateTime.now().toUtc())),
                        visualDensity: VisualDensity.compact,
                      ));
                      // spacing to separate date from session indicator
                      chips.add(const SizedBox(width: 8));
                      if (settings.sessions.isEmpty) {
                        chips.add(Chip(
                          label: const Text('All sessions'),
                          visualDensity: VisualDensity.compact,
                        ));
                      } else {
                        chips.addAll(settings.sessions.map((s) => Chip(
                              label: Text(s),
                              visualDensity: VisualDensity.compact,
                            )));
                      }
                      return SingleChildScrollView(
                        scrollDirection: Axis.horizontal,
                        child: Row(children: chips),
                      );
                    }),
                  ),
                  timelineAsync.whenOrNull(
                        data: (events) {
                          final settings = ref.watch(liveFeedFilterProvider);
                          final filtered =
                              events.where(settings.matches).toList();
                          if (filtered.isEmpty) return null;
                          return IconButton(
                            onPressed: () => _copyBulkLog(filtered),
                            icon: const Icon(Icons.copy_all),
                            tooltip:
                                'Copy all ${filtered.length} events to clipboard',
                            visualDensity: VisualDensity.compact,
                          );
                        },
                      ) ??
                      const SizedBox.shrink(),
                ],
              ),
            ),
          ),

          // Refreshed-at timestamp
          if (refreshedLabel.isNotEmpty)
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(14, 0, 12, 2),
                child: Text(
                  'Refreshed at $refreshedLabel · auto-refreshes every 15s',
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                        color: cs.onSurfaceVariant,
                        fontSize: 10,
                      ),
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
              final settings = ref.watch(liveFeedFilterProvider);
              final filtered = events.where(settings.matches).toList();

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
                          style:
                              Theme.of(context).textTheme.titleMedium?.copyWith(
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
                    final eventItem = filtered[index];
                    return _EventCard(
                      item: eventItem,
                      onCopy: () => _copyEventToClipboard(eventItem),
                    );
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
  const _EventCard({required this.item, required this.onCopy});

  final RuntimeTimelineItem item;
  final VoidCallback onCopy;

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
                padding: const EdgeInsets.fromLTRB(12, 10, 4, 10),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Header row: icon + description + time + copy button
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
                        const SizedBox(width: 4),
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.end,
                          children: [
                            Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
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
                                // Per-item copy button
                                IconButton(
                                  onPressed: onCopy,
                                  icon: Icon(Icons.copy_outlined,
                                      size: 14, color: cs.onSurfaceVariant),
                                  tooltip: 'Copy event log',
                                  padding: const EdgeInsets.all(4),
                                  constraints: const BoxConstraints(
                                    minWidth: 28,
                                    minHeight: 28,
                                  ),
                                  visualDensity: VisualDensity.compact,
                                ),
                              ],
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
                              size: 12, color: cs.onSurfaceVariant),
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
                              size: 12, color: cs.onSurfaceVariant),
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
                              size: 12, color: cs.onSurfaceVariant),
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
