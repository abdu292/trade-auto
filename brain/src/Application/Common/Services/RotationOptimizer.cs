using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Rotation Optimizer: decides how capital should be deployed across valid trade opportunities.
/// Modes: SINGLE_ENTRY | STAGGERED | BUY_STOP | STAND_DOWN.
/// PRETABLE BLOCK → NO_TRADE regardless of optimizer output.
/// </summary>
public static class RotationOptimizer
{
    // Staggered size splits (level fractions must sum to 1.0)
    // Primary split: 25 / 35 / 40  (deeper levels receive more size)
    private static readonly decimal[] StaggeredFractionsDefault = [0.25m, 0.35m, 0.40m];

    // Balanced split: 30 / 30 / 40
    private static readonly decimal[] StaggeredFractionsBalanced = [0.30m, 0.30m, 0.40m];

    // Stand-down threshold: ADR usage above this % means day is too consumed for new trades
    private const decimal StandDownAdrUsedPctThreshold = 90m;

    // Minimum PRETABLE size modifier for staggered mode (ensures capital can fill all levels)
    private const decimal MinPretableSizeModifierForStagger = 0.30m;

    // Spread instability multiplier for staggered mode gate
    private const decimal StaggerSpreadInstabilityMultiplier = 2.5m;

    // Minimum compression candles required for staggered ladder
    private const int MinCompressionCandlesForStagger = 3;

    // Minimum compression candles for buy-stop mode (compression before breakout)
    private const int MinCompressionCandlesForBuyStop = 3;

    // Weak breakout score threshold (below = fake/weak breakout)
    private const decimal WeakBreakoutImpulseScore = 0.5m;

    // Maximum expansion candles before blocking buy-stop
    private const int MaxExpansionCandlesForBuyStop = 3;

