using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v7 §7 — Confidence score 0–100. Weights: H1 context +15, M15 base/lid +15,
/// sweep+reclaim +15, volatility fit +10, RSI+MA not stretched +10, session fit +10,
/// ADR not overused +10, spread normal +5, no FAIL/hazard/waterfall +10.
/// Thresholds: &lt;60 WAIT, 60–74 MICRO, 75–89 normal, ≥90 high.
/// </summary>
public static class ConfidenceScoreCalculator
{
    public static ConfidenceScoreResult Calculate(
        MarketSnapshotContract snapshot,
        H1ContextResult h1Context,
        M15SetupResult? m15Setup,
        SweepReclaimResult sweepReclaim,
        OverextensionResult overextension,
        string session,
        string sessionPhase,
        string waterfallRisk,
        bool newsBlock,
        IGoldEngineThresholds? thresholds = null)
    {
        thresholds ??= new ConfidenceDefaultThresholds();

        var h1Points = H1ContextAligned(h1Context);
        var m15Points = M15BaseLidValid(m15Setup);
        var sweepPoints = SweepReclaimPoints(sweepReclaim);
        var volPoints = VolatilityFitPoints(snapshot, thresholds);
        var stretchPoints = RsiMaNotStretched(overextension, snapshot, thresholds);
        var sessionPoints = SessionFitPoints(session, sessionPhase);
        var adrPoints = AdrNotOverused(snapshot, thresholds);
        var spreadPoints = SpreadNormal(snapshot, thresholds);
        var safetyPoints = NoFailHazardWaterfall(waterfallRisk, newsBlock);

        var total = h1Points + m15Points + sweepPoints + volPoints + stretchPoints
            + sessionPoints + adrPoints + spreadPoints + safetyPoints;
        total = Math.Clamp(total, 0, 100);

        var tier = total >= thresholds.ConfidenceHighMin ? "HIGH"
            : total >= thresholds.ConfidenceNormalMin ? "NORMAL"
            : total >= thresholds.ConfidenceMicroMin ? "MICRO"
            : "WAIT";

        var reason = $"Score={total} (H1={h1Points} M15={m15Points} sweep={sweepPoints} vol={volPoints} stretch={stretchPoints} session={sessionPoints} ADR={adrPoints} spread={spreadPoints} safety={safetyPoints}) → {tier}";

        return new ConfidenceScoreResult(
            Score: total,
            Tier: tier,
            H1ContextPoints: h1Points,
            M15StructurePoints: m15Points,
            SweepReclaimPoints: sweepPoints,
            VolatilityFitPoints: volPoints,
            StretchPoints: stretchPoints,
            SessionFitPoints: sessionPoints,
            AdrPoints: adrPoints,
            SpreadPoints: spreadPoints,
            SafetyPoints: safetyPoints,
            Reason: reason);
    }

    private static int H1ContextAligned(H1ContextResult h1)
    {
        if (h1?.Context == "BULLISH") return 15;
        if (h1?.Context == "BEARISH") return 0; // buy-only
        return 0;
    }

    private static int M15BaseLidValid(M15SetupResult? m15)
    {
        if (m15?.IsValid == true) return 15;
        return 0;
    }

    private static int SweepReclaimPoints(SweepReclaimResult sweep)
    {
        return sweep.State == SweepReclaimState.SweepReclaim ? 15 : (sweep.State == SweepReclaimState.SweepOnly ? 5 : 0);
    }

    private static int VolatilityFitPoints(MarketSnapshotContract s, IGoldEngineThresholds t)
    {
        if (s.Spread >= t.SpreadBlock) return 0;
        var vci = ComputeVci(s);
        if (vci > 1.3m) return 2;
        if (vci >= 0.7m && vci <= 1.3m) return 10;
        return 6;
    }

    private static decimal ComputeVci(MarketSnapshotContract s)
    {
        var ranges = s.CompressionRangesM15;
        if (ranges is null || ranges.Count < 50) return 1m;
        var last10 = ranges.TakeLast(10).Average();
        var last50 = ranges.TakeLast(50).Average();
        return last50 > 0m ? last10 / last50 : 1m;
    }

    private static int RsiMaNotStretched(OverextensionResult o, MarketSnapshotContract s, IGoldEngineThresholds t)
    {
        if (o.State == OverextensionState.Overextended) return 0;
        if (o.State == OverextensionState.Stretched) return 4;
        if (s.RsiM15 >= t.RsiBuyLimitWaitHigh) return 2;
        return 10;
    }

    private static int SessionFitPoints(string session, string phase)
    {
        if (session is "LONDON" or "NY") return 10;
        if (session is "INDIA" or "JAPAN") return 6;
        return 2;
    }

    private static int AdrNotOverused(MarketSnapshotContract s, IGoldEngineThresholds t)
    {
        var adr = s.AdrUsedPct;
        if (adr > 1.0m) return 0;
        if (adr <= 0.9m) return 10;
        return 5;
    }

    private static int SpreadNormal(MarketSnapshotContract s, IGoldEngineThresholds t)
    {
        if (s.Spread >= t.SpreadBlock) return 0;
        if (s.Spread >= t.SpreadCaution) return 2;
        return 5;
    }

    private static int NoFailHazardWaterfall(string waterfallRisk, bool newsBlock)
    {
        if (newsBlock || waterfallRisk == "HIGH") return 0;
        if (waterfallRisk == "MEDIUM") return 5;
        return 10;
    }

    private sealed class ConfidenceDefaultThresholds : IGoldEngineThresholds
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
