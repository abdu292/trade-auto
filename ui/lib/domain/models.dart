class StrategyProfile {
  const StrategyProfile({
    required this.id,
    required this.name,
    required this.description,
    required this.isActive,
  });

  final String id;
  final String name;
  final String description;
  final bool isActive;

  factory StrategyProfile.fromJson(Map<String, dynamic> json) =>
      StrategyProfile(
        id: _readString(json, 'id'),
        name: _readString(json, 'name'),
        description: _readString(json, 'description'),
        isActive: _readBool(json, 'isActive'),
      );
}

class RiskProfile {
  const RiskProfile({
    required this.id,
    required this.name,
    required this.level,
    required this.maxDrawdownPercent,
    required this.isActive,
  });

  final String id;
  final String name;
  final String level;
  final double maxDrawdownPercent;
  final bool isActive;

  factory RiskProfile.fromJson(Map<String, dynamic> json) => RiskProfile(
        id: _readString(json, 'id'),
        name: _readString(json, 'name'),
        level: _readString(json, 'level'),
        maxDrawdownPercent: _readDouble(json, 'maxDrawdownPercent'),
        isActive: _readBool(json, 'isActive'),
      );
}

class TradeItem {
  const TradeItem({
    required this.id,
    required this.symbol,
    required this.rail,
    required this.entry,
    required this.tp,
    required this.expiryUtc,
    required this.maxLifeSeconds,
    required this.status,
    required this.createdAtUtc,
  });

  final String id;
  final String symbol;
  final String rail;
  final double entry;
  final double tp;
  final DateTime expiryUtc;
  final int maxLifeSeconds;
  final String status;
  final DateTime createdAtUtc;

  factory TradeItem.fromJson(Map<String, dynamic> json) => TradeItem(
        id: _readString(json, 'id'),
        symbol: _readString(json, 'symbol'),
        rail: _readString(json, 'rail'),
        entry: _readDouble(json, 'entry'),
        tp: _readDouble(json, 'tp'),
        expiryUtc: _readDateTime(json, 'expiryUtc'),
        maxLifeSeconds: _readInt(json, 'maxLifeSeconds'),
        status: _readString(json, 'status'),
        createdAtUtc: _readDateTime(json, 'createdAtUtc'),
      );
}

class SessionStateItem {
  const SessionStateItem({
    required this.id,
    required this.session,
    required this.isEnabled,
    required this.updatedAtUtc,
  });

  final String id;
  final String session;
  final bool isEnabled;
  final DateTime updatedAtUtc;

  factory SessionStateItem.fromJson(Map<String, dynamic> json) =>
      SessionStateItem(
        id: _readString(json, 'id'),
        session: _readString(json, 'session'),
        isEnabled: _readBool(json, 'isEnabled'),
        updatedAtUtc: _readDateTime(json, 'updatedAtUtc'),
      );
}

class LedgerState {
  const LedgerState({
    required this.cashAed,
    required this.goldGrams,
    required this.openExposurePercent,
    required this.deployableCashAed,
    required this.openBuyCount,
    this.goldAedEquivalent = 0,
    this.netEquityAed = 0,
    this.purchasePowerAed = 0,
    this.deployedAed = 0,
    this.openPositionsAed = 0,
    this.pendingReservedAed = 0,
    this.startingInvestmentAed = 0,
    this.equityMultiple = 0,
    this.bucketC1Aed = 0,
    this.bucketC2Aed = 0,
  });

  final double cashAed;
  final double goldGrams;
  final double openExposurePercent;
  final double deployableCashAed;
  final int openBuyCount;
  final double goldAedEquivalent;
  final double netEquityAed;
  final double purchasePowerAed;
  final double deployedAed;
  final double openPositionsAed;
  final double pendingReservedAed;
  final double startingInvestmentAed;
  final double equityMultiple;
  // Section 4: C1/C2 bucket split (C1=80%, C2=20% of deployable cash)
  final double bucketC1Aed;
  final double bucketC2Aed;

  factory LedgerState.fromJson(Map<String, dynamic> json) => LedgerState(
        cashAed: _readDouble(json, 'cashAed'),
        goldGrams: _readDouble(json, 'goldGrams'),
        openExposurePercent: _readDouble(json, 'openExposurePercent'),
        deployableCashAed: _readDouble(json, 'deployableCashAed'),
        openBuyCount: _readInt(json, 'openBuyCount'),
        goldAedEquivalent: _readDouble(json, 'goldAedEquivalent'),
        netEquityAed: _readDouble(json, 'netEquityAed'),
        purchasePowerAed: _readDouble(json, 'purchasePowerAed'),
        deployedAed: _readDouble(json, 'deployedAed'),
        openPositionsAed: _readDouble(json, 'openPositionsAed'),
        pendingReservedAed: _readDouble(json, 'pendingReservedAed'),
        startingInvestmentAed: _readDouble(json, 'startingInvestmentAed'),
        equityMultiple: _readDouble(json, 'equityMultiple'),
        bucketC1Aed: _readDouble(json, 'bucketC1Aed'),
        bucketC2Aed: _readDouble(json, 'bucketC2Aed'),
      );
}

