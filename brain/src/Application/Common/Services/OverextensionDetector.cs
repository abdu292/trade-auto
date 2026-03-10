using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v7 §5.1 — OverextensionDetector.
/// Inputs: MA20 distance (M15 + H1), RSI, candle range/ATR, distance from base.
/// Outputs: NORMAL / STRETCHED / OVEREXTENDED.
/// </summary>
public static class OverextensionDetector
{
    /// <summary>Detect overextension using snapshot and optional base level. If baseLevel is null, uses SessionLow as proxy for buy context.</summary>
    public static OverextensionResult Detect(
        MarketSnapshotContract snapshot,
        decimal? baseLevel,
        IGoldEngineThresholds? thresholds = null)
    {
        thresholds ??= new DefaultThresholds();

        var atrH1 = snapshot.AtrH1 > 0m ? snapshot.AtrH1 : snapshot.Atr;
        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;
        var close = snapshot.AuthoritativeRate > 0m ? snapshot.AuthoritativeRate : snapshot.Bid;
        if (close <= 0m)
            return new OverextensionResult(OverextensionState.Normal, 0m, 0m, 0m, 0m, 0m, 0m, "No price data.");

        // MA20 distance normalized by ATR (§6.1)
        var ma20H1 = snapshot.Ma20H1 > 0m ? snapshot.Ma20H1 : snapshot.Ma20;
        var ma20DistH1 = atrH1 > 0m && ma20H1 > 0m
            ? Math.Abs(close - ma20H1) / atrH1
            : 0m;

        var m15Candle = snapshot.TimeframeData?.FirstOrDefault(x =>
            string.Equals(x.Timeframe, "M15", StringComparison.OrdinalIgnoreCase));
        var ma20M15 = m15Candle?.Ma20Value ?? 0m;
        var ma20DistM15 = atrM15 > 0m && ma20M15 > 0m
            ? Math.Abs(close - ma20M15) / atrM15
            : 0m;

        // RSI
        var rsiM15 = snapshot.RsiM15;
        var rsiH1 = snapshot.RsiH1;

        // Candle range vs ATR
        var candleRange = m15Candle?.CandleRange ?? 0m;
        var candleRangeAtr = atrM15 > 0m && candleRange > 0m ? candleRange / atrM15 : 0m;

        // Distance from last valid base (§6.3)
        var baseL = baseLevel ?? snapshot.SessionLow;
        if (baseL <= 0m) baseL = snapshot.PreviousSessionLow;
        var baseDistAtr = atrM15 > 0m && baseL > 0m && close >= baseL
            ? (close - baseL) / atrM15
            : 0m;

        // H1 has higher priority than M15 for overextension
        string state;
        string reason;

        if (ma20DistH1 > thresholds.Ma20DistStretchedMax)
        {
            state = OverextensionState.Overextended;
            reason = $"H1 ma20DistATR={ma20DistH1:0.00} > {thresholds.Ma20DistStretchedMax} (EXTREME).";
        }
        else if (ma20DistH1 > thresholds.Ma20DistNormalMax || ma20DistM15 > thresholds.Ma20DistNormalMax)
        {
            state = OverextensionState.Stretched;
            reason = $"ma20Dist H1={ma20DistH1:0.00} M15={ma20DistM15:0.00} in STRETCHED band.";
        }
        else if (rsiM15 >= thresholds.RsiBuyLimitWaitHigh && ma20DistH1 >= 1.2m)
        {
            state = OverextensionState.Stretched;
            reason = $"RSI_M15={rsiM15:0.0} high and H1 ma20DistATR={ma20DistH1:0.00} ≥ 1.2 → STRETCHED.";
        }
        else if (baseDistAtr > thresholds.BaseDistAtrBuyLimitValidMax && baseL > 0m)
        {
            state = OverextensionState.Overextended;
            reason = $"baseDistATR={baseDistAtr:0.00} > {thresholds.BaseDistAtrBuyLimitValidMax} (above base).";
        }
        else
        {
            state = OverextensionState.Normal;
            reason = $"ma20Dist H1={ma20DistH1:0.00} M15={ma20DistM15:0.00} baseDistATR={baseDistAtr:0.00} within NORMAL.";
        }

        return new OverextensionResult(
            State: state,
            Ma20DistAtrH1: ma20DistH1,
            Ma20DistAtrM15: ma20DistM15,
            RsiM15: rsiM15,
            RsiH1: rsiH1,
            BaseDistAtr: baseDistAtr,
            CandleRangeAtr: candleRangeAtr,
            Reason: reason);
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