    /// <summary>Decides the optimal rotation mode for the current market setup.</summary>
    /// <param name="snapshot">Current market snapshot.</param>
    /// <param name="pretable">PRETABLE risk result.</param>
    /// <param name="liquiditySweep">Liquidity sweep detection result.</param>
    /// <param name="rotationRegime">CR11 regime string: TREND | RANGE | SHOCK.</param>
    /// <param name="ledger">Current ledger state.</param>
    /// <param name="microRotationMode">
    /// When true, activates MICRO_ROTATION_MODE (refinement spec §D):
    /// single pending trade, no staggered ladder, BUY_LIMIT / BUY_STOP only.
    /// Designed for safe live testing with small free balance.
    /// </param>
    public static RotationOptimizerResult Optimize(
        MarketSnapshotContract snapshot,
        PretableResult pretable,
        LiquiditySweepResult liquiditySweep,
        string rotationRegime,
        LedgerStateContract ledger,
        bool microRotationMode = false)
    {
        // BLOCK from PRETABLE → NO order regardless of optimizer
        if (pretable.RiskLevel == "BLOCK")
        {
            return new RotationOptimizerResult(
                Mode: "STAND_DOWN",
                RotationRegime: rotationRegime,
                StaggeredLevels: null,
                Reason: $"PRETABLE={pretable.RiskLevel}: stand-down enforced. {pretable.Reason}",
                EfficiencyState: EfficiencyStates.Low,
                EfficiencyScore: 0);
        }

        // ── §D MICRO_ROTATION_MODE: single pending trade, no ladder ──────────
        // Phase 1: single entry only, no staggered ladder, no stand-down from ADR.
        // All safety rules still apply; only ladder and breakout modes are restricted.
        if (microRotationMode)
        {
            var microEff = ComputeEfficiency(snapshot);
            // BUY_STOP still allowed in micro mode if breakout is justified and TREND regime
            if (rotationRegime == "TREND" && IsBuyStopJustified(snapshot))
            {
                return new RotationOptimizerResult(
                    Mode: "MICRO_ROTATION_MODE",
                    RotationRegime: rotationRegime,
                    StaggeredLevels: null,
                    Reason: "MICRO_ROTATION_MODE(BUY_STOP): TREND regime with justified breakout. Single entry only, no ladder.",
                    EfficiencyState: microEff.EfficiencyState,
                    EfficiencyScore: microEff.EfficiencyScore);
            }

            return new RotationOptimizerResult(
                Mode: "MICRO_ROTATION_MODE",
                RotationRegime: rotationRegime,
                StaggeredLevels: null,
                Reason: "MICRO_ROTATION_MODE(SINGLE_ENTRY): single pending trade, ladder disabled.",
                EfficiencyState: microEff.EfficiencyState,
                EfficiencyScore: microEff.EfficiencyScore);
        }

        // ── STAND_DOWN: poor capital efficiency conditions ───────────────────
        if (ShouldStandDown(snapshot, rotationRegime))
        {
            return new RotationOptimizerResult(
                Mode: "STAND_DOWN",
                RotationRegime: rotationRegime,
                StaggeredLevels: null,
                Reason: $"Stand-down: poor capital efficiency for {rotationRegime} regime. SameSessionTP probability too low or environment unclear.",
                EfficiencyState: EfficiencyStates.Low,
                EfficiencyScore: 0);
        }

        // ── BUY_STOP: continuation breakout in TREND regime ─────────────────
        if (rotationRegime == "TREND" && IsBuyStopJustified(snapshot))
        {
            var buyStopEff = ComputeEfficiency(snapshot);
            return new RotationOptimizerResult(
                Mode: "BUY_STOP",
                RotationRegime: rotationRegime,
                StaggeredLevels: null,
                Reason: $"BUY_STOP: TREND regime with confirmed continuation breakout and no impulse exhaustion.",
                EfficiencyState: buyStopEff.EfficiencyState,
                EfficiencyScore: buyStopEff.EfficiencyScore);
        }

        // ── STAGGERED: multiple real liquidity shelves ───────────────────────
        if (CanUseStaggeredMode(snapshot, pretable, ledger))
        {
            var usePrimary = !snapshot.IsCompression; // balanced splits during compression, else primary
            var fractions = usePrimary ? StaggeredFractionsDefault : StaggeredFractionsBalanced;

            var levels = BuildStaggeredLevels(snapshot, fractions);
            var staggerEff = ComputeEfficiency(snapshot);
            return new RotationOptimizerResult(
                Mode: "STAGGERED",
                RotationRegime: rotationRegime,
                StaggeredLevels: levels,
                Reason: $"STAGGERED: {levels.Count} levels over real liquidity shelves (split={string.Join("/", fractions.Select(f => $"{(int)(f * 100)}"))}).",
                EfficiencyState: staggerEff.EfficiencyState,
                EfficiencyScore: staggerEff.EfficiencyScore);
        }

        // ── SINGLE ENTRY: clean structure with one defended level ────────────
        var singleEff = ComputeEfficiency(snapshot);
        return new RotationOptimizerResult(
            Mode: "SINGLE_ENTRY",
            RotationRegime: rotationRegime,
            StaggeredLevels: null,
            Reason: $"SINGLE_ENTRY: clean structure with high single-touch probability. Sweep={liquiditySweep.IsConfirmed}, Compression={snapshot.IsCompression}.",
            EfficiencyState: singleEff.EfficiencyState,
            EfficiencyScore: singleEff.EfficiencyScore);
    }