class NotificationFeedItem {
  const NotificationFeedItem({
    required this.id,
    required this.channel,
    required this.title,
    required this.message,
    required this.createdAtUtc,
  });

  final String id;
  final String channel;
  final String title;
  final String message;
  final DateTime createdAtUtc;

  factory NotificationFeedItem.fromJson(Map<String, dynamic> json) =>
      NotificationFeedItem(
        id: _readString(json, 'id'),
        channel: _readString(json, 'channel'),
        title: _readString(json, 'title'),
        message: _readString(json, 'message'),
        createdAtUtc: _readDateTime(json, 'createdAtUtc'),
      );
}

class PendingOrderSnapshot {
  const PendingOrderSnapshot({
    required this.type,
    required this.price,
    required this.tp,
    this.expiry,
    required this.volumeGramsEquivalent,
  });

  final String type;
  final double price;
  final double tp;
  final DateTime? expiry;
  final double volumeGramsEquivalent;

  factory PendingOrderSnapshot.fromJson(Map<String, dynamic> json) =>
      PendingOrderSnapshot(
        type: _readString(json, 'type'),
        price: _readDouble(json, 'price'),
        tp: _readDouble(json, 'tp'),
        expiry: _readNullableDateTime(json, 'expiry'),
        volumeGramsEquivalent: _readDouble(json, 'volumeGramsEquivalent'),
      );
}

class OpenPositionSnapshot {
  const OpenPositionSnapshot({
    required this.entryPrice,
    required this.currentPnlPoints,
    required this.tp,
    required this.volumeGramsEquivalent,
  });

  final double entryPrice;
  final double currentPnlPoints;
  final double tp;
  final double volumeGramsEquivalent;

  factory OpenPositionSnapshot.fromJson(Map<String, dynamic> json) =>
      OpenPositionSnapshot(
        entryPrice: _readDouble(json, 'entryPrice'),
        currentPnlPoints: _readDouble(json, 'currentPnlPoints'),
        tp: _readDouble(json, 'tp'),
        volumeGramsEquivalent: _readDouble(json, 'volumeGramsEquivalent'),
      );
}

class PendingApproval {
  const PendingApproval({
    required this.id,
    required this.symbol,
    required this.type,
    required this.price,
    required this.tp,
    required this.expiry,
    required this.ml,
    required this.grams,
    required this.alignmentScore,
    required this.regime,
    required this.riskTag,
    required this.consensusPassed,
    required this.agreementCount,
    required this.requiredAgreement,
    required this.providerVotes,
    required this.summary,
    required this.modeHint,
    required this.modeConfidence,
    this.disagreementReason,
  });

  final String id;
  final String symbol;
  final String type;
  final double price;
  final double tp;
  final DateTime expiry;
  final int ml;
  final double grams;
  final double alignmentScore;
  final String regime;
  final String riskTag;
  final bool consensusPassed;
  final int agreementCount;
  final int requiredAgreement;
  final List<String> providerVotes;
  final String summary;
  final String modeHint;
  final double modeConfidence;
  final String? disagreementReason;

  factory PendingApproval.fromJson(Map<String, dynamic> json) =>
      PendingApproval(
        id: _readString(json, 'id'),
        symbol: _readString(json, 'symbol'),
        type: _readString(json, 'type'),
        price: _readDouble(json, 'price'),
        tp: _readDouble(json, 'tp'),
        expiry: _readDateTime(json, 'expiry'),
        ml: _readInt(json, 'ml'),
        grams: _readDouble(json, 'grams'),
        alignmentScore: _readDouble(json, 'alignmentScore'),
        regime: _readString(json, 'regime'),
        riskTag: _readString(json, 'riskTag'),
        consensusPassed: _readBool(json, 'consensusPassed'),
        agreementCount: _readInt(json, 'agreementCount'),
        requiredAgreement: _readInt(json, 'requiredAgreement'),
        providerVotes: _readStringList(json, 'providerVotes'),
        summary: _readString(json, 'summary'),
        modeHint: _readString(json, 'modeHint'),
        modeConfidence: _readDouble(json, 'modeConfidence'),
        disagreementReason: _readNullableString(json, 'disagreementReason'),
      );
}

class AiProviderCoverage {
  const AiProviderCoverage({
    required this.openai,
    required this.gemini,
    required this.grok,
    required this.perplexity,
    required this.allFourEnabled,
  });

  final bool openai;
  final bool gemini;
  final bool grok;
  final bool perplexity;
  final bool allFourEnabled;

  factory AiProviderCoverage.fromJson(Map<String, dynamic> json) =>
      AiProviderCoverage(
        openai: _readBool(json, 'openai'),
        gemini: _readBool(json, 'gemini'),
        grok: _readBool(json, 'grok'),
        perplexity: _readBool(json, 'perplexity'),
        allFourEnabled: _readBool(json, 'allFourEnabled'),
      );
}

class AiHealthStatus {
  const AiHealthStatus({
    required this.analyzerCount,
    required this.analyzers,
    required this.coverage,
    required this.parityBlockers,
  });

  final int analyzerCount;
  final List<String> analyzers;
  final AiProviderCoverage coverage;
  final List<String> parityBlockers;

