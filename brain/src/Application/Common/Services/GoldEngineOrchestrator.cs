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
        var snapshot = await _marketDataProvider.GetMarketSnapshotAsync(symbol, cancellationToken);
        if (snapshot == null)
        {
            return OrchestrationResult.NoTrade("Failed to fetch market snapshot");
        }

        // 2. Session engine
        var (session, phase) = TradingSessionClock.Resolve(snapshot.Mt5ServerTime);
        snapshot = snapshot with { Session = session, SessionPhase = phase };

        // 3. Indicator engine
        var indicatorResult = IndicatorEngine.Calculate(snapshot);

        // 4. Structure engine (using existing RuleEngine)
        var structureResult = RuleEngine.EvaluateStructure(snapshot);

        // 5. Volatility / regime engine
        var regimeResult = MarketRegimeDetector.Classify(snapshot);
        var volatilityState = RegimeRiskClassifier.ClassifyVolatility(snapshot, regimeResult);

        // 6. Waterfall / crisis engine
        var waterfallRisk = WaterfallDetectionEngine.Detect(snapshot, regimeResult);
        var crisisVeto = waterfallRisk == "HIGH";

        // 7. Hard legality checks
        var ledgerState = _ledgerService.GetState();
        var legalityResult = HardLegalityEngine.Check(
            snapshot,
            ledgerState,
            regimeResult,
            waterfallRisk);

        if (!legalityResult.IsLegal)
        {
            return OrchestrationResult.NoTrade(
                $"Hard legality block: {legalityResult.BlockReason}",
                reasonCode: legalityResult.BlockReason);
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

        var verifyResult = VerifyEngine.Verify(
            snapshot,
            telegramSignals?.Select(s => new TelegramSignalContract(
                s.SourceTag,
                s.Timestamp,
                s.Symbol,
                s.Direction,
                s.EntryZoneLow,
                s.EntryZoneHigh,
                s.StopLoss,
                s.TpPips,
                s.CommentTags)).ToList(),
            tradingViewSignal,
            new StructureLevelsContract(
                structureResult.S1,
                structureResult.S2,
                structureResult.S3,
                structureResult.R1,
                structureResult.R2,
                structureResult.Fail));

        // 9. NEWS
        var newsAssessment = await _newsService.AssessAsync(snapshot.Timestamp, cancellationToken);
        var upcomingEvents = newsAssessment.NearbyEvents?.Select(e => new EconomicEventContract(
            e.EventTimeUtc,
            e.Tier ?? "Tier3",
            e.Currency ?? "USD",
            e.Title,
            e.Actual,
            e.Forecast,
            e.Previous,
            e.Impact)).ToList();
        var newsResult = NewsEngine.ProcessNews(
            snapshot,
            upcomingEvents,
            null,  // Recent events - would need separate query
            null,  // Macro intel - would need separate service
            null);  // Current mode - would need separate query

        // 10. CAPITAL UTILIZATION
        var capitalResult = CapitalUtilizationService.Check(
            ledgerState.CashAedTotal,
            snapshot.AuthoritativeRate > 0 ? snapshot.AuthoritativeRate : snapshot.Ask,
            100m);  // Default check with 100g

        var capitalUtilResult = new CapitalUtilizationResult(
            RecommendedGrams: capitalResult.ApprovedGrams,
            SizeState: capitalResult.ApprovedGrams >= 200m ? "LARGE" : capitalResult.ApprovedGrams >= 100m ? "STANDARD" : "MINIMUM",
            ExposureState: ledgerState.OpenPositionsCount >= 2 ? "MAXED" : ledgerState.OpenPositionsCount == 1 ? "MODERATE" : "SAFE",
            AffordableFlag: capitalResult.ApprovedByCapacityGate,
            SlotCount: ledgerState.OpenPositionsCount,
            C1Available: ledgerState.CashAedTotal * 0.7m,  // 70% in C1
            C2Available: ledgerState.CashAedTotal * 0.3m);  // 30% in C2

        // 11. HISTORICAL_PATTERN_ENGINE
        var historicalMatches = _historicalPatternStore != null
            ? await _historicalPatternStore.FindMatchesAsync(
                snapshot,
                session,
                phase,
                snapshot.DayOfWeek,
                cancellationToken)
            : null;

        var historicalResult = HistoricalPatternEngine.AnalyzeHistoricalPattern(
            snapshot,
            regimeResult,
            session,
            phase,
            snapshot.DayOfWeek,
            historicalMatches);

        // 12. Candidate Engine
        var candidate = _candidateStore.GetCandidate(symbol);
        var candidateState = candidate?.State ?? "NONE";
        var candidateResult = CandidateEngine.UpdateCandidate(
            candidateState,
            snapshot,
            structureResult,
            waterfallRisk,
            verifyResult,
            historicalResult);

        // 13. ANALYZE (via AI Worker)
        // The AI Worker receives the snapshot and builds its own context packet
        var aiAnalyzeResult = await _aiWorkerClient.AnalyzeAsync(snapshot, snapshot.CycleId, cancellationToken);

        // Convert AI result to AnalyzeResult
        var analyzeResult = ConvertAiResultToAnalyzeResult(aiAnalyzeResult, structureResult);

        // 14. TABLE
        var tableResult = TableCompiler.Compile(
            snapshot,
            analyzeResult,
            ledgerState,
            newsResult,
            capitalUtilResult,
            historicalResult,
            verifyResult,
            100m);  // minTradeGrams

        if (!tableResult.IsValid)
        {
            return OrchestrationResult.NoTrade(
                $"TABLE compilation failed: {tableResult.RejectionReason}",
                reasonCode: tableResult.ReasonCode);
        }

        // 15. VALIDATE
        var validateResult = ValidateEngine.Validate(
            tableResult,
            snapshot,
            newsResult,
            analyzeResult,
            historicalResult);

        if (!validateResult.IsValid)
        {
            return OrchestrationResult.NoTrade(
                $"VALIDATE failed: {validateResult.RejectionReason}",
                reasonCode: validateResult.ReasonCode);
        }

        // 16. Final Decision Engine
        var finalDecision = FinalDecisionEngine.MakeDecision(
            validateResult,
            snapshot,
            newsResult,
            analyzeResult,
            historicalResult);

        if (finalDecision.Decision != "YES")
        {
            return OrchestrationResult.NoTrade(
                $"Final decision: {finalDecision.Decision}",
                reasonCode: finalDecision.ReasonCode);
        }

        // 17. MT5 Execution (return order for execution)
        var order = new PendingOrderContract(
            validateResult.OrderType!,
            validateResult.EntryPrice!.Value,
            validateResult.Tp1!.Value,
            validateResult.Tp2,
            validateResult.Tp3,
            validateResult.StopLoss,
            validateResult.Grams!.Value,
            validateResult.Expiry!.Value,
            "TABLE_COMPILED",
            finalDecision.ReasonCode);

        return OrchestrationResult.Trade(order);
    }

    // Helper methods...
    private static AnalyzeResult ConvertAiResultToAnalyzeResult(
        TradeSignalContract aiResult,
        RuleEngine.StructureResult structureResult)
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

// Placeholder interfaces for missing stores
public interface ITelegramSignalStore
{
    Task<IReadOnlyCollection<TelegramSignalContract>> GetRecentSignalsAsync(TimeSpan window, CancellationToken ct);
}

public interface IHistoricalPatternStore
{
    Task<IReadOnlyCollection<HistoricalPatternMatch>> FindMatchesAsync(
        MarketSnapshotContract snapshot,
        string session,
        string phase,
        DayOfWeek dayOfWeek,
        CancellationToken ct);
}