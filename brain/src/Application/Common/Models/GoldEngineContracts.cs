namespace Brain.Application.Common.Models;

// ─── Spec v7: Path model ─────────────────────────────────────────────────────
/// <summary>Explicit path states per spec §2. No generic "M5 not confirmed = abort".</summary>
public static class PathState
{
    public const string BuyLimit = "BUY_LIMIT";
    public const string BuyStop = "BUY_STOP";
    public const string WaitPullback = "WAIT_PULLBACK";
    public const string Overextended = "OVEREXTENDED";
    public const string StandDown = "STAND_DOWN";
}

/// <summary>Wait/stand-down reason codes per spec §2C and §8.</summary>
public static class WaitReasonCode
{
    public const string OverextendedAboveBase = "OVEREXTENDED_ABOVE_BASE";
    public const string WaitPullbackBase = "WAIT_PULLBACK_BASE";
    public const string RangeNoStructure = "RANGE_NO_STRUCTURE";
    public const string HazardWindowBlock = "HAZARD_WINDOW_BLOCK";
    public const string HighWaterfallBlock = "HIGH_WATERFALL_BLOCK";
    public const string FailThreatened = "FAIL_THREATENED";
    public const string BuyStopBreakoutNotReady = "BUY_STOP_BREAKOUT_NOT_READY";
}

/// <summary>Overextension detector output per spec §5.1.</summary>
public static class OverextensionState
{
    public const string Normal = "NORMAL";
    public const string Stretched = "STRETCHED";
    public const string Overextended = "OVEREXTENDED";
}

/// <summary>Sweep + reclaim detector output per spec §5.2.</summary>
public static class SweepReclaimState
{
    public const string None = "NONE";
    public const string SweepOnly = "SWEEP_ONLY";
    public const string SweepReclaim = "SWEEP_RECLAIM";
    public const string FailedReclaim = "FAILED_RECLAIM";
}

/// <summary>Aggregated engine states per spec §3.</summary>
public static class LegalityState
{
    public const string Legal = "LEGAL";
    public const string Caution = "CAUTION";
    public const string Block = "BLOCK";
}

public static class BiasState
{
    public const string Bullish = "BULLISH";
    public const string Neutral = "NEUTRAL";
    public const string Bearish = "BEARISH";
    public const string Shock = "SHOCK";
}

public static class SizeState
{
    public const string Full = "FULL";
    public const string Reduced = "REDUCED";
    public const string Micro = "MICRO";
    public const string Zero = "ZERO";
}

public static class ExitState
{
    public const string MagnetTp = "MAGNET_TP";
    public const string StandardTp = "STANDARD_TP";
    public const string TightExpiry = "TIGHT_EXPIRY";
    public const string StandDown = "STAND_DOWN";
}

/// <summary>Spec v8 §11 — Rotation Efficiency Engine states.</summary>
public static class EfficiencyStates
{
    public const string High = "HIGH";
    public const string Medium = "MEDIUM";
    public const string Low = "LOW";
    public const string CapitalSleepRisk = "CAPITAL_SLEEP_RISK";
}

// ─── Result contracts ──────────────────────────────────────────────────────

/// <summary>OverextensionDetector output. Spec §5.1.</summary>
public sealed record OverextensionResult(
    string State,
    decimal Ma20DistAtrH1,
    decimal Ma20DistAtrM15,
    decimal RsiM15,
    decimal RsiH1,
    decimal BaseDistAtr,
    decimal CandleRangeAtr,
    string Reason);

/// <summary>Sweep + reclaim detector output. Spec §5.2 — NONE / SWEEP_ONLY / SWEEP_RECLAIM / FAILED_RECLAIM.</summary>
public sealed record SweepReclaimResult(
    string State,
    bool PriceSweptPriorLevel,
    bool CandleClosedBackInside,
    bool VolumeOrRangeSpike,
    bool NearStructuralLiquidity,
    string Reason);

