namespace Brain.Application.Common.Models;

public sealed record TimeframeDataContract(string Timeframe, decimal Open, decimal High, decimal Low, decimal Close);

public sealed record MarketSnapshotContract(
    string Symbol,
    IReadOnlyCollection<TimeframeDataContract> TimeframeData,
    decimal Atr,
    decimal Adr,
    decimal Ma20,
    string Session,
    DateTimeOffset Timestamp,
    decimal VolatilityExpansion = 0m,
    DayOfWeek DayOfWeek = default,
    DateTimeOffset Mt5ServerTime = default,
    DateTimeOffset KsaTime = default,
    int Mt5ToKsaOffsetMinutes = 50,
    bool IsUsRiskWindow = false,
    bool IsFriday = false);

public sealed record TradeSignalContract(
    string Rail,
    decimal Entry,
    decimal Tp,
    DateTimeOffset Pe,
    int Ml,
    decimal Confidence,
    string SafetyTag = "CAUTION",
    string DirectionBias = "BULLISH",
    decimal AlignmentScore = 0.5m,
    IReadOnlyCollection<string>? NewsTags = null,
    string Summary = "");

public sealed record RegimeClassificationContract(
    string Regime,
    string RiskTag,
    bool IsBlocked,
    bool IsWaterfall,
    string Reason);

public sealed record DecisionResultContract(
    bool IsTradeAllowed,
    string Status,
    string Reason,
    string Rail,
    decimal Entry,
    decimal Tp,
    decimal Grams,
    DateTimeOffset ExpiryUtc,
    int MaxLifeSeconds,
    decimal AlignmentScore);

public sealed record LedgerStateContract(
    decimal CashAed,
    decimal GoldGrams,
    decimal OpenExposurePercent,
    decimal DeployableCashAed,
    int OpenBuyCount);

public sealed record TradeSlipContract(
    string SlipType,
    Guid TradeId,
    decimal Grams,
    decimal Mt5Price,
    decimal ShopPrice,
    decimal AmountAed,
    decimal NetProfitAed,
    decimal CashBalanceAed,
    decimal GoldBalanceGrams,
    DateTimeOffset Mt5Time,
    DateTimeOffset KsaTime,
    string Message);

public sealed record TradingViewSignalContract(
    string Symbol,
    string Timeframe,
    string Signal,
    string Bias,
    string RiskTag,
    decimal Score,
    decimal Volatility,
    DateTimeOffset Timestamp,
    string Source,
    string Notes);

public sealed record TradeCommandContract(
    string Type,
    decimal Price,
    decimal Tp,
    DateTimeOffset Expiry,
    int Ml);

public sealed record PendingTradeContract(
    Guid Id,
    string Symbol,
    string Type,
    decimal Price,
    decimal Tp,
    DateTimeOffset Expiry,
    int Ml,
    decimal Grams = 0m,
    decimal AlignmentScore = 0m,
    string Regime = "",
    string RiskTag = "");
