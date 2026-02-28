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
  });

  final double cashAed;
  final double goldGrams;
  final double openExposurePercent;
  final double deployableCashAed;
  final int openBuyCount;

  factory LedgerState.fromJson(Map<String, dynamic> json) => LedgerState(
        cashAed: _readDouble(json, 'cashAed'),
        goldGrams: _readDouble(json, 'goldGrams'),
        openExposurePercent: _readDouble(json, 'openExposurePercent'),
        deployableCashAed: _readDouble(json, 'deployableCashAed'),
        openBuyCount: _readInt(json, 'openBuyCount'),
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
      );
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
    required this.macroBias,
    required this.institutionalBias,
    required this.cbFlowFlag,
    required this.positioningFlag,
    required this.macroCacheAgeMinutes,
    required this.activeBlockedHazardWindows,
  });

  final String symbol;
  final String session;
  final DateTime? mt5ServerTime;
  final DateTime? ksaTime;
  final double bid;
  final double ask;
  final double spread;
  final double spreadMedian60m;
  final double spreadMax60m;
  final String telegramState;
  final bool panicSuspected;
  final String tvAlertType;
  final int pendingQueueDepth;
  final String macroBias;
  final String institutionalBias;
  final String cbFlowFlag;
  final String positioningFlag;
  final int macroCacheAgeMinutes;
  final int activeBlockedHazardWindows;

  factory RuntimeStatus.fromJson(Map<String, dynamic> json) => RuntimeStatus(
        symbol: _readString(json, 'symbol'),
        session: _readString(json, 'session'),
        mt5ServerTime: _readNullableDateTime(json, 'mt5ServerTime'),
        ksaTime: _readNullableDateTime(json, 'ksaTime'),
        bid: _readDouble(json, 'bid'),
        ask: _readDouble(json, 'ask'),
        spread: _readDouble(json, 'spread'),
        spreadMedian60m: _readDouble(json, 'spreadMedian60m'),
        spreadMax60m: _readDouble(json, 'spreadMax60m'),
        telegramState: _readString(json, 'telegramState'),
        panicSuspected: _readBool(json, 'panicSuspected'),
        tvAlertType: _readString(json, 'tvAlertType'),
        pendingQueueDepth: _readInt(json, 'pendingQueueDepth'),
        macroBias: _readString(json, 'macroBias'),
        institutionalBias: _readString(json, 'institutionalBias'),
        cbFlowFlag: _readString(json, 'cbFlowFlag'),
        positioningFlag: _readString(json, 'positioningFlag'),
        macroCacheAgeMinutes: _readInt(json, 'macroCacheAgeMinutes'),
        activeBlockedHazardWindows:
            _readInt(json, 'activeBlockedHazardWindows'),
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

String _readString(Map<String, dynamic> json, String key) {
  final value = json[key] ?? json[_pascal(key)] ?? '';
  return value.toString();
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