  factory AiHealthStatus.fromJson(Map<String, dynamic> json) {
    final ai = _readMap(json, 'ai');
    final coverage = _readMap(ai, 'coverage');
    return AiHealthStatus(
      analyzerCount: _readInt(ai, 'analyzerCount'),
      analyzers: _readStringList(ai, 'analyzers'),
      coverage: AiProviderCoverage.fromJson(coverage),
      parityBlockers: _readStringList(ai, 'parityBlockers'),
    );
  }
}

/// Always-available market state from latest snapshot (no regime/trade pipeline). For "where rates are heading" chart.
class MarketState {
  const MarketState({
    required this.bid,
    required this.ask,
    required this.session,
    required this.sessionPhase,
    required this.sessionHigh,
    required this.sessionLow,
    this.timestamp,
  });

  final double bid;
  final double ask;
  final String session;
  final String sessionPhase;
  final double sessionHigh;
  final double sessionLow;
  final DateTime? timestamp;

  factory MarketState.fromJson(Map<String, dynamic> json) {
    final ts = json['timestamp'];
    return MarketState(
      bid: _readDouble(json, 'bid'),
      ask: _readDouble(json, 'ask'),
      session: _readString(json, 'session'),
      sessionPhase: _readString(json, 'sessionPhase'),
      sessionHigh: _readDouble(json, 'sessionHigh'),
      sessionLow: _readDouble(json, 'sessionLow'),
      timestamp: ts == null ? null : DateTime.tryParse(ts.toString()),
    );
  }
}

class RuntimeStatus {
  const RuntimeStatus({
    required this.symbol,
    required this.session,
    required this.mt5ServerTime,
    required this.ksaTime,
    required this.bid,
    required this.ask,
    required this.spread,
    required this.spreadMedian60m,
    required this.spreadMax60m,
    required this.telegramState,
    required this.panicSuspected,
    required this.tvAlertType,
    required this.pendingQueueDepth,
    required this.approvalQueueDepth,
    required this.executionMode,
    required this.macroBias,
    required this.institutionalBias,
    required this.cbFlowFlag,
    required this.positioningFlag,
    required this.macroCacheAgeMinutes,
    required this.activeBlockedHazardWindows,
    this.tickRatePer30s = 0,
    this.freezeGapDetected = false,
    this.pendingOrders = const [],
    this.openPositions = const [],
    this.balance = 0,
    this.equity = 0,
    this.freeMargin = 0,
  });

  final String symbol;
  final String session;
  final DateTime? mt5ServerTime;
  final DateTime? ksaTime;
  final double bid;
  final double ask;
  final double balance;
  final double equity;
  final double freeMargin;
  final double spread;
  final double spreadMedian60m;
  final double spreadMax60m;
  final String telegramState;
  final bool panicSuspected;
  final String tvAlertType;
  final int pendingQueueDepth;
  final int approvalQueueDepth;
  final String executionMode;
  final String macroBias;
  final String institutionalBias;
  final String cbFlowFlag;
  final String positioningFlag;
  final int macroCacheAgeMinutes;
  final int activeBlockedHazardWindows;
  final double tickRatePer30s;
  final bool freezeGapDetected;
  final List<PendingOrderSnapshot> pendingOrders;
  final List<OpenPositionSnapshot> openPositions;

  factory RuntimeStatus.fromJson(Map<String, dynamic> json) {
    final rawPending = json['pendingOrders'] ?? json['PendingOrders'];
    final pendingOrders = <PendingOrderSnapshot>[];
    if (rawPending is List) {
      for (final item in rawPending) {
        if (item is Map<String, dynamic>) {
          pendingOrders.add(PendingOrderSnapshot.fromJson(item));
        } else if (item is Map) {
          pendingOrders.add(PendingOrderSnapshot.fromJson(
              item.map((k, v) => MapEntry(k.toString(), v))));
        }
      }
    }
    final rawOpen = json['openPositions'] ?? json['OpenPositions'];
    final openPositions = <OpenPositionSnapshot>[];
    if (rawOpen is List) {
      for (final item in rawOpen) {
        if (item is Map<String, dynamic>) {
          openPositions.add(OpenPositionSnapshot.fromJson(item));
        } else if (item is Map) {
          openPositions.add(OpenPositionSnapshot.fromJson(
              item.map((k, v) => MapEntry(k.toString(), v))));
        }
      }
    }
    return RuntimeStatus(
        symbol: _readString(json, 'symbol'),
        session: _readString(json, 'session'),
        mt5ServerTime: _readNullableDateTime(json, 'mt5ServerTime'),
        ksaTime: _readNullableDateTime(json, 'ksaTime'),
        bid: _readDouble(json, 'bid'),
        ask: _readDouble(json, 'ask'),
        balance: _readDouble(json, 'balance'),
        equity: _readDouble(json, 'equity'),
        freeMargin: _readDouble(json, 'freeMargin'),
        spread: _readDouble(json, 'spread'),
        spreadMedian60m: _readDouble(json, 'spreadMedian60m'),
        spreadMax60m: _readDouble(json, 'spreadMax60m'),
        telegramState: _readString(json, 'telegramState'),
        panicSuspected: _readBool(json, 'panicSuspected'),
        tvAlertType: _readString(json, 'tvAlertType'),
        pendingQueueDepth: _readInt(json, 'pendingQueueDepth'),
        approvalQueueDepth: _readInt(json, 'approvalQueueDepth'),
        executionMode: _readString(json, 'executionMode'),
        macroBias: _readString(json, 'macroBias'),
        institutionalBias: _readString(json, 'institutionalBias'),
        cbFlowFlag: _readString(json, 'cbFlowFlag'),
        positioningFlag: _readString(json, 'positioningFlag'),
        macroCacheAgeMinutes: _readInt(json, 'macroCacheAgeMinutes'),
        activeBlockedHazardWindows:
            _readInt(json, 'activeBlockedHazardWindows'),
        tickRatePer30s: _readDouble(json, 'tickRatePer30s'),
        freezeGapDetected: _readBool(json, 'freezeGapDetected'),
        pendingOrders: pendingOrders,
        openPositions: openPositions,
      );
  }
}

