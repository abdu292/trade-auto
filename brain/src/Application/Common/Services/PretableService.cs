using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// PRETABLE Risk Intelligence.
/// Determines trade aggressiveness before execution.
///
/// Outputs:
///   RiskLevel:    SAFE | CAUTION | BLOCK
///   RiskScore:    0.0 – 1.0 (higher = riskier)
///   RiskFlags[]:  named risk contributors
///   Session:      current trading session
///   SizeModifier: multiplier applied to computed gram size
///
/// PRETABLE cannot override WATERFALL_RISK, FAIL laws, hazard windows,
/// exposure limits, capital limits, or panic interrupts.
///
/// Pattern Detector (CR8) feeds directly into PRETABLE (Section F of refinement spec):
///   WATERFALL_RISK HIGH or FAIL_THREATENED → BLOCK (hard gate)
///   CONTINUATION_BREAKOUT SAFE/LOW         → may upgrade CAUTION → SAFE
///   SESSION_TRANSITION_TRAP                → adds SESSION_TRANSITION flag
///
/// If LiquiditySweepResult.IsConfirmed the risk level may improve one tier:
///   CAUTION → SAFE  (but never BLOCK → SAFE/CAUTION)
/// </summary>
public static class PretableService
{
    // Risk score thresholds that determine SAFE / CAUTION / BLOCK classification
    private const decimal BlockRiskScoreThreshold  = 0.65m;
    private const decimal CautionRiskScoreThreshold = 0.30m;

    // Risk score contributions per risk factor
    private const decimal ImpulseExhaustionBlockContribution  = 0.40m;
    private const decimal ImpulseExhaustionCautionContribution = 0.20m;
    private const decimal SpreadInstabilityContribution        = 0.15m;
    private const decimal AdrExhaustedContribution             = 0.15m;
    private const decimal AdrHighUsageContribution             = 0.08m;
    private const decimal PanicContribution                    = 0.25m;
    private const decimal HighImpactNewsContribution           = 0.15m;
    private const decimal FridayOverlapContribution            = 0.10m;
    private const decimal AtrExpansionContribution             = 0.10m;

    // Pattern Detector gate contributions (Section F — Pattern Detector as Active Gate)
    private const decimal PatternSessionTransitionContribution = 0.12m;

    // ADR usage thresholds
    private const decimal AdrExhaustedPct  = 85m;
    private const decimal AdrHighUsagePct  = 70m;

    // Spread instability: spread max ≥ this multiple of median = unstable
    private const decimal SpreadInstabilityMultiplier = 2.0m;

    // Size modifiers per risk level
    private const decimal SafeSizeModifier    = 1.0m;
    private const decimal CautionSizeModifier = 0.60m;
    private const decimal BlockSizeModifier   = 0.0m;

