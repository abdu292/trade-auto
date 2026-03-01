using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Application.Common.Services;
using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var marketData = scope.ServiceProvider.GetRequiredService<IMarketDataProvider>();
            var aiWorker = scope.ServiceProvider.GetRequiredService<IAIWorkerClient>();
            var pendingTrades = scope.ServiceProvider.GetRequiredService<IPendingTradeStore>();
            var approvals = scope.ServiceProvider.GetRequiredService<ITradeApprovalStore>();
            var ledger = scope.ServiceProvider.GetRequiredService<ITradeLedgerService>();
            var mt5Control = scope.ServiceProvider.GetRequiredService<IMt5ControlStore>();
            var tradingViewStore = scope.ServiceProvider.GetRequiredService<ITradingViewSignalStore>();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            try
            {
                var snapshot = await marketData.GetSnapshotAsync("XAUUSD", stoppingToken);
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

                var modeSignal = await aiWorker.GetModeAsync(snapshot, stoppingToken);
                var aiSignal = await aiWorker.AnalyzeAsync(snapshot, stoppingToken);
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
                    SizeClass: decision.SizeClass,
                    TelegramState: decision.TelegramState);

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

    private static ExecutionMode ResolveExecutionMode(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "MANUAL" => ExecutionMode.Manual,
            "HYBRID" => ExecutionMode.Hybrid,
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
