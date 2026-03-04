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
    ILogger<SignalPollingBackgroundService> logger) : BackgroundService
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
            var simulator = scope.ServiceProvider.GetRequiredService<IMarketSimulationService>();
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
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var symbol = runtimeSettings.GetSymbol();
                var rawSnapshot = await marketData.GetSnapshotAsync(symbol, stoppingToken);
                var snapshot = rawSnapshot with { CycleId = cycleId };

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

                // ── Rule engine: must generate a setup candidate before AI is invoked ──
                var setupCandidate = RuleEngine.Evaluate(snapshot);

                await timeline.WriteAsync(
                    eventType: setupCandidate.IsValid ? "RULE_ENGINE_SETUP_CANDIDATE" : "RULE_ENGINE_ABORT",
                    stage: "rule_engine",
                    source: "brain",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        setupCandidate.IsValid,
                        h1Context = setupCandidate.H1Context,
                        m15Setup = setupCandidate.M15Setup,
                        m5Entry = setupCandidate.M5Entry,
                        abortReason = setupCandidate.AbortReason,
                    },
                    cancellationToken: stoppingToken);

                if (!setupCandidate.IsValid && !forceWhereToTrade)
                {
                    logger.LogInformation(
                        "RULE_ENGINE_ABORT — no setup candidate. Reason={Reason}",
                        setupCandidate.AbortReason);
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

                    logger.LogInformation("NO_TRADE due to economic news block. Reason={Reason}", newsRisk.Reason);
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                var modeSignal = await aiWorker.GetModeAsync(snapshot, stoppingToken);

                var aiSignal = await aiWorker.AnalyzeAsync(snapshot, cycleId, stoppingToken);
                var aiTrace = ParseAiTrace(aiSignal.AiTraceJson);
                var aiRequest = TryGetTraceNode(aiTrace, "ai_request");
                var providerTraces = TryGetTraceNode(aiTrace, "provider_traces");
                var providerRequests = ExtractProviderTraceEntries(providerTraces, "AI_PROVIDER_REQUEST");
                var providerResponses = ExtractProviderTraceEntries(providerTraces, "AI_PROVIDER_RESPONSE");
                var aiUsed = ExtractAiUsed(providerTraces);

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
                        aiRequest,
                        aiUsed,
                        promptsSentToAi = providerRequests,
                    },
                    cancellationToken: stoppingToken);

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

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    continue;
                }

                if (tradingViewStore.TryGetLatest(out var tv) && tv is not null)
                {
                    aiSignal = MergeTradingView(aiSignal, tv, snapshot, logger);
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

                var decision = DecisionEngine.Evaluate(snapshot, regime, aiSignal, state, activeStrategy);
                var snapshotHash = ComputeSnapshotHash(snapshot);

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
                        decision.Grams,
                        decision.TelegramState,
                        snapshotHash,
                    },
                    cancellationToken: stoppingToken);

                LogTransitionsIfChanged(db, snapshot, decision, forceWhereToTrade, snapshotHash);

                var simulatorRunning = simulator.GetStatus().IsRunning;
                if (!decision.IsTradeAllowed
                    && simulatorRunning
                    && forceWhereToTrade
                    && string.Equals(decision.Cause, "MID_AIR_BAN", StringComparison.OrdinalIgnoreCase))
                {
                    var primaryClose = snapshot.TimeframeData
                        .FirstOrDefault(x => string.Equals(x.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.Close
                        ?? snapshot.TimeframeData.First().Close;

                    var fallbackEntry = decimal.Round(primaryClose - Math.Clamp(snapshot.Atr * 0.40m, 6m, 12m), 2);
                    var fallbackTp = decimal.Round(fallbackEntry + Math.Clamp(snapshot.Atr * 0.95m, 10m, 22m), 2);
                    var fallbackExpiry = snapshot.Timestamp.AddMinutes(30);

                    decision = decision with
                    {
                        IsTradeAllowed = true,
                        Status = "ARMED",
                        EngineState = "ARMED",
                        Cause = "SIM_SHELF_PROOF_BYPASS",
                        Reason = "Simulation fallback armed after prolonged no-trade shelf-proof gating.",
                        Bucket = "SIM",
                        Rail = "BUY_LIMIT",
                        SizeClass = "SIM_FALLBACK",
                        Entry = fallbackEntry,
                        Tp = fallbackTp,
                        Grams = 100m,
                        ExpiryUtc = fallbackExpiry,
                        MaxLifeSeconds = 1800,
                        RailPermissionA = "ALLOWED",
                        RailPermissionB = "BLOCKED",
                        RotationCapThisSession = 1,
                    };

                    logger.LogInformation(
                        "Simulation fallback armed to prevent perpetual empty approvals. Cause={Cause}, Session={Session}, Entry={Entry}, TP={Tp}",
                        decision.Cause,
                        decision.Session,
                        decision.Entry,
                        decision.Tp);
                }

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

                var pending = new PendingTradeContract(
                    Id: Guid.NewGuid(),
                    Symbol: snapshot.Symbol,
                    Type: decision.Rail,
                    Price: decision.Entry,
                    Tp: decision.Tp,
                    Expiry: decision.ExpiryUtc,
                    Ml: decision.MaxLifeSeconds,
                    Grams: decision.Grams,
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
                    CycleId: cycleId);

                var executionMode = ResolveExecutionMode(configuration["Execution:Mode"]);
                var hybridSessions = ResolveHybridAutoSessions(configuration["Execution:HybridAutoSessions"]);
                var directToMt5 = ShouldQueueToMt5(executionMode, pending.Session, hybridSessions);

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
                        pending.Grams,
                        pending.Session,
                        pending.SessionPhase,
                        pending.EngineState,
                        pending.Cause,
                        route = directToMt5 ? "MT5_PENDING" : "APPROVAL_QUEUE",
                        executionMode = executionMode.ToString().ToUpperInvariant(),
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
}

internal enum ExecutionMode
{
    Auto,
    Manual,
    Hybrid,
}
