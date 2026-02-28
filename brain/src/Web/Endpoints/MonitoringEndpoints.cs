using Brain.Application.Common.Interfaces;
using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

        monitoring.MapGet(
            "/notifications",
            (INotificationFeedStore feedStore, int take = 50) => TypedResults.Ok(feedStore.GetLatest(take)))
            .WithName("GetNotificationFeed")
            .WithDescription("Returns mock outbound notifications (WhatsApp + mobile app feed).");

        monitoring.MapGet(
            "/runtime",
            IResult (
                ILatestMarketSnapshotStore snapshotStore,
                ITradingViewSignalStore tradingViewStore,
                IPendingTradeStore pendingTrades,
                INotificationFeedStore feedStore,
                IApplicationDbContext db,
                IMarketSimulationService simulator) =>
            {
                snapshotStore.TryGet(out var snapshot);
                tradingViewStore.TryGetLatest(out var tv);
                var latestNotifications = feedStore.GetLatest(5);
                var macro = db.MacroCacheStates.AsNoTracking().FirstOrDefault();
                var now = DateTimeOffset.UtcNow;
                var activeHazardCount = db.HazardWindows
                    .AsNoTracking()
                    .Count(x => x.IsActive && x.IsBlocked && x.StartUtc <= now && x.EndUtc >= now);

                return TypedResults.Ok(new
                {
                    symbol = snapshot?.Symbol ?? "XAUUSD",
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
                    EnableShockEvents: request?.EnableShockEvents ?? true);

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
                var cache = await db.MacroCacheStates.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
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

        return group;
    }
}

public sealed record CreateHazardWindowRequest(
    string Title,
    string Category,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsBlocked = true);

public sealed record StartMarketSimulationRequest(
    decimal? StartPrice,
    decimal? VolatilityUsd,
    decimal? BaseSpread,
    int? IntervalSeconds,
    string? SessionOverride,
    bool? EnableShockEvents);
