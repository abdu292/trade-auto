using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v7 §1 — Mandatory decision stack order. Do not let M5 abort before path is known.
/// 1. Ledger/capital (caller provides) 2. Session+phase 3. Macro/risk 4. Regime 5. Structure
/// 6. Overextension 7. Path routing 8. PRETABLE/legality/confidence 9. Size/TP/expiry 10. Order gen 11. UI.
/// This class runs steps 2–7 and returns whether to proceed to AI (step 8+) and path/reason.
/// </summary>
public static class GoldEngineDecisionStack
{
    /// <summary>
    /// Run decision stack up to path routing. Returns result with ProceedToAi = true only when
    /// path is BUY_LIMIT (M5 optional) or BUY_STOP with M5+impulse confirmed.
    /// </summary>
    public static GoldEngineDecisionStackResult Evaluate(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regimeClassification,
        string waterfallRisk,
        IGoldEngineThresholds? thresholds = null)
    {
        thresholds ??= new DefaultThresholds();

        // 1. Regime (layer 0)
        var marketRegime = MarketRegimeDetector.Detect(snapshot);
        if (!marketRegime.IsTradeable)
        {
            var overext = OverextensionDetector.Detect(snapshot, null, thresholds);
            var sweep = LiquiditySweepDetectorService.DetectSweepReclaim(snapshot);
            return NoProceed($"Regime not tradeable: {marketRegime.Regime}", marketRegime, null, null, null, null, overext, sweep, PathState.StandDown, WaitReasonCode.RangeNoStructure, LegalityState.Block, null);
        }

        // 2. H1 context
        var h1 = RuleEngine.EvaluateH1Context(snapshot);
        if (h1.Context == "NEUTRAL")
        {
            var overext = OverextensionDetector.Detect(snapshot, null, thresholds);
            var sweep = LiquiditySweepDetectorService.DetectSweepReclaim(snapshot);
            return NoProceed($"H1 neutral: {h1.Reason}", marketRegime, h1, null, null, null, overext, sweep, PathState.StandDown, null, LegalityState.Block, null);
        }

        // 3. M15 setup
        var m15 = RuleEngine.EvaluateM15Setup(snapshot);
        if (!m15.IsValid)
        {
            var overext = OverextensionDetector.Detect(snapshot, null, thresholds);
            var sweep = LiquiditySweepDetectorService.DetectSweepReclaim(snapshot);
            return NoProceed($"M15 invalid: {m15.Reason}", marketRegime, h1, m15, null, null, overext, sweep, PathState.WaitPullback, WaitReasonCode.RangeNoStructure, LegalityState.Block, null);
        }

        // 4. Overextension
        var baseLevel = snapshot.SessionLow > 0m ? snapshot.SessionLow : snapshot.PreviousSessionLow;
        var overextension = OverextensionDetector.Detect(snapshot, baseLevel, thresholds);

        // 5. Sweep + reclaim
        var sweepReclaim = LiquiditySweepDetectorService.DetectSweepReclaim(snapshot);

        // 6. M5 + Impulse (needed for path routing when BUY_STOP candidate)
        var m5 = RuleEngine.EvaluateM5Entry(snapshot);
        var impulse = RuleEngine.EvaluateImpulse(snapshot);
        var m5Valid = m5.IsValid && impulse.IsConfirmed;

        // 7. Legality (simple: regime block, spread block, waterfall)
        var legalityState = regimeClassification.IsBlocked || snapshot.Spread >= thresholds.SpreadBlock || waterfallRisk == "HIGH"
            ? LegalityState.Block
            : (snapshot.Spread >= thresholds.SpreadCaution || waterfallRisk == "MEDIUM" ? LegalityState.Caution : LegalityState.Legal);

        // 8. Path routing
        var hasValidBaseOrReclaim = m15.HasBase || m15.IsCompression || sweepReclaim.State == SweepReclaimState.SweepReclaim;
        var hasValidLidAndCompression = m15.IsCompression;
        var pathRouting = PathRouter.Route(
            legalityState,
            overextension,
            sweepReclaim,
            marketRegime,
            hasValidBaseOrReclaim,
            hasValidLidAndCompression,
            m5Valid,
            snapshot,
            thresholds);

        var pathState = pathRouting.PathState;
        var reasonCode = pathRouting.ReasonCode;

        // Proceed to AI only when: BUY_LIMIT (M5 optional) or BUY_STOP with M5 ready
        var proceedToAi = (pathState == PathState.BuyLimit)
            || (pathState == PathState.BuyStop && reasonCode != WaitReasonCode.BuyStopBreakoutNotReady);

        var engineStates = FactorEngine.Aggregate(
            snapshot,
            overextension,
            sweepReclaim,
            regimeClassification,
            pathState,
            waterfallRisk,
            null);

        return new GoldEngineDecisionStackResult(
            ProceedToAi: proceedToAi,
            PathState: pathState,
            ReasonCode: reasonCode,
            MarketRegime: marketRegime,
            H1Context: h1,
            M15Setup: m15,
            M5Entry: m5,
            ImpulseConfirmation: impulse,
            Overextension: overextension,
            SweepReclaim: sweepReclaim,
            PathRouting: pathRouting,
            LegalityState: legalityState,
            EngineStates: engineStates);
    }

    private static GoldEngineDecisionStackResult NoProceed(
        string _,
        MarketRegimeResult marketRegime,
        H1ContextResult? h1,
        M15SetupResult? m15,
        M5EntryResult? m5,
        ImpulseConfirmationResult? impulse,
        OverextensionResult overext,
        SweepReclaimResult sweep,
        string pathState,
        string? reasonCode,
        string legalityState,
        EngineStatesContract? engineStates)
    {
        var pathRouting = new PathRoutingResult(pathState, reasonCode, null, null);
        return new GoldEngineDecisionStackResult(
            ProceedToAi: false,
            pathState,
            reasonCode,
            marketRegime,
            h1 ?? new H1ContextResult("SKIPPED", false, false, false, "Aborted"),
            m15 ?? new M15SetupResult(false, false, false, "Aborted"),
            m5,
            impulse,
            overext,
            sweep,
            pathRouting,
            legalityState,
            engineStates);
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