/// <summary>Structural levels for PENDING_LIMIT_PATH. Spec §2A — S1 base shelf, S2 sweep pocket, S3 deeper exhaustion.</summary>
public sealed record PendingLimitPathContract(
    decimal S1BaseShelf,
    decimal? S2SweepPocket,
    decimal? S3ExhaustionPocket);

/// <summary>Path routing result. Spec §2 and §8.</summary>
public sealed record PathRoutingResult(
    string PathState,
    string? ReasonCode,
    PendingLimitPathContract? PendingLimitPath,
    string? RailHint);

/// <summary>Factor impact per spec §3. Single factor contribution.</summary>
public sealed record FactorImpactContract(
    string FactorName,
    string StateOrValue,
    string ImpactDirection,
    decimal ImpactStrength,
    string TimeHorizon,
    bool AffectsLegality,
    bool AffectsBias,
    bool AffectsSize,
    bool AffectsTp,
    bool AffectsExpiry,
    bool AffectsStandDown);

/// <summary>Aggregated engine states per spec §3.</summary>
public sealed record EngineStatesContract(
    string LegalityState,
    string BiasState,
    string PathState,
    string SizeState,
    string ExitState,
    string OverextensionState,
    string WaterfallRisk,
    string Session,
    string SessionPhase,
    IReadOnlyCollection<FactorImpactContract> Factors,
    // Spec v8 §11 — Rotation Efficiency state for dashboard
    string EfficiencyState = "LOW");

/// <summary>Confidence score result. Spec §7 — 0–100, thresholds &lt;60 WAIT, 60–74 MICRO, 75–89 normal, ≥90 high.</summary>
public sealed record ConfidenceScoreResult(
    int Score,
    string Tier,
    int H1ContextPoints,
    int M15StructurePoints,
    int SweepReclaimPoints,
    int VolatilityFitPoints,
    int StretchPoints,
    int SessionFitPoints,
    int AdrPoints,
    int SpreadPoints,
    int SafetyPoints,
    string Reason);

/// <summary>
/// Spec v8 §11 — Rotation Efficiency Engine result.
/// Metrics: same-session TP probability, expected AED return, expected hold time, AED per minute, sleep risk.
/// States: HIGH | MEDIUM | LOW | CAPITAL_SLEEP_RISK.
/// </summary>
public sealed record RotationEfficiencyResult(
    string EfficiencyState,
    int EfficiencyScore,
    decimal SameSessionTpProbability,
    decimal ExpectedAedReturn,
    int ExpectedHoldTimeMinutes,
    decimal AedPerMinute,
    bool SleepRisk,
    string Reason);

/// <summary>Session size and expiry bands. Spec §6.7, §6.8.</summary>
public sealed record SessionBandContract(
    string Session,
    decimal SizeMultiplier,
    int ExpiryMinutesMin,
    int ExpiryMinutesMax);

/// <summary>Auto-Tune Phase 1 report only. Spec §9. Never auto-apply.</summary>
public sealed record AutoTuneReportContract(
    string ReportId,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyCollection<string> SuggestedAdjustments,
    IReadOnlyCollection<string> BoundsRespected,
    IReadOnlyCollection<string> NeverTouched,
    string Summary);

/// <summary>Spec v7 §1 — Decision stack output. ProceedToAi = true only for BUY_LIMIT or BUY_STOP (with M5 when path=BUY_STOP).</summary>
public sealed record GoldEngineDecisionStackResult(
    bool ProceedToAi,
    string PathState,
    string? ReasonCode,
    MarketRegimeResult MarketRegime,
    H1ContextResult H1Context,
    M15SetupResult M15Setup,
    M5EntryResult? M5Entry,
    ImpulseConfirmationResult? ImpulseConfirmation,
    OverextensionResult Overextension,
    SweepReclaimResult SweepReclaim,
    PathRoutingResult PathRouting,
    string LegalityState,
    EngineStatesContract? EngineStates);
