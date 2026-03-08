using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// PATTERN DETECTOR — Live pattern recognition module (CR8).
/// Detects structural patterns from market snapshot data using deterministic rules first,
/// with optional AI confidence ranking second (DETECTION_MODE = RULE_ONLY | RULE_PLUS_AI).
///
/// Non-executing: produces structured pattern intelligence only.
/// Feeds: ANALYZE, TABLE, MANAGE, RE ANALYZE, STUDY.
/// </summary>
public static class PatternDetector
{
    private const string Version = "1.0";

    /// <summary>
    /// Runs all mandatory pattern classes against the snapshot and returns
    /// a collection of detected patterns with full metadata.
    /// </summary>
    public static IReadOnlyCollection<PatternDetectionResult> Detect(MarketSnapshotContract snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var results = new List<PatternDetectionResult>();
        var session = NormalizeSession(snapshot.Session);

        results.AddRange(DetectLiquiditySweep(snapshot, session));
        results.AddRange(DetectWaterfallRisk(snapshot, session));
        results.AddRange(DetectContinuationBreakout(snapshot, session));
        results.AddRange(DetectFalseBreakout(snapshot, session));
        results.AddRange(DetectRangeReload(snapshot, session));
        results.AddRange(DetectSessionTransitionTrap(snapshot, session));

        return results;
    }

    // ── LIQUIDITY_SWEEP ────────────────────────────────────────────────────────

    private static IEnumerable<PatternDetectionResult> DetectLiquiditySweep(
        MarketSnapshotContract snapshot,
        string session)
    {
        if (!snapshot.HasLiquiditySweep)
        {
            yield break;
        }

        var hasReclaim = snapshot.TvAlertType is "SHELF_RECLAIM" or "RETEST_HOLD";
        var compressionAfter = snapshot.CompressionCountM5 >= 3 || snapshot.IsCompression;

        var subtype = hasReclaim && compressionAfter ? "SWEEP_AND_RECLAIM"
            : hasReclaim ? "SWEEP_RECLAIM_NO_COMPRESSION"
            : "SWEEP_ONLY";

        var confidence = hasReclaim && compressionAfter ? 0.90m
            : hasReclaim ? 0.70m
            : 0.45m;

        var action = hasReclaim && compressionAfter
            ? RecommendedAction.AllowRailAOnly
            : RecommendedAction.WaitReclaim;

        var failThreatened = IsFailThreatened(snapshot);

        yield return Build(
            patternType: PatternType.LiquiditySweep,
            subtype: subtype,
            confidence: confidence,
            session: session,
            snapshot: snapshot,
            entrySafety: hasReclaim ? "SAFE" : "CAUTION",
            waterfallRisk: snapshot.HasPanicDropSequence ? "HIGH" : "LOW",
            failThreatened: failThreatened,
            action: action);
    }

    // ── WATERFALL_RISK ─────────────────────────────────────────────────────────

    private static IEnumerable<PatternDetectionResult> DetectWaterfallRisk(
        MarketSnapshotContract snapshot,
        string session)
    {
        var isHighRisk = snapshot.HasPanicDropSequence
            || (snapshot.RsiH1 > 0m && snapshot.RsiH1 < 35m && snapshot.IsExpansion && snapshot.IsAtrExpanding);

        var isMediumRisk = !isHighRisk && (
            (snapshot.AdrUsedPct > 80m && snapshot.IsAtrExpanding)
            || (snapshot.SpreadMax60m > 0m && snapshot.SpreadMedian60m > 0m
                && snapshot.SpreadMax60m >= snapshot.SpreadMedian60m * 2.0m));

        if (!isHighRisk && !isMediumRisk)
        {
            yield break;
        }

        var subtype = isHighRisk ? "ACTIVE_PANIC_DROP" : "ELEVATED_VOLATILITY";
        var confidence = isHighRisk ? 0.92m : 0.65m;
        var waterfallRisk = isHighRisk ? "HIGH" : "MEDIUM";
        var action = isHighRisk ? RecommendedAction.BlockNewBuys : RecommendedAction.WaitCompression;

        yield return Build(
            patternType: PatternType.WaterfallRisk,
            subtype: subtype,
            confidence: confidence,
            session: session,
            snapshot: snapshot,
            entrySafety: isHighRisk ? "BLOCKED" : "CAUTION",
            waterfallRisk: waterfallRisk,
            failThreatened: IsFailThreatened(snapshot),
            action: action);
    }

