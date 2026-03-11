using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v11 §4 — Gold-specific Market Regime Detection Engine.
/// Primary regime states: RANGE, RANGE_RELOAD, TREND_CONTINUATION, FLUSH_CATCH,
/// EXPANSION, EXHAUSTION, LIQUIDATION, NEWS_SPIKE, SHOCK, DEAD/NO_EDGE.
/// Drives path preference, confidenceScore, sizeState, TP realism, expiry strictness.
/// </summary>
public static class GoldMarketRegimeDetector
{
    public const string Range = "RANGE";
    public const string RangeReload = "RANGE_RELOAD";
    public const string TrendContinuation = "TREND_CONTINUATION";
    public const string FlushCatch = "FLUSH_CATCH";
    public const string Expansion = "EXPANSION";
    public const string Exhaustion = "EXHAUSTION";
    public const string ContinuationRebuild = "CONTINUATION_REBUILD";
    public const string BullishRecovery = "BULLISH_RECOVERY";
    public const string Liquidation = "LIQUIDATION";
    public const string NewsSpike = "NEWS_SPIKE";
    public const string Shock = "SHOCK";
    public const string DeadNoEdge = "DEAD";

    /// <summary>
    /// Classify gold-specific regime from structural detector result + snapshot.
    /// Uses existing MarketRegimeDetector then refines to spec v11 taxonomy.
    /// </summary>
    public static GoldRegimeResult Detect(
        MarketRegimeResult structuralRegime,
        MarketSnapshotContract snapshot,
        SweepReclaimResult sweepReclaim,
        bool newsEventFlag)
    {
        if (!structuralRegime.IsTradeable)
        {
            if (string.Equals(structuralRegime.Regime, "DEAD", StringComparison.OrdinalIgnoreCase))
                return new GoldRegimeResult(DeadNoEdge, false, structuralRegime.Reason, "Stand down.");
            if (string.Equals(structuralRegime.Regime, "CHOPPY", StringComparison.OrdinalIgnoreCase))
            {
                // CR12 §9: Upgrade from EXHAUSTION to RANGE_RELOAD/CONTINUATION_REBUILD/BULLISH_RECOVERY when structure has repaired
                if (IsStructureRepaired(snapshot))
                    return new GoldRegimeResult(RangeReload, true, "Structure repaired; upgraded from EXHAUSTION.", "Bullish rebuild / range reload; rotation allowed.");
                return new GoldRegimeResult(Exhaustion, false, structuralRegime.Reason, "ADR/expansion exhausted.");
            }
            if (string.Equals(structuralRegime.Regime, "TRENDING_BEAR", StringComparison.OrdinalIgnoreCase))
                return new GoldRegimeResult(Liquidation, false, structuralRegime.Reason, "Buy-only: no catch without rebuild.");
        }

        if (newsEventFlag)
            return new GoldRegimeResult(NewsSpike, false, "High-impact news nearby.", "Event-driven distortion; breakout chasing blocked.");

        var close = snapshot.AuthoritativeRate > 0m ? snapshot.AuthoritativeRate : snapshot.Bid;
        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;
        var adrUsed = snapshot.AdrUsedPct;

        // EXHAUSTION: large prior move, weak follow-through — CR12 §9: upgrade when structure repaired
        if (adrUsed >= 85m && snapshot.CompressionCountM15 <= 1)
        {
            if (IsStructureRepaired(snapshot))
                return new GoldRegimeResult(BullishRecovery, true, "Structure repaired; upgraded from EXHAUSTION.", "Rebound/shelf rebuilding; re-qualified.");
            return new GoldRegimeResult(Exhaustion, false, $"ADR used {adrUsed:0}%.", "Wait for pullback or re-qualification.");
        }

        // FLUSH_CATCH: sharp dump into known liquidity, reclaim visible
        if (sweepReclaim.State == SweepReclaimState.SweepReclaim && structuralRegime.IsTradeable)
            return new GoldRegimeResult(FlushCatch, true, sweepReclaim.Reason, "BUY_LIMIT after reclaim/hold.");

        // RANGE_RELOAD: prior move happened, now reloading, shallow pullback into defended floor
        if (string.Equals(structuralRegime.Regime, "RANGING", StringComparison.OrdinalIgnoreCase)
            && snapshot.CompressionCountM15 >= 2
            && sweepReclaim.State != SweepReclaimState.None)
            return new GoldRegimeResult(RangeReload, true, structuralRegime.Reason, "One of the best physical-gold rotation regimes.");

        // TREND_CONTINUATION: H1 aligned, price respecting higher lows
        if (string.Equals(structuralRegime.Regime, "TRENDING_BULL", StringComparison.OrdinalIgnoreCase)
            && snapshot.CompressionCountM15 >= 2)
            return new GoldRegimeResult(TrendContinuation, true, structuralRegime.Reason, "BUY_STOP allowed if not overextended.");

        // EXPANSION: strong move already underway
        if (snapshot.ExpansionCountM15 >= 4 && snapshot.CompressionCountM15 <= 1)
            return new GoldRegimeResult(Expansion, false, "Strong move underway.", "Danger of late chase.");

        // RANGE: defended shelves both sides, rotation-friendly
        if (string.Equals(structuralRegime.Regime, "RANGING", StringComparison.OrdinalIgnoreCase))
            return new GoldRegimeResult(Range, true, structuralRegime.Reason, "BUY_LIMIT favored near shelves.");

        if (string.Equals(structuralRegime.Regime, "TRENDING_BULL", StringComparison.OrdinalIgnoreCase))
            return new GoldRegimeResult(TrendContinuation, true, structuralRegime.Reason, "H1 bullish, clean compression possible.");

        return new GoldRegimeResult(Range, structuralRegime.IsTradeable, structuralRegime.Reason, structuralRegime.Reason);
    }

    /// <summary>CR12 §9: Structure has materially repaired — H1 bullish, M15 base, no fail/waterfall/hazard.</summary>
    private static bool IsStructureRepaired(MarketSnapshotContract snapshot)
    {
        var h1 = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "H1", StringComparison.OrdinalIgnoreCase));
        var h1Close = h1?.Close ?? 0m;
        var h1Bullish = snapshot.Ma20H1 > 0m && h1Close > 0m && h1Close > snapshot.Ma20H1;
        var m15Base = snapshot.HasOverlapCandles && snapshot.CompressionCountM15 >= 2;
        var noFail = snapshot.AdrUsedPct <= 85m || snapshot.AdrUsedPct == 0m;
        var noWaterfall = !snapshot.HasPanicDropSequence && (!snapshot.IsExpansion || !snapshot.IsAtrExpanding);
        var noHazard = !snapshot.IsUsRiskWindow;
        return h1Bullish && m15Base && noFail && noWaterfall && noHazard;
    }
}

/// <summary>Spec v11 §4 — Gold-specific regime result.</summary>
public sealed record GoldRegimeResult(
    string Regime,
    bool IsTradeable,
    string Reason,
    string Interpretation);
