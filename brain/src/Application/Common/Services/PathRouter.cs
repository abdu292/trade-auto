using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v7 §2 and §8 — Path routing. Always classify one of:
/// BUY_LIMIT / BUY_STOP / WAIT_PULLBACK / OVEREXTENDED / STAND_DOWN.
/// No generic "M5 not confirmed = abort"; use explicit reason codes.
/// </summary>
public static class PathRouter
{
    /// <summary>
    /// Route path from: legality, overextension, structure (base/lid), sweep/reclaim,
    /// regime, M5 readiness (for BUY_STOP), and snapshot.
    /// </summary>
    public static PathRoutingResult Route(
        string legalityState,
        OverextensionResult overextension,
        SweepReclaimResult sweepReclaim,
        MarketRegimeResult marketRegime,
        bool hasValidBaseOrReclaim,
        bool hasValidLidAndCompression,
        bool m5EntryValid,
        MarketSnapshotContract snapshot,
        IGoldEngineThresholds? thresholds = null)
    {
        thresholds ??= new PathRouterDefaultThresholds();

        // 1. If legality = BLOCK → STAND_DOWN
        if (string.Equals(legalityState, LegalityState.Block, StringComparison.OrdinalIgnoreCase))
        {
            return new PathRoutingResult(
                PathState.StandDown,
                WaitReasonCode.HighWaterfallBlock,
                null,
                null);
        }

        // 2. OverextensionDetector: if OVEREXTENDED → WAIT_PULLBACK / OVEREXTENDED
        if (string.Equals(overextension.State, OverextensionState.Overextended, StringComparison.OrdinalIgnoreCase))
        {
            return new PathRoutingResult(
                PathState.Overextended,
                overextension.BaseDistAtr > thresholds.BaseDistAtrBuyLimitValidMax
                    ? WaitReasonCode.OverextendedAboveBase
                    : WaitReasonCode.WaitPullbackBase,
                null,
                null);
        }

        if (overextension.State == OverextensionState.Stretched && !hasValidBaseOrReclaim)
        {
            return new PathRoutingResult(
                PathState.WaitPullback,
                WaitReasonCode.WaitPullbackBase,
                null,
                null);
        }

        // 3. Structure: valid base/reclaim/sweep near price → BUY_LIMIT candidate
        var baseLevel = snapshot.SessionLow > 0m ? snapshot.SessionLow : snapshot.PreviousSessionLow;
        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;
        var close = snapshot.AuthoritativeRate > 0m ? snapshot.AuthoritativeRate : snapshot.Bid;
        var baseDistAtr = atrM15 > 0m && baseLevel > 0m && close >= baseLevel
            ? (close - baseLevel) / atrM15
            : 999m;

        if (hasValidBaseOrReclaim && baseDistAtr <= thresholds.BaseDistAtrBuyLimitValidMax
            && marketRegime.IsTradeable)
        {
            var s1 = baseLevel;
            decimal? s2 = null;
            decimal? s3 = null;
            if (snapshot.PreviousSessionLow > 0m && snapshot.PreviousSessionLow < baseLevel)
                s2 = snapshot.PreviousSessionLow;
            if (snapshot.PreviousDayLow > 0m && snapshot.PreviousDayLow < (s2 ?? baseLevel))
                s3 = snapshot.PreviousDayLow;

            return new PathRoutingResult(
                PathState.BuyLimit,
                null,
                new PendingLimitPathContract(S1BaseShelf: s1, S2SweepPocket: s2, S3ExhaustionPocket: s3),
                "BUY_LIMIT");
        }

        // 4. Valid lid + compression → BUY_STOP candidate (M5 mandatory)
        if (hasValidLidAndCompression && marketRegime.IsTradeable)
        {
            if (!m5EntryValid)
            {
                return new PathRoutingResult(
                    PathState.BuyStop,
                    WaitReasonCode.BuyStopBreakoutNotReady,
                    null,
                    "BUY_STOP");
            }
            return new PathRoutingResult(
                PathState.BuyStop,
                null,
                null,
                "BUY_STOP");
        }

        // 5. Neither base nor lid → WAIT
        return new PathRoutingResult(
            PathState.WaitPullback,
            WaitReasonCode.RangeNoStructure,
            null,
            null);
    }

    private sealed class PathRouterDefaultThresholds : IGoldEngineThresholds
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
        public decimal BaseDistAtrBuyLimitRearmMax => 0.4m;
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
