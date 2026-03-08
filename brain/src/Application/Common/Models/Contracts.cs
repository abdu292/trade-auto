namespace Brain.Application.Common.Models;

public sealed record TimeframeDataContract(
    string Timeframe,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume = 0L,
    DateTimeOffset? CandleStartTime = null,
    DateTimeOffset? CandleCloseTime = null,
    decimal CandleBodySize = 0m,
    decimal UpperWickSize = 0m,
    decimal LowerWickSize = 0m,
    decimal CandleRange = 0m,
    decimal Ma20Value = 0m,
    decimal Ma20Distance = 0m,
    decimal Rsi = 0m,
    decimal Atr = 0m);

public sealed record MarketSnapshotContract(
    string Symbol,
    IReadOnlyCollection<TimeframeDataContract> TimeframeData,
    decimal Atr,
    decimal Adr,
    decimal Ma20,
    string Session,
    DateTimeOffset Timestamp,
    decimal Ma20H4 = 0m,
    decimal Ma20H1 = 0m,
    decimal Ma20M30 = 0m,
    decimal RsiH1 = 0m,
    decimal RsiM15 = 0m,
    decimal AtrH1 = 0m,
    decimal AtrM15 = 0m,
    decimal PreviousDayHigh = 0m,
    decimal PreviousDayLow = 0m,
    decimal WeeklyHigh = 0m,
    decimal WeeklyLow = 0m,
    decimal DayOpen = 0m,
    decimal WeekOpen = 0m,
    decimal SessionHigh = 0m,
    decimal SessionLow = 0m,
    decimal SessionHighJapan = 0m,
    decimal SessionLowJapan = 0m,
    decimal SessionHighIndia = 0m,
    decimal SessionLowIndia = 0m,
    decimal SessionHighLondon = 0m,
    decimal SessionLowLondon = 0m,
    decimal SessionHighNy = 0m,
    decimal SessionLowNy = 0m,
    decimal PreviousSessionHigh = 0m,
    decimal PreviousSessionLow = 0m,
    decimal Ema50H1 = 0m,
    decimal Ema200H1 = 0m,
    decimal AdrUsedPct = 0m,
    decimal VolatilityExpansion = 0m,
    DayOfWeek DayOfWeek = default,
    DateTimeOffset Mt5ServerTime = default,
    DateTimeOffset KsaTime = default,
    DateTimeOffset UaeTime = default,
    DateTimeOffset IndiaTime = default,
    DateTimeOffset InternalClockUtc = default,
    DateTimeOffset UtcReferenceTime = default,
    decimal TimeSkewMs = 0m,
    int Mt5ToKsaOffsetMinutes = 50,
    string TelegramImpactTag = "LOW",
    string TradingViewConfirmation = "NEUTRAL",
    bool IsCompression = false,
    bool IsExpansion = false,
    bool IsAtrExpanding = false,
    bool HasOverlapCandles = false,
    bool HasImpulseCandles = false,
    bool HasLiquiditySweep = false,
    bool HasPanicDropSequence = false,
    bool IsPostSpikePullback = false,
    bool IsLondonNyOverlap = false,
    bool IsBreakoutConfirmed = false,
    bool IsUsRiskWindow = false,
    bool IsFriday = false,
    decimal Bid = 0m,
    decimal Ask = 0m,
    decimal Spread = 0m,
    decimal SpreadMedian60m = 0m,
    decimal SpreadMax60m = 0m,
    int CompressionCountM15 = 0,
    int CompressionCountM5 = 0,
    int ExpansionCountM15 = 0,
    decimal ImpulseStrengthScore = 0m,
    string TelegramState = "QUIET",
    bool PanicSuspected = false,
    string TvAlertType = "NONE",
    string SessionPhase = "UNKNOWN",
    // Spread stats per 1m and 5m windows (spec_v5.md A1)
    decimal SpreadMin1m = 0m,
    decimal SpreadAvg1m = 0m,
    decimal SpreadMax1m = 0m,
    decimal SpreadMin5m = 0m,
    decimal SpreadAvg5m = 0m,
    decimal SpreadMax5m = 0m,
    // Account state for exposure cap enforcement (spec_v5.md A1)
    decimal FreeMargin = 0m,
    decimal Equity = 0m,
    decimal Balance = 0m,
    // Tick/market quality (PRD)
    decimal TickRatePer30s = 0m,
    bool FreezeGapDetected = false,
    decimal SlippageEstimatePoints = 0m,
    decimal SessionVwap = 0m,
    decimal SystemFetchedGoldRate = 0m,
    decimal RateDeltaUsd = 0m,
    string RateAuthority = "MT5",
    decimal AuthoritativeRate = 0m,
    string? CycleId = null,
    // Compression metric support (PRD)
    IReadOnlyCollection<decimal>? CompressionRangesM15 = null,
    // Pending/open/execution snapshots (PRD)
    IReadOnlyCollection<PendingOrderSnapshotContract>? PendingOrders = null,
    IReadOnlyCollection<OpenPositionSnapshotContract>? OpenPositions = null,
    IReadOnlyCollection<OrderExecutionEventContract>? OrderExecutionEvents = null);
    
