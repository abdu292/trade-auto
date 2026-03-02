using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Application.Common.Services;
using Brain.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Brain.Web.Endpoints;

public static class MonitoringEndpoints
{
    public static RouteGroupBuilder MapMonitoringEndpoints(this RouteGroupBuilder group)
    {
        var monitoring = group.MapGroup("/monitoring").WithTags("Monitoring");

        monitoring.MapGet(
            "/ledger",
            (ITradeLedgerService ledger) => TypedResults.Ok(ledger.GetState()))
            .WithName("GetLedgerState")
            .WithDescription("Returns deterministic ledger state (cash, grams, exposure, deployable cash).");

        monitoring.MapPost(
            "/ledger/deposit",
            IResult (LedgerActionRequest request, ITradeLedgerService ledger) =>
            {
                try
                {
                    var slip = ledger.AddCapital(request.AmountAed, request.Note ?? "Manual deposit", DateTimeOffset.UtcNow);
                    return TypedResults.Ok(new { slip, ledger = ledger.GetState() });
                }
                catch (InvalidOperationException ex)
                {
                    return TypedResults.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("LedgerDeposit")
            .WithDescription("Add capital (deposit) to ledger cash.");

        monitoring.MapPost(
            "/ledger/withdraw",
            IResult (LedgerActionRequest request, ITradeLedgerService ledger) =>
            {
                try
                {
                    var slip = ledger.WithdrawCapital(request.AmountAed, request.Note ?? "Manual withdrawal", DateTimeOffset.UtcNow);
                    return TypedResults.Ok(new { slip, ledger = ledger.GetState() });
                }
                catch (InvalidOperationException ex)
                {
                    return TypedResults.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("LedgerWithdraw")
            .WithDescription("Withdraw capital from ledger cash.");

        monitoring.MapPost(
            "/ledger/adjustment",
            IResult (LedgerAdjustmentRequest request, ITradeLedgerService ledger) =>
            {
                try
                {
                    var slip = ledger.ShopAdjustment(request.AdjustmentAed, request.Note ?? "Shop adjustment", DateTimeOffset.UtcNow);
                    return TypedResults.Ok(new { slip, ledger = ledger.GetState() });
                }
                catch (InvalidOperationException ex)
                {
                    return TypedResults.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("LedgerShopAdjustment")
            .WithDescription("Apply a shop price adjustment to ledger cash (positive = gain, negative = loss).");

        monitoring.MapGet(
            "/notifications",
            (INotificationFeedStore feedStore, int take = 50) => TypedResults.Ok(feedStore.GetLatest(take)))
            .WithName("GetNotificationFeed")
            .WithDescription("Returns mock outbound notifications (WhatsApp + mobile app feed).");

        monitoring.MapGet(
            "/ai-quorum",
            async Task<IResult> (IApplicationDbContext db, CancellationToken cancellationToken) =>
            {
                var fromUtc = DateTimeOffset.UtcNow.AddHours(-24);
                var recent = await db.DecisionLogs
                    .AsNoTracking()
                    .Where(x => x.CreatedAtUtc >= fromUtc)
                    .ToListAsync(cancellationToken);

                var total = recent.Count;
                var quorumFailed = recent.Where(x => x.Cause == "AI_QUORUM_FAILED").ToList();
                var noTradeCount = recent.Count(x => x.Status == "NO_TRADE");

                var examples = quorumFailed
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Take(10)
                    .Select(x => new
                    {
                        x.CreatedAtUtc,
                        x.Symbol,
                        x.Status,
                        x.Cause,
                        x.Reason,
                        x.SnapshotHash,
                    })
                    .ToList();

                return TypedResults.Ok(new
                {
                    windowHours = 24,
                    totals = new
                    {
                        decisions = total,
                        noTrade = noTradeCount,
                        quorumFailures = quorumFailed.Count,
                        quorumFailureRate = total == 0 ? 0m : Math.Round((decimal)quorumFailed.Count / total, 4),
                        quorumShareOfNoTrade = noTradeCount == 0 ? 0m : Math.Round((decimal)quorumFailed.Count / noTradeCount, 4),
                    },
                    recentFailures = examples,
                });
            })
            .WithName("GetAiQuorumTelemetry")
            .WithDescription("Returns recent AI committee disagreement telemetry and failure-rate aggregates.");

        monitoring.MapGet(
            "/ai-health",
            async Task<IResult> (IConfiguration configuration, CancellationToken cancellationToken) =>
            {
                var configured = (configuration["External:AIWorkerBaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
                var candidates = new[]
                {
                    configured,
                    "http://127.0.0.1:8001",
                    "http://localhost:8001",
                }
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

                var errors = new List<string>();
                foreach (var baseUrl in candidates)
                {
                    try
                    {
                        var response = await client.GetAsync($"{baseUrl}/health", cancellationToken);
                        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                        if (!response.IsSuccessStatusCode)
                        {
                            errors.Add($"{baseUrl}: HTTP {(int)response.StatusCode}");
                            continue;
                        }

                        var parsed = JsonSerializer.Deserialize<object>(payload);
                        return TypedResults.Ok(parsed ?? new { status = "unknown" });
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{baseUrl}: {ex.Message}");
                    }
                }

                return TypedResults.Problem(
                    $"AI health check failed. Tried: {string.Join(" | ", errors)}",
                    statusCode: 502);
            })
            .WithName("GetAiWorkerHealth")
            .WithDescription("Returns proxied AI worker health including active analyzers and provider coverage.");

        monitoring.MapGet(
            "/runtime",
            IResult (
                ILatestMarketSnapshotStore snapshotStore,
                ITradingViewSignalStore tradingViewStore,
                IPendingTradeStore pendingTrades,
                ITradeApprovalStore approvals,
                INotificationFeedStore feedStore,
                ITradingRuntimeSettingsStore runtimeSettings,
                IConfiguration configuration,
                IApplicationDbContext db,
                IMarketSimulationService simulator) =>
            {
                snapshotStore.TryGet(out var snapshot);
                tradingViewStore.TryGetLatest(out var tv);
                var configuredSymbol = runtimeSettings.GetSymbol();
                var latestNotifications = feedStore.GetLatest(5);
                var macro = db.MacroCacheStates
                    .AsNoTracking()
                    .OrderByDescending(x => x.LastRefreshedUtc)
                    .FirstOrDefault();
                var now = DateTimeOffset.UtcNow;
                var activeHazardCount = db.HazardWindows
                    .AsNoTracking()
                    .Count(x => x.IsActive && x.IsBlocked && x.StartUtc <= now && x.EndUtc >= now);

                return TypedResults.Ok(new
                {
                    symbol = snapshot?.Symbol ?? configuredSymbol,
                    configuredSymbol,
                    mt5ServerTime = snapshot?.Mt5ServerTime,
                    ksaTime = snapshot?.KsaTime,
                    session = snapshot?.Session ?? "UNKNOWN",
                    bid = snapshot?.Bid ?? 0m,
                    ask = snapshot?.Ask ?? 0m,
                    spread = snapshot?.Spread ?? 0m,
                    spreadMedian60m = snapshot?.SpreadMedian60m ?? 0m,
                    spreadMax60m = snapshot?.SpreadMax60m ?? 0m,
                    telegramState = snapshot?.TelegramState ?? "QUIET",
                    panicSuspected = snapshot?.PanicSuspected ?? false,
                    tvAlertType = snapshot?.TvAlertType ?? "NONE",
                    pendingQueueDepth = pendingTrades.Count(),
                    approvalQueueDepth = approvals.GetPending(200).Count,
                    executionMode = (configuration["Execution:Mode"] ?? "auto").Trim().ToLowerInvariant(),
                    hybridAutoSessions = (configuration["Execution:HybridAutoSessions"] ?? "JAPAN,INDIA").Trim(),
                    macroBias = macro?.MacroBias ?? "UNKNOWN",
                    institutionalBias = macro?.InstitutionalBias ?? "UNKNOWN",
                    cbFlowFlag = macro?.CbFlowFlag ?? "UNKNOWN",
                    positioningFlag = macro?.PositioningFlag ?? "UNKNOWN",
                    macroCacheAgeMinutes = macro is null ? -1 : (int)Math.Max(0, (DateTimeOffset.UtcNow - macro.LastRefreshedUtc).TotalMinutes),
                    activeBlockedHazardWindows = activeHazardCount,
                    simulation = simulator.GetStatus(),
                    tradingView = tv,
                    latestNotifications,
                });
            })
            .WithName("GetRuntimeStatus")
            .WithDescription("Returns live runtime telemetry for MT5 demo/live operation monitoring.");

        monitoring.MapGet(
            "/tick-ingestion",
            IResult (ILatestMarketSnapshotStore snapshotStore, int take = 20) =>
            {
                var telemetry = snapshotStore.GetTickTelemetry(take <= 0 ? 20 : take);
                return TypedResults.Ok(telemetry);
            })
            .WithName("GetTickIngestionTelemetry")
            .WithDescription("Returns MT5 tick ingestion rate/freshness and recent ingested tick snapshots.");

        monitoring.MapGet(
            "/runtime-settings",
            IResult (ITradingRuntimeSettingsStore runtimeSettings) =>
            {
                var symbol = runtimeSettings.GetSymbol();
                return TypedResults.Ok(new { symbol });
            })
            .WithName("GetRuntimeSettings")
            .WithDescription("Returns mutable runtime trading settings managed from app UI.");

        monitoring.MapPut(
            "/runtime-settings",
            IResult (UpdateRuntimeSettingsRequest request, ITradingRuntimeSettingsStore runtimeSettings) =>
            {
                var symbol = (request.Symbol ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return TypedResults.BadRequest(new { error = "symbol is required." });
                }

                runtimeSettings.SetSymbol(symbol);
                return TypedResults.Ok(new { symbol = runtimeSettings.GetSymbol() });
            })
            .WithName("UpdateRuntimeSettings")
            .WithDescription("Updates mutable runtime trading settings without server restart.");

        monitoring.MapGet(
            "/simulator/status",
            (IMarketSimulationService simulator) => TypedResults.Ok(simulator.GetStatus()))
            .WithName("GetSimulatorStatus")
            .WithDescription("Returns current weekend/off-hours simulator status.");

        monitoring.MapPost(
            "/simulator/start",
            (StartMarketSimulationRequest? request, IMarketSimulationService simulator) =>
            {
                var payload = new Brain.Application.Common.Models.MarketSimulationStartContract(
                    StartPrice: request?.StartPrice ?? 2890m,
                    VolatilityUsd: request?.VolatilityUsd ?? 0.45m,
                    BaseSpread: request?.BaseSpread ?? 0.18m,
                    IntervalSeconds: request?.IntervalSeconds ?? 5,
                    SessionOverride: request?.SessionOverride,
                    EnableShockEvents: request?.EnableShockEvents ?? true,
                    StrategyProfile: request?.StrategyProfile ?? "Standard");

                simulator.Start(payload);
                return TypedResults.Ok(simulator.GetStatus());
            })
            .WithName("StartMarketSimulator")
            .WithDescription("Starts realistic MT5-like snapshot simulation for market-closed testing.");

        monitoring.MapPost(
            "/simulator/stop",
            (IMarketSimulationService simulator) =>
            {
                simulator.Stop();
                return TypedResults.Ok(simulator.GetStatus());
            })
            .WithName("StopMarketSimulator")
            .WithDescription("Stops weekend/off-hours market snapshot simulation.");

        monitoring.MapPost(
            "/simulator/step",
            (IMarketSimulationService simulator) =>
            {
                simulator.StepOnce();
                return TypedResults.Ok(simulator.GetStatus());
            })
            .WithName("StepMarketSimulator")
            .WithDescription("Generates one simulated MT5 snapshot tick immediately.");

        monitoring.MapGet(
            "/macro-cache",
            async Task<IResult> (IApplicationDbContext db, CancellationToken cancellationToken) =>
            {
                var cache = await db.MacroCacheStates
                    .AsNoTracking()
                    .OrderByDescending(x => x.LastRefreshedUtc)
                    .FirstOrDefaultAsync(cancellationToken);
                if (cache is null)
                {
                    return TypedResults.NotFound(new { message = "Macro cache not initialized." });
                }

                return TypedResults.Ok(new
                {
                    cache.MacroBias,
                    cache.InstitutionalBias,
                    cache.CbFlowFlag,
                    cache.PositioningFlag,
                    cache.Source,
                    cache.LastRefreshedUtc,
                    cacheAgeMinutes = (int)Math.Max(0, (DateTimeOffset.UtcNow - cache.LastRefreshedUtc).TotalMinutes),
                });
            })
            .WithName("GetMacroCache")
            .WithDescription("Returns current asynchronous macro/institutional cache state.");

        monitoring.MapGet(
            "/hazard-windows",
            async Task<IResult> (IApplicationDbContext db, CancellationToken cancellationToken) =>
            {
                var windows = await db.HazardWindows
                    .AsNoTracking()
                    .OrderByDescending(x => x.StartUtc)
                    .Take(100)
                    .ToListAsync(cancellationToken);
                return TypedResults.Ok(windows);
            })
            .WithName("GetHazardWindows")
            .WithDescription("Returns configured hazard windows used for expiry veto.");

        monitoring.MapPost(
            "/hazard-windows",
            async Task<IResult> (CreateHazardWindowRequest request, IApplicationDbContext db, CancellationToken cancellationToken) =>
            {
                if (request.EndUtc <= request.StartUtc)
                {
                    return TypedResults.BadRequest(new { error = "EndUtc must be after StartUtc." });
                }

                var window = HazardWindow.Create(
                    title: request.Title,
                    category: request.Category,
                    startUtc: request.StartUtc,
                    endUtc: request.EndUtc,
                    isBlocked: request.IsBlocked);
                db.HazardWindows.Add(window);
                await db.SaveChangesAsync(cancellationToken);
                return TypedResults.Ok(window);
            })
            .WithName("CreateHazardWindow")
            .WithDescription("Creates a blocked hazard window for hard expiry veto enforcement.");

        monitoring.MapPost(
            "/hazard-windows/{id:guid}/disable",
            async Task<IResult> (Guid id, IApplicationDbContext db, CancellationToken cancellationToken) =>
            {
                var window = await db.HazardWindows.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
                if (window is null)
                {
                    return TypedResults.NotFound(new { message = "Hazard window not found." });
                }

                window.Disable();
                await db.SaveChangesAsync(cancellationToken);
                return TypedResults.Ok(new { disabled = true, id = window.Id });
            })
            .WithName("DisableHazardWindow")
            .WithDescription("Disables a hazard window so it no longer blocks trading.");

        monitoring.MapGet(
            "/telegram-channels",
            async Task<IResult> (IApplicationDbContext db, CancellationToken cancellationToken) =>
            {
                var channels = await db.TelegramChannels
                    .AsNoTracking()
                    .OrderByDescending(x => x.Weight)
                    .ThenBy(x => x.ChannelKey)
                    .Take(200)
                    .ToListAsync(cancellationToken);
                return TypedResults.Ok(channels);
            })
            .WithName("GetTelegramChannels")
            .WithDescription("Returns persistent telegram channel registry with dynamic weights.");

        monitoring.MapGet(
            "/telegram-consensus",
            async Task<IResult> (IApplicationDbContext db, int lookbackMinutes, CancellationToken cancellationToken) =>
            {
                var minutes = lookbackMinutes <= 0 ? 5 : Math.Min(180, lookbackMinutes);
                var since = DateTimeOffset.UtcNow.AddMinutes(-minutes);

                var signals = await db.TelegramSignals
                    .AsNoTracking()
                    .Where(x => x.ServerTimeUtc >= since)
                    .OrderByDescending(x => x.ServerTimeUtc)
                    .ToListAsync(cancellationToken);
                var channels = await db.TelegramChannels
                    .AsNoTracking()
                    .ToDictionaryAsync(x => x.ChannelKey, x => x, cancellationToken);

                decimal buyScore = 0m;
                decimal sellScore = 0m;

                foreach (var signal in signals)
                {
                    var key = signal.ChannelKey.Trim().ToLowerInvariant();
                    var weight = channels.TryGetValue(key, out var channel) ? Math.Max(0.3m, channel.Weight) : 1.0m;
                    var direction = signal.Direction.Trim().ToUpperInvariant();
                    if (direction == "BUY") buyScore += weight;
                    if (direction == "SELL") sellScore += weight;
                }

                var total = buyScore + sellScore;
                var dominance = total <= 0m ? 0m : Math.Round(Math.Max(buyScore, sellScore) / total, 4);
                var state = "QUIET";
                if (signals.Count >= 2 && total >= 1.5m)
                {
                    if (buyScore > sellScore)
                    {
                        state = dominance >= 0.85m ? "STRONG_BUY" : (dominance >= 0.70m ? "BUY" : "MIXED");
                    }
                    else if (sellScore > buyScore)
                    {
                        state = dominance >= 0.85m ? "STRONG_SELL" : (dominance >= 0.70m ? "SELL" : "MIXED");
                    }
                    else
                    {
                        state = "MIXED";
                    }
                }

                return TypedResults.Ok(new
                {
                    lookbackMinutes = minutes,
                    signalCount = signals.Count,
                    buyScore,
                    sellScore,
                    dominance,
                    state,
                    topSignals = signals.Take(20).Select(x => new
                    {
                        x.ChannelKey,
                        x.Direction,
                        x.Confidence,
                        x.ConsensusState,
                        x.PanicSuspected,
                        x.ServerTimeUtc,
                        channelWeight = channels.TryGetValue(x.ChannelKey, out var channel) ? channel.Weight : 1.0m,
                    }),
                });
            })
            .WithName("GetTelegramConsensusTelemetry")
            .WithDescription("Returns weighted telegram consensus score from persisted signals and channel weights.");

        monitoring.MapPost(
            "/telegram-channels/{channelKey}/outcome/{outcome}",
            async Task<IResult> (string channelKey, string outcome, IApplicationDbContext db, CancellationToken cancellationToken) =>
            {
                var key = channelKey.Trim().ToLowerInvariant();
                var channel = await db.TelegramChannels.FirstOrDefaultAsync(x => x.ChannelKey == key, cancellationToken);
                if (channel is null)
                {
                    return TypedResults.NotFound(new { message = $"Telegram channel '{channelKey}' not found." });
                }

                channel.ApplyOutcome(outcome);
                await db.SaveChangesAsync(cancellationToken);
                return TypedResults.Ok(channel);
            })
            .WithName("UpdateTelegramChannelOutcome")
            .WithDescription("Applies GOOD/HOLD/BAD_CONTEXT outcome learning to channel weight/profile.");

        monitoring.MapGet(
            "/approvals",
            IResult (ITradeApprovalStore approvals, int take = 20) => TypedResults.Ok(approvals.GetPending(take <= 0 ? 20 : take)))
            .WithName("GetApprovalQueue")
            .WithDescription("Returns manual approval queue. APPROVE moves trade to MT5 pending queue.");

        monitoring.MapPost(
            "/approvals/{tradeId:guid}/approve",
            IResult (Guid tradeId, ITradeApprovalStore approvals, IPendingTradeStore pendingTrades) =>
            {
                if (!approvals.TryApprove(tradeId, out var trade) || trade is null)
                {
                    return TypedResults.NotFound(new { message = "Trade not found in approval queue." });
                }

                pendingTrades.Enqueue(trade);
                return TypedResults.Ok(new { approved = true, tradeId = trade.Id });
            })
            .WithName("ApproveTrade")
            .WithDescription("Approves a queued trade and releases it to MT5 pending order pull endpoint.");

        monitoring.MapPost(
            "/approvals/{tradeId:guid}/reject",
            IResult (Guid tradeId, ITradeApprovalStore approvals) =>
            {
                var rejected = approvals.Reject(tradeId);
                if (!rejected)
                {
                    return TypedResults.NotFound(new { message = "Trade not found in approval queue." });
                }

                return TypedResults.Ok(new { rejected = true, tradeId });
            })
            .WithName("RejectTrade")
            .WithDescription("Rejects a queued trade and removes it from the manual approval queue.");

        monitoring.MapPost(
            "/replay/run",
            IResult (ReplayRunRequest? request, ITradeLedgerService ledger) =>
            {
                var runs = Math.Clamp(request?.Runs ?? 120, 20, 2000);
                var startPrice = request?.StartPrice ?? 2890m;
                var now = DateTimeOffset.UtcNow;
                var ledgerState = ledger.GetState();

                var noTrade = 0;
                var armed = 0;
                var buyLimit = 0;
                var buyStop = 0;
                var blockedByWaterfall = 0;
                var blockedByAlignment = 0;
                var blockedByCapacity = 0;

                for (var i = 0; i < runs; i++)
                {
                    var strategyProfile = request?.StrategyProfile ?? "Standard";
                    var snapshot = ReplayHarnessFactory.BuildReplaySnapshot(startPrice, now.AddMinutes(i), i, strategyProfile);
                    var regime = RegimeRiskClassifier.Classify(snapshot);
                    var aiSignal = ReplayHarnessFactory.BuildReplaySignal(snapshot, i);
                    var decision = DecisionEngine.Evaluate(snapshot, regime, aiSignal, ledgerState, strategyProfile);

                    if (!decision.IsTradeAllowed)
                    {
                        noTrade++;
                        if (decision.WaterfallRisk == "HIGH")
                        {
                            blockedByWaterfall++;
                        }

                        if (decision.Reason.Contains("Alignment", StringComparison.OrdinalIgnoreCase))
                        {
                            blockedByAlignment++;
                        }

                        if (decision.Reason.Contains("Capacity", StringComparison.OrdinalIgnoreCase))
                        {
                            blockedByCapacity++;
                        }

                        continue;
                    }

                    armed++;
                    if (decision.Rail == "BUY_LIMIT") buyLimit++;
                    if (decision.Rail == "BUY_STOP") buyStop++;
                }

                return TypedResults.Ok(new
                {
                    runs,
                    startPrice,
                    summary = new
                    {
                        armed,
                        noTrade,
                        armedRate = runs == 0 ? 0m : Math.Round((decimal)armed / runs, 4),
                        buyLimit,
                        buyStop,
                    },
                    blockers = new
                    {
                        waterfall = blockedByWaterfall,
                        alignment = blockedByAlignment,
                        capacity = blockedByCapacity,
                    },
                });
            })
            .WithName("RunReplayHarness")
            .WithDescription("Runs a deterministic replay batch against DecisionEngine and returns gating outcome metrics.");

        return group;
    }
}

public sealed record CreateHazardWindowRequest(
    string Title,
    string Category,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsBlocked = true);

public sealed record LedgerActionRequest(decimal AmountAed, string? Note);
public sealed record LedgerAdjustmentRequest(decimal AdjustmentAed, string? Note);

public sealed record StartMarketSimulationRequest(
    decimal? StartPrice,
    decimal? VolatilityUsd,
    decimal? BaseSpread,
    int? IntervalSeconds,
    string? SessionOverride,
    bool? EnableShockEvents,
    string? StrategyProfile);

public sealed record ReplayRunRequest(int? Runs, decimal? StartPrice, string? StrategyProfile);

public sealed record UpdateRuntimeSettingsRequest(string? Symbol);

internal static class ReplayHarnessFactory
{
    internal static MarketSnapshotContract BuildReplaySnapshot(decimal startPrice, DateTimeOffset timestamp, int step, string strategyProfile)
    {
        var isWar = string.Equals(strategyProfile, "WarPremium", StringComparison.OrdinalIgnoreCase);
        var wave = (decimal)Math.Sin(step / 10d) * 2.2m;
        var drift = (step % 40 < 20 ? (isWar ? 0.55m : 0.35m) : (isWar ? -0.35m : -0.22m));
        var close = startPrice + wave + drift;
        var atr = (isWar ? 8m : 6m) + Math.Abs(wave);
        var adr = Math.Max(14m, atr * 2.4m);
        var spread = (isWar ? 0.22m : 0.16m) + ((step % 9 == 0) ? (isWar ? 0.16m : 0.08m) : 0m);
        var isExpansion = atr / adr > 0.38m;
        var panic = step % (isWar ? 29 : 47) == 0;

        return new MarketSnapshotContract(
            Symbol: "XAUUSD",
            TimeframeData:
            [
                new TimeframeDataContract("M5", close - 0.8m, close + 0.6m, close - 1.1m, close),
                new TimeframeDataContract("M15", close - 1.3m, close + 0.9m, close - 1.9m, close),
                new TimeframeDataContract("H1", close - 2.2m, close + 1.8m, close - 2.6m, close),
            ],
            Atr: atr,
            Adr: adr,
            Ma20: close - 0.5m,
            Ma20H1: close - 0.9m,
            Ma20H4: close - 1.5m,
            Session: (step % 4) switch { 0 => "JAPAN", 1 => "INDIA", 2 => "LONDON", _ => "NY" },
            Timestamp: timestamp,
            Mt5ServerTime: timestamp,
            KsaTime: timestamp.AddMinutes(50),
            RsiH1: 58m + (step % 8),
            RsiM15: 54m + (step % 10),
            Bid: close - (spread / 2m),
            Ask: close + (spread / 2m),
            Spread: spread,
            SpreadMedian60m: 0.16m,
            SpreadMax60m: 0.32m,
            IsExpansion: isExpansion,
            IsAtrExpanding: step % 3 == 0,
                HasOverlapCandles: step % (isWar ? 4 : 5) == 0,
                HasImpulseCandles: step % (isWar ? 5 : 7) == 0,
                IsBreakoutConfirmed: step % (isWar ? 4 : 6) == 0,
            IsUsRiskWindow: step % 9 == 0,
            PanicSuspected: panic,
            HasPanicDropSequence: panic,
                TelegramState: step % 6 == 0 ? (isWar ? "STRONG_BUY" : "BUY") : (step % 5 == 0 ? "MIXED" : "QUIET"),
                TvAlertType: step % 8 == 0 ? (isWar ? "LID_BREAK" : "BREAKOUT") : "NONE",
            ImpulseStrengthScore: Math.Min(1m, atr / 10m));
    }

    internal static TradeSignalContract BuildReplaySignal(MarketSnapshotContract snapshot, int step)
    {
        var confidence = 0.62m + ((step % 5) * 0.05m);
        return new TradeSignalContract(
            Rail: step % 4 == 0 ? "BUY_STOP" : "BUY_LIMIT",
            Entry: snapshot.TimeframeData.First().Close,
            Tp: snapshot.TimeframeData.First().Close + 9m,
            Pe: snapshot.Timestamp.AddMinutes(30),
            Ml: 1800,
            Confidence: Math.Min(0.93m, confidence),
            SafetyTag: snapshot.PanicSuspected ? "BLOCK" : "SAFE",
            DirectionBias: "BULLISH",
            AlignmentScore: Math.Min(0.95m, confidence),
            NewsImpactTag: snapshot.PanicSuspected ? "HIGH" : "LOW",
            TvConfirmationTag: snapshot.TvAlertType == "BREAKOUT" ? "CONFIRM" : "NEUTRAL",
            NewsTags: ["replay_harness"],
            Summary: "Deterministic replay signal",
            ConsensusPassed: true,
            AgreementCount: 2,
            RequiredAgreement: 2,
            DisagreementReason: null,
            ProviderVotes: ["replay:grok", "replay:openai"]);
    }
}