    // ── CONTINUATION_BREAKOUT ──────────────────────────────────────────────────

    private static IEnumerable<PatternDetectionResult> DetectContinuationBreakout(
        MarketSnapshotContract snapshot,
        string session)
    {
        if (!snapshot.IsBreakoutConfirmed)
        {
            yield break;
        }

        var hasLidBreak = snapshot.TvAlertType is "LID_BREAK" or "BREAKOUT" or "SESSION_BREAK";
        var hasCompression = snapshot.CompressionCountM15 >= 4 && snapshot.IsCompression;
        var h1BullishContext = snapshot.Ma20H1 > 0m
            && snapshot.TimeframeData
                .FirstOrDefault(x => string.Equals(x.Timeframe, "H1", StringComparison.OrdinalIgnoreCase))
                ?.Close > snapshot.Ma20H1;

        if (!hasLidBreak && !hasCompression)
        {
            yield break;
        }

        var confidence = hasLidBreak && hasCompression && h1BullishContext == true ? 0.88m
            : hasLidBreak ? 0.72m
            : 0.55m;

        var action = snapshot.IsAtrExpanding
            ? RecommendedAction.AllowRailB
            : RecommendedAction.WaitRetest;

        yield return Build(
            patternType: PatternType.ContinuationBreakout,
            subtype: hasLidBreak ? "LID_BREAK_CONFIRMED" : "COMPRESSION_RELEASE",
            confidence: confidence,
            session: session,
            snapshot: snapshot,
            entrySafety: "SAFE",
            waterfallRisk: "LOW",
            failThreatened: IsFailThreatened(snapshot),
            action: action);
    }

    // ── FALSE_BREAKOUT ─────────────────────────────────────────────────────────

    private static IEnumerable<PatternDetectionResult> DetectFalseBreakout(
        MarketSnapshotContract snapshot,
        string session)
    {
        // False breakout: breakout briefly confirmed then price reverses back into range
        var falseBreakSuspected = snapshot.IsBreakoutConfirmed
            && snapshot.IsPostSpikePullback
            && snapshot.CompressionCountM5 >= 3
            && !snapshot.IsAtrExpanding;

        if (!falseBreakSuspected)
        {
            yield break;
        }

        yield return Build(
            patternType: PatternType.FalseBreakout,
            subtype: "SPIKE_REVERSAL",
            confidence: 0.75m,
            session: session,
            snapshot: snapshot,
            entrySafety: "CAUTION",
            waterfallRisk: "MEDIUM",
            failThreatened: IsFailThreatened(snapshot),
            action: RecommendedAction.WaitReclaim);
    }

    // ── RANGE_RELOAD ───────────────────────────────────────────────────────────

    private static IEnumerable<PatternDetectionResult> DetectRangeReload(
        MarketSnapshotContract snapshot,
        string session)
    {
        // Range reload: price consolidates in compression with no expansion, liquidity building
        var isRangeReload = snapshot.IsCompression
            && !snapshot.IsExpansion
            && !snapshot.IsAtrExpanding
            && snapshot.CompressionCountM15 >= 6
            && !snapshot.HasPanicDropSequence;

        if (!isRangeReload)
        {
            yield break;
        }

        var hasGoodBase = snapshot.HasOverlapCandles && snapshot.CompressionCountM5 >= 4;
        var subtype = hasGoodBase ? "COIL_WITH_BASE" : "FLAT_RANGE";
        var confidence = hasGoodBase ? 0.80m : 0.55m;

        yield return Build(
            patternType: PatternType.RangeReload,
            subtype: subtype,
            confidence: confidence,
            session: session,
            snapshot: snapshot,
            entrySafety: hasGoodBase ? "SAFE" : "CAUTION",
            waterfallRisk: "LOW",
            failThreatened: IsFailThreatened(snapshot),
            action: hasGoodBase ? RecommendedAction.AllowRailAOnly : RecommendedAction.WaitCompression);
    }