class RuntimeSettings {
  const RuntimeSettings({required this.symbol, this.autoTradeEnabled = false, this.minTradeGrams = 0.1, this.microRotationEnabled = false});

  final String symbol;
  final bool autoTradeEnabled;
  final double minTradeGrams;
  final bool microRotationEnabled;

  factory RuntimeSettings.fromJson(Map<String, dynamic> json) {
    final minGrams = _readDouble(json, 'minTradeGrams');
    return RuntimeSettings(
      symbol: _readString(json, 'symbol'),
      autoTradeEnabled: _readBool(json, 'autoTradeEnabled'),
      minTradeGrams: minGrams > 0 ? minGrams : 0.1,
      microRotationEnabled: _readBool(json, 'microRotationEnabled'),
    );
  }
}

class ReplayStatus {
  const ReplayStatus({
    required this.isRunning,
    required this.isPaused,
    required this.symbol,
    required this.totalCandles,
    required this.processedCandles,
    required this.cyclesTriggered,
    required this.setupCandidatesFound,
    required this.tradesArmed,
    this.replayFrom,
    this.replayTo,
    this.startedUtc,
    required this.driverTimeframe,
    this.phase = 'IDLE',
  });

  final bool isRunning;
  final bool isPaused;
  final String symbol;
  final int totalCandles;
  final int processedCandles;
  final int cyclesTriggered;
  final int setupCandidatesFound;
  final int tradesArmed;
  final DateTime? replayFrom;
  final DateTime? replayTo;
  final DateTime? startedUtc;
  final String driverTimeframe;
  final String phase;

  factory ReplayStatus.fromJson(Map<String, dynamic> json) => ReplayStatus(
        isRunning: _readBool(json, 'isRunning'),
        isPaused: _readBool(json, 'isPaused'),
        symbol: _readString(json, 'symbol'),
        totalCandles: _readInt(json, 'totalCandles'),
        processedCandles: _readInt(json, 'processedCandles'),
        cyclesTriggered: _readInt(json, 'cyclesTriggered'),
        setupCandidatesFound: _readInt(json, 'setupCandidatesFound'),
        tradesArmed: _readInt(json, 'tradesArmed'),
        replayFrom: _readNullableDateTime(json, 'replayFrom'),
        replayTo: _readNullableDateTime(json, 'replayTo'),
        startedUtc: _readNullableDateTime(json, 'startedUtc'),
        driverTimeframe: _readString(json, 'driverTimeframe'),
        phase: _readString(json, 'phase').isEmpty ? 'IDLE' : _readString(json, 'phase'),
      );
}

class ReplayStatusResponse {
  const ReplayStatusResponse({
    required this.status,
    required this.importedCandles,
  });

  final ReplayStatus status;
  final Map<String, int> importedCandles;

  factory ReplayStatusResponse.fromJson(Map<String, dynamic> json) {
    final statusMap = _readMap(json, 'status');
    final importedRaw = json['importedCandles'] ?? json['ImportedCandles'];
    final imported = <String, int>{};
    if (importedRaw is Map) {
      importedRaw.forEach((key, value) {
        imported[key.toString()] = value is num ? value.toInt() : int.tryParse(value.toString()) ?? 0;
      });
    }

    return ReplayStatusResponse(
      status: ReplayStatus.fromJson(statusMap),
      importedCandles: imported,
    );
  }
}

class RuntimeTimelineItem {
  const RuntimeTimelineItem({
    required this.id,
    required this.eventType,
    required this.stage,
    required this.source,
    required this.symbol,
    this.cycleId,
    this.tradeId,
    required this.createdAtUtc,
    required this.payload,
  });

  final String id;
  final String eventType;
  final String stage;
  final String source;
  final String symbol;
  final String? cycleId;
  final String? tradeId;
  final DateTime createdAtUtc;
  final Map<String, dynamic> payload;

  factory RuntimeTimelineItem.fromJson(Map<String, dynamic> json) => RuntimeTimelineItem(
        id: _readString(json, 'id'),
        eventType: _readString(json, 'eventType'),
        stage: _readString(json, 'stage'),
        source: _readString(json, 'source'),
        symbol: _readString(json, 'symbol'),
        cycleId: _readNullableString(json, 'cycleId'),
        tradeId: _readNullableString(json, 'tradeId'),
        createdAtUtc: _readDateTime(json, 'createdAtUtc'),
        payload: _readMap(json, 'payload'),
      );
}

