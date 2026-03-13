using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Main orchestrator for the Physical Gold EA 20-engine system.
/// Implements the exact runtime flow from the integration map:
/// 1. MT5 snapshot ingestion
/// 2. Session engine
/// 3. Indicator engine
/// 4. Structure engine
/// 5. Volatility / regime engine
/// 6. Waterfall / crisis engine
/// 7. Hard legality checks
/// 8. VERIFY
/// 9. NEWS
/// 10. CAPITAL UTILIZATION
/// 11. HISTORICAL_PATTERN_ENGINE
/// 12. Candidate Engine
/// 13. ANALYZE
/// 14. TABLE
/// 15. VALIDATE
/// 16. Final Decision Engine
/// 17. MT5 Execution
/// 18. MANAGE / RE-ANALYZE
/// 19. SLIPS
/// 20. STUDY / SELF-CROSSCHECK / ENGINE HEALTH
/// </summary>
public static class PhysicalGoldEngineOrchestrator
{
    /// <summary>
    /// Main orchestration method - runs all 20 engines in exact order
    /// </summary>
    public static async Task<PhysicalGoldEngineResult> RunFullCycle(
        MarketSnapshotContract snapshot,
        LedgerStateContract? ledgerState,
        bool hazardWindowBlocked,
        IGoldEngineThresholds? thresholds = null)
    {
        thresholds ??= new DefaultThresholds();
        
        // Engine 1: SESSION ENGINE
        var session = SessionEngine.Resolve(snapshot.Mt5ServerTime);
        
        // Engine 2: INDICATOR ENGINE
        var indicators = IndicatorEngine.Calculate(snapshot, session);
        
        // Engine 3: STRUCTURE ENGINE
        var structure = StructureEngine.Analyze(snapshot, indicators, session);
        
        // Engine 4: VOLATILITY/REGIME ENGINE
        var volatility = VolatilityRegimeEngine.Analyze(snapshot, indicators, structure);
        
        // Engine 5: WATERFALL/CRISIS ENGINE
        var waterfall = WaterfallCrisisEngine.Analyze(snapshot, indicators, structure, volatility);
        
        // Engine 6: HARD LEGALITY ENGINE
        var legality = HardLegalityEngine.Check(snapshot, ledgerState, waterfall, structure, hazardWindowBlocked);
        
        // Early exit if hard legality fails
        if (!legality.IsLegal)
        {
            return new PhysicalGoldEngineResult(
                IsTradeAllowed: false,
                Reason: $"Hard legality blocked: {string.Join(", ", legality.BlockingChecks.Select(c => c.Reason))}",
                EngineStates: new Dictionary<string, object>
                {
                    ["Session"] = session,
                    ["Indicators"] = indicators,
                    ["Structure"] = structure,
                    ["Volatility"] = volatility,
                    ["Waterfall"] = waterfall,
                    ["Legality"] = legality
                });
        }
        
        // Engine 7: VERIFY ENGINE
        var verify = VerifyEngine.Process(snapshot, structure, session);
        
        // Engine 8: NEWS ENGINE
        var news = NewsEngine.Process(snapshot, waterfall, volatility, session);
        
        // Engine 9: CAPITAL UTILIZATION ENGINE
        var capitalUtilization = CapitalUtilizationEngine.Process(snapshot, ledgerState, indicators);
        
        // Engine 10: HISTORICAL_PATTERN_ENGINE
        var historicalPattern = HistoricalPatternEngine.Process(snapshot, indicators, structure, session);
        
        // Engine 11: CANDIDATE ENGINE
        var candidate = CandidateEngine.Process(
            snapshot,
            structure,
            volatility,
            waterfall,
            news,
            capitalUtilization,
            historicalPattern,
            verify,
            session);
        
        // Engine 12: ANALYZE ENGINE
        var analyze = AnalyzeEngine.Process(
            snapshot,
            structure,
            volatility,
            waterfall,
            news,
            historicalPattern,
            candidate,
            indicators,
            session);
        
        // Engine 13: TABLE COMPILER
        var table = TableCompiler.Compile(
            snapshot,
            candidate,
            analyze,
            structure,
            volatility,
            waterfall,
            news,
            capitalUtilization,
            historicalPattern,
            ledgerState,
            thresholds);
        
        // Engine 14: VALIDATE ENGINE
        var validate = ValidateEngine.Validate(
            snapshot,
            table,
            structure,
            volatility,
            news,
            session);
        
        // Engine 15: FINAL DECISION ENGINE
        var finalDecision = FinalDecisionEngine.Decide(
            candidate,
            table,
            validate,
            legality,
            waterfall,
            news);
        
        // Engine 16: MT5 EXECUTION (would be called if finalDecision.IsTradeAllowed)
        // This is handled by the calling service
        
        // Engine 17: MANAGE/RE-ANALYZE (would be called for pending orders)
        // This is handled by the calling service
        
        // Engine 18: SLIPS (would be called after fills)
        // This is handled by the calling service
        
        // Engine 19: STUDY/SELF-CROSSCHECK (post-trade analysis)
        // This is handled by the calling service
        
        // Engine 20: ENGINE HEALTH/REGRESSION (maintenance)
        // This is handled by the calling service
        
        return new PhysicalGoldEngineResult(
            IsTradeAllowed: finalDecision.Decision == "YES",
            Reason: finalDecision.Reason,
            DecisionResult: finalDecision.DecisionResult,
            EngineStates: new Dictionary<string, object>
            {
                ["Session"] = session,
                ["Indicators"] = indicators,
                ["Structure"] = structure,
                ["Volatility"] = volatility,
                ["Waterfall"] = waterfall,
                ["Legality"] = legality,
                ["Verify"] = verify,
                ["News"] = news,
                ["CapitalUtilization"] = capitalUtilization,
                ["HistoricalPattern"] = historicalPattern,
                ["Candidate"] = candidate,
                ["Analyze"] = analyze,
                ["Table"] = table,
                ["Validate"] = validate,
                ["FinalDecision"] = finalDecision
            });
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

/// <summary>
/// Physical Gold Engine Orchestrator result
/// </summary>
public sealed record PhysicalGoldEngineResult(
    bool IsTradeAllowed,
    string Reason,
    DecisionResultContract? DecisionResult = null,
    Dictionary<string, object>? EngineStates = null);
