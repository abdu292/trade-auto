using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v11 §8 — Re-Qualification after overextension.
/// When pathState is OVEREXTENDED_ABOVE_BASE or WAIT_PULLBACK_BASE, the engine keeps memory
/// of the last valid candidate and checks whether price has cooled off into a fresh safe zone.
/// If re-qualification passes: candidateState = REQUALIFIED, BUY_LIMIT may be re-armed.
/// </summary>
public static class ReQualificationChecker
{
    /// <summary>
    /// Check if the market has cooled off enough to re-qualify a previously overextended or passed setup.
    /// </summary>
    public static ReQualificationResult Check(
        MarketSnapshotContract snapshot,
        ArmedSetupCandidate? lastCandidate,
        string pathState,
        OverextensionResult overextension,
        SweepReclaimResult sweepReclaim,
        string waterfallRisk,
        bool hazardWindowBlocked,
        decimal spreadBlockThreshold = 0.7m,
        IGoldEngineThresholds? thresholds = null)
    {
        thresholds ??= new DefaultThresholds();

        if (lastCandidate == null)
            return new ReQualificationResult(CanRequalify: false, Reason: "No prior candidate to re-qualify.");

        if (waterfallRisk == "HIGH" || hazardWindowBlocked || snapshot.Spread >= spreadBlockThreshold)
            return new ReQualificationResult(CanRequalify: false, Reason: "Safety block: waterfall, hazard, or spread.");

        // Only re-qualify from BUY_LIMIT path (pullback entries).
        if (!string.Equals(lastCandidate.PathType, PathState.BuyLimit, StringComparison.OrdinalIgnoreCase))
            return new ReQualificationResult(CanRequalify: false, Reason: "Re-qualification only for BUY_LIMIT path.");

        // MA20 distance normalizes (not overextended)
        if (string.Equals(overextension.State, OverextensionState.Overextended, StringComparison.OrdinalIgnoreCase))
            return new ReQualificationResult(CanRequalify: false, Reason: "MA20 distance still overextended.");

        // RSI cools from HIGH/EXTREME
        if (snapshot.RsiM15 >= thresholds.RsiHighBound || snapshot.RsiH1 >= thresholds.RsiHighBound)
            return new ReQualificationResult(CanRequalify: false, Reason: "RSI still elevated.");

        // Price returns near valid shelf/reclaim (within base-dist ATR)
        var baseLevel = lastCandidate.BaseLevel;
        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;
        var close = snapshot.AuthoritativeRate > 0m ? snapshot.AuthoritativeRate : snapshot.Bid;
        var baseDistAtr = atrM15 > 0m && baseLevel > 0m && close >= baseLevel
            ? (close - baseLevel) / atrM15
            : 999m;
        if (baseDistAtr > thresholds.BaseDistAtrBuyLimitRearmMax)
            return new ReQualificationResult(CanRequalify: false, Reason: $"Price not near shelf: baseDistAtr={baseDistAtr:0.2}.");

        // M15 structure still acceptable (sweep-reclaim or base present)
        if (sweepReclaim.State != SweepReclaimState.SweepReclaim && sweepReclaim.State != SweepReclaimState.SweepOnly)
        {
            // Allow if we have at least some structure; strict reclaim not required for re-qual
            if (baseDistAtr > thresholds.BaseDistAtrBuyLimitValidMax)
                return new ReQualificationResult(CanRequalify: false, Reason: "M15 structure not rebuilt near shelf.");
        }

        return new ReQualificationResult(CanRequalify: true, Reason: "MA20 normalized, RSI cooled, price near shelf, no safety block.");
    }

    private sealed class DefaultThresholds : IGoldEngineThresholds
    {
        public decimal Ma20DistNormalMax => 0.8m;
        public decimal Ma20DistStretchedMax => 1.5m;
        public decimal RsiLowBound => 35m;
        public decimal RsiMidLow => 35m;
        public decimal RsiMidHigh => 65m;
        public decimal RsiHighBound => 75m;
        public decimal RsiExtremeBound => 75m;
        public decimal RsiBuyLimitCautionHigh => 72m;
        public decimal RsiBuyLimitWaitHigh => 75m;
        public decimal BaseDistAtrBuyLimitValidMax => 1.0m;
        public decimal BaseDistAtrBuyLimitRearmMax => 0.6m;
        public decimal AdrUsedFullBound => 0.9m;
        public decimal AdrUsedBlockContinuationBuyStopMin => 1.0m;
        public decimal VciCompressedMax => 0.7m;
        public decimal VciNormalMax => 1.3m;
        public decimal SpreadCaution => 0.5m;
        public decimal SpreadBlock => 0.7m;
        public decimal TpDistanceSpreadMinRatio => 3m;
        public decimal SessionSizeJapan => 0.5m;
        public decimal SessionSizeIndia => 0.7m;
        public decimal SessionSizeLondon => 1.0m;
        public decimal SessionSizeNy => 0.6m;
        public (int Min, int Max) ExpiryJapan => (90, 120);
        public (int Min, int Max) ExpiryIndia => (90, 150);
        public (int Min, int Max) ExpiryLondon => (60, 90);
        public (int Min, int Max) ExpiryNy => (45, 60);
        public int ConfidenceWaitMax => 59;
        public int ConfidenceMicroMin => 60;
        public int ConfidenceMicroMax => 74;
        public int ConfidenceNormalMin => 75;
        public int ConfidenceHighMin => 90;
    }
}

/// <summary>Spec v11 §8 — Result of re-qualification check.</summary>
public sealed record ReQualificationResult(bool CanRequalify, string Reason);