class HazardWindow {
  const HazardWindow({
    required this.id,
    required this.title,
    required this.category,
    required this.startUtc,
    required this.endUtc,
    required this.isBlocked,
    required this.isActive,
  });

  final String id;
  final String title;
  final String category;
  final DateTime startUtc;
  final DateTime endUtc;
  final bool isBlocked;
  final bool isActive;

  factory HazardWindow.fromJson(Map<String, dynamic> json) => HazardWindow(
        id: _readString(json, 'id'),
        title: _readString(json, 'title'),
        category: _readString(json, 'category'),
        startUtc: _readDateTime(json, 'startUtc'),
        endUtc: _readDateTime(json, 'endUtc'),
        isBlocked: _readBool(json, 'isBlocked'),
        isActive: _readBool(json, 'isActive'),
      );
}

class TradeSignal {
  const TradeSignal({
    required this.id,
    required this.symbol,
    required this.rail,
    required this.entry,
    required this.tp,
    required this.pe,
    required this.ml,
    required this.confidence,
    required this.createdAtUtc,
  });

  final String id;
  final String symbol;
  final String rail;
  final double entry;
  final double tp;
  final DateTime pe;
  final int ml;
  final double confidence;
  final DateTime createdAtUtc;

  factory TradeSignal.fromJson(Map<String, dynamic> json) => TradeSignal(
        id: _readString(json, 'id'),
        symbol: _readString(json, 'symbol'),
        rail: _readString(json, 'rail'),
        entry: _readDouble(json, 'entry'),
        tp: _readDouble(json, 'tp'),
        pe: _readDateTime(json, 'pe'),
        ml: _readInt(json, 'ml'),
        confidence: _readDouble(json, 'confidence'),
        createdAtUtc: _readDateTime(json, 'createdAtUtc'),
      );
}

class AnalyzeSnapshotInput {
  const AnalyzeSnapshotInput({
    required this.symbol,
    required this.session,
    required this.price,
  });

  final String symbol;
  final String session;
  final double price;

  Map<String, dynamic> toJson() {
    final now = DateTime.now().toUtc();
    return {
      'symbol': symbol,
      'timeframeData': [
        {
          'timeframe': 'M15',
          'open': price,
          'high': price,
          'low': price,
          'close': price,
        },
      ],
      'atr': 1.2,
      'adr': 10,
      'ma20': price,
      'session': session,
      'timestamp': now.toIso8601String(),
    };
  }
}

class SessionKpi {
  final double profitAed;
  final int rotations;
  final double avgCycleTimeMinutes;
  final int waterfallBlocks;

  const SessionKpi({
    required this.profitAed,
    required this.rotations,
    this.avgCycleTimeMinutes = 0,
    this.waterfallBlocks = 0,
  });

  factory SessionKpi.fromJson(Map<String, dynamic> json) => SessionKpi(
        profitAed: _readDouble(json, 'profitAed'),
        rotations: _readInt(json, 'rotations'),
        avgCycleTimeMinutes: _readDouble(json, 'avgCycleTimeMinutes'),
        waterfallBlocks: _readInt(json, 'waterfallBlocks'),
      );
}

class CompoundingStats {
  final double startingInvestmentAed;
  final double currentEquityAed;
  final double multiple;
  final bool milestoneReached;
  final double neededForFourXAed;

  const CompoundingStats({
    required this.startingInvestmentAed,
    required this.currentEquityAed,
    required this.multiple,
    required this.milestoneReached,
    required this.neededForFourXAed,
  });

  factory CompoundingStats.fromJson(Map<String, dynamic> json) =>
      CompoundingStats(
        startingInvestmentAed: _readDouble(json, 'startingInvestmentAed'),
        currentEquityAed: _readDouble(json, 'currentEquityAed'),
        multiple: _readDouble(json, 'multiple'),
        milestoneReached: _readBool(json, 'milestoneReached'),
        neededForFourXAed: _readDouble(json, 'neededForFourXAed'),
      );
}

class KpiStats {
  final String todayKsaDate;
  final double todayProfitAed;
  final int todayRotations;
  final double todayAvgProfitAed;
  final double todayHitRate;
  final Map<String, SessionKpi> sessionStats;
  final double weeklyProfitAed;
  final int weeklyRotations;
  final Map<String, SessionKpi> weeklySessionStats;
  final String weeklyBestSession;
  final String weeklyWorstSession;
  final Map<String, int> weeklyNoTradeBlocks;
  final CompoundingStats compounding;
  final int openPositionsCount;
  final int openBuyCount;

  const KpiStats({
    required this.todayKsaDate,
    required this.todayProfitAed,
    required this.todayRotations,
    required this.todayAvgProfitAed,
    required this.todayHitRate,
    required this.sessionStats,
    required this.weeklyProfitAed,
    required this.weeklyRotations,
    this.weeklySessionStats = const {},
    this.weeklyBestSession = '',
    this.weeklyWorstSession = '',
    this.weeklyNoTradeBlocks = const {},
    required this.compounding,
    required this.openPositionsCount,
    required this.openBuyCount,
  });