public sealed record PendingOrderSnapshotContract(
    string Type,
    decimal Price,
    decimal Tp,
    DateTimeOffset? Expiry,
    decimal VolumeGramsEquivalent = 0m);

public sealed record OpenPositionSnapshotContract(
    decimal EntryPrice,
    decimal CurrentPnlPoints,
    decimal Tp,
    decimal VolumeGramsEquivalent = 0m);

public sealed record OrderExecutionEventContract(
    string Status,
    DateTimeOffset Timestamp,
    decimal Price = 0m,
    decimal VolumeGramsEquivalent = 0m,
    ulong Ticket = 0UL);

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
    string NewsImpactTag = "LOW",
    string TvConfirmationTag = "NEUTRAL",
    IReadOnlyCollection<string>? NewsTags = null,
    string Summary = "",
    bool ConsensusPassed = true,
    int AgreementCount = 1,
    int RequiredAgreement = 1,
    string? DisagreementReason = null,
    IReadOnlyCollection<string>? ProviderVotes = null,
    string ModeHint = "UNKNOWN",
    decimal ModeConfidence = 0.5m,
    int ModeTtlSeconds = 900,
    IReadOnlyCollection<string>? ModeKeywords = null,
    string RegimeTag = "STANDARD",
    string RiskState = "CAUTION",
    string GeoHeadline = "NONE",
    string DxyBias = "NEUTRAL",
    string YieldsBias = "NEUTRAL",
    string CrossMetalsBias = "NEUTRAL",
    string CbFlow = "UNKNOWN",
    string InstPositioning = "UNKNOWN",
    string EventRisk = "LOW",
    IReadOnlyCollection<string>? PromptRefs = null,
    IReadOnlyCollection<string>? ProviderModels = null,
    string? AiTraceJson = null,
    string? CycleId = null);

public sealed record ModeSignalContract(
    string Mode,
    decimal Confidence,
    IReadOnlyCollection<string> Keywords,
    int TtlSeconds,
    DateTimeOffset CapturedAtUtc);

public sealed record RegimeClassificationContract(
    string Regime,
    string RiskTag,
    bool IsBlocked,
    bool IsWaterfall,
    string Reason);

