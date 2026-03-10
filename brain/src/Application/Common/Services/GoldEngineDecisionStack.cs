using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v7 §1 — Mandatory decision stack order. Do not let M5 abort before path is known.
/// 1. Ledger/capital (caller provides) 2. Session+phase 3. Macro/risk 4. Regime 5. Structure
/// 6. Overextension 7. Path routing 8. PRETABLE/legality/confidence 9. Size/TP/expiry 10. Order gen 11. UI.
/// This class runs steps 2–8 and returns whether to proceed to AI (step 9+) and path/reason.
/// </summary>
public static class GoldEngineDecisionStack
{
    /// <summary>
    /// Run decision stack up to confidence gating. Returns result with ProceedToAi = true only when
    /// path is BUY_LIMIT (M5 optional) or BUY_STOP with M5+impulse confirmed and confidence >= MICRO.
    /// </summary>
    public static GoldEngineDecisionStackResult Evaluate(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regimeClassification,
        string waterfallRisk,
        LedgerStateContract? ledgerState = null,
        bool hazardWindowBlocked = false,
        IGoldEngineThresholds? thresholds = null)
    {
        thresholds ??= new DefaultThresholds();
        var (session, phase) = TradingSessionClock.Resolve(snapshot.KsaTime);
        var newsBlock = snapshot.NewsEventFlag || snapshot.IsUsRiskWindow || hazardWindowBlocked;

        if (ledgerState is not null && (ledgerState.DeployableCashAed <= 0m || ledgerState.OpenExposurePercent >= 65m))
        {
            var capitalOverext = OverextensionDetector.Detect(snapshot, null, thresholds);
            var capitalSweep = LiquiditySweepDetectorService.DetectSweepReclaim(snapshot);
            return NoProceed(
                snapshot,
                regimeClassification,
                waterfallRisk,
                ledgerState,
                hazardWindowBlocked,
                MarketRegimeDetector.Detect(snapshot),
                null,
                null,
                null,
                null,
                capitalOverext,
                capitalSweep,
                PathState.StandDown,
                WaitReasonCode.CapitalLawBlock,
                LegalityState.Block,
                BuildWaitConfidence("Capital or exposure laws blocked the cycle."));
        }

        var marketRegime = MarketRegimeDetector.Detect(snapshot);
        if (!marketRegime.IsTradeable)
        {
            var overext = OverextensionDetector.Detect(snapshot, null, thresholds);
            var sweep = LiquiditySweepDetectorService.DetectSweepReclaim(snapshot);
            return NoProceed(
                snapshot,
                regimeClassification,
                waterfallRisk,
                ledgerState,
                hazardWindowBlocked,
                marketRegime,
                null,
                null,
                null,
                null,
                overext,
                sweep,
                PathState.StandDown,
                WaitReasonCode.RangeNoStructure,
                LegalityState.Block,
                BuildWaitConfidence($"Regime not tradeable: {marketRegime.Regime}"));
        }

        var h1 = RuleEngine.EvaluateH1Context(snapshot);
        if (h1.Context == "NEUTRAL")
        {
            var overext = OverextensionDetector.Detect(snapshot, null, thresholds);
            var sweep = LiquiditySweepDetectorService.DetectSweepReclaim(snapshot);
            return NoProceed(
                snapshot,
                regimeClassification,
                waterfallRisk,
                ledgerState,
                hazardWindowBlocked,
                marketRegime,
                h1,
                null,
                null,
                null,
                overext,
                sweep,
                PathState.StandDown,
                null,
                LegalityState.Block,
                BuildWaitConfidence($"H1 neutral: {h1.Reason}"));
        }

        var m15 = RuleEngine.EvaluateM15Setup(snapshot);
        if (!m15.IsValid)
        {
            var overext = OverextensionDetector.Detect(snapshot, null, thresholds);
            var sweep = LiquiditySweepDetectorService.DetectSweepReclaim(snapshot);
            return NoProceed(
                snapshot,
                regimeClassification,
                waterfallRisk,
                ledgerState,
                hazardWindowBlocked,
                marketRegime,
                h1,
                m15,
                null,
                null,
                overext,
                sweep,
                PathState.WaitPullback,
                WaitReasonCode.RangeNoStructure,
                LegalityState.Block,
                BuildWaitConfidence($"M15 invalid: {m15.Reason}"));
        }

        var baseLevel = snapshot.SessionLow > 0m ? snapshot.SessionLow : snapshot.PreviousSessionLow;
        var overextension = OverextensionDetector.Detect(snapshot, baseLevel, thresholds);
        var sweepReclaim = LiquiditySweepDetectorService.DetectSweepReclaim(snapshot);

        var m5 = RuleEngine.EvaluateM5Entry(snapshot);
        var impulse = RuleEngine.EvaluateImpulse(snapshot);
        var m5Valid = m5.IsValid && impulse.IsConfirmed;

        var legalityState = LegalityState.Legal;
        string? legalityReasonCode = null;
        if (hazardWindowBlocked)
        {
            legalityState = LegalityState.Block;
            legalityReasonCode = WaitReasonCode.HazardWindowBlock;
        }
        else if (regimeClassification.IsBlocked || waterfallRisk == "HIGH")
        {
            legalityState = LegalityState.Block;
            legalityReasonCode = WaitReasonCode.HighWaterfallBlock;
        }
        else if (snapshot.Spread >= thresholds.SpreadBlock)
        {
            legalityState = LegalityState.Block;
            legalityReasonCode = WaitReasonCode.SpreadGuardBlock;
        }
        else if (snapshot.Spread >= thresholds.SpreadCaution || waterfallRisk == "MEDIUM")
        {
            legalityState = LegalityState.Caution;
        }

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
            legalityReasonCode,
            thresholds);

