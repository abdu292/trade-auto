namespace Brain.Application.Common.Models;

/// <summary>
/// Mandatory pattern classes for the Pattern Detector module (CR8).
/// </summary>
public enum PatternType
{
    LiquiditySweep,
    WaterfallRisk,
    ContinuationBreakout,
    FalseBreakout,
    RangeReload,
    SessionTransitionTrap,
}

/// <summary>
/// Detection mode: deterministic rules only, or rules + AI confidence ranking.
/// AI ranking never overrides hard bans.
/// </summary>
public enum DetectionMode
{
    RuleOnly,
    RulePlusAi,
}

/// <summary>
/// Standardized recommended actions produced by the Pattern Detector.
/// </summary>
public enum RecommendedAction
{
    AllowRailAOnly,
    AllowRailB,
    WaitReclaim,
    WaitRetest,
    WaitCompression,
    NoBreakoutBuy,
    BlockNewBuys,
    CapitalProtected,
}

/// <summary>
/// Full output record for a single pattern detection result (CR8).
/// Includes PATTERN_ID, PATTERN_VERSION, DETECTION_MODE and all structured output fields.
/// </summary>
public sealed record PatternDetectionResult(
    string PatternId,
    string PatternVersion,
    DetectionMode DetectionMode,
    PatternType PatternType,
    string Subtype,
    decimal Confidence,
    string Session,
    string TimeframePrimary,
    string EntrySafety,
    string WaterfallRisk,
    bool FailThreatened,
    RecommendedAction RecommendedAction);