  factory KpiStats.fromJson(Map<String, dynamic> json) {
    final sessionMap = <String, SessionKpi>{};
    final rawSessions = json['sessionStats'] ?? json['SessionStats'];
    if (rawSessions is Map) {
      rawSessions.forEach((key, value) {
        if (value is Map<String, dynamic>) {
          sessionMap[key.toString()] = SessionKpi.fromJson(value);
        } else if (value is Map) {
          sessionMap[key.toString()] = SessionKpi.fromJson(
              value.map((k, v) => MapEntry(k.toString(), v)));
        }
      });
    }
    final weeklySessionMap = <String, SessionKpi>{};
    final rawWeeklySessions =
        json['weeklySessionStats'] ?? json['WeeklySessionStats'];
    if (rawWeeklySessions is Map) {
      rawWeeklySessions.forEach((key, value) {
        if (value is Map<String, dynamic>) {
          weeklySessionMap[key.toString()] = SessionKpi.fromJson(value);
        } else if (value is Map) {
          weeklySessionMap[key.toString()] = SessionKpi.fromJson(
              value.map((k, v) => MapEntry(k.toString(), v)));
        }
      });
    }
    final weeklyNoTradeMap = <String, int>{};
    final rawNoTrade =
        json['weeklyNoTradeBlocks'] ?? json['WeeklyNoTradeBlocks'];
    if (rawNoTrade is Map) {
      rawNoTrade.forEach((key, value) {
        weeklyNoTradeMap[key.toString()] =
            value is int ? value : int.tryParse(value.toString()) ?? 0;
      });
    }
    final compoundingRaw =
        json['compounding'] ?? json['Compounding'] ?? const <String, dynamic>{};
    final compoundingMap = compoundingRaw is Map<String, dynamic>
        ? compoundingRaw
        : (compoundingRaw is Map
            ? compoundingRaw.map((k, v) => MapEntry(k.toString(), v))
            : <String, dynamic>{});
    return KpiStats(
      todayKsaDate: _readString(json, 'todayKsaDate'),
      todayProfitAed: _readDouble(json, 'todayProfitAed'),
      todayRotations: _readInt(json, 'todayRotations'),
      todayAvgProfitAed: _readDouble(json, 'todayAvgProfitAed'),
      todayHitRate: _readDouble(json, 'todayHitRate'),
      sessionStats: sessionMap,
      weeklyProfitAed: _readDouble(json, 'weeklyProfitAed'),
      weeklyRotations: _readInt(json, 'weeklyRotations'),
      weeklySessionStats: weeklySessionMap,
      weeklyBestSession: _readString(json, 'weeklyBestSession'),
      weeklyWorstSession: _readString(json, 'weeklyWorstSession'),
      weeklyNoTradeBlocks: weeklyNoTradeMap,
      compounding: CompoundingStats.fromJson(compoundingMap),
      openPositionsCount: _readInt(json, 'openPositionsCount'),
      openBuyCount: _readInt(json, 'openBuyCount'),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Spec v7 §10 — Gold Engine dashboard contracts (Flutter side)
// ─────────────────────────────────────────────────────────────────────────────

/// Path map card: market bias, current path state (ladder), next likely move, nearest legal entry zone, why blocked/armed.
class PathMapSummary {
  const PathMapSummary({
    required this.marketBias,
    required this.currentPathState,
    required this.nextLikelyMove,
    this.nearestLegalEntryZone,
    this.whyBlockedOrArmed,
  });

  final String marketBias;
  final String currentPathState;
  final String nextLikelyMove;
  final double? nearestLegalEntryZone;
  final String? whyBlockedOrArmed;

  factory PathMapSummary.fromJson(Map<String, dynamic> json) =>
      PathMapSummary(
        marketBias: _readString(json, 'marketBias'),
        currentPathState: _readString(json, 'currentPathState'),
        nextLikelyMove: _readString(json, 'nextLikelyMove'),
        nearestLegalEntryZone: _readDoubleOrNull(json, 'nearestLegalEntryZone'),
        whyBlockedOrArmed: json['whyBlockedOrArmed']?.toString(),
      );
}

class GoldDashboard {
  const GoldDashboard({
    required this.physicalLedger,
    required this.mt5ExecutionAccount,
    required this.factorStatePanel,
    required this.tradeMapChart,
    required this.executionMode,
    this.validationSummary,
    this.pathMap,
  });

  final PhysicalLedgerCard physicalLedger;
  final Mt5ExecutionCard mt5ExecutionAccount;
  final FactorStatePanel? factorStatePanel;
  final TradeMapSummary tradeMapChart;
  final String executionMode;
  final ValidationSummary? validationSummary;
  final PathMapSummary? pathMap;

  factory GoldDashboard.fromJson(Map<String, dynamic> json) => GoldDashboard(
        physicalLedger: PhysicalLedgerCard.fromJson(
            _readMap(json, 'physicalLedger')),
        mt5ExecutionAccount: Mt5ExecutionCard.fromJson(
            _readMap(json, 'mt5ExecutionAccount')),
        factorStatePanel: json['factorStatePanel'] == null
            ? null
            : FactorStatePanel.fromJson(
                _readMap(json, 'factorStatePanel'),
              ),
        tradeMapChart:
            TradeMapSummary.fromJson(_readMap(json, 'tradeMapChart')),
        executionMode: _readString(json, 'executionMode'),
        validationSummary: json['validationSummary'] == null
            ? null
            : ValidationSummary.fromJson(
                _readMap(json, 'validationSummary')),
        pathMap: json['pathMap'] == null
            ? null
            : PathMapSummary.fromJson(_readMap(json, 'pathMap')),
      );
}

/// Indicators and factors sent for validation (DXY, Silver, cross-metal, full indicators list).
class ValidationSummary {
  const ValidationSummary({
    required this.rateNow,
    required this.rateAsk,
    required this.rateLabel,
    required this.indicators,
    required this.completeIndicatorsList,
    required this.dxyState,
    required this.silverCrossMetalState,
    this.dxyRate,
    this.silverRate,
    this.crossMetalNote,
    this.historicalComparison,
  });

  final double rateNow;
  final double rateAsk;
  final String rateLabel;
  final ValidationIndicators indicators;
  final String completeIndicatorsList;
  final String dxyState;
  final String silverCrossMetalState;
  final double? dxyRate;
  final double? silverRate;
  final String? crossMetalNote;
  final String? historicalComparison;

  factory ValidationSummary.fromJson(Map<String, dynamic> json) {
    final ind = json['indicators'];
    final dxyRateVal = json['dxyRate'];
    final silverRateVal = json['silverRate'];
    return ValidationSummary(
      rateNow: _readDouble(json, 'rateNow'),
      rateAsk: _readDouble(json, 'rateAsk'),
      rateLabel: _readString(json, 'rateLabel'),
      indicators: ind is Map<String, dynamic>
          ? ValidationIndicators.fromJson(ind)
          : const ValidationIndicators(),
      completeIndicatorsList:
          _readString(json, 'completeIndicatorsList'),
      dxyState: _readString(json, 'dxyState'),
      silverCrossMetalState:
          _readString(json, 'silverCrossMetalState'),
      dxyRate: dxyRateVal is num ? dxyRateVal.toDouble() : null,
      silverRate: silverRateVal is num ? silverRateVal.toDouble() : null,
      crossMetalNote: _readNullableString(json, 'crossMetalNote'),
      historicalComparison:
          _readNullableString(json, 'historicalComparison'),
    );
  }
}

class ValidationIndicators {
  const ValidationIndicators({
    this.rsiH1 = 0,
    this.rsiM15 = 0,
    this.ma20H1 = 0,
    this.ma20M15 = 0,
    this.atrM15 = 0,
    this.compressionM15 = 0,
    this.expansionM15 = 0,
    this.adrUsedPct = 0,
  });

  final double rsiH1;
  final double rsiM15;
  final double ma20H1;
  final double ma20M15;
  final double atrM15;
  final int compressionM15;
  final int expansionM15;
  final double adrUsedPct;

  factory ValidationIndicators.fromJson(Map<String, dynamic> json) =>
      ValidationIndicators(
        rsiH1: _readDouble(json, 'rsiH1'),
        rsiM15: _readDouble(json, 'rsiM15'),
        ma20H1: _readDouble(json, 'ma20H1'),
        ma20M15: _readDouble(json, 'ma20M15'),
        atrM15: _readDouble(json, 'atrM15'),
        compressionM15: (json['compressionM15'] as num?)?.toInt() ?? 0,
        expansionM15: (json['expansionM15'] as num?)?.toInt() ?? 0,
        adrUsedPct: _readDouble(json, 'adrUsedPct'),
      );
}

class PhysicalLedgerCard {
  const PhysicalLedgerCard({
    required this.cashAed,
    required this.goldGrams,
    required this.deployableAed,
    required this.buyableGrams,
  });

  final double cashAed;
  final double goldGrams;
  final double deployableAed;
  final double buyableGrams;

  factory PhysicalLedgerCard.fromJson(Map<String, dynamic> json) =>
      PhysicalLedgerCard(
        cashAed: _readDouble(json, 'cashAed'),
        goldGrams: _readDouble(json, 'goldGrams'),
        deployableAed: _readDouble(json, 'deployableAed'),
        buyableGrams: _readDouble(json, 'buyableGrams'),
      );
}

class Mt5ExecutionCard {
  const Mt5ExecutionCard({
    required this.balance,
    required this.equity,
    required this.freeMargin,
    required this.bid,
    required this.ask,
    required this.spread,
  });

  final double balance;
  final double equity;
  final double freeMargin;
  final double bid;
  final double ask;
  final double spread;

  factory Mt5ExecutionCard.fromJson(Map<String, dynamic> json) =>
      Mt5ExecutionCard(
        balance: _readDouble(json, 'balance'),
        equity: _readDouble(json, 'equity'),
        freeMargin: _readDouble(json, 'freeMargin'),
        bid: _readDouble(json, 'bid'),
        ask: _readDouble(json, 'ask'),
        spread: _readDouble(json, 'spread'),
      );
}

class FactorStatePanel {
  const FactorStatePanel({
    required this.legalityState,
    required this.biasState,
    required this.pathState,
    required this.overextensionState,
    required this.waterfallRisk,
    required this.session,
    required this.sessionPhase,
    this.efficiencyState = 'LOW',
    this.pathStateLadder,
  });

  final String legalityState;
  final String biasState;
  final String pathState;
  final String overextensionState;
  final String waterfallRisk;
  final String session;
  final String sessionPhase;
  // Spec v8 §11 — Rotation Efficiency state
  final String efficiencyState;
  /// Normalized ladder: STAND_DOWN | WATCH | EARLY_FLUSH_CANDIDATE | CANDIDATE | ARMED | TABLE_READY
  final String? pathStateLadder;

  /// Display path state: prefer ladder when available for consistency across cards.
  String get pathStateDisplay => pathStateLadder ?? pathState;

  factory FactorStatePanel.fromJson(Map<String, dynamic> json) =>
      FactorStatePanel(
        legalityState: _readString(json, 'legalityState'),
        biasState: _readString(json, 'biasState'),
        pathState: _readString(json, 'pathState'),
        overextensionState: _readString(json, 'overextensionState'),
        waterfallRisk: _readString(json, 'waterfallRisk'),
        session: _readString(json, 'session'),
        sessionPhase: _readString(json, 'sessionPhase'),
        efficiencyState: json['efficiencyState'] as String? ?? 'LOW',
        pathStateLadder: json['pathStateLadder']?.toString(),
      );
}

class TradeMapSummary {
  const TradeMapSummary({
    required this.bases,
    required this.sessionHigh,
    required this.sessionLow,
    required this.pendingBuyLimit,
    required this.pendingBuyStop,
  });

  final List<double> bases;
  final double sessionHigh;
  final double sessionLow;
  final List<PendingLevelSummary> pendingBuyLimit;
  final List<PendingLevelSummary> pendingBuyStop;

  factory TradeMapSummary.fromJson(Map<String, dynamic> json) {
    final rawBases = json['bases'];
    final bases = <double>[];
    if (rawBases is List) {
      for (final v in rawBases) {
        bases.add(
            v is num ? v.toDouble() : double.tryParse(v.toString()) ?? 0.0);
      }
    }
    List<PendingLevelSummary> _parseList(dynamic raw) {
      final list = <PendingLevelSummary>[];
      if (raw is List) {
        for (final item in raw) {
          if (item is Map<String, dynamic>) {
            list.add(PendingLevelSummary.fromJson(item));
          } else if (item is Map) {
            list.add(PendingLevelSummary.fromJson(
                item.map((k, v) => MapEntry(k.toString(), v))));
          }
        }
      }
      return list;
    }

    return TradeMapSummary(
      bases: bases,
      sessionHigh: _readDouble(json, 'sessionHigh'),
      sessionLow: _readDouble(json, 'sessionLow'),
      pendingBuyLimit: _parseList(json['pendingBuyLimit']),
      pendingBuyStop: _parseList(json['pendingBuyStop']),
    );
  }
}

class PendingLevelSummary {
  const PendingLevelSummary({
    required this.price,
    required this.tp,
    required this.expiry,
  });

  final double price;
  final double tp;
  final DateTime? expiry;

  factory PendingLevelSummary.fromJson(Map<String, dynamic> json) =>
      PendingLevelSummary(
        price: _readDouble(json, 'price'),
        tp: _readDouble(json, 'tp'),
        expiry: _readNullableDateTime(json, 'expiry'),
      );
}
String _readString(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)] ?? '';
  return value.toString();
}

String? _readNullableString(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)];
  if (value == null) {
    return null;
  }
  final text = value.toString();
  return text.isEmpty ? null : text;
}