        var pathState = pathRouting.PathState;
        var reasonCode = pathRouting.ReasonCode;

        var confidence = ConfidenceScoreCalculator.Calculate(
            snapshot,
            h1,
            m15,
            sweepReclaim,
            overextension,
            session,
            phase,
            waterfallRisk,
            newsBlock,
            thresholds);

        if (confidence.Tier == "WAIT" && pathState is PathState.BuyLimit or PathState.BuyStop)
        {
            pathState = overextension.State == OverextensionState.Overextended ? PathState.Overextended : PathState.WaitPullback;
            reasonCode ??= pathState == PathState.Overextended
                ? WaitReasonCode.OverextendedAboveBase
                : WaitReasonCode.WaitPullbackBase;
            pathRouting = pathRouting with { PathState = pathState, ReasonCode = reasonCode };
        }

        var proceedToAi = (pathState == PathState.BuyLimit)
            || (pathState == PathState.BuyStop && reasonCode != WaitReasonCode.BuyStopBreakoutNotReady);

        var engineStates = FactorEngine.Aggregate(
            snapshot,
            overextension,
            sweepReclaim,
            regimeClassification,
            pathState,
            waterfallRisk,
            ledgerState,
            null,
            null,
            confidence,
            hazardWindowBlocked,
            reasonCode);

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
            ConfidenceScore: confidence,
            EngineStates: engineStates);
    }

    private static GoldEngineDecisionStackResult NoProceed(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regimeClassification,
        string waterfallRisk,
        LedgerStateContract? ledgerState,
        bool hazardWindowBlocked,
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
        ConfidenceScoreResult confidence)
    {
        var pathRouting = new PathRoutingResult(pathState, reasonCode, null, null);
        var engineStates = FactorEngine.Aggregate(
            snapshot,
            overext,
            sweep,
            regimeClassification,
            pathState,
            waterfallRisk,
            ledgerState,
            null,
            null,
            confidence,
            hazardWindowBlocked,
            reasonCode);

        return new GoldEngineDecisionStackResult(
            ProceedToAi: false,
            PathState: pathState,
            ReasonCode: reasonCode,
            MarketRegime: marketRegime,
            H1Context: h1 ?? new H1ContextResult("SKIPPED", false, false, false, "Aborted"),
            M15Setup: m15 ?? new M15SetupResult(false, false, false, "Aborted"),
            M5Entry: m5,
            ImpulseConfirmation: impulse,
            Overextension: overext,
            SweepReclaim: sweep,
            PathRouting: pathRouting,
            LegalityState: legalityState,
            ConfidenceScore: confidence,
            EngineStates: engineStates);
    }

    private static ConfidenceScoreResult BuildWaitConfidence(string reason)
        => new(
            Score: 0,
            Tier: "WAIT",
            H1ContextPoints: 0,
            M15StructurePoints: 0,
            SweepReclaimPoints: 0,
            VolatilityFitPoints: 0,
            StretchPoints: 0,
            SessionFitPoints: 0,
            AdrPoints: 0,
            SpreadPoints: 0,
            SafetyPoints: 0,
            Reason: reason);

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