    // ── SESSION_TRANSITION_TRAP ────────────────────────────────────────────────

    private static IEnumerable<PatternDetectionResult> DetectSessionTransitionTrap(
        MarketSnapshotContract snapshot,
        string session)
    {
        // Session transition trap: false move at the open of a new session (stop hunt / fake spike)
        var isTransitionPhase = string.Equals(snapshot.SessionPhase, "START", StringComparison.OrdinalIgnoreCase);
        var hasSpike = snapshot.IsExpansion && snapshot.IsAtrExpanding
            && snapshot.SpreadMax1m > 0m && snapshot.SpreadAvg1m > 0m
            && snapshot.SpreadMax1m >= snapshot.SpreadAvg1m * 1.6m;

        // London/NY opens are highest-risk for session transition traps
        var highRiskSession = session is "LONDON" or "NY";

        if (!isTransitionPhase || (!hasSpike && !highRiskSession))
        {
            yield break;
        }

        var confidence = hasSpike && highRiskSession ? 0.82m
            : hasSpike ? 0.65m
            : 0.48m;

        var action = hasSpike
            ? RecommendedAction.WaitReclaim
            : RecommendedAction.WaitRetest;

        yield return Build(
            patternType: PatternType.SessionTransitionTrap,
            subtype: hasSpike ? "OPEN_SPIKE_TRAP" : "TRANSITION_CAUTION",
            confidence: confidence,
            session: session,
            snapshot: snapshot,
            entrySafety: "CAUTION",
            waterfallRisk: hasSpike ? "MEDIUM" : "LOW",
            failThreatened: IsFailThreatened(snapshot),
            action: action);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static bool IsFailThreatened(MarketSnapshotContract snapshot)
    {
        // FAIL is threatened when ADR usage is extreme or panic/breakdown conditions met
        return snapshot.AdrUsedPct > 90m
            || snapshot.HasPanicDropSequence
            || (snapshot.RsiH1 > 0m && snapshot.RsiH1 < 30m && snapshot.IsExpansion);
    }

    private static string NormalizeSession(string? session)
    {
        var s = (session ?? string.Empty).Trim().ToUpperInvariant();
        return s switch
        {
            "ASIA" => "JAPAN",
            "EUROPE" => "LONDON",
            "NEW_YORK" => "NY",
            _ => s,
        };
    }

    private static PatternDetectionResult Build(
        PatternType patternType,
        string subtype,
        decimal confidence,
        string session,
        MarketSnapshotContract snapshot,
        string entrySafety,
        string waterfallRisk,
        bool failThreatened,
        RecommendedAction action)
    {
        var primaryTf = snapshot.AtrM15 > 0m ? "M15" : "M5";

        return new PatternDetectionResult(
            PatternId: $"{patternType}_{session}_{snapshot.Timestamp.ToUnixTimeSeconds()}",
            PatternVersion: Version,
            DetectionMode: DetectionMode.RuleOnly,
            PatternType: patternType,
            Subtype: subtype,
            Confidence: Math.Clamp(confidence, 0m, 1m),
            Session: session,
            TimeframePrimary: primaryTf,
            EntrySafety: entrySafety,
            WaterfallRisk: waterfallRisk,
            FailThreatened: failThreatened,
            RecommendedAction: action);
    }
}