List<String> _readStringList(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)];
  if (value is List) {
    return value.map((item) => item.toString()).toList();
  }
  return const [];
}

Map<String, dynamic> _readMap(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)];
  if (value is Map<String, dynamic>) {
    return value;
  }
  if (value is Map) {
    return value.map((k, v) => MapEntry(k.toString(), v));
  }
  return const <String, dynamic>{};
}

bool _readBool(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)] ?? false;
  return value is bool ? value : value.toString().toLowerCase() == 'true';
}

int _readInt(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)] ?? 0;
  if (value is int) {
    return value;
  }
  if (value is num) {
    return value.toInt();
  }
  return int.tryParse(value.toString()) ?? 0;
}

double _readDouble(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)] ?? 0;
  if (value is double) {
    return value;
  }
  if (value is num) {
    return value.toDouble();
  }
  return double.tryParse(value.toString()) ?? 0;
}

double? _readDoubleOrNull(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)];
  if (value == null) return null;
  if (value is double) return value;
  if (value is num) return value.toDouble();
  return double.tryParse(value.toString());
}

DateTime _readDateTime(Map<String, dynamic> json, String key) {
  final value = json[key] ??
      json[_pascal(key)] ??
      DateTime.now().toUtc().toIso8601String();
  if (value is DateTime) {
    return value.toUtc();
  }
  return DateTime.tryParse(value.toString())?.toUtc() ?? DateTime.now().toUtc();
}

DateTime? _readNullableDateTime(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)];
  if (value == null) {
    return null;
  }
  if (value is DateTime) {
    return value.toUtc();
  }
  return DateTime.tryParse(value.toString())?.toUtc();
}

String _pascal(String key) => '${key[0].toUpperCase()}${key.substring(1)}';
