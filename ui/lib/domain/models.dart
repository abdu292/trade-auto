class StrategyProfile {
  const StrategyProfile({required this.id, required this.name, required this.isActive});

  final String id;
  final String name;
  final bool isActive;
}

class RiskProfile {
  const RiskProfile({required this.id, required this.name, required this.level, required this.isActive});

  final String id;
  final String name;
  final String level;
  final bool isActive;
}

class TradeItem {
  const TradeItem({required this.id, required this.symbol, required this.status});

  final String id;
  final String symbol;
  final String status;
}

class SessionStateItem {
  const SessionStateItem({required this.session, required this.isEnabled});

  final String session;
  final bool isEnabled;
}
