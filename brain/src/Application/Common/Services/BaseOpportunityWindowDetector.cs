using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v11 §7 — Base-Opportunity Window Detector.
/// Activates when a valid shelf/defended base exists and price is still near it,
/// so the engine can create early BUY_LIMIT candidates before the rate runs away.
/// Prevents "valid shelf forms → system waits too long → move expands → OVEREXTENDED" failure mode.
/// </summary>
public static class BaseOpportunityWindowDetector
{
    /// <summary>
    /// Detect whether we are in a base-opportunity window: early shelf/reclaim opportunity before expansion.
    /// Does not place mid-air buys; only indicates that early candidate creation is allowed at real structural shelves.
    /// </summary>
    public static BaseOpportunityResult Detect(
        MarketSnapshotContract snapshot,
        MarketRegimeResult marketRegime,
        H1ContextResult h1,
        M15SetupResult m15,
        SweepReclaimResult sweepReclaim,
        string waterfallRisk,
        bool hazardWindowBlocked,
        decimal spreadBlockThreshold = 0.7m,
        decimal baseDistAtrMax = 0.6m)
    {
        if (waterfallRisk == "HIGH" || hazardWindowBlocked || snapshot.Spread >= spreadBlockThreshold)
            return new BaseOpportunityResult(IsActive: false, Reason: "FAIL/hazard/waterfall/spread block.");

        if (h1.Context == "NEUTRAL")
            return new BaseOpportunityResult(IsActive: false, Reason: "H1 context not acceptable.");

        bool hasShelfOrBase = m15.HasBase || m15.IsCompression || sweepReclaim.State == SweepReclaimState.SweepReclaim;
        if (!hasShelfOrBase)
            return new BaseOpportunityResult(IsActive: false, Reason: "M15 has no valid shelf/defended base.");

        // Regime: RANGE, RANGE_RELOAD, FLUSH_CATCH recovery, or cooled continuation
        bool regimeOk = marketRegime.IsTradeable
            && (string.Equals(marketRegime.Regime, "RANGING", StringComparison.OrdinalIgnoreCase)
                || string.Equals(marketRegime.Regime, "RANGE_RELOAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(marketRegime.Regime, "TRENDING_BULL", StringComparison.OrdinalIgnoreCase));

        if (!regimeOk)
            return new BaseOpportunityResult(IsActive: false, Reason: $"Regime {marketRegime.Regime} not in RANGE/RANGE_RELOAD/TREND.");

        var baseLevel = snapshot.SessionLow > 0m ? snapshot.SessionLow : snapshot.PreviousSessionLow;
        if (baseLevel <= 0m)
            return new BaseOpportunityResult(IsActive: false, Reason: "No base level available.");

        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;
        var close = snapshot.AuthoritativeRate > 0m ? snapshot.AuthoritativeRate : snapshot.Bid;
        var baseDistAtr = atrM15 > 0m && close >= baseLevel ? (close - baseLevel) / atrM15 : 999m;

        if (baseDistAtr > baseDistAtrMax)
            return new BaseOpportunityResult(IsActive: false, Reason: $"Price not near shelf: baseDistAtr={baseDistAtr:0.2} (max {baseDistAtrMax}).");

        return new BaseOpportunityResult(IsActive: true, Reason: "H1 ok, M15 shelf/base, price near shelf, regime and safety pass.");
    }
}

/// <summary>Spec v11 §7 — Base-opportunity window detection result.</summary>
public sealed record BaseOpportunityResult(bool IsActive, string Reason);
