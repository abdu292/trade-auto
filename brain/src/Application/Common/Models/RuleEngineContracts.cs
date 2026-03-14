namespace Brain.Application.Common.Models;

public sealed record H1ContextResult(
    string Context,
    bool HasLiquiditySweep,
    bool HasReclaim,
    bool IsTrendAligned,
    string Reason);

public sealed record M15SetupResult(
    bool IsValid,
    bool IsCompression,
    bool HasBase,
    string Reason);

public sealed record M5EntryResult(
    bool IsValid,
    bool IsBreakout,
    bool IsMomentumShift,
    bool IsRetest,
    string Reason);

/// <summary>
/// Result from MarketRegimeDetector. Indicates the structural market regime
/// and whether conditions are suitable for intraday scalping entries.
/// Regimes: TRENDING_BULL, TRENDING_BEAR, RANGING, CHOPPY, DEAD.
/// IsTradeable is false for TRENDING_BEAR (buy-only system), CHOPPY, and DEAD.
/// </summary>
public sealed record MarketRegimeResult(
    string Regime,
    bool IsTradeable,
    string Reason);

/// <summary>
/// Result from the impulse confirmation layer (Layer 4 in the rule engine).
/// Checks whether the entry timeframe (M5) shows real directional momentum
/// before the setup is approved, filtering out weak consolidation, slow movement,
/// and fake breakouts that lack actual price energy.
/// </summary>
public sealed record ImpulseConfirmationResult(
    bool IsConfirmed,
    bool HasMomentumExpansion,
    bool HasRangeExpansion,
    bool HasBodyExpansion,
    decimal ImpulseScore,
    string Reason);

public sealed record SetupCandidateResult(
    bool IsValid,
    H1ContextResult H1Context,
    M15SetupResult? M15Setup,
    M5EntryResult? M5Entry,
    string AbortReason,
    MarketRegimeResult? MarketRegime = null,
    ImpulseConfirmationResult? ImpulseConfirmation = null)
{
    public static SetupCandidateResult Valid(H1ContextResult h1, M15SetupResult m15, M5EntryResult m5, MarketRegimeResult marketRegime, ImpulseConfirmationResult impulse)
        => new(true, h1, m15, m5, string.Empty, marketRegime, impulse);

    public static SetupCandidateResult Aborted(H1ContextResult h1, string reason, MarketRegimeResult? marketRegime = null)
        => new(false, h1, null, null, reason, marketRegime);

    public static SetupCandidateResult Aborted(H1ContextResult h1, M15SetupResult m15, string reason, MarketRegimeResult? marketRegime = null)
        => new(false, h1, m15, null, reason, marketRegime);

    public static SetupCandidateResult AbortedByImpulse(H1ContextResult h1, M15SetupResult m15, M5EntryResult m5, ImpulseConfirmationResult impulse, MarketRegimeResult marketRegime)
        => new(false, h1, m15, m5, impulse.Reason, marketRegime, impulse);

    public static SetupCandidateResult AbortedByRegime(MarketRegimeResult marketRegime)
        => new(false,
            new H1ContextResult("SKIPPED", false, false, false, $"Aborted by market regime detector: {marketRegime.Regime}"),
            null,
            null,
            marketRegime.Reason,
            marketRegime);
}

public sealed record ReplayCandle(
    string Symbol,
    string Timeframe,
    DateTimeOffset Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public sealed record StructureResult(
    decimal? S1,
    decimal? S2,
    decimal? S3,
    decimal? R1,
    decimal? R2,
    decimal? Fail,
    bool FailThreatened,
    bool FailBroken,
    bool HasShelf,
    bool HasLid,
    bool HasSweep,
    bool HasReclaim,
    bool HasCompression,
    bool IsMidAir);

/// <summary>
/// AI-worker conversion result used by GoldEngineOrchestrator (TABLE path).
/// </summary>
public sealed record AnalyzeResult(
    string Regime,
    string WaterfallRisk,
    string MidAirStatus,
    string RailAStatus,
    string RailBStatus,
    string? RailAReason,
    string? RailBReason,
    decimal? S1,
    decimal? S2,
    decimal? S3,
    decimal? R1,
    decimal? R2,
    decimal? FailPrice,
    bool FailThreatened,
    bool FailBroken,
    bool FailProtected,
    bool StructureValid,
    string? CurrentSessionAnchor,
    string? NextSessionAnchor,
    decimal? NearestMagnet,
    string PrimaryTradeConcept,
    string RotationEnvelope,
    string? TriggerObject,
    string BottomType,
    string PatternType,
    decimal ImpulseHarvestScore,
    decimal SessionHistoricalModifier,
    decimal ConfidenceScore,
    decimal? LidPrice);

public sealed record ReplayStartRequest(
    string Symbol = "XAUUSD.gram",
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    /// <summary>Optional. If set (e.g. "Asia/Kolkata"), From/To are interpreted as local time in this zone and converted to UTC for candle filtering.</summary>
    string? TimezoneId = null,
    int SpeedMultiplier = 100,
    bool UseAI = true,
    bool UseMockAI = false,
    decimal InitialCashAed = 350000m,
    bool IgnoreNewsGate = true,
    string TelegramReplayState = "QUIET",
    /// <summary>When true, replay uses live Telegram/news in AI (for testing). Default false = neutral context for historical candles.</summary>
    bool UseLiveNewsAndTelegramInReplay = false);

public sealed record ReplayStatusContract(
    bool IsRunning,
    bool IsPaused,
    string Symbol,
    int TotalCandles,
    int ProcessedCandles,
    int CyclesTriggered,
    int SetupCandidatesFound,
    int TradesArmed,
    DateTimeOffset? ReplayFrom,
    DateTimeOffset? ReplayTo,
    DateTimeOffset? StartedUtc,
    string DriverTimeframe,
    /// <summary>
    /// IDLE | MT5_FETCH_QUEUED | MT5_FETCH_RECEIVED | IMPORTING | RUNNING | PAUSED | DONE | ERROR
    /// </summary>
    string Phase = "IDLE");

/// <summary>
/// Candle payload posted by the MT5 EA in response to a history-fetch request.
/// Each element represents one OHLCV bar.
/// </summary>
public sealed record Mt5HistoryCandleDto(
    long TimestampUnix,   // seconds since unix epoch (MT5 bar open time)
    double Open,
    double High,
    double Low,
    double Close,
    long Volume);

/// <summary>
/// Batch of candles for one symbol + timeframe posted by the MT5 EA.
/// </summary>
public sealed record Mt5HistoryBatchRequest(
    string Symbol,
    string Timeframe,
    Mt5HistoryCandleDto[] Candles,
    bool IsFinalBatch = false);

/// <summary>Request body for the combined MT5-fetch → import → replay endpoint.</summary>
public sealed record RunReplayRequest(
    string Symbol = "XAUUSD.gram",
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    /// <summary>Optional. If set (e.g. "Asia/Kolkata"), From/To are interpreted as local time in this zone and converted to UTC for fetch/filter.</summary>
    string? TimezoneId = null,
    int SpeedMultiplier = 100,
    bool UseMockAI = true,
    decimal InitialCashAed = 350000m,
    bool IgnoreNewsGate = true,
    string TelegramReplayState = "QUIET",
    bool UseLiveNewsAndTelegramInReplay = false);