public sealed record DecisionResultContract(
    bool IsTradeAllowed,
    string Status,
    string EngineState,
    string Mode,
    string Cause,
    string WaterfallRisk,
    string Reason,
    string Bucket,
    string Rail,
    string Session,
    string SessionPhase,
    string RegimeTag,
    string RiskState,
    string SizeClass,
    decimal Entry,
    decimal Tp,
    decimal Grams,
    DateTimeOffset ExpiryUtc,
    int MaxLifeSeconds,
    decimal AlignmentScore,
    string TelegramState,
    string RailPermissionA,
    string RailPermissionB,
    int RotationCapThisSession,
    // Section 8.2: Full TABLE columns — shop prices and dual-timezone expiry
    decimal ShopBuy = 0m,
    decimal ShopSell = 0m,
    DateTimeOffset ExpiryKSA = default,
    DateTimeOffset ExpiryServer = default);

public sealed record LedgerStateContract(
    decimal CashAed,
    decimal GoldGrams,
    decimal OpenExposurePercent,
    decimal DeployableCashAed,
    int OpenBuyCount,
    decimal GoldAedEquivalent = 0m,
    decimal NetEquityAed = 0m,
    decimal PurchasePowerAed = 0m,
    decimal DeployedAed = 0m,
    decimal OpenPositionsAed = 0m,
    decimal PendingReservedAed = 0m,
    decimal StartingInvestmentAed = 0m,
    decimal EquityMultiple = 0m,
    // Section 4: C1/C2 bucket capacity (C1=80%, C2=20% of deployable cash)
    decimal BucketC1Aed = 0m,
    decimal BucketC2Aed = 0m);

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
    string ConfirmationTag,
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
    string RiskTag = "",
    string EngineState = "ARMED",
    string Mode = "EXHAUSTION",
    string Cause = "UNKNOWN",
    string WaterfallRisk = "LOW",
    string Bucket = "C1",
    string Session = "",
    string SessionPhase = "UNKNOWN",
    string RegimeTag = "STANDARD",
    string RiskState = "CAUTION",
    string SizeClass = "25%",
    string TelegramState = "QUIET",
    bool ConsensusPassed = true,
    int AgreementCount = 1,
    int RequiredAgreement = 1,
    string? DisagreementReason = null,
    IReadOnlyCollection<string>? ProviderVotes = null,
    string Summary = "",
    string ModeHint = "UNKNOWN",
    decimal ModeConfidence = 0.5m,
    string? CycleId = null,
    // Section 8.2: Full TABLE columns — shop prices and dual-timezone expiry
    decimal ShopBuy = 0m,
    decimal ShopSell = 0m,
    DateTimeOffset ExpiryKSA = default,
    DateTimeOffset ExpiryServer = default);

/// <summary>
/// Score breakdown produced by TradeScoreCalculator for a valid trade setup.
/// Score range: 0–100. Thresholds: &lt;45 NO_TRADE, 45–59 WEAK, 60–79 VALID, ≥80 HIGH_CONVICTION.
/// </summary>
public sealed record TradeScoreContract(
    int StructureScore,
    int MomentumScore,
    int ExecutionScore,
    int AiScore,
    int SentimentScore,
    int TotalScore,
    string DecisionTier);

/// <summary>
/// Context passed to the aiworker /study-analyze endpoint when STUDY_LOCK is active.
/// Contains information about recent waterfall failures and blocked trade candidates.
/// </summary>
public sealed record StudyContextContract(
    int ConsecutiveWaterfallFailures,
    string StudyCycleId,
    IReadOnlyCollection<object> RecentBlockedCandidates,
    IReadOnlyCollection<string> RecentWaterfallReasons);

/// <summary>
/// Result from the aiworker autonomous study/self-crosscheck refinement loop.
/// Verdicts: bottomPermissionVerdict = TOO_STRICT | CORRECT | TOO_LOOSE,
///           waterfallVerdict = CORRECT | OVER_SENSITIVE | UNDER_SENSITIVE.
/// </summary>
public sealed record StudyRefinementSuggestionContract(
    string StudyCycleId,
    string BottomPermissionVerdict,
    string WaterfallVerdict,
    IReadOnlyCollection<string> RuleAdjustments,
    double Confidence,
    string Reasoning,
    IReadOnlyCollection<string> ProviderVotes);
