using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Application.Common.Services;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Brain.Infrastructure.Services.Background;

public sealed class SignalPollingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SignalPollingBackgroundService> logger,
    DynamicSessionRiskService dynamicSessionRisk) : BackgroundService
{
    private static DateTimeOffset _lastArmedAtUtc = DateTimeOffset.MinValue;
    private static readonly Lock _transitionGate = new();
    private static string _lastMode = string.Empty;
    private static string _lastWaterfallRisk = string.Empty;
    private const decimal OunceToGram = 31.1035m;
    private const decimal UsdToAed = 3.674m;
    private const decimal ShopSpreadUsdPerOz = 0.80m;

    // Waterfall failure tracking: after 2 consecutive failures the system enters study lock
    // (PRD point 4: "if two orders fail to either trigger or gets caught in the waterfall then
    //  the system should do study & self_cross check and hard lock only refinements")
    private static int _consecutiveWaterfallFailures = 0;
    private static DateTimeOffset _studyLockExpiry = DateTimeOffset.MinValue;
    private static readonly Lock _waterfallGate = new();
    private const int StudyLockWaterfallThreshold = 2;
    private static readonly TimeSpan StudyLockDuration = TimeSpan.FromMinutes(30);

    // Study refinement: autonomous study/self-crosscheck is triggered once per STUDY_LOCK period.
    // _studyRanForLockExpiry tracks the lock expiry for which the study has already been dispatched
    // so we don't re-run the study on every 30-second polling cycle during the lock window.
    private static DateTimeOffset _studyRanForLockExpiry = DateTimeOffset.MinValue;
    private static readonly List<string> _recentWaterfallReasons = [];
    private static readonly Lock _studyGate = new();
    private const int MaxRecentWaterfallReasons = 10;

    // Candle-aligned execution: strategy runs only on new M5 or M15 candle close.
    private static DateTimeOffset? _lastM5CandleTime = null;
    private static DateTimeOffset? _lastM15CandleTime = null;
    private static readonly TimeSpan CandleCheckInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var marketData = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();
            var aiWorker = scope.ServiceProvider.GetRequiredService<IAIWorkerClient>();
            var economicNews = scope.ServiceProvider.GetRequiredService<IEconomicNewsService>();
            var pendingTrades = scope.ServiceProvider.GetRequiredService<IPendingTradeStore>();
            var approvals = scope.ServiceProvider.GetRequiredService<ITradeApprovalStore>();
            var ledger = scope.ServiceProvider.GetRequiredService<ITradeLedgerService>();
            var mt5Control = scope.ServiceProvider.GetRequiredService<IMt5ControlStore>();
            var tradingViewStore = scope.ServiceProvider.GetRequiredService<ITradingViewSignalStore>();
            var runtimeSettings = scope.ServiceProvider.GetRequiredService<ITradingRuntimeSettingsStore>();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var timeline = scope.ServiceProvider.GetRequiredService<IRuntimeTimelineWriter>();

            try
            {
                var cycleId = $"cyc_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";

                // Check study lock (PRD point 4): after 2 consecutive waterfall failures,
                // the system pauses new signal generation and requires STUDY & SELF_CROSSCHECK.
                bool isStudyLockActive;
                lock (_waterfallGate)
                {
                    isStudyLockActive = _studyLockExpiry > DateTimeOffset.UtcNow;
                    if (!isStudyLockActive && _consecutiveWaterfallFailures >= StudyLockWaterfallThreshold)
                    {
                        // Lock expired, reset failure counter so it can accumulate again
                        _consecutiveWaterfallFailures = 0;
                    }
                }

                if (isStudyLockActive)
                {
                    var remaining = _studyLockExpiry - DateTimeOffset.UtcNow;
                    logger.LogWarning(
                        "STUDY_LOCK_ACTIVE — {ConsecutiveFailures} consecutive waterfall failures detected. " +
                        "Perform STUDY & SELF_CROSSCHECK before trading resumes. Lock expires in {RemainingMinutes:0.0}m.",
                        StudyLockWaterfallThreshold,
                        remaining.TotalMinutes);

                    // Autonomous study refinement: dispatch once per STUDY_LOCK period so the
                    // aiworker can run a full self-crosscheck with ALL analyzers and surface
                    // rule adjustment suggestions to the timeline.
                    bool shouldRunStudy;
                    DateTimeOffset currentLockExpiry;
                    IReadOnlyCollection<string> capturedReasons;
                    lock (_studyGate)
                    {
                        currentLockExpiry = _studyLockExpiry;
                        shouldRunStudy = _studyRanForLockExpiry != currentLockExpiry;
                        capturedReasons = [.. _recentWaterfallReasons];
                    }

                    if (shouldRunStudy)
                    {
                        lock (_studyGate)
                        {
                            _studyRanForLockExpiry = currentLockExpiry;
                        }

                        var studyCycleId = $"study_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";

                        try
                        {
                            var rawSnapshotForStudy = await marketData.GetSnapshotAsync(
                                runtimeSettings.GetSymbol(), stoppingToken);
                            var snapshotForStudy = rawSnapshotForStudy with { CycleId = studyCycleId };

                            await timeline.WriteAsync(
                                eventType: "STUDY_REFINEMENT_STARTED",
                                stage: "study",
                                source: "brain",
                                symbol: snapshotForStudy.Symbol,
                                cycleId: studyCycleId,
                                tradeId: null,
                                payload: new
                                {
                                    studyCycleId,
                                    consecutiveWaterfallFailures = StudyLockWaterfallThreshold,
                                    recentWaterfallReasons = capturedReasons,
                                    note = "Autonomous study refinement dispatched to aiworker (all analyzers).",
                                },
                                cancellationToken: stoppingToken);

                            var studyContext = new StudyContextContract(
                                ConsecutiveWaterfallFailures: StudyLockWaterfallThreshold,
                                StudyCycleId: studyCycleId,
                                RecentBlockedCandidates: [],
                                RecentWaterfallReasons: capturedReasons);

                            var studyResult = await aiWorker.StudyAnalyzeAsync(
                                snapshotForStudy, studyContext, stoppingToken);

                            if (studyResult is not null)
                            {
                                await timeline.WriteAsync(
                                    eventType: "STUDY_REFINEMENT_RESULT",
                                    stage: "study",
                                    source: "aiworker",
                                    symbol: snapshotForStudy.Symbol,
                                    cycleId: studyCycleId,
                                    tradeId: null,
                                    payload: new
                                    {
                                        studyResult.StudyCycleId,
                                        studyResult.BottomPermissionVerdict,
                                        studyResult.WaterfallVerdict,
                                        studyResult.RuleAdjustments,
                                        studyResult.Confidence,
                                        studyResult.Reasoning,
                                        studyResult.ProviderVotes,
                                    },
                                    cancellationToken: stoppingToken);

                                logger.LogInformation(
                                    "STUDY_REFINEMENT_RESULT — bottom={Bottom} waterfall={Waterfall} adjustments={Adjustments} confidence={Confidence:0.00}",
                                    studyResult.BottomPermissionVerdict,
                                    studyResult.WaterfallVerdict,
                                    studyResult.RuleAdjustments.Count,
                                    studyResult.Confidence);
                            }
                        }
                        catch (Exception studyEx)
                        {
                            logger.LogWarning(studyEx, "Study refinement call failed for studyCycleId={StudyCycleId}.", studyCycleId);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var symbol = runtimeSettings.GetSymbol();
                var rawSnapshot = await marketData.GetSnapshotAsync(symbol, stoppingToken);
                // Spec v8 §13: enrich snapshot with Ma20M5 extracted from TimeframeData
                var m5CandleForMa = (rawSnapshot.TimeframeData ?? []).FirstOrDefault(x =>
                    string.Equals(x.Timeframe, "M5", StringComparison.OrdinalIgnoreCase));
                var enrichedMa20M5 = m5CandleForMa?.Ma20Value ?? 0m;
                var snapshot = rawSnapshot with { CycleId = cycleId, Ma20M5 = enrichedMa20M5 };

                // Candle-aligned gate: only run the strategy when a new M5 or M15 candle has closed.
                var currentM5Time = snapshot.TimeframeData
                    .FirstOrDefault(x => string.Equals(x.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.CandleStartTime;
                var currentM15Time = snapshot.TimeframeData
                    .FirstOrDefault(x => string.Equals(x.Timeframe, "M15", StringComparison.OrdinalIgnoreCase))?.CandleStartTime;

                if (currentM5Time.HasValue || currentM15Time.HasValue)
                {
                    var isM5New = currentM5Time.HasValue && currentM5Time != _lastM5CandleTime;
                    var isM15New = currentM15Time.HasValue && currentM15Time != _lastM15CandleTime;

                    if (!isM5New && !isM15New)
                    {
                        // No new M5 or M15 candle since last cycle — skip strategy to reduce noise.
                        await Task.Delay(CandleCheckInterval, stoppingToken);
                        continue;
                    }

                    var trigger = isM5New && isM15New ? "M5+M15" : isM5New ? "M5" : "M15";
                    logger.LogInformation("Candle-aligned cycle triggered by {Trigger} close.", trigger);

                    if (currentM5Time.HasValue) _lastM5CandleTime = currentM5Time;
                    if (currentM15Time.HasValue) _lastM15CandleTime = currentM15Time;
                }

                await timeline.WriteAsync(
                    eventType: "CYCLE_STARTED",
                    stage: "polling",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        snapshot.Symbol,
                        snapshot.Session,
                        snapshot.SessionPhase,
                        snapshot.Timestamp,
                        snapshot.Mt5ServerTime,
                        snapshot.KsaTime,
                        snapshot.UaeTime,
                        snapshot.IndiaTime,
                        snapshot.Bid,
                        snapshot.Ask,
                        snapshot.Spread,
                        snapshot.Atr,
                        snapshot.Adr,
                        snapshot.TelegramState,
                        snapshot.RateAuthority,
                        snapshot.AuthoritativeRate,
                    },
                    cancellationToken: stoppingToken);
                var normalizedSession = MapSessionType(snapshot.Session);
                var sessionEnabled = await db.SessionStates
                    .AsNoTracking()
                    .Where(x => x.Session == normalizedSession)
                    .Select(x => (bool?)x.IsEnabled)
                    .FirstOrDefaultAsync(stoppingToken);
                if (sessionEnabled == false)
                {
                    logger.LogInformation(
                        "NO_TRADE due to disabled session. SnapshotSession={SnapshotSession} MappedSession={MappedSession}",
                        snapshot.Session,
                        normalizedSession.Value);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var regime = RegimeRiskClassifier.Classify(snapshot);
                var forceWhereToTrade = ShouldForceWhereToTrade(snapshot, regime);

                if (regime.IsBlocked)
                {
                    logger.LogInformation(
                        "NO TRADE - HIGH RISK. Symbol={Symbol}, Regime={Regime}, Reason={Reason}",
                        snapshot.Symbol,
                        regime.Regime,
                        regime.Reason);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                if (forceWhereToTrade)
                {
                    logger.LogInformation("Watch/Waste kill-switch forcing cycle search (no ARMED order for >=25 minutes).");
                }

                // ── Spec v7: Gold Engine decision stack (path-aware; M5 optional for BUY_LIMIT) ──
                var rawStackLedgerState = ledger.GetState();
                var reservedPendingAedForStack = EstimateReservedPendingAed(pendingTrades.Snapshot());
                var stackLedgerState = new LedgerStateContract(
                    CashAed: rawStackLedgerState.CashAed,
                    GoldGrams: rawStackLedgerState.GoldGrams,
                    OpenExposurePercent: rawStackLedgerState.OpenExposurePercent,
                    DeployableCashAed: Math.Max(0m, rawStackLedgerState.DeployableCashAed - reservedPendingAedForStack),
                    OpenBuyCount: rawStackLedgerState.OpenBuyCount);
                var activeHazardWindowBlockNow = await db.HazardWindows
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.IsActive && x.IsBlocked && x.StartUtc <= snapshot.Timestamp && x.EndUtc >= snapshot.Timestamp,
                        stoppingToken);
                var waterfallRiskForStack = regime.IsWaterfall ? "HIGH" : (regime.RiskTag == "CAUTION" ? "MEDIUM" : "LOW");
                var decisionStackResult = GoldEngineDecisionStack.Evaluate(
                    snapshot,
                    regime,
                    waterfallRiskForStack,
                    stackLedgerState,
                    activeHazardWindowBlockNow,
                    null);

                await timeline.WriteAsync(
                    eventType: "MARKET_REGIME_DETECTED",
                    stage: "regime",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        regime = decisionStackResult.MarketRegime.Regime,
                        isTradeable = decisionStackResult.MarketRegime.IsTradeable,
                        reason = decisionStackResult.MarketRegime.Reason,
                        ema50H1 = snapshot.Ema50H1,
                        ema200H1 = snapshot.Ema200H1,
                        rsiH1 = snapshot.RsiH1,
                    },
                    cancellationToken: stoppingToken);

                await timeline.WriteAsync(
                    eventType: "GOLD_ENGINE_DECISION_STACK",
                    stage: "rule_engine",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        proceedToAi = decisionStackResult.ProceedToAi,
                        pathState = decisionStackResult.PathState,
                        reasonCode = decisionStackResult.ReasonCode,
                        legalityState = decisionStackResult.LegalityState,
                        confidenceScore = decisionStackResult.ConfidenceScore.Score,
                        confidenceTier = decisionStackResult.ConfidenceScore.Tier,
                        overextensionState = decisionStackResult.Overextension.State,
                        sweepReclaimState = decisionStackResult.SweepReclaim.State,
                        engineStates = decisionStackResult.EngineStates == null ? null : new
                        {
                            decisionStackResult.EngineStates.LegalityState,
                            decisionStackResult.EngineStates.BiasState,
                            decisionStackResult.EngineStates.PathState,
                            decisionStackResult.EngineStates.SizeState,
                            decisionStackResult.EngineStates.OverextensionState,
                            decisionStackResult.EngineStates.WaterfallRisk,
                            decisionStackResult.EngineStates.Session,
                            decisionStackResult.EngineStates.SessionPhase,
                            decisionStackResult.EngineStates.ConfidenceScore,
                            decisionStackResult.EngineStates.ConfidenceTier,
                            decisionStackResult.EngineStates.ReasonCode,
                            decisionStackResult.EngineStates.HazardWindowActive,
                        },
                    },
                    cancellationToken: stoppingToken);

                var setupCandidate = decisionStackResult.ProceedToAi
                    ? SetupCandidateResult.Valid(
                        decisionStackResult.H1Context,
                        decisionStackResult.M15Setup,
                        decisionStackResult.M5Entry!,
                        decisionStackResult.MarketRegime,
                        decisionStackResult.ImpulseConfirmation!)
                    : SetupCandidateResult.Aborted(
                        decisionStackResult.H1Context,
                        decisionStackResult.M15Setup,
                        decisionStackResult.ReasonCode ?? $"PATH_{decisionStackResult.PathState}",
                        decisionStackResult.MarketRegime);

                var lastEngineStore = scope.ServiceProvider.GetRequiredService<ILastGoldEngineStateStore>();
                lastEngineStore.SetLast(decisionStackResult.EngineStates, decisionStackResult.PathRouting, snapshot);

                // ── Pattern Detector (CR8): runs after regime check, before AI ──────────────
                // Produces structured pattern intelligence for ANALYZE/TABLE/MANAGE/STUDY feeds.
                // Non-executing: no trades placed here, only intelligence emitted.
                var patterns = PatternDetector.Detect(snapshot);
                if (patterns.Count > 0)
                {
                    await timeline.WriteAsync(
                        eventType: "PATTERN_DETECTOR_RESULTS",
                        stage: "pattern",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            patternCount = patterns.Count,
                            patterns = patterns.Select(p => new
                            {
                                patternId = p.PatternId,
                                patternVersion = p.PatternVersion,
                                detectionMode = p.DetectionMode.ToString(),
                                patternType = p.PatternType.ToString(),
                                subtype = p.Subtype,
                                confidence = p.Confidence,
                                session = p.Session,
                                timeframePrimary = p.TimeframePrimary,
                                entrySafety = p.EntrySafety,
                                waterfallRisk = p.WaterfallRisk,
                                failThreatened = p.FailThreatened,
                                recommendedAction = p.RecommendedAction.ToString(),
                            }).ToList(),
                        },
                        cancellationToken: stoppingToken);
                }

                await timeline.WriteAsync(
                    eventType: setupCandidate.IsValid ? "RULE_ENGINE_SETUP_CANDIDATE" : "PATH_WAIT",
                    stage: "rule_engine",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        setupCandidate.IsValid,
                        pathState = decisionStackResult.PathState,
                        reasonCode = decisionStackResult.ReasonCode,
                        marketRegime = setupCandidate.MarketRegime,
                        h1Context = setupCandidate.H1Context,
                        m15Setup = setupCandidate.M15Setup,
                        m5Entry = setupCandidate.M5Entry,
                        impulseConfirmation = setupCandidate.ImpulseConfirmation,
                        abortReason = setupCandidate.AbortReason,
                    },
                    cancellationToken: stoppingToken);

                if (!setupCandidate.IsValid)
                {
                    logger.LogInformation(
                        "PATH_WAIT — path={PathState} reason={Reason} (spec v7: no generic M5 abort)",
                        decisionStackResult.PathState,
                        decisionStackResult.ReasonCode ?? setupCandidate.AbortReason);

                    await timeline.WriteAsync(
                        eventType: "AI_SKIPPED_PATH_WAIT",
                        stage: "rule_engine",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            reason = "path-aware decision stack: stand down or BUY_STOP not ready",
                            pathState = decisionStackResult.PathState,
                            reasonCode = decisionStackResult.ReasonCode,
                            abortReason = setupCandidate.AbortReason,
                        },
                        cancellationToken: stoppingToken);

                    await timeline.WriteAsync(
                        eventType: "FINAL_DECISION",
                        stage: "decision",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            finalDecision = "NO_TRADE",
                            primaryReason = decisionStackResult.ReasonCode ?? $"PATH_{decisionStackResult.PathState}",
                            abortReason = setupCandidate.AbortReason,
                        },
                        cancellationToken: stoppingToken);

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var newsRisk = await economicNews.AssessAsync(snapshot.Timestamp, stoppingToken);
                await timeline.WriteAsync(
                    eventType: "NEWS_CHECK",
                    stage: "news",
                    source: "forexfactory",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        blocked = newsRisk.IsBlocked,
                        newsRisk.Reason,
                        newsRisk.NearbyEvents,
                        newsRisk.RefreshedAtUtc,
                        newsRisk.IsStale,
                    },
                    cancellationToken: stoppingToken);

                if (newsRisk.IsBlocked)
                {
                    db.DecisionLogs.Add(DecisionLog.Create(
                        symbol: snapshot.Symbol,
                        status: "NO_TRADE",
                        engineState: "CAPITAL_PROTECTED",
                        mode: "EXHAUSTION",
                        cause: "NEWS_RISK_BLOCK",
                        waterfallRisk: "LOW",
                        telegramState: snapshot.TelegramState,
                        railPermissionA: "BLOCKED",
                        railPermissionB: "BLOCKED",
                        reason: newsRisk.Reason,
                        entry: 0m,
                        tp: 0m,
                        grams: 0m,
                        rotationCapThisSession: 0,
                        forceWhereToTrade: forceWhereToTrade,
                        snapshotHash: ComputeSnapshotHash(snapshot)));
                    await db.SaveChangesAsync(stoppingToken);

                    await timeline.WriteAsync(
                        eventType: "CYCLE_ABORTED",
                        stage: "news",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            reason = newsRisk.Reason,
                            newsRisk.NearbyEvents,
                        },
                        cancellationToken: stoppingToken);

                    await timeline.WriteAsync(
                        eventType: "FINAL_DECISION",
                        stage: "decision",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            finalDecision = "NO_TRADE",
                            primaryReason = "NEWS_BLOCK",
                            reason = newsRisk.Reason,
                        },
                        cancellationToken: stoppingToken);

                    logger.LogInformation("NO_TRADE due to economic news block. Reason={Reason}", newsRisk.Reason);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                // Spec v8 §13: enrich snapshot with NewsEventFlag (any nearby news events present)
                snapshot = snapshot with { NewsEventFlag = newsRisk.NearbyEvents?.Count > 0 };

                await timeline.WriteAsync(
                    eventType: "AI_ANALYZE_REQUEST",
                    stage: "ai",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        promptPolicy = "prompts/master_prompt*.md + prompts/short_prompt_*.md",
                        requestToAiWorker = snapshot,
                        dispatchState = "sent_to_aiworker",
                    },
                    cancellationToken: stoppingToken);

                ModeSignalContract? modeSignal;
                TradeSignalContract aiSignal;
                try
                {
                    modeSignal = await aiWorker.GetModeAsync(snapshot, stoppingToken);
                    aiSignal = await aiWorker.AnalyzeAsync(snapshot, cycleId, stoppingToken);
                }
                catch (Exception ex)
                {
                    await timeline.WriteAsync(
                        eventType: "AI_ANALYZE_FAILED",
                        stage: "ai",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            error = ex.Message,
                            errorType = ex.GetType().Name,
                        },
                        cancellationToken: stoppingToken);

                    logger.LogWarning(ex, "AI analyze call failed for cycle {CycleId}.", cycleId);
                    throw;
                }

                // Spec v8 §13: enrich snapshot with GeoRiskFlag from AI geo/event risk assessment
                var geoRiskFlag = !string.IsNullOrEmpty(aiSignal.GeoHeadline) && aiSignal.GeoHeadline != "NONE"
                    || aiSignal.EventRisk is "HIGH" or "MEDIUM";
                snapshot = snapshot with { GeoRiskFlag = geoRiskFlag };

                var aiTrace = ParseAiTrace(aiSignal.AiTraceJson);
                var aiRequest = TryGetTraceNode(aiTrace, "ai_request");
                var providerTraces = TryGetTraceNode(aiTrace, "provider_traces");
                var providerRequests = ExtractProviderTraceEntries(providerTraces, "AI_PROVIDER_REQUEST");
                var providerResponses = ExtractProviderTraceEntries(providerTraces, "AI_PROVIDER_RESPONSE");
                var aiUsed = ExtractAiUsed(providerTraces);

                await timeline.WriteAsync(
                    eventType: "AI_ANALYZE_RESPONSE",
                    stage: "ai",
                    source: "aiworker",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        aiSignal.Rail,
                        aiSignal.Entry,
                        aiSignal.Tp,
                        aiSignal.Confidence,
                        aiSignal.ConsensusPassed,
                        aiSignal.AgreementCount,
                        aiSignal.RequiredAgreement,
                        aiSignal.DisagreementReason,
                        aiSignal.ProviderVotes,
                        aiSignal.PromptRefs,
                        aiSignal.ProviderModels,
                        aiSignal.CycleId,
                        aiRequest,
                        aiUsed,
                        providerTraces,
                        exactAiResponses = providerResponses,
                        aiTrace,
                        aiSignal.Summary,
                    },
                    cancellationToken: stoppingToken);

                await EmitAiStageEventsAsync(timeline, snapshot.Symbol, cycleId, aiSignal, stoppingToken);

                // Detect when the aiworker used a deterministic fallback simulation (no live AI providers).
                // This happens when all configured AI analyzers fail or are unavailable. The fallback
                // signal is technically valid (consensusPassed=true) but does not come from a live AI model.
                var isFallbackSimulation = aiSignal.ProviderVotes is not null
                    && aiSignal.ProviderVotes.Any(v => v.StartsWith("fallback-sim:", StringComparison.OrdinalIgnoreCase));

                if (isFallbackSimulation)
                {
                    logger.LogWarning(
                        "AI_SIGNAL_FALLBACK_USED — deterministic fallback simulation used for cycle {CycleId}. " +
                        "Lead committee and full failover were both unavailable. " +
                        "Check aiworker provider configuration and API key health.",
                        cycleId);

                    await timeline.WriteAsync(
                        eventType: "AI_SIGNAL_FALLBACK_USED",
                        stage: "ai",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            reason = "All AI providers unavailable; deterministic fallback simulation used.",
                            providerVotes = aiSignal.ProviderVotes,
                            summary = aiSignal.Summary,
                            note = "LEAD_COMMITTEE: uses first configured analyzer. If it fails, FAILOVER tries all analyzers. " +
                                   "If all fail, FALLBACK SIMULATION generates a rule-based signal deterministically.",
                        },
                        cancellationToken: stoppingToken);
                }

                if (modeSignal is not null)
                {
                    aiSignal = aiSignal with
                    {
                        ModeHint = modeSignal.Mode,
                        ModeConfidence = modeSignal.Confidence,
                        ModeTtlSeconds = modeSignal.TtlSeconds,
                        ModeKeywords = modeSignal.Keywords,
                    };
                }
                if (!aiSignal.ConsensusPassed)
                {
                    var quorumReason = string.IsNullOrWhiteSpace(aiSignal.DisagreementReason)
                        ? "AI committee did not reach required agreement."
                        : aiSignal.DisagreementReason;
                    var votesSummary = aiSignal.ProviderVotes is null || aiSignal.ProviderVotes.Count == 0
                        ? "votes=none"
                        : $"votes={string.Join(';', aiSignal.ProviderVotes.Take(3))}";
                    var loggedReason = $"{quorumReason} (agree={aiSignal.AgreementCount}/{aiSignal.RequiredAgreement}; {votesSummary})";

                    db.DecisionLogs.Add(DecisionLog.Create(
                        symbol: snapshot.Symbol,
                        status: "NO_TRADE",
                        engineState: "CAPITAL_PROTECTED",
                        mode: "EXHAUSTION",
                        cause: "AI_QUORUM_FAILED",
                        waterfallRisk: "LOW",
                        telegramState: snapshot.TelegramState,
                        railPermissionA: "BLOCKED",
                        railPermissionB: "BLOCKED",
                        reason: loggedReason.Length > 390 ? loggedReason[..390] : loggedReason,
                        entry: 0m,
                        tp: 0m,
                        grams: 0m,
                        rotationCapThisSession: 0,
                        forceWhereToTrade: forceWhereToTrade,
                        snapshotHash: ComputeSnapshotHash(snapshot)));
                    await db.SaveChangesAsync(stoppingToken);

                    logger.LogInformation(
                        "NO_TRADE due to AI quorum failure. Agreement={Agreement}/{Required}. Reason={Reason}",
                        aiSignal.AgreementCount,
                        aiSignal.RequiredAgreement,
                        loggedReason);

                    await timeline.WriteAsync(
                        eventType: "AI_CONSENSUS_FAILED",
                        stage: "consensus",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            aiSignal.AgreementCount,
                            aiSignal.RequiredAgreement,
                            aiSignal.DisagreementReason,
                            aiSignal.ProviderVotes,
                            reason = loggedReason,
                        },
                        cancellationToken: stoppingToken);

                    await timeline.WriteAsync(
                        eventType: "FINAL_DECISION",
                        stage: "decision",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            finalDecision = "NO_TRADE",
                            primaryReason = "AI_QUORUM_FAILED",
                            reason = loggedReason,
                        },
                        cancellationToken: stoppingToken);

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                if (tradingViewStore.TryGetLatest(out var tv) && tv is not null)
                {
                    aiSignal = MergeTradingView(aiSignal, tv, snapshot, logger);
                }

                // ── Trade Scoring: ranks setup quality after all gates have passed ──
                // Runs only when rule engine is valid, news is clear, and AI consensus passed.
                var tradeScore = TradeScoreCalculator.Calculate(snapshot, setupCandidate, aiSignal);

                await timeline.WriteAsync(
                    eventType: "TRADE_SCORE_CALCULATION",
                    stage: "scoring",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        structureScore = tradeScore.StructureScore,
                        momentumScore = tradeScore.MomentumScore,
                        executionScore = tradeScore.ExecutionScore,
                        aiScore = tradeScore.AiScore,
                        sentimentScore = tradeScore.SentimentScore,
                        totalScore = tradeScore.TotalScore,
                        decisionTier = tradeScore.DecisionTier,
                        threshold = TradeScoreCalculator.NoTradeThreshold,
                    },
                    cancellationToken: stoppingToken);

                if (tradeScore.TotalScore < TradeScoreCalculator.NoTradeThreshold)
                {
                    db.DecisionLogs.Add(DecisionLog.Create(
                        symbol: snapshot.Symbol,
                        status: "NO_TRADE",
                        engineState: "CAPITAL_PROTECTED",
                        mode: "EXHAUSTION",
                        cause: "SCORE_BELOW_THRESHOLD",
                        waterfallRisk: "LOW",
                        telegramState: snapshot.TelegramState,
                        railPermissionA: "BLOCKED",
                        railPermissionB: "BLOCKED",
                        reason: $"Trade score {tradeScore.TotalScore} below threshold {TradeScoreCalculator.NoTradeThreshold}. Tier: {tradeScore.DecisionTier}",
                        entry: 0m,
                        tp: 0m,
                        grams: 0m,
                        rotationCapThisSession: 0,
                        forceWhereToTrade: forceWhereToTrade,
                        snapshotHash: ComputeSnapshotHash(snapshot)));
                    await db.SaveChangesAsync(stoppingToken);

                    logger.LogInformation(
                        "NO_TRADE due to low trade score. Score={Score} Threshold={Threshold} Tier={Tier}",
                        tradeScore.TotalScore,
                        TradeScoreCalculator.NoTradeThreshold,
                        tradeScore.DecisionTier);

                    await timeline.WriteAsync(
                        eventType: "FINAL_DECISION",
                        stage: "decision",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            finalDecision = "NO_TRADE",
                            primaryReason = "SCORE_BELOW_THRESHOLD",
                            score = tradeScore.TotalScore,
                            threshold = TradeScoreCalculator.NoTradeThreshold,
                        },
                        cancellationToken: stoppingToken);

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var activeStrategy = await db.StrategyProfiles
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync(stoppingToken)
                    ?? "Standard";

                var rawState = ledger.GetState();
                var reservedPendingAed = EstimateReservedPendingAed(pendingTrades.Snapshot());
                var state = new LedgerStateContract(
                    CashAed: rawState.CashAed,
                    GoldGrams: rawState.GoldGrams,
                    OpenExposurePercent: rawState.OpenExposurePercent,
                    DeployableCashAed: Math.Max(0m, rawState.DeployableCashAed - reservedPendingAed),
                    OpenBuyCount: rawState.OpenBuyCount);

                // ── CR11 Layer B: PRETABLE Risk Intelligence ─────────────────────────────
                // Run Impulse Exhaustion Guard, Liquidity Sweep Detector, and PRETABLE before
                // decision engine. PRETABLE BLOCK → no trade regardless of engine output.
                // Pattern Detector results are passed to PRETABLE as active gate (Section F).

                var impulseExhaustion = ImpulseExhaustionGuard.Evaluate(snapshot);
                var liquiditySweep = LiquiditySweepDetectorService.Detect(snapshot);
                var sessionForPretable = NormalizeSessionForPretable(snapshot.Session);
                var pretable = PretableService.Evaluate(snapshot, regime, impulseExhaustion, liquiditySweep, sessionForPretable, patterns);

                // CR11 CR11: Regime → TREND / RANGE / SHOCK for Rotation Optimizer
                var crRegime = MapToCr11Regime(setupCandidate.MarketRegime?.Regime ?? regime.Regime);

                // Dynamic Session Risk: get current session size modifier
                var dynamicSessionRiskResult = dynamicSessionRisk.GetModifier(snapshot.Session);

                await timeline.WriteAsync(
                    eventType: "CR11_PRETABLE_RESULT",
                    stage: "pretable",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        pretable.RiskLevel,
                        pretable.RiskScore,
                        pretable.RiskFlags,
                        pretable.SizeModifier,
                        pretable.Session,
                        pretableReason = pretable.Reason,
                        impulseExhaustionLevel = impulseExhaustion.Level,
                        impulseExhaustionFlags = impulseExhaustion.Flags,
                        impulseExhaustion.ImpulseDistancePoints,
                        impulseExhaustion.ImpulseDistanceAtr,
                        liquiditySweepConfirmed = liquiditySweep.IsConfirmed,
                        liquiditySweepReason = liquiditySweep.Reason,
                        crRegime,
                        dynamicSessionModifier = dynamicSessionRiskResult.Modifier,
                        dynamicSessionWaterfallCap = dynamicSessionRiskResult.WaterfallCapActive,
                        patternCount = patterns.Count,
                        patternTypes = patterns.Select(p => p.PatternType.ToString()).ToList(),
                    },
                    cancellationToken: stoppingToken);

                // PRETABLE BLOCK: stop pipeline — this is a Layer B hard block
                if (pretable.RiskLevel == "BLOCK")
                {
                    logger.LogInformation(
                        "CR11_PRETABLE_BLOCK — {Reason}",
                        pretable.Reason);

                    await timeline.WriteAsync(
                        eventType: "FINAL_DECISION",
                        stage: "decision",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            finalDecision = "NO_TRADE",
                            primaryReason = "CR11_PRETABLE_BLOCK",
                            reason = pretable.Reason,
                        },
                        cancellationToken: stoppingToken);

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                // Rotation Optimizer: decide execution mode
                var microRotationMode = runtimeSettings.GetMicroRotationEnabled();
                var rotationResult = RotationOptimizer.Optimize(snapshot, pretable, liquiditySweep, crRegime, state, microRotationMode);

                await timeline.WriteAsync(
                    eventType: "CR11_ROTATION_OPTIMIZER",
                    stage: "rotation",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        rotationResult.Mode,
                        rotationResult.CrRegime,
                        rotationResult.Reason,
                        staggeredLevels = rotationResult.StaggeredLevels,
                        // Spec v8 §11 — efficiency state
                        efficiencyState = rotationResult.EfficiencyState,
                        efficiencyScore = rotationResult.EfficiencyScore,
                    },
                    cancellationToken: stoppingToken);

                // Spec v8 §11: update lastEngineStore with efficiency state now that it's known
                {
                    var (currentEngineStates, currentPathRouting, currentSnap) = lastEngineStore.GetLast();
                    if (currentEngineStates != null)
                    {
                        var updatedEngineStates = currentEngineStates with { EfficiencyState = rotationResult.EfficiencyState };
                        lastEngineStore.SetLast(updatedEngineStates, currentPathRouting, currentSnap);
                    }
                }

                // CR11 STUDY_CANDIDATE_LOG: log full candidate context for STUDY analysis.
                // Every evaluated candidate (including blocked ones) must log all CR11 fields.
                await timeline.WriteAsync(
                    eventType: "CR11_STUDY_CANDIDATE_LOG",
                    stage: "study",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        // CR11 §LOGGING_FOR_STUDY required fields:
                        session = snapshot.Session,
                        sessionPhase = snapshot.SessionPhase,
                        crRegime,
                        structureValid = setupCandidate.IsValid,
                        pretableRiskLevel = pretable.RiskLevel,
                        pretableRiskScore = pretable.RiskScore,
                        pretableRiskFlags = pretable.RiskFlags,
                        pretableSizeModifier = pretable.SizeModifier,
                        liquiditySweepConfirmed = liquiditySweep.IsConfirmed,
                        impulseExhaustionLevel = impulseExhaustion.Level,
                        impulseExhaustionFlags = impulseExhaustion.Flags,
                        rotationMode = rotationResult.Mode,
                        dynamicSessionModifier = dynamicSessionRiskResult.Modifier,
                        dynamicSessionWaterfallCap = dynamicSessionRiskResult.WaterfallCapActive,
                        tradeScore = tradeScore.TotalScore,
                        tradeScoreTier = tradeScore.DecisionTier,
                        aiConfidence = aiSignal.Confidence,
                        aiConsensus = aiSignal.ConsensusPassed,
                        marketRegime = setupCandidate.MarketRegime?.Regime,
                        waterfallRisk = regime.IsWaterfall ? "HIGH" : (regime.RiskTag == "BLOCK" ? "MEDIUM" : "LOW"),
                        isFriday = snapshot.IsFriday,
                        adrUsedPct = snapshot.AdrUsedPct,
                        // §K Pattern Detector fields for STUDY + UI
                        patternCount = patterns.Count,
                        patternTypes = patterns.Select(p => p.PatternType.ToString()).ToList(),
                        patternWaterfallRisks = patterns.Select(p => p.WaterfallRisk).ToList(),
                        patternEntrySafeties = patterns.Select(p => p.EntrySafety).ToList(),
                        patternRecommendedActions = patterns.Select(p => p.RecommendedAction.ToString()).ToList(),
                    },
                    cancellationToken: stoppingToken);

                // STAND_DOWN from Rotation Optimizer (poor capital efficiency)
                if (rotationResult.Mode == "STAND_DOWN")
                {
                    logger.LogInformation(
                        "CR11_ROTATION_STANDDOWN — {Reason}",
                        rotationResult.Reason);

                    await timeline.WriteAsync(
                        eventType: "FINAL_DECISION",
                        stage: "decision",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            finalDecision = "NO_TRADE",
                            primaryReason = "CR11_STAND_DOWN",
                            reason = rotationResult.Reason,
                        },
                        cancellationToken: stoppingToken);

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                // Apply combined size modifier: PRETABLE × dynamic session risk
                var effectiveSizeModifier = pretable.SizeModifier * dynamicSessionRiskResult.Modifier;

                var minTradeGrams = runtimeSettings.GetMinTradeGrams();
                var decision = DecisionEngine.Evaluate(
                    snapshot,
                    regime,
                    aiSignal,
                    state,
                    activeStrategy,
                    minTradeGrams,
                    effectiveSizeModifier,
                    decisionStackResult.PathState,
                    decisionStackResult.PathRouting.PendingLimitPath);
                var snapshotHash = ComputeSnapshotHash(snapshot);

                // Spec v8 §14: compute entry levels (limit1, limit2, stop1) from rotation result and decision
                decimal limit1 = 0m, limit2 = 0m, stop1 = 0m;
                if (decision.Rail == "BUY_LIMIT")
                {
                    limit1 = decision.Entry;
                    // limit2 from staggered levels or path routing S2
                    var staggeredLevels = rotationResult.StaggeredLevels;
                    if (staggeredLevels?.Count >= 2)
                    {
                        var s2 = staggeredLevels.OrderBy(l => l.LevelIndex).Skip(1).FirstOrDefault();
                        limit2 = s2 != null ? Math.Round(decision.Entry + s2.EntryOffset, 2) : 0m;
                    }
                    else
                    {
                        var pathRouting = decisionStackResult.PathRouting?.PendingLimitPath;
                        limit2 = pathRouting?.S2SweepPocket ?? 0m;
                    }
                }
                else if (decision.Rail == "BUY_STOP")
                {
                    stop1 = decision.Entry;
                }

                // Spec v8 §14: enrich decision with entry levels and efficiency score
                decision = decision with
                {
                    Limit1 = limit1,
                    Limit2 = limit2,
                    Stop1 = stop1,
                    EfficiencyScore = rotationResult.EfficiencyScore,
                };

                await timeline.WriteAsync(
                    eventType: "DECISION_EVALUATED",
                    stage: "decision",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        decision.IsTradeAllowed,
                        decision.Status,
                        decision.EngineState,
                        decision.Cause,
                        decision.Reason,
                        decision.Mode,
                        decision.WaterfallRisk,
                        decision.Rail,
                        decision.Entry,
                        decision.Tp,
                        expiryUtc = decision.ExpiryUtc,
                        decision.MaxLifeSeconds,
                        decision.Grams,
                        effectiveSizeModifier,
                        decision.TelegramState,
                        snapshotHash,
                    },
                    cancellationToken: stoppingToken);

                LogTransitionsIfChanged(db, snapshot, decision, forceWhereToTrade, snapshotHash);

                if (modeSignal is not null)
                {
                    db.DecisionLogs.Add(DecisionLog.Create(
                        symbol: snapshot.Symbol,
                        status: "MODE_FEED",
                        engineState: decision.EngineState,
                        mode: modeSignal.Mode,
                        cause: "AI_MODE_FEED",
                        waterfallRisk: decision.WaterfallRisk,
                        telegramState: snapshot.TelegramState,
                        railPermissionA: decision.RailPermissionA,
                        railPermissionB: decision.RailPermissionB,
                        reason: $"mode={modeSignal.Mode};conf={modeSignal.Confidence:0.00};ttl={modeSignal.TtlSeconds};keywords={string.Join(',', modeSignal.Keywords.Take(5))}",
                        entry: 0m,
                        tp: 0m,
                        grams: 0m,
                        rotationCapThisSession: 0,
                        forceWhereToTrade: forceWhereToTrade,
                        snapshotHash: snapshotHash));
                }

                if (!decision.IsTradeAllowed)
                {
                    if (decision.WaterfallRisk == "HIGH")
                    {
                        var canceled = pendingTrades.Clear();
                        mt5Control.RequestCancelPending("waterfall_high");
                        if (canceled > 0)
                        {
                            logger.LogInformation("Canceled {Count} pending orders due to HIGH waterfall veto.", canceled);
                        }

                        // Track consecutive waterfall failures for study lock (PRD point 4)
                        lock (_waterfallGate)
                        {
                            _consecutiveWaterfallFailures++;
                            if (_consecutiveWaterfallFailures >= StudyLockWaterfallThreshold)
                            {
                                _studyLockExpiry = DateTimeOffset.UtcNow + StudyLockDuration;
                                logger.LogWarning(
                                    "STUDY_LOCK_TRIGGERED — {Count} consecutive HIGH waterfall failures. " +
                                    "System locked for {DurationMinutes}m. Perform STUDY & SELF_CROSSCHECK.",
                                    _consecutiveWaterfallFailures,
                                    StudyLockDuration.TotalMinutes);
                            }
                        }

                        // Capture the failure reason for the upcoming study refinement context.
                        lock (_studyGate)
                        {
                            _recentWaterfallReasons.Add(
                                $"{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss}|{decision.Cause}|{decision.Reason}");
                            if (_recentWaterfallReasons.Count > MaxRecentWaterfallReasons)
                            {
                                _recentWaterfallReasons.RemoveAt(0);
                            }
                        }
                    }

                    db.DecisionLogs.Add(DecisionLog.Create(
                        symbol: snapshot.Symbol,
                        status: decision.Status,
                        engineState: decision.EngineState,
                        mode: decision.Mode,
                        cause: decision.Cause,
                        waterfallRisk: decision.WaterfallRisk,
                        telegramState: decision.TelegramState,
                        railPermissionA: decision.RailPermissionA,
                        railPermissionB: decision.RailPermissionB,
                        reason: decision.Reason,
                        entry: decision.Entry,
                        tp: decision.Tp,
                        grams: decision.Grams,
                        rotationCapThisSession: decision.RotationCapThisSession,
                        forceWhereToTrade: forceWhereToTrade,
                        snapshotHash: snapshotHash));
                    await db.SaveChangesAsync(stoppingToken);

                    logger.LogInformation(
                        "{Status} ({EngineState}) cause={Cause} waterfall={WaterfallRisk}. Reason={Reason}",
                        decision.Status,
                        decision.EngineState,
                        decision.Cause,
                        decision.WaterfallRisk,
                        decision.Reason);

                    await timeline.WriteAsync(
                        eventType: "FINAL_DECISION",
                        stage: "decision",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            finalDecision = "NO_TRADE",
                            primaryReason = decision.Cause,
                            reason = decision.Reason,
                            engineState = decision.EngineState,
                            waterfallRisk = decision.WaterfallRisk,
                        },
                        cancellationToken: stoppingToken);

                    // BLOCKED_VALID_SETUP_CANDIDATE (CR8): when a setup passed scoring but was
                    // blocked by the final bottom-permission gate, tag it as a study candidate.
                    // STUDY module uses these to determine if the permission rule is too strict.
                    if (string.Equals(decision.Cause, "BOTTOMPERMISSION_FALSE", StringComparison.Ordinal))
                    {
                        logger.LogInformation(
                            "BLOCKED_VALID_SETUP_CANDIDATE — setup passed scoring (score>={Threshold}) but blocked by BottomPermission. Queued for STUDY.",
                            TradeScoreCalculator.NoTradeThreshold);

                        await timeline.WriteAsync(
                            eventType: "BLOCKED_VALID_SETUP_CANDIDATE",
                            stage: "study",
                            source: "brain",
                            symbol: snapshot.Symbol,
                            cycleId: cycleId,
                            tradeId: null,
                            payload: new
                            {
                                cause = decision.Cause,
                                bottomPermissionReason = decision.Reason,
                                tradeScore = tradeScore.TotalScore,
                                session = snapshot.Session,
                                sessionPhase = snapshot.SessionPhase,
                                waterfallRisk = decision.WaterfallRisk,
                                note = "Study candidate: passed scoring but blocked by BottomPermission. " +
                                       "STUDY should determine if block saved from waterfall or if rule is too strict.",
                            },
                            cancellationToken: stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var hazardWindows = await db.HazardWindows
                    .AsNoTracking()
                    .Where(x => x.IsActive && x.IsBlocked && x.EndUtc >= snapshot.Timestamp)
                    .ToListAsync(stoppingToken);

                var intersectsHazard = hazardWindows.Any(x => x.StartUtc <= decision.ExpiryUtc && x.EndUtc >= snapshot.Timestamp);
                if (intersectsHazard)
                {
                    var canceled = pendingTrades.Clear();
                    mt5Control.RequestCancelPending("hazard_window_intersection");
                    logger.LogInformation(
                        "NO_TRADE due to hazard-window intersection. PendingCanceled={CanceledCount}",
                        canceled);

                    db.DecisionLogs.Add(DecisionLog.Create(
                        symbol: snapshot.Symbol,
                        status: "NO_TRADE",
                        engineState: "CAPITAL_PROTECTED",
                        mode: decision.Mode,
                        cause: decision.Cause,
                        waterfallRisk: decision.WaterfallRisk,
                        telegramState: decision.TelegramState,
                        railPermissionA: decision.RailPermissionA,
                        railPermissionB: decision.RailPermissionB,
                        reason: "Expiry intersects active blocked hazard window.",
                        entry: 0m,
                        tp: 0m,
                        grams: 0m,
                        rotationCapThisSession: 0,
                        forceWhereToTrade: forceWhereToTrade,
                        snapshotHash: snapshotHash));
                    await db.SaveChangesAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var currentPrice = snapshot.TimeframeData
                    .FirstOrDefault(x => string.Equals(x.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.Close
                    ?? snapshot.TimeframeData.First().Close;

                if (!ledger.CanScaleIn(currentPrice, regime, minSpacingPercent: 0.0025m, exposureCapPercent: 65m))
                {
                    logger.LogInformation(
                        "Scale-in blocked by exposure/spacing/risk checks. Symbol={Symbol}, Regime={Regime}",
                        snapshot.Symbol,
                        regime.Regime);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                if (string.Equals(activeStrategy, "WarPremium", StringComparison.OrdinalIgnoreCase)
                    && decision.Rail == "BUY_STOP"
                    && pendingTrades.Count() > 0)
                {
                    logger.LogInformation("WarPremium Rail-B skipped: one pending BUY_STOP already exists.");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                // §D MICRO_ROTATION_MODE: enforce single active pending trade cap.
                // While micro rotation is active only one pending trade is allowed at a time.
                if (microRotationMode && pendingTrades.Count() > 0)
                {
                    logger.LogInformation(
                        "MICRO_ROTATION_MODE — single pending trade cap reached, skipping new order.");

                    await timeline.WriteAsync(
                        eventType: "FINAL_DECISION",
                        stage: "decision",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            finalDecision = "NO_TRADE",
                            primaryReason = "MICRO_ROTATION_MODE_SINGLE_TRADE_CAP",
                            reason = "Micro rotation mode: one pending trade already active.",
                        },
                        cancellationToken: stoppingToken);

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var projectedReservedAed = reservedPendingAed + EstimateOrderReserveAed(decision.Entry, decision.Grams);
                if (projectedReservedAed > rawState.CashAed)
                {
                    logger.LogInformation(
                        "NO_TRADE due to projected reserve breach. Reserved={Reserved} Cash={Cash}",
                        projectedReservedAed,
                        rawState.CashAed);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                // ── CR10: Capital Utilization Gate ─────────────────────────────────────────
                // Immutable execution gate: only orders approved by this check may proceed to MT5.
                // Resizes orders that exceed available capital; rejects when cash is insufficient.
                var mt5BuyPrice = snapshot.AuthoritativeRate > 0m
                    ? snapshot.AuthoritativeRate
                    : snapshot.Ask > 0m ? snapshot.Ask : decision.Entry;
                var capitalCheck = CapitalUtilizationService.Check(rawState.CashAed, mt5BuyPrice, decision.Grams);

                logger.LogInformation(
                    "CAPITAL_GATE Cash={CashAed} Price={Price} Attempted={Attempted}g MaxLegal={MaxLegal}g Required={Required}AED Allowed={Allowed}AED Status={Status}",
                    capitalCheck.CashAed, capitalCheck.Mt5BuyPriceUsd, capitalCheck.AttemptedGrams,
                    capitalCheck.MaxLegalGrams, capitalCheck.RequiredAed, capitalCheck.AllowedCapitalAed,
                    capitalCheck.OrderStatus);

                await timeline.WriteAsync(
                    eventType: "CAPITAL_UTILIZATION_CHECK",
                    stage: "capital_gate",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        capitalCheck.OrderStatus,
                        capitalCheck.ApprovedByCapacityGate,
                        capitalCheck.AttemptedGrams,
                        capitalCheck.ApprovedGrams,
                        capitalCheck.MaxLegalGrams,
                        capitalCheck.RequiredAed,
                        capitalCheck.AllowedCapitalAed,
                        capitalCheck.AedPerGram,
                        capitalCheck.ShopBuyUsd,
                        cashAed = capitalCheck.CashAed,
                        mt5BuyPriceUsd = capitalCheck.Mt5BuyPriceUsd,
                    },
                    cancellationToken: stoppingToken);

                if (!capitalCheck.ApprovedByCapacityGate)
                {
                    logger.LogWarning(
                        "CAPITAL_GATE REJECTED — insufficient cash. Cash={CashAed} MaxLegal={MaxLegal}g Price={Price}",
                        rawState.CashAed, capitalCheck.MaxLegalGrams, mt5BuyPrice);

                    db.DecisionLogs.Add(DecisionLog.Create(
                        symbol: snapshot.Symbol,
                        status: "NO_TRADE",
                        engineState: "CAPITAL_PROTECTED",
                        mode: decision.Mode,
                        cause: "CAPITAL_GATE_REJECTED",
                        waterfallRisk: decision.WaterfallRisk,
                        telegramState: decision.TelegramState,
                        railPermissionA: decision.RailPermissionA,
                        railPermissionB: decision.RailPermissionB,
                        reason: $"CR10 capital gate rejected order. MaxLegalGrams={capitalCheck.MaxLegalGrams:0.00}, Cash={rawState.CashAed:0.00}AED",
                        entry: 0m,
                        tp: 0m,
                        grams: 0m,
                        rotationCapThisSession: 0,
                        forceWhereToTrade: forceWhereToTrade,
                        snapshotHash: snapshotHash));
                    await db.SaveChangesAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                // Use the capital-gate approved gram quantity (may be resized down from decision.Grams)
                var approvedGrams = capitalCheck.ApprovedGrams;
                if (capitalCheck.OrderStatus == "RESIZE_REQUIRED")
                {
                    logger.LogInformation(
                        "CAPITAL_GATE RESIZE — order resized from {Original}g to {Approved}g",
                        decision.Grams, approvedGrams);
                }

                // ── Portfolio Exposure Gate ────────────────────────────────────────────────
                // Rejects new orders when total open position grams + proposed grams exceeds
                // 25% of account equity expressed in gold grams. Prevents position stacking
                // even when individual orders pass the capital utilization gate (CR10).
                //
                // maxSymbolExposureGrams = (accountEquityUsd * 0.25) / goldPricePerGram
                // where goldPricePerGram = authoritativeRate / 31.1035
                var accountEquityUsd = snapshot.Equity;
                var authoritativeRate = snapshot.AuthoritativeRate;
                if (accountEquityUsd <= 0m || authoritativeRate <= 0m)
                {
                    var rejectionReason =
                        $"EXPOSURE_GATE_DATA_INVALID accountEquityUsd={accountEquityUsd:0.00} authoritativeRate={authoritativeRate:0.0000}";

                    logger.LogWarning(
                        "EXPOSURE_GATE_DATA_INVALID — rejecting trade because required exposure inputs are invalid. AccountEquityUsd={EquityUsd} AuthoritativeRate={AuthoritativeRate}",
                        accountEquityUsd,
                        authoritativeRate);

                    await timeline.WriteAsync(
                        eventType: "SYMBOL_EXPOSURE_REJECTED",
                        stage: "exposure_gate",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            orderStatus = "REJECTED",
                            code = "EXPOSURE_GATE_DATA_INVALID",
                            accountEquityUsd,
                            authoritativeRate,
                            rejectionReason,
                        },
                        cancellationToken: stoppingToken);

                    db.DecisionLogs.Add(DecisionLog.Create(
                        symbol: snapshot.Symbol,
                        status: "NO_TRADE",
                        engineState: "EXPOSURE_PROTECTED",
                        mode: decision.Mode,
                        cause: "SYMBOL_EXPOSURE_GATE_DATA_INVALID",
                        waterfallRisk: decision.WaterfallRisk,
                        telegramState: decision.TelegramState,
                        railPermissionA: decision.RailPermissionA,
                        railPermissionB: decision.RailPermissionB,
                        reason: rejectionReason,
                        entry: 0m,
                        tp: 0m,
                        grams: 0m,
                        rotationCapThisSession: 0,
                        forceWhereToTrade: forceWhereToTrade,
                        snapshotHash: snapshotHash));
                    await db.SaveChangesAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var goldPricePerGram = authoritativeRate / OunceToGram;
                var maxSymbolExposureGrams = (accountEquityUsd * 0.25m) / goldPricePerGram;

                var openPositionGrams = snapshot.OpenPositions?.Sum(p => p.VolumeGramsEquivalent) ?? 0m;
                var totalProjectedExposure = openPositionGrams + approvedGrams;
                if (totalProjectedExposure > maxSymbolExposureGrams)
                {
                    var rejectionReason = $"Exposure gate rejected order. OpenPositionGrams={openPositionGrams:0.00}g + ApprovedGrams={approvedGrams:0.00}g = {totalProjectedExposure:0.00}g exceeds MaxSymbolExposureGrams={maxSymbolExposureGrams:0.00}g (25% of equity {accountEquityUsd:0.00} USD at {goldPricePerGram:0.0000} USD/g)";

                    logger.LogWarning(
                        "EXPOSURE_GATE REJECTED — total projected exposure {Total}g exceeds max {Max}g. Open={Open}g Proposed={Proposed}g AccountEquityUsd={EquityUsd} GoldPricePerGram={GoldPrice}",
                        totalProjectedExposure, maxSymbolExposureGrams, openPositionGrams, approvedGrams, accountEquityUsd, goldPricePerGram);

                    await timeline.WriteAsync(
                        eventType: "SYMBOL_EXPOSURE_REJECTED",
                        stage: "exposure_gate",
                        source: "brain",
                        symbol: snapshot.Symbol,
                        cycleId: cycleId,
                        tradeId: null,
                        payload: new
                        {
                            orderStatus = "REJECTED",
                            openPositionGrams,
                            proposedGrams = approvedGrams,
                            totalProjectedExposure,
                            maxSymbolExposureGrams,
                            rejectionReason,
                        },
                        cancellationToken: stoppingToken);

                    db.DecisionLogs.Add(DecisionLog.Create(
                        symbol: snapshot.Symbol,
                        status: "NO_TRADE",
                        engineState: "EXPOSURE_PROTECTED",
                        mode: decision.Mode,
                        cause: "SYMBOL_EXPOSURE_GATE_REJECTED",
                        waterfallRisk: decision.WaterfallRisk,
                        telegramState: decision.TelegramState,
                        railPermissionA: decision.RailPermissionA,
                        railPermissionB: decision.RailPermissionB,
                        reason: rejectionReason,
                        entry: 0m,
                        tp: 0m,
                        grams: 0m,
                        rotationCapThisSession: 0,
                        forceWhereToTrade: forceWhereToTrade,
                        snapshotHash: snapshotHash));
                    await db.SaveChangesAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var pending = new PendingTradeContract(
                    Id: Guid.NewGuid(),
                    Symbol: snapshot.Symbol,
                    Type: decision.Rail,
                    Price: decision.Entry,
                    Tp: decision.Tp,
                    Expiry: decision.ExpiryUtc,
                    Ml: decision.MaxLifeSeconds,
                    Grams: approvedGrams,
                    AlignmentScore: decision.AlignmentScore,
                    Regime: regime.Regime,
                    RiskTag: regime.RiskTag,
                    EngineState: decision.EngineState,
                    Mode: decision.Mode,
                    Cause: decision.Cause,
                    WaterfallRisk: decision.WaterfallRisk,
                    Bucket: decision.Bucket,
                    Session: decision.Session,
                    SessionPhase: decision.SessionPhase,
                    RegimeTag: decision.RegimeTag,
                    RiskState: decision.RiskState,
                    SizeClass: decision.SizeClass,
                    TelegramState: decision.TelegramState,
                    ConsensusPassed: aiSignal.ConsensusPassed,
                    AgreementCount: aiSignal.AgreementCount,
                    RequiredAgreement: aiSignal.RequiredAgreement,
                    DisagreementReason: aiSignal.DisagreementReason,
                    ProviderVotes: aiSignal.ProviderVotes,
                    Summary: aiSignal.Summary,
                    ModeHint: aiSignal.ModeHint,
                    ModeConfidence: aiSignal.ModeConfidence,
                    CycleId: cycleId,
                    ShopBuy: decision.ShopBuy,
                    ShopSell: decision.ShopSell,
                    ExpiryKSA: decision.ExpiryKSA,
                    ExpiryServer: decision.ExpiryServer);

                var executionMode = ResolveExecutionMode(configuration["Execution:Mode"]);
                var hybridSessions = ResolveHybridAutoSessions(configuration["Execution:HybridAutoSessions"]);
                var autoTradeEnabled = runtimeSettings.GetAutoTradeEnabled();
                // Auto Trade toggle (CR7): when disabled (default OFF), all ARMED trades go to approval queue
                // regardless of execution mode, preventing unintended automated execution.
                var directToMt5 = autoTradeEnabled && ShouldQueueToMt5(executionMode, pending.Session, hybridSessions);

                if (directToMt5)
                {
                    pendingTrades.Enqueue(pending);
                }
                else
                {
                    approvals.Enqueue(pending);
                }

                _lastArmedAtUtc = DateTimeOffset.UtcNow;

                // Reset waterfall failure counter on successful trade arm (PRD point 4)
                lock (_waterfallGate)
                {
                    _consecutiveWaterfallFailures = 0;
                }

                db.DecisionLogs.Add(DecisionLog.Create(
                    symbol: snapshot.Symbol,
                    status: decision.Status,
                    engineState: decision.EngineState,
                    mode: decision.Mode,
                    cause: decision.Cause,
                    waterfallRisk: decision.WaterfallRisk,
                    telegramState: decision.TelegramState,
                    railPermissionA: decision.RailPermissionA,
                    railPermissionB: decision.RailPermissionB,
                    reason: decision.Reason,
                    entry: decision.Entry,
                    tp: decision.Tp,
                    grams: decision.Grams,
                    rotationCapThisSession: decision.RotationCapThisSession,
                    forceWhereToTrade: forceWhereToTrade,
                    snapshotHash: snapshotHash));
                await db.SaveChangesAsync(stoppingToken);

                await timeline.WriteAsync(
                    eventType: "TRADE_ROUTED",
                    stage: "routing",
                    source: "brain",
                    symbol: pending.Symbol,
                    cycleId: cycleId,
                    tradeId: pending.Id.ToString(),
                    payload: new
                    {
                        pending.Id,
                        pending.CycleId,
                        pending.Type,
                        pending.Price,
                        pending.Tp,
                        pending.Expiry,
                        pending.Grams,
                        pending.Session,
                        pending.SessionPhase,
                        pending.EngineState,
                        pending.Cause,
                        route = directToMt5 ? "MT5_PENDING" : "APPROVAL_QUEUE",
                        executionMode = executionMode.ToString().ToUpperInvariant(),
                        autoTradeEnabled,
                    },
                    cancellationToken: stoppingToken);

                await timeline.WriteAsync(
                    eventType: "FINAL_DECISION",
                    stage: "decision",
                    source: "brain",
                    symbol: pending.Symbol,
                    cycleId: cycleId,
                    tradeId: pending.Id.ToString(),
                    payload: new
                    {
                        finalDecision = "TRADE_APPROVED",
                        entry = pending.Price,
                        takeProfit = pending.Tp,
                        rail = pending.Type,
                        grams = pending.Grams,
                        route = directToMt5 ? "MT5_PENDING" : "APPROVAL_QUEUE",
                        // Spec v8 §14 — entry levels and efficiency score
                        limit1 = decision.Limit1,
                        limit2 = decision.Limit2,
                        stop1 = decision.Stop1,
                        efficiencyScore = decision.EfficiencyScore,
                    },
                    cancellationToken: stoppingToken);

                logger.LogInformation(
                    "Trade {TradeId} {Type} {Symbol} @ {Price} TP={Tp} grams={Grams} state={State} cause={Cause} session={Session} score={Score:0.00} routed={Route} mode={Mode}",
                    pending.Id,
                    pending.Type,
                    pending.Symbol,
                    pending.Price,
                    pending.Tp,
                    pending.Grams,
                    pending.EngineState,
                    pending.Cause,
                    pending.Session,
                        pending.AlignmentScore,
                        directToMt5 ? "MT5_PENDING" : "APPROVAL_QUEUE",
                        executionMode.ToString().ToUpperInvariant());
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No MT5 snapshot available yet.", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Waiting for first MT5 snapshot before running signal loop.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Signal polling iteration failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private static bool ShouldForceWhereToTrade(MarketSnapshotContract snapshot, RegimeClassificationContract regime)
    {
        if (regime.IsBlocked || regime.IsWaterfall)
        {
            return false;
        }

        var session = (snapshot.Session ?? string.Empty).Trim().ToUpperInvariant();
        if (session is not ("JAPAN" or "INDIA" or "LONDON" or "NY" or "ASIA" or "EUROPE" or "NEW_YORK"))
        {
            return false;
        }

        if (snapshot.IsUsRiskWindow && snapshot.TelegramImpactTag == "HIGH")
        {
            return false;
        }

        return DateTimeOffset.UtcNow - _lastArmedAtUtc >= TimeSpan.FromMinutes(25);
    }

    private static object? ParseAiTrace(string? traceJson)
    {
        if (string.IsNullOrWhiteSpace(traceJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<object>(traceJson);
        }
        catch
        {
            return new { raw = traceJson };
        }
    }

    private static object? TryGetTraceNode(object? traceObject, string propertyName)
    {
        if (traceObject is null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(traceObject);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(propertyName, out var node))
            {
                return JsonSerializer.Deserialize<object>(node.GetRawText());
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static IReadOnlyCollection<object> ExtractProviderTraceEntries(object? providerTraces, string eventType)
    {
        if (providerTraces is null)
        {
            return [];
        }

        try
        {
            var json = JsonSerializer.Serialize(providerTraces);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<object>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("eventType", out var eventTypeElement))
                {
                    continue;
                }

                if (!string.Equals(eventTypeElement.GetString(), eventType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parsed = JsonSerializer.Deserialize<object>(item.GetRawText());
                if (parsed is not null)
                {
                    list.Add(parsed);
                }
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyCollection<object> ExtractAiUsed(object? providerTraces)
    {
        var requestEntries = ExtractProviderTraceEntries(providerTraces, "AI_PROVIDER_REQUEST");
        var used = new List<object>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in requestEntries)
        {
            try
            {
                var json = JsonSerializer.Serialize(entry);
                using var doc = JsonDocument.Parse(json);
                var analyzer = doc.RootElement.TryGetProperty("analyzer", out var analyzerElement)
                    ? analyzerElement.GetString() ?? string.Empty
                    : string.Empty;
                var provider = doc.RootElement.TryGetProperty("provider", out var providerElement)
                    ? providerElement.GetString() ?? string.Empty
                    : string.Empty;
                var model = doc.RootElement.TryGetProperty("model", out var modelElement)
                    ? modelElement.GetString() ?? string.Empty
                    : string.Empty;

                var key = $"{analyzer}|{provider}|{model}";
                if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                {
                    continue;
                }

                used.Add(new
                {
                    analyzer,
                    provider,
                    model,
                });
            }
            catch
            {
                // Ignore malformed traces.
            }
        }

        return used;
    }

    private async Task EmitAiStageEventsAsync(
        IRuntimeTimelineWriter timeline,
        string symbol,
        string cycleId,
        TradeSignalContract aiSignal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aiSignal.AiTraceJson))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(aiSignal.AiTraceJson);
            if (!doc.RootElement.TryGetProperty("events", out var eventsElement)
                || eventsElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in eventsElement.EnumerateArray())
            {
                var eventType = item.TryGetProperty("eventType", out var eventTypeElement)
                    ? eventTypeElement.GetString()
                    : "AI_STAGE_EVENT";
                var stage = item.TryGetProperty("stage", out var stageElement)
                    ? stageElement.GetString()
                    : "ai_stage";
                var source = item.TryGetProperty("source", out var sourceElement)
                    ? sourceElement.GetString()
                    : "aiworker";

                object payload = new { raw = item.GetRawText() };
                if (item.TryGetProperty("payload", out var payloadElement))
                {
                    payload = JsonSerializer.Deserialize<object>(payloadElement.GetRawText())
                        ?? new { raw = payloadElement.GetRawText() };
                }

                await timeline.WriteAsync(
                    eventType: string.IsNullOrWhiteSpace(eventType) ? "AI_STAGE_EVENT" : eventType,
                    stage: string.IsNullOrWhiteSpace(stage) ? "ai_stage" : stage,
                    source: string.IsNullOrWhiteSpace(source) ? "aiworker" : source,
                    symbol: symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: payload,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to emit AI stage subevents for cycle {CycleId}", cycleId);
        }
    }

    private static ExecutionMode ResolveExecutionMode(string? value)
    {
        var normalized = (value ?? "AUTO").Trim().ToUpperInvariant();
        return normalized switch
        {
            "AUTO" => ExecutionMode.Auto,
            "HYBRID" => ExecutionMode.Hybrid,
            "MANUAL" => ExecutionMode.Manual,
            _ => ExecutionMode.Auto,
        };
    }

    private static HashSet<string> ResolveHybridAutoSessions(string? value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "JAPAN,INDIA" : value;
        var sessions = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (sessions.Count == 0)
        {
            sessions.Add("JAPAN");
            sessions.Add("INDIA");
        }

        return sessions;
    }

    private static bool ShouldQueueToMt5(ExecutionMode mode, string session, HashSet<string> hybridAutoSessions)
    {
        if (mode == ExecutionMode.Auto)
        {
            return true;
        }

        if (mode == ExecutionMode.Manual)
        {
            return false;
        }

        var normalizedSession = (session ?? string.Empty).Trim().ToUpperInvariant();
        return hybridAutoSessions.Contains(normalizedSession);
    }

    private static SessionType MapSessionType(string? snapshotSession)
    {
        var value = (snapshotSession ?? string.Empty).Trim().ToUpperInvariant();
        return value switch
        {
            "JAPAN" => SessionType.Japan,
            "INDIA" => SessionType.India,
            "ASIA" => SessionType.Japan,
            "LONDON" => SessionType.London,
            "EUROPE" => SessionType.London,
            "NY" => SessionType.NewYork,
            "NEW_YORK" => SessionType.NewYork,
            _ => SessionType.OffHours,
        };
    }

    private static string ComputeSnapshotHash(MarketSnapshotContract snapshot)
    {
        var payload = string.Join('|',
            snapshot.Symbol,
            snapshot.Timestamp.ToUnixTimeSeconds(),
            snapshot.Session,
            snapshot.Bid,
            snapshot.Ask,
            snapshot.Spread,
            snapshot.Atr,
            snapshot.Adr,
            snapshot.TelegramState,
            snapshot.TvAlertType);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static decimal EstimateReservedPendingAed(IReadOnlyCollection<PendingTradeContract> pending)
    {
        if (pending.Count == 0)
        {
            return 0m;
        }

        return pending.Sum(item => EstimateOrderReserveAed(item.Price, item.Grams));
    }

    private static decimal EstimateOrderReserveAed(decimal mt5EntryUsdPerOunce, decimal grams)
    {
        if (mt5EntryUsdPerOunce <= 0m || grams <= 0m)
        {
            return 0m;
        }

        var shopBuy = mt5EntryUsdPerOunce + ShopSpreadUsdPerOz;
        var usdPerGram = shopBuy / OunceToGram;
        var aedPerGram = usdPerGram * UsdToAed;
        return grams * aedPerGram;
    }

    private static void LogTransitionsIfChanged(
        IApplicationDbContext db,
        MarketSnapshotContract snapshot,
        DecisionResultContract decision,
        bool forceWhereToTrade,
        string snapshotHash)
    {
        lock (_transitionGate)
        {
            if (!string.Equals(_lastMode, decision.Mode, StringComparison.Ordinal))
            {
                db.DecisionLogs.Add(DecisionLog.Create(
                    symbol: snapshot.Symbol,
                    status: "STATE_TRANSITION",
                    engineState: decision.EngineState,
                    mode: decision.Mode,
                    cause: "MODE_CHANGE",
                    waterfallRisk: decision.WaterfallRisk,
                    telegramState: decision.TelegramState,
                    railPermissionA: decision.RailPermissionA,
                    railPermissionB: decision.RailPermissionB,
                    reason: $"mode_transition:{_lastMode}->{decision.Mode}",
                    entry: 0m,
                    tp: 0m,
                    grams: 0m,
                    rotationCapThisSession: 0,
                    forceWhereToTrade: forceWhereToTrade,
                    snapshotHash: snapshotHash));
                _lastMode = decision.Mode;
            }

            if (!string.Equals(_lastWaterfallRisk, decision.WaterfallRisk, StringComparison.Ordinal))
            {
                db.DecisionLogs.Add(DecisionLog.Create(
                    symbol: snapshot.Symbol,
                    status: "STATE_TRANSITION",
                    engineState: decision.EngineState,
                    mode: decision.Mode,
                    cause: "WATERFALL_CHANGE",
                    waterfallRisk: decision.WaterfallRisk,
                    telegramState: decision.TelegramState,
                    railPermissionA: decision.RailPermissionA,
                    railPermissionB: decision.RailPermissionB,
                    reason: $"waterfall_transition:{_lastWaterfallRisk}->{decision.WaterfallRisk}",
                    entry: 0m,
                    tp: 0m,
                    grams: 0m,
                    rotationCapThisSession: 0,
                    forceWhereToTrade: forceWhereToTrade,
                    snapshotHash: snapshotHash));
                _lastWaterfallRisk = decision.WaterfallRisk;
            }
        }
    }

    private static TradeSignalContract MergeTradingView(
        TradeSignalContract aiSignal,
        TradingViewSignalContract tradingView,
        MarketSnapshotContract snapshot,
        ILogger logger)
    {
        if (!string.Equals(tradingView.Symbol, snapshot.Symbol, StringComparison.OrdinalIgnoreCase))
        {
            return aiSignal;
        }

        var age = DateTimeOffset.UtcNow - tradingView.Timestamp;
        if (age > TimeSpan.FromHours(8))
        {
            return aiSignal;
        }

        var mergedScore = aiSignal.AlignmentScore;
        var mergedSafety = aiSignal.SafetyTag;
        var mergedBias = aiSignal.DirectionBias;

        if (string.Equals(tradingView.RiskTag, "BLOCK", StringComparison.OrdinalIgnoreCase))
        {
            mergedSafety = "BLOCK";
            mergedScore -= 0.35m;
        }
        else if (string.Equals(tradingView.RiskTag, "CAUTION", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(mergedSafety, "BLOCK", StringComparison.OrdinalIgnoreCase))
            {
                mergedSafety = "CAUTION";
            }
            mergedScore -= 0.10m;
        }

        if (string.Equals(tradingView.Bias, "BULLISH", StringComparison.OrdinalIgnoreCase))
        {
            mergedBias = "BULLISH";
            mergedScore += 0.05m;
        }
        else if (string.Equals(tradingView.Bias, "BEARISH", StringComparison.OrdinalIgnoreCase))
        {
            mergedBias = "BEARISH";
            mergedScore -= 0.16m;
            if (!string.Equals(mergedSafety, "BLOCK", StringComparison.OrdinalIgnoreCase))
            {
                mergedSafety = "CAUTION";
            }
        }

        if (tradingView.Signal.Contains("SELL", StringComparison.OrdinalIgnoreCase))
        {
            mergedScore -= 0.10m;
        }
        else if (tradingView.Signal.Contains("BUY", StringComparison.OrdinalIgnoreCase))
        {
            mergedScore += 0.04m;
        }

        if (string.Equals(tradingView.ConfirmationTag, "CONFIRM", StringComparison.OrdinalIgnoreCase))
        {
            mergedScore += 0.08m;
        }
        else if (string.Equals(tradingView.ConfirmationTag, "CONTRADICT", StringComparison.OrdinalIgnoreCase))
        {
            mergedScore -= 0.18m;
            if (!string.Equals(mergedSafety, "BLOCK", StringComparison.OrdinalIgnoreCase))
            {
                mergedSafety = "CAUTION";
            }
        }

        mergedScore = Math.Clamp(mergedScore, 0m, 1m);
        var mergedTags = (aiSignal.NewsTags ?? [])
            .Concat([
                $"tradingview_signal_{tradingView.Signal.ToLowerInvariant()}",
                $"tradingview_confirmation_{tradingView.ConfirmationTag.ToLowerInvariant()}",
                $"tradingview_risk_{tradingView.RiskTag.ToLowerInvariant()}",
                $"tradingview_bias_{tradingView.Bias.ToLowerInvariant()}"
            ])
            .Distinct()
            .ToArray();

        logger.LogInformation(
            "Merged TradingView into AI signal: signal={Signal}, bias={Bias}, risk={RiskTag}, score={Score:0.00}",
            tradingView.Signal,
            tradingView.Bias,
            tradingView.RiskTag,
            mergedScore);

        return aiSignal with
        {
            AlignmentScore = mergedScore,
            SafetyTag = mergedSafety,
            DirectionBias = mergedBias,
            TvConfirmationTag = tradingView.ConfirmationTag,
            NewsTags = mergedTags,
            Summary = string.IsNullOrWhiteSpace(aiSignal.Summary)
                ? $"TV:{tradingView.Signal}/{tradingView.Bias}/{tradingView.RiskTag}"
                : $"{aiSignal.Summary} | TV:{tradingView.Signal}/{tradingView.Bias}/{tradingView.RiskTag}",
        };
    }

    /// <summary>
    /// CR11: Normalizes session name for the PRETABLE service.
    /// </summary>
    private static string NormalizeSessionForPretable(string? session)
    {
        var s = (session ?? string.Empty).Trim().ToUpperInvariant();
        return s switch
        {
            "ASIA"     => "JAPAN",
            "EUROPE"   => "LONDON",
            "NEW_YORK" => "NY",
            _          => s,
        };
    }

    /// <summary>
    /// CR11: Maps the existing market regime taxonomy to the CR11 three-way regime:
    ///   TREND (TRENDING_BULL)
    ///   RANGE (RANGING, CHOPPY)
    ///   SHOCK (any waterfall/news spike / DEAD)
    /// </summary>
    private static string MapToCr11Regime(string? regime)
    {
        var r = (regime ?? string.Empty).Trim().ToUpperInvariant();
        return r switch
        {
            "TRENDING_BULL"   => "TREND",
            "TRENDING_BEAR"   => "SHOCK",   // buy-only system: bear trend = shock-like for our purposes
            "RANGING"         => "RANGE",
            "CHOPPY"          => "RANGE",
            "DEAD"            => "SHOCK",
            "NEWS_SPIKE"      => "SHOCK",
            "FRIDAY_HIGH_RISK"=> "SHOCK",
            "COMPRESSION"     => "RANGE",
            "EXPANSION"       => "TREND",
            "POST_SPIKE_PULLBACK" => "RANGE",
            _                 => "RANGE",
        };
    }
}

internal enum ExecutionMode
{
    Auto,
    Manual,
    Hybrid,
}