    /// <summary>Compute efficiency score using current snapshot bid/ask and session context. Used when no specific entry/TP are known yet.</summary>
    private static RotationEfficiencyResult ComputeEfficiency(MarketSnapshotContract snapshot)
    {
        // Use snapshot mid-price and a realistic ATR-based TP estimate when no specific entry/TP known
        var entry = snapshot.Bid > 0m ? snapshot.Bid : snapshot.AuthoritativeRate;
        var atrH1 = snapshot.AtrH1 > 0m ? snapshot.AtrH1 : (snapshot.Atr > 0m ? snapshot.Atr : 3m);
        var tp = entry > 0m ? entry + atrH1 * 0.8m : 0m; // rough 0.8-ATR TP estimate
        var grams = 1m; // neutral grams for rate-of-return calculation
        return RotationEfficiencyEngine.Evaluate(snapshot, entry, tp, grams);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static bool ShouldStandDown(MarketSnapshotContract snapshot, string rotationRegime)
    {
        // SHOCK regime: most trades blocked
        if (rotationRegime == "SHOCK") return true;

        // Expiry realism too poor (ADR fully consumed — no room for TP within session)
        if (snapshot.AdrUsedPct >= StandDownAdrUsedPctThreshold) return true;

        // Structure unclear: no compression, no base, no sweep, no breakout
        var noStructure = !snapshot.IsCompression
            && !snapshot.HasOverlapCandles
            && !snapshot.HasLiquiditySweep
            && !snapshot.IsBreakoutConfirmed;
        if (noStructure) return true;

        // Very late in impulse with no reclaim (waterfall reopen risk elevated)
        var lateImpulseNoReclaim = snapshot.IsExpansion
            && snapshot.IsAtrExpanding
            && !snapshot.HasLiquiditySweep
            && snapshot.ImpulseStrengthScore >= 0.7m;
        if (lateImpulseNoReclaim) return true;

        return false;
    }

    private static bool IsBuyStopJustified(MarketSnapshotContract snapshot)
    {
        // Confirmed breakout above compression or resistance
        if (!snapshot.IsBreakoutConfirmed) return false;

        // Compression before breakout — price squeezed then broke
        if (snapshot.CompressionCountM15 < MinCompressionCandlesForBuyStop) return false;

        // Momentum expansion present (not a fake/weak breakout)
        if (!snapshot.IsExpansion && !snapshot.HasImpulseCandles) return false;

        // No impulse exhaustion (breakout not already overextended)
        if (snapshot.ExpansionCountM15 >= MaxExpansionCandlesForBuyStop && snapshot.ImpulseStrengthScore < WeakBreakoutImpulseScore) return false;

        // No liquidity vacuum (excessive spread spike)
        var vacuumRisk = snapshot.SpreadMax60m > 0m
            && snapshot.SpreadMedian60m > 0m
            && snapshot.SpreadMax60m >= snapshot.SpreadMedian60m * StaggerSpreadInstabilityMultiplier;
        if (vacuumRisk) return false;

        return true;
    }

    private static bool CanUseStaggeredMode(
        MarketSnapshotContract snapshot,
        PretableResult pretable,
        LedgerStateContract ledger)
    {
        // Absolute law: never stagger under HIGH waterfall risk
        // (PRETABLE BLOCK already handled above; check underlying conditions)
        if (snapshot.HasPanicDropSequence || snapshot.PanicSuspected) return false;

        // Liquidity vacuum = no staggering
        var vacuumRisk = snapshot.SpreadMax60m > 0m
            && snapshot.SpreadMedian60m > 0m
            && snapshot.SpreadMax60m >= snapshot.SpreadMedian60m * StaggerSpreadInstabilityMultiplier;
        if (vacuumRisk) return false;

        // No hazard window (proxied by US risk window)
        if (snapshot.IsUsRiskWindow) return false;

        // Friday overlap — too risky for ladder
        if (snapshot.IsFriday && snapshot.IsLondonNyOverlap) return false;

        // Structure must support multiple real shelves:
        // At least MinCompressionCandlesForStagger compression candles and a valid base
        if (snapshot.CompressionCountM15 < MinCompressionCandlesForStagger) return false;
        if (!snapshot.HasOverlapCandles) return false;

        // Sweep / reclaim / compression logic must still be valid
        if (!snapshot.HasLiquiditySweep && !snapshot.IsCompression) return false;

        // Capital gate: require minimum PRETABLE size modifier for ladder deployment
        if (pretable.SizeModifier < MinPretableSizeModifierForStagger) return false;

        return true;
    }

    private static IReadOnlyCollection<StaggeredLevelContract> BuildStaggeredLevels(
        MarketSnapshotContract snapshot,
        decimal[] fractions)
    {
        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;
        var baseBuffer = atrM15 > 0m ? atrM15 * 0.20m : 2.0m;

        var levels = new List<StaggeredLevelContract>();
        for (var i = 0; i < fractions.Length; i++)
        {
            // S1 = shallow defended base (0.20 ATR below current)
            // S2 = deeper sweep pocket  (0.60 ATR below)
            // S3 = final exhaustion/flush zone (1.20 ATR below)
            var depthMultiplier = i switch
            {
                0 => 0.20m,
                1 => 0.60m,
                _ => 1.20m,
            };

            var entryOffset = -(baseBuffer + atrM15 * depthMultiplier);
            var label = i switch
            {
                0 => "S1_SHALLOW_BASE",
                1 => "S2_SWEEP_POCKET",
                _ => "S3_EXHAUSTION_FLUSH",
            };

            levels.Add(new StaggeredLevelContract(
                LevelIndex: i + 1,
                EntryOffset: decimal.Round(entryOffset, 2),
                SizeFraction: fractions[i],
                Label: label));
        }

        return levels;
    }
}
