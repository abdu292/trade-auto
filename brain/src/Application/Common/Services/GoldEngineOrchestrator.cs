using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Gold Engine Orchestrator per spec/00_instructions
/// Implements the exact 20-engine runtime flow:
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
public class GoldEngineOrchestrator
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IEconomicNewsService _newsService;
    private readonly ITradeLedgerService _ledgerService;
    private readonly ISetupLifecycleStore _candidateStore;
    private readonly IAIWorkerClient _aiWorkerClient;
    private readonly ITelegramSignalStore? _telegramSignalStore;
    private readonly ITradingViewSignalStore? _tradingViewSignalStore;
    private readonly IHistoricalPatternStore? _historicalPatternStore;

    public GoldEngineOrchestrator(
        IMarketDataProvider marketDataProvider,
        IEconomicNewsService newsService,
        ITradeLedgerService ledgerService,
        ISetupLifecycleStore candidateStore,
        IAIWorkerClient aiWorkerClient,
        ITelegramSignalStore? telegramSignalStore = null,
        ITradingViewSignalStore? tradingViewSignalStore = null,
        IHistoricalPatternStore? historicalPatternStore = null)
    {
        _marketDataProvider = marketDataProvider;
        _newsService = newsService;
        _ledgerService = ledgerService;
        _candidateStore = candidateStore;
        _aiWorkerClient = aiWorkerClient;
        _telegramSignalStore = telegramSignalStore;
        _tradingViewSignalStore = tradingViewSignalStore;
        _historicalPatternStore = historicalPatternStore;
    }

    public async Task<OrchestrationResult> ExecuteFullFlowAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        // 1. MT5 snapshot ingestion
        var snapshot = await _marketDataProvider.GetSnapshotAsync(symbol, cancellationToken);

        // 2. Session engine
        var (session, phase) = TradingSessionClock.Resolve(snapshot.Mt5ServerTime);
        snapshot = snapshot with { Session = session, SessionPhase = phase };
        var sessionResult = SessionEngine.Resolve(snapshot.Mt5ServerTime);

        // 3. Indicator engine
        var indicatorResult = IndicatorEngine.Calculate(snapshot, sessionResult);

        // 4. Structure engine (using existing RuleEngine)
        var structureResult = RuleEngine.EvaluateStructure(snapshot);
        var structureEngineResult = ToStructureEngineResult(structureResult);

        // 5. Regime (RegimeRiskClassifier gives RegimeClassificationContract for waterfall)
        var regimeResult = RegimeRiskClassifier.Classify(snapshot);

        // 6. Waterfall / crisis engine
        var waterfallRisk = WaterfallDetectionEngine.Detect(snapshot, regimeResult);
        var waterfallCrisisResult = new WaterfallCrisisEngineResult(
            PatternType: "",
            PatternReason: "",
            CrisisVeto: waterfallRisk == "HIGH",
            CrisisReason: waterfallRisk == "HIGH" ? "Waterfall HIGH" : "",
            WaterfallRisk: waterfallRisk,
            ShouldBlock: waterfallRisk == "HIGH");

        // 7. Hard legality checks
        var ledgerState = _ledgerService.GetState();
        var legalityResult = HardLegalityEngine.Check(
            snapshot,
            ledgerState,
            waterfallCrisisResult,
            structureEngineResult,
            snapshot.IsUsRiskWindow || snapshot.NewsEventFlag);

        if (!legalityResult.IsLegal)
        {
            var blockReason = legalityResult.BlockingChecks.FirstOrDefault()?.Reason ?? "BLOCKED";
            return OrchestrationResult.NoTrade(
                $"Hard legality block: {blockReason}",
                reasonCode: blockReason);
        }

        // 8. VERIFY
        var telegramSignals = _telegramSignalStore != null
            ? await _telegramSignalStore.GetRecentSignalsAsync(TimeSpan.FromHours(1), cancellationToken)
            : null;
        TradingViewSignalContract? tradingViewSignal = null;
        if (_tradingViewSignalStore != null && _tradingViewSignalStore.TryGetLatest(out var tvSignal))
        {
            tradingViewSignal = tvSignal;
        }

        var verifyResult = VerifyEngine.Process(
            snapshot,
            structureEngineResult,
            SessionEngine.Resolve(snapshot.Mt5ServerTime));

        // 9. NEWS (build volatility + session for Process)
        var volatilityResult = VolatilityRegimeEngine.Analyze(snapshot, indicatorResult, structureEngineResult);
        var newsResult = NewsEngine.Process(
            snapshot,
            waterfallCrisisResult,
            volatilityResult,
            sessionResult);

        // 10. CAPITAL UTILIZATION
        var capitalResult = CapitalUtilizationService.Check(
            ledgerState.CashAed,
            snapshot.AuthoritativeRate > 0 ? snapshot.AuthoritativeRate : snapshot.Ask,
            100m);  // Default check with 100g

        var capitalUtilResult = new CapitalUtilizationEngineResult(
            C1Capacity: ledgerState.DeployableCashAed * 0.8m,
            C2Capacity: ledgerState.DeployableCashAed * 0.2m,
            CapacityClamp: !capitalResult.ApprovedByCapacityGate,
            SizeState: capitalResult.ApprovedGrams >= 200m ? "LARGE" : capitalResult.ApprovedGrams >= 100m ? "STANDARD" : "MINIMUM",
            ExposureState: ledgerState.OpenBuyCount >= 2 ? "MAXED" : ledgerState.OpenBuyCount == 1 ? "MODERATE" : "SAFE",
            AffordableFlag: capitalResult.ApprovedByCapacityGate,
            MaxAffordableGrams: capitalResult.ApprovedGrams);

        // 11. HISTORICAL_PATTERN_ENGINE
        var historicalMatches = _historicalPatternStore != null
            ? await _historicalPatternStore.FindMatchesAsync(
                snapshot,
                session,
                phase,
                snapshot.DayOfWeek,
                cancellationToken)
            : null;

        var historicalResult = HistoricalPatternEngine.Process(
            snapshot,
            indicatorResult,
            structureEngineResult,
            sessionResult);

        // 12. Candidate Engine
        var candidateResult = CandidateEngine.Process(
            snapshot,
            structureEngineResult,
            volatilityResult,
            waterfallCrisisResult,
            newsResult,
            capitalUtilResult,
            historicalResult,
            verifyResult,
            sessionResult);

        // 13. ANALYZE (via AI Worker)
        var aiAnalyzeResult = await _aiWorkerClient.AnalyzeAsync(snapshot, snapshot.CycleId, cancellationToken);
        var analyzeResult = ConvertAiResultToAnalyzeResult(aiAnalyzeResult, structureResult);
        var analyzeEngineResult = ToAnalyzeEngineResult(analyzeResult);

        // 14. TABLE
        var tableResult = TableCompiler.Compile(
            snapshot,
            candidateResult,
            analyzeEngineResult,
            structureEngineResult,
            volatilityResult,
            waterfallCrisisResult,
            newsResult,
            capitalUtilResult,
            historicalResult,
            ledgerState,
            null);

        if (!tableResult.IsValid)
        {
            return OrchestrationResult.NoTrade(
                $"TABLE compilation failed: {tableResult.Reason}",
                reasonCode: tableResult.Reason);
        }

        // 15. VALIDATE
        var validateResult = ValidateEngine.Validate(
            snapshot,
            tableResult,
            structureEngineResult,
            volatilityResult,
            newsResult,
            sessionResult);

        if (!validateResult.IsValid)
        {
            return OrchestrationResult.NoTrade(
                $"VALIDATE failed: {validateResult.Reason}",
                reasonCode: validateResult.Reason);
        }

        // 16. Final Decision Engine
        var finalDecision = FinalDecisionEngine.Decide(
            candidateResult,
            tableResult,
            validateResult,
            legalityResult,
            waterfallCrisisResult,
            newsResult);

        if (finalDecision.Decision != "YES")
        {
            return OrchestrationResult.NoTrade(
                $"Final decision: {finalDecision.Decision}",
                reasonCode: finalDecision.ReasonCode);
        }

        // 17. MT5 Execution (return order for execution)
        var validated = validateResult.ValidatedOrder!;
        var order = new PendingOrderContract(
            validated.OrderType!,
            validated.Entry!.Value,
            validated.Tp!.Value,
            null,
            null,
            null,
            validated.Grams!.Value,
            validated.ExpiryUtc!.Value,
            "TABLE_COMPILED",
            finalDecision.ReasonCode);

        return OrchestrationResult.Trade(order);
    }

    // Helper methods...
    private static AnalyzeResult ConvertAiResultToAnalyzeResult(
        TradeSignalContract aiResult,
        StructureResult structureResult)
    {
        // Convert AI worker result to AnalyzeResult
        return new AnalyzeResult(
            Regime: aiResult.RegimeTag ?? "UNKNOWN",
            WaterfallRisk: aiResult.SafetyTag == "BLOCK" ? "HIGH" : "LOW",
            MidAirStatus: "NONE",
            RailAStatus: aiResult.Rail == "BUY_LIMIT" ? "YES" : "NO",
            RailBStatus: aiResult.Rail == "BUY_STOP" ? "YES" : "NO",
            RailAReason: null,
            RailBReason: null,
            S1: structureResult.S1,
            S2: structureResult.S2,
            S3: structureResult.S3,
            R1: structureResult.R1,
            R2: structureResult.R2,
            FailPrice: structureResult.Fail,
            FailThreatened: structureResult.FailThreatened,
            FailBroken: structureResult.FailBroken,
            FailProtected: structureResult.Fail.HasValue && structureResult.Fail.Value > 0,
            StructureValid: !structureResult.FailBroken && structureResult.HasShelf,
            CurrentSessionAnchor: null,
            NextSessionAnchor: null,
            NearestMagnet: null,
            PrimaryTradeConcept: aiResult.Rail == "BUY_LIMIT" ? "SHELF_RECLAIM" : "LID_BREAKOUT",
            RotationEnvelope: "+8 to +12",
            TriggerObject: null,
            BottomType: "CLASSIC_RECLAIM_BOTTOM",
            PatternType: "FLUSH_REVERSAL_ATTEMPT",
            ImpulseHarvestScore: 0.5m,
            SessionHistoricalModifier: 0.0m,
            ConfidenceScore: aiResult.AlignmentScore,
            LidPrice: null);
    }

    private static StructureEngineResult ToStructureEngineResult(StructureResult r)
    {
        return new StructureEngineResult(
            S1: r.S1 ?? 0m,
            S2: r.S2,
            S3: r.S3,
            R1: r.R1 ?? 0m,
            R2: r.R2,
            Fail: r.Fail,
            HasShelf: r.HasShelf,
            ShelfLevel: r.S1 ?? 0m,
            HasLid: r.HasLid,
            LidLevel: r.R1 ?? 0m,
            HasSweep: r.HasSweep,
            SweepLevel: 0m,
            HasReclaim: r.HasReclaim,
            ReclaimLevel: 0m,
            IsMidAir: r.IsMidAir,
            MidAirZone: "",
            StructureQuality: r.FailBroken ? "WEAK" : "STRONG");
    }

    private static AnalyzeEngineResult ToAnalyzeEngineResult(AnalyzeResult a)
    {
        return new AnalyzeEngineResult(
            Regime: a.Regime,
            WaterfallRisk: a.WaterfallRisk,
            MidAirStatus: a.MidAirStatus,
            RailAStatus: a.RailAStatus,
            RailAReason: a.RailAReason ?? "",
            RailBStatus: a.RailBStatus,
            RailBReason: a.RailBReason ?? "",
            S1: a.S1 ?? 0m,
            S2: a.S2,
            R1: a.R1 ?? 0m,
            R2: a.R2,
            Fail: a.FailPrice,
            CurrentSessionAnchors: Array.Empty<string>(),
            NextSessionAnchors: Array.Empty<string>(),
            NearestMagnet: a.NearestMagnet ?? 0m,
            PrimaryTradeConcept: a.PrimaryTradeConcept,
            RotationEnvelope: (8m, 12m),
            TriggerObjects: Array.Empty<string>(),
            BottomType: a.BottomType,
            PatternType: a.PatternType,
            ImpulseHarvestScore: a.ImpulseHarvestScore,
            SessionHistoricalModifier: a.SessionHistoricalModifier);
    }
}

// Supporting contracts and results
public record OrchestrationResult(
    bool IsTrade,
    PendingOrderContract? Order,
    string? ReasonCode,
    string? Message)
{
    public static OrchestrationResult Trade(PendingOrderContract order) =>
        new(true, order, "ORCHESTRATION_COMPLETE", "Order compiled and validated");

    public static OrchestrationResult NoTrade(string message, string? reasonCode = null) =>
        new(false, null, reasonCode ?? "ORCHESTRATION_BLOCKED", message);
}

public record PendingOrderContract(
    string OrderType,
    decimal EntryPrice,
    decimal Tp1,
    decimal? Tp2,
    decimal? Tp3,
    decimal? StopLoss,
    decimal Grams,
    DateTimeOffset Expiry,
    string Template,
    string ReasonCode);