    /// <summary>Evaluates PRETABLE risk given the current market context.</summary>
    /// <param name="snapshot">Current market snapshot.</param>
    /// <param name="regime">Regime classification from the regime classifier.</param>
    /// <param name="impulseExhaustion">Impulse exhaustion guard result.</param>
    /// <param name="liquiditySweep">Liquidity sweep detection result.</param>
    /// <param name="session">Normalized session name.</param>
    /// <param name="patterns">
    /// Optional Pattern Detector results (CR8 Section F).
    /// WATERFALL_RISK HIGH or FAIL_THREATENED → hard BLOCK.
    /// CONTINUATION_BREAKOUT SAFE/LOW         → eligible for CAUTION → SAFE upgrade.
    /// SESSION_TRANSITION_TRAP                → adds SESSION_TRANSITION risk flag.
    /// </param>
    public static PretableResult Evaluate(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        ImpulseExhaustionResult impulseExhaustion,
        LiquiditySweepResult liquiditySweep,
        string session,
        IReadOnlyCollection<PatternDetectionResult>? patterns = null)
    {
        var flags = new List<string>();
        var riskScore = 0m;

        // ── Hard BLOCK conditions (regime-level overrides) ──────────────────
        if (regime.IsBlocked || regime.IsWaterfall)
        {
            flags.Add("REGIME_BLOCK");
            return BuildResult("BLOCK", 1.0m, flags, session, BlockSizeModifier,
                $"Regime hard block: {regime.Reason}");
        }

        // ── Section F: Pattern Detector hard gates ───────────────────────────
        // WATERFALL_RISK pattern with HIGH risk or FAIL_THREATENED → PRETABLE = BLOCK
        // This is a hard gate: Pattern Detector actively feeds into PRETABLE (CR8 refinement spec §F).
        if (patterns is not null)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.PatternType == PatternType.WaterfallRisk
                    && (string.Equals(pattern.WaterfallRisk, "HIGH", StringComparison.OrdinalIgnoreCase)
                        || pattern.FailThreatened))
                {
                    flags.Add("PATTERN_WATERFALL_HIGH");
                    return BuildResult("BLOCK", 1.0m, flags, session, BlockSizeModifier,
                        $"Pattern hard block: WATERFALL_RISK={pattern.WaterfallRisk}, FailThreatened={pattern.FailThreatened}, subtype={pattern.Subtype}");
                }
            }
        }

        // ── Impulse Exhaustion contributes to score ─────────────────────────
        if (impulseExhaustion.Level == "BLOCK")
        {
            flags.Add("IMPULSE_EXHAUSTION_BLOCK");
            riskScore += ImpulseExhaustionBlockContribution;
        }
        else if (impulseExhaustion.Level == "CAUTION")
        {
            flags.Add("IMPULSE_EXHAUSTION_CAUTION");
            riskScore += ImpulseExhaustionCautionContribution;
        }

        // ── Volatility / spread risk ─────────────────────────────────────────
        var spreadInstability = snapshot.SpreadMedian60m > 0m
            && snapshot.SpreadMax60m > 0m
            && snapshot.SpreadMax60m >= snapshot.SpreadMedian60m * SpreadInstabilityMultiplier;
        if (spreadInstability)
        {
            flags.Add("SPREAD_INSTABILITY");
            riskScore += SpreadInstabilityContribution;
        }

        // ── ADR exhaustion ───────────────────────────────────────────────────
        if (snapshot.AdrUsedPct >= AdrExhaustedPct)
        {
            flags.Add("ADR_EXHAUSTED");
            riskScore += AdrExhaustedContribution;
        }
        else if (snapshot.AdrUsedPct >= AdrHighUsagePct)
        {
            flags.Add("ADR_HIGH_USAGE");
            riskScore += AdrHighUsageContribution;
        }

        // ── Panic / news ────────────────────────────────────────────────────
        if (snapshot.HasPanicDropSequence || snapshot.PanicSuspected)
        {
            flags.Add("PANIC_SUSPECTED");
            riskScore += PanicContribution;
        }

        if (string.Equals(snapshot.TelegramImpactTag, "HIGH", StringComparison.OrdinalIgnoreCase))
        {
            flags.Add("HIGH_IMPACT_NEWS");
            riskScore += HighImpactNewsContribution;
        }

        // ── Friday overlap risk ──────────────────────────────────────────────
        if (snapshot.IsFriday && snapshot.IsLondonNyOverlap)
        {
            flags.Add("FRIDAY_OVERLAP");
            riskScore += FridayOverlapContribution;
        }

        // ── ATR expansion ────────────────────────────────────────────────────
        if (snapshot.IsAtrExpanding && snapshot.IsExpansion)
        {
            flags.Add("ATR_EXPANSION");
            riskScore += AtrExpansionContribution;
        }

        // ── Section F: Pattern Detector soft contributions ───────────────────
        // SESSION_TRANSITION_TRAP → adds SESSION_TRANSITION risk flag.
        // CONTINUATION_BREAKOUT SAFE/LOW → tracked for potential CAUTION → SAFE upgrade.
        var hasBreakoutSafePattern = false;
        if (patterns is not null)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.PatternType == PatternType.SessionTransitionTrap
                    && !flags.Contains("SESSION_TRANSITION"))
                {
                    flags.Add("SESSION_TRANSITION");
                    riskScore += PatternSessionTransitionContribution;
                }

                if (pattern.PatternType == PatternType.ContinuationBreakout
                    && string.Equals(pattern.EntrySafety, "SAFE", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(pattern.WaterfallRisk, "LOW", StringComparison.OrdinalIgnoreCase)
                    && !pattern.FailThreatened)
                {
                    hasBreakoutSafePattern = true;
                }
            }
        }

        riskScore = Math.Clamp(riskScore, 0m, 1.0m);

        // ── Determine base risk level from score ─────────────────────────────
        string riskLevel;
        if (riskScore >= BlockRiskScoreThreshold || impulseExhaustion.Level == "BLOCK")
        {
            riskLevel = "BLOCK";
        }
        else if (riskScore >= CautionRiskScoreThreshold || impulseExhaustion.Level == "CAUTION")
        {
            riskLevel = "CAUTION";
        }
        else
        {
            riskLevel = "SAFE";
        }

        // ── Liquidity sweep upgrade: CAUTION → SAFE ──────────────────────────
        // A confirmed liquidity sweep indicates a controlled reversal entry.
        // It may improve risk by one tier — only from CAUTION to SAFE,
        // never from BLOCK to anything better.
        if (riskLevel == "CAUTION" && liquiditySweep.IsConfirmed)
        {
            flags.Add("SWEEP_CONFIRMED_UPGRADE");
            riskLevel = "SAFE";
        }

        // ── Section F: Continuation breakout upgrade: CAUTION → SAFE ─────────
        // CONTINUATION_BREAKOUT with SAFE entry + LOW waterfall + no FAIL threat
        // can upgrade CAUTION → SAFE (per refinement spec §F Breakout continuation).
        // Never upgrades from BLOCK.
        if (riskLevel == "CAUTION" && hasBreakoutSafePattern)
        {
            flags.Add("PATTERN_BREAKOUT_SAFE_UPGRADE");
            riskLevel = "SAFE";
        }

        // ── Compute size modifier ────────────────────────────────────────────
        var sizeModifier = riskLevel switch
        {
            "SAFE"    => SafeSizeModifier,
            "CAUTION" => CautionSizeModifier,
            _          => BlockSizeModifier,
        };

        var reason = $"PRETABLE {riskLevel}: score={riskScore:0.00}, flags=[{string.Join(",", flags)}], sweep={liquiditySweep.IsConfirmed}, impulse={impulseExhaustion.Level}, breakoutSafe={hasBreakoutSafePattern}";
        return BuildResult(riskLevel, riskScore, flags, session, sizeModifier, reason);
    }

    private static PretableResult BuildResult(
        string riskLevel,
        decimal riskScore,
        List<string> flags,
        string session,
        decimal sizeModifier,
        string reason) =>
        new(
            RiskLevel: riskLevel,
            RiskScore: riskScore,
            RiskFlags: flags,
            Session: session,
            SizeModifier: sizeModifier,
            Reason: reason);
}
