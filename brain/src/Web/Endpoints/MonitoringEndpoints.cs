using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Application.Common.Services;
using Brain.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Brain.Web.Endpoints;

public static class MonitoringEndpoints
{
    public static RouteGroupBuilder MapMonitoringEndpoints(this RouteGroupBuilder group)
    {
        var monitoring = group.MapGroup("/monitoring").WithTags("Monitoring");

        monitoring.MapGet(
            "/ledger",
            IResult (ITradeLedgerService ledger, ILatestMarketSnapshotStore snapshotStore) =>
            {
                snapshotStore.TryGet(out var snapshot);
                var bid = snapshot?.Bid ?? 0m;
                return TypedResults.Ok(ledger.GetExtendedState(bid));
            })
            .WithName("GetLedgerState")
            .WithDescription("Returns deterministic ledger state with extended capital metrics (cash, gold, equity, compounding).");

        // Spec v7 §10 — Gold Engine dashboard: Physical Ledger card, MT5 Execution card, factor state panel, trade-map, execution mode
        monitoring.MapGet(
            "/dashboard",
            IResult (
                ITradeLedgerService ledger,
                ILatestMarketSnapshotStore snapshotStore,
                ILastGoldEngineStateStore engineStateStore,
                IPendingTradeStore pendingTrades,
                IConfiguration configuration) =>
            {
                snapshotStore.TryGet(out var snapshot);
                var bid = snapshot?.Bid ?? snapshot?.AuthoritativeRate ?? 0m;
                var ledgerState = ledger.GetExtendedState(bid);
                var (engineStates, pathRouting, _) = engineStateStore.GetLast();
                var modeRaw = (configuration["Execution:Mode"] ?? "AUTO").Trim().ToUpperInvariant();
                var executionMode = modeRaw is "HYBRID" or "MANUAL" ? modeRaw : "AUTO";
                var pricePerGramAed = bid > 0m ? bid * 3.674m / 31.1035m : 0m;
                var buyableGrams = pricePerGramAed > 0m && ledgerState.DeployableCashAed > 0m
                    ? Math.Round(ledgerState.DeployableCashAed / pricePerGramAed, 2)
                    : 0m;

                return TypedResults.Ok(new
                {
                    physicalLedger = new
                    {
                        cashAed = ledgerState.CashAed,
                        goldGrams = ledgerState.GoldGrams,
                        deployableAed = ledgerState.DeployableCashAed,
                        buyableGrams,
                    },
                    mt5ExecutionAccount = new
                    {
                        balance = snapshot?.Balance ?? 0m,
                        equity = snapshot?.Equity ?? 0m,
                        freeMargin = snapshot?.FreeMargin ?? 0m,
                        bid = snapshot?.Bid ?? 0m,
                        ask = snapshot?.Ask ?? 0m,
                        spread = snapshot?.Spread ?? 0m,
                    },
                    factorStatePanel = engineStates == null ? null : new
                    {
                        legalityState = engineStates.LegalityState,
                        biasState = engineStates.BiasState,
                        pathState = engineStates.PathState,
                        overextensionState = engineStates.OverextensionState,
                        waterfallRisk = engineStates.WaterfallRisk,
                        session = engineStates.Session,
                        sessionPhase = engineStates.SessionPhase,
                    },
                    tradeMapChart = new
                    {
                        bases = pathRouting?.PendingLimitPath != null
                            ? new[] { pathRouting.PendingLimitPath.S1BaseShelf }
                                .Concat(pathRouting.PendingLimitPath.S2SweepPocket.HasValue ? new[] { pathRouting.PendingLimitPath.S2SweepPocket.Value } : Array.Empty<decimal>())
                                .Concat(pathRouting.PendingLimitPath.S3ExhaustionPocket.HasValue ? new[] { pathRouting.PendingLimitPath.S3ExhaustionPocket.Value } : Array.Empty<decimal>())
                                .ToArray()
                            : Array.Empty<decimal>(),
                        sessionHigh = snapshot?.SessionHigh ?? 0m,
                        sessionLow = snapshot?.SessionLow ?? 0m,
                        pendingBuyLimit = pendingTrades.Snapshot().Where(p => string.Equals(p.Type, "BUY_LIMIT", StringComparison.OrdinalIgnoreCase)).Select(p => new { p.Price, p.Tp, p.Expiry }).ToList(),
                        pendingBuyStop = pendingTrades.Snapshot().Where(p => string.Equals(p.Type, "BUY_STOP", StringComparison.OrdinalIgnoreCase)).Select(p => new { p.Price, p.Tp, p.Expiry }).ToList(),
                    },
                    executionMode,
                });
            })
            .WithName("GetGoldEngineDashboard")
            .WithDescription("Spec v7 §10 — Physical Ledger card, MT5 Execution card, factor state panel, trade-map chart, execution mode.");

        // Spec v7 §9 — Auto-Tune Phase 1: report only, no auto-apply
        monitoring.MapGet(
            "/auto-tune-report",
            IResult () =>
            {
                var report = new AutoTuneReportContract(
                    ReportId: $"atr_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                    GeneratedAtUtc: DateTimeOffset.UtcNow,
                    SuggestedAdjustments: Array.Empty<string>(),
                    BoundsRespected: new[] { "stretched threshold 0.7–1.1 ATR", "extreme threshold 1.3–1.8 ATR", "BUY_LIMIT baseDistATR 0.8–1.2", "compressionCount 0–2" },
                    NeverTouched: new[]
                    {
                        "WATERFALL_RISK logic", "FAIL laws", "hazard windows", "no-market-buy law",
                        "exposure/capital gates", "spread block rules", "pending-before-level law", "PRETABLE BLOCK",
                    },
                    Summary: "Phase 1: recommendation only. No auto-apply.");
                return TypedResults.Ok(report);
            })
            .WithName("GetAutoTuneReport")
            .WithDescription("Spec v7 §9 — Auto-Tune Phase 1 report only; no auto-apply.");

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
                IApplicationDbContext db) =>
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
                    uaeTime = snapshot?.UaeTime,
                    indiaTime = snapshot?.IndiaTime,
                    internalClockUtc = snapshot?.InternalClockUtc,
                    utcReferenceTime = snapshot?.UtcReferenceTime,
                    timeSkewMs = snapshot?.TimeSkewMs ?? 0m,
                    session = snapshot?.Session ?? "UNKNOWN",
                    bid = snapshot?.Bid ?? 0m,
                    ask = snapshot?.Ask ?? 0m,
                    spread = snapshot?.Spread ?? 0m,
                    spreadMedian60m = snapshot?.SpreadMedian60m ?? 0m,
                    spreadMax60m = snapshot?.SpreadMax60m ?? 0m,
                    spreadMin1m = snapshot?.SpreadMin1m ?? 0m,
                    spreadAvg1m = snapshot?.SpreadAvg1m ?? 0m,
                    spreadMax1m = snapshot?.SpreadMax1m ?? 0m,
                    spreadMin5m = snapshot?.SpreadMin5m ?? 0m,
                    spreadAvg5m = snapshot?.SpreadAvg5m ?? 0m,
                    spreadMax5m = snapshot?.SpreadMax5m ?? 0m,
                    tickRatePer30s = snapshot?.TickRatePer30s ?? 0m,
                    freezeGapDetected = snapshot?.FreezeGapDetected ?? false,
                    slippageEstimatePoints = snapshot?.SlippageEstimatePoints ?? 0m,
                    sessionVwap = snapshot?.SessionVwap ?? 0m,
                    systemFetchedGoldRate = snapshot?.SystemFetchedGoldRate ?? 0m,
                    rateDeltaUsd = snapshot?.RateDeltaUsd ?? 0m,
                    rateAuthority = snapshot?.RateAuthority ?? "MT5_FALLBACK",
                    authoritativeRate = snapshot?.AuthoritativeRate ?? 0m,
                    freeMargin = snapshot?.FreeMargin ?? 0m,
                    equity = snapshot?.Equity ?? 0m,
                    balance = snapshot?.Balance ?? 0m,
                    telegramState = snapshot?.TelegramState ?? "QUIET",
                    panicSuspected = snapshot?.PanicSuspected ?? false,
                    tvAlertType = snapshot?.TvAlertType ?? "NONE",
                    timeframeData = snapshot?.TimeframeData ?? Array.Empty<TimeframeDataContract>(),
                    compressionRangesM15 = snapshot?.CompressionRangesM15 ?? Array.Empty<decimal>(),
                    pendingOrders = snapshot?.PendingOrders ?? Array.Empty<PendingOrderSnapshotContract>(),
                    openPositions = snapshot?.OpenPositions ?? Array.Empty<OpenPositionSnapshotContract>(),
                    orderExecutionEvents = snapshot?.OrderExecutionEvents ?? Array.Empty<OrderExecutionEventContract>(),
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
            "/timeline",
            async Task<IResult> (IApplicationDbContext db, string? cycleId, string? tradeId, int take = 200, CancellationToken cancellationToken = default) =>
            {
                var query = db.RuntimeTimelineEvents
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(cycleId))
                {
                    query = query.Where(x => x.CycleId == cycleId);
                }

                if (!string.IsNullOrWhiteSpace(tradeId))
                {
                    query = query.Where(x => x.TradeId == tradeId);
                }

                var events = await query
                    .Take(Math.Clamp(take, 1, 1000))
                    .ToListAsync(cancellationToken);

                // Return newest events first so the monitor feed shows the latest activity at the top.
                var ordered = events.OrderByDescending(x => x.CreatedAtUtc).ToList();

                return TypedResults.Ok(new
                {
                    count = ordered.Count,
                    filters = new { cycleId, tradeId, take = Math.Clamp(take, 1, 1000) },
                    events = ordered.Select(x => new
                    {
                        x.Id,
                        x.EventType,
                        x.Stage,
                        x.Source,
                        x.Symbol,
                        x.CycleId,
                        x.TradeId,
                        createdAtUtc = x.CreatedAtUtc,
                        createdAtKsa = x.CreatedAtUtc.ToOffset(TimeSpan.FromHours(3)),
                        createdAtDubai = x.CreatedAtUtc.ToOffset(TimeSpan.FromHours(4)),
                        createdAtIndia = x.CreatedAtUtc.ToOffset(TimeSpan.FromMinutes(330)),
                        payload = ParseJsonOrRaw(x.PayloadJson),
                    }),
                });
            })
            .WithName("GetRuntimeTimeline")
            .WithDescription("Returns end-to-end runtime timeline events with cycle/trade correlation IDs.");

        monitoring.MapGet(
            "/timeline/markdown",
            async Task<IResult> (IApplicationDbContext db, string? cycleId, string? tradeId, int take = 200, CancellationToken cancellationToken = default) =>
            {
                var query = db.RuntimeTimelineEvents
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(cycleId))
                {
                    query = query.Where(x => x.CycleId == cycleId);
                }

                if (!string.IsNullOrWhiteSpace(tradeId))
                {
                    query = query.Where(x => x.TradeId == tradeId);
                }

                var events = await query
                    .Take(Math.Clamp(take, 1, 1000))
                    .ToListAsync(cancellationToken);

                var ordered = events.OrderBy(x => x.CreatedAtUtc).ToList();
                var markdown = BuildTimelineMarkdown(ordered);

                return TypedResults.Text(markdown, "text/markdown");
            })
            .WithName("GetRuntimeTimelineMarkdown")
            .WithDescription("Returns human-readable timeline markdown with UTC, KSA(AST), Dubai(GST), and India(IST) times.");

        monitoring.MapGet(
            "/runtime-settings",
            IResult (ITradingRuntimeSettingsStore runtimeSettings) =>
            {
                var symbol = runtimeSettings.GetSymbol();
                var autoTradeEnabled = runtimeSettings.GetAutoTradeEnabled();
                var minTradeGrams = runtimeSettings.GetMinTradeGrams();
                var microRotationEnabled = runtimeSettings.GetMicroRotationEnabled();
                return TypedResults.Ok(new { symbol, autoTradeEnabled, minTradeGrams, microRotationEnabled });
            })
            .WithName("GetRuntimeSettings")
            .WithDescription("Returns mutable runtime trading settings managed from app UI.");

        monitoring.MapPut(
            "/runtime-settings",
            IResult (UpdateRuntimeSettingsRequest request, ITradingRuntimeSettingsStore runtimeSettings) =>
            {
                var symbol = (request.Symbol ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return TypedResults.BadRequest(new { error = "symbol is required." });
                }

                runtimeSettings.SetSymbol(symbol);

                if (request.AutoTradeEnabled.HasValue)
                {
                    runtimeSettings.SetAutoTradeEnabled(request.AutoTradeEnabled.Value);
                }

                if (request.MinTradeGrams.HasValue && request.MinTradeGrams.Value > 0m)
                {
                    runtimeSettings.SetMinTradeGrams(request.MinTradeGrams.Value);
                }

                return TypedResults.Ok(new
                {
                    symbol = runtimeSettings.GetSymbol(),
                    autoTradeEnabled = runtimeSettings.GetAutoTradeEnabled(),
                    minTradeGrams = runtimeSettings.GetMinTradeGrams(),
                });
            })
            .WithName("UpdateRuntimeSettings")
            .WithDescription("Updates mutable runtime trading settings without server restart.");

        monitoring.MapPut(
            "/runtime-settings/min-trade-grams",
            IResult (MinTradeGramsRequest request, ITradingRuntimeSettingsStore runtimeSettings) =>
            {
                if (request.MinTradeGrams <= 0m)
                {
                    return TypedResults.BadRequest(new { error = "minTradeGrams must be greater than zero." });
                }
                runtimeSettings.SetMinTradeGrams(request.MinTradeGrams);
                return TypedResults.Ok(new
                {
                    minTradeGrams = runtimeSettings.GetMinTradeGrams(),
                });
            })
            .WithName("SetMinTradeGrams")
            .WithDescription("Updates the minimum trade size in grams. Default is 100 g. Lowering this allows smaller test trades.");

        monitoring.MapPut(
            "/runtime-settings/auto-trade",
            IResult (AutoTradeToggleRequest request, ITradingRuntimeSettingsStore runtimeSettings) =>
            {
                runtimeSettings.SetAutoTradeEnabled(request.Enabled);
                return TypedResults.Ok(new
                {
                    autoTradeEnabled = runtimeSettings.GetAutoTradeEnabled(),
                });
            })
            .WithName("SetAutoTradeEnabled")
            .WithDescription("Toggles Auto Trade mode. When disabled (default), all ARMED trades go to approval queue. When enabled, ARMED trades are routed directly to MT5.");

        monitoring.MapPut(
            "/runtime-settings/micro-rotation",
            IResult (MicroRotationToggleRequest request, ITradingRuntimeSettingsStore runtimeSettings) =>
            {
                runtimeSettings.SetMicroRotationEnabled(request.Enabled);
                return TypedResults.Ok(new
                {
                    microRotationEnabled = runtimeSettings.GetMicroRotationEnabled(),
                });
            })
            .WithName("SetMicroRotationEnabled")
            .WithDescription("Toggles Micro Rotation Mode (refinement spec §D). When enabled: single pending trade at a time, no staggered ladder, BUY_LIMIT/BUY_STOP only with mandatory TP and expiry. Designed for safe live-experience testing with small free balance.");

        monitoring.MapPost(
            "/panic-interrupt",
            async Task<IResult> (
                IPendingTradeStore pendingTrades,
                IMt5ControlStore mt5Control,
                IRuntimeTimelineWriter timeline,
                CancellationToken cancellationToken) =>
            {
                var canceled = pendingTrades.Clear();
                mt5Control.RequestCancelPending("panic_interrupt");

                await timeline.WriteAsync(
                    eventType: "PANIC_INTERRUPT_TRIGGERED",
                    stage: "safety",
                    source: "brain",
                    symbol: "XAUUSD",
                    cycleId: null,
                    tradeId: null,
                    payload: new
                    {
                        trigger = "client_manual_panic",
                        pendingCanceled = canceled,
                        triggeredAtUtc = DateTimeOffset.UtcNow,
                        message = "Global panic interrupt — all pending orders canceled, MT5 cancel signal sent.",
                    },
                    cancellationToken: cancellationToken);

                return TypedResults.Ok(new
                {
                    triggered = true,
                    pendingCanceled = canceled,
                    message = $"Panic interrupt executed. {canceled} pending order(s) cleared. Cancel signal sent to EA.",
                    triggeredAtUtc = DateTimeOffset.UtcNow,
                });
            })
            .WithName("TriggerPanicInterrupt")
            .WithDescription("Global panic interrupt: cancels all pending orders immediately and sends cancel signal to MT5 EA. Use when FAIL is threatened, sudden liquidation, or macro shock.");

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

        monitoring.MapGet(
            "/kpi",
            async Task<IResult> (IApplicationDbContext db, ILatestMarketSnapshotStore snapshotStore, ITradeLedgerService ledger, CancellationToken cancellationToken) =>
            {
                var nowUtc = DateTimeOffset.UtcNow;
                var ksaNow = nowUtc.AddHours(3);
                var ksaDayStart = new DateTimeOffset(ksaNow.Year, ksaNow.Month, ksaNow.Day, 0, 0, 0, TimeSpan.FromHours(3));
                var ksaDayStartUtc = ksaDayStart.ToUniversalTime();

                // KSA week: Mon-Sun
                var dow = (int)ksaNow.DayOfWeek; // 0=Sun
                var daysToMon = dow == 0 ? 6 : dow - 1;
                var ksaWeekStartUtc = ksaDayStartUtc.AddDays(-daysToMon);

                var closedPositions = await db.LedgerPositions
                    .AsNoTracking()
                    .Where(x => x.IsClosed)
                    .ToListAsync(cancellationToken);

                var openPositions = await db.LedgerPositions
                    .AsNoTracking()
                    .Where(x => !x.IsClosed)
                    .ToListAsync(cancellationToken);

                var todayClosed = closedPositions.Where(x => x.ClosedAtUtc >= ksaDayStartUtc).ToList();
                var todayProfitAed = todayClosed.Sum(x => x.NetProfitAed);
                var todayRotations = todayClosed.Count;
                var todayAvgProfit = todayRotations > 0 ? todayProfitAed / todayRotations : 0m;
                var openBuyCount = openPositions.Count;
                // Hit rate: profitable rotations / total closed rotations today
                var todayProfitableRotations = todayClosed.Count(x => x.NetProfitAed > 0m);
                var todayHitRate = todayRotations > 0 ? (double)todayProfitableRotations / todayRotations : 0.0;

                var weeklyClosed = closedPositions.Where(x => x.ClosedAtUtc >= ksaWeekStartUtc).ToList();
                var weeklyProfitAed = weeklyClosed.Sum(x => x.NetProfitAed);
                var weeklyRotations = weeklyClosed.Count;

                // Session classification per PRD: JAPAN 03-12 KSA, INDIA 07-16, LONDON 10-19, NY 15-00
                // When sessions overlap, prefer stored value; otherwise use KSA hour midpoint ordering (JAPAN → INDIA → LONDON → NY)
                static string ClassifySession(DateTimeOffset? closedUtc, string stored)
                {
                    if (!string.IsNullOrEmpty(stored)) return stored;
                    if (closedUtc is null) return "UNKNOWN";
                    var ksaHour = closedUtc.Value.ToOffset(TimeSpan.FromHours(3)).Hour;
                    // NY: 15:00-00:00 (hour 15-23)
                    if (ksaHour >= 15) return "NY";
                    // LONDON: 10:00-15:00
                    if (ksaHour >= 10) return "LONDON";
                    // INDIA: 07:00-10:00
                    if (ksaHour >= 7) return "INDIA";
                    // JAPAN: 03:00-07:00, or post-midnight NY (00:00-03:00)
                    if (ksaHour >= 3) return "JAPAN";
                    return "NY"; // 00:00-03:00 KSA = late NY
                }

                // Waterfall blocks per session from today's decision logs
                var todayWaterfallLogs = await db.DecisionLogs
                    .AsNoTracking()
                    .Where(x => x.CreatedAtUtc >= ksaDayStartUtc && x.WaterfallRisk == "HIGH")
                    .Select(x => new { x.CreatedAtUtc })
                    .ToListAsync(cancellationToken);

                var sessions = new[] { "JAPAN", "INDIA", "LONDON", "NY" };
                var sessionStats = sessions.ToDictionary(s => s, s =>
                {
                    var sp = todayClosed.Where(x => ClassifySession(x.ClosedAtUtc, x.ClosedSession) == s).ToList();
                    var wfBlocks = todayWaterfallLogs.Count(x =>
                    {
                        var ksaH = x.CreatedAtUtc.ToOffset(TimeSpan.FromHours(3)).Hour;
                        return s switch
                        {
                            "JAPAN" => ksaH >= 3 && ksaH < 7,
                            "INDIA" => ksaH >= 7 && ksaH < 10,
                            "LONDON" => ksaH >= 10 && ksaH < 15,
                            "NY" => ksaH >= 15 || ksaH < 3,
                            _ => false,
                        };
                    });
                    return new { profitAed = decimal.Round(sp.Sum(x => x.NetProfitAed), 2), rotations = sp.Count, waterfallBlocks = wfBlocks };
                });

                // Weekly per-session profit stats + best/worst session
                var weeklySessionStats = sessions.ToDictionary(s => s, s =>
                {
                    var sp = weeklyClosed.Where(x => ClassifySession(x.ClosedAtUtc, x.ClosedSession) == s).ToList();
                    return new { profitAed = decimal.Round(sp.Sum(x => x.NetProfitAed), 2), rotations = sp.Count };
                });
                var bestSession = weeklySessionStats.Count > 0
                    ? weeklySessionStats.OrderByDescending(kv => kv.Value.profitAed).First().Key
                    : "N/A";
                var worstSession = weeklySessionStats.Count > 0
                    ? weeklySessionStats.OrderBy(kv => kv.Value.profitAed).First().Key
                    : "N/A";

                // Weekly no-trade blocks by cause
                var weeklyNoTradeLogs = await db.DecisionLogs
                    .AsNoTracking()
                    .Where(x => x.CreatedAtUtc >= ksaWeekStartUtc && x.Status == "NO_TRADE")
                    .Select(x => new { x.Cause })
                    .ToListAsync(cancellationToken);
                var weeklyNoTradeBlocks = weeklyNoTradeLogs
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Cause) ? "UNKNOWN" : x.Cause)
                    .ToDictionary(g => g.Key, g => g.Count());

                snapshotStore.TryGet(out var snapshot);
                var currentBid = snapshot?.Bid ?? 0m;
                var extState = ledger.GetExtendedState(currentBid);

                var account = await db.LedgerAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken);
                var startingInvestment = extState.StartingInvestmentAed > 0m
                    ? extState.StartingInvestmentAed
                    : (account?.InitialCashAed ?? 100000m);
                var currentEquityAed = extState.NetEquityAed > 0m ? extState.NetEquityAed : extState.CashAed;
                var multiple = startingInvestment > 0m ? decimal.Round(currentEquityAed / startingInvestment, 4) : 0m;
                var milestoneReached = multiple >= 4.0m;
                var neededForFourX = Math.Max(0m, startingInvestment * 4m - currentEquityAed);

                return TypedResults.Ok(new
                {
                    todayKsaDate = ksaNow.ToString("yyyy-MM-dd"),
                    todayProfitAed = decimal.Round(todayProfitAed, 2),
                    todayRotations,
                    todayAvgProfitAed = decimal.Round(todayAvgProfit, 2),
                    todayHitRate = Math.Round(todayHitRate, 4),
                    sessionStats,
                    weeklyProfitAed = decimal.Round(weeklyProfitAed, 2),
                    weeklyRotations,
                    weeklySessionStats,
                    weeklyBestSession = bestSession,
                    weeklyWorstSession = worstSession,
                    weeklyNoTradeBlocks,
                    compounding = new
                    {
                        startingInvestmentAed = decimal.Round(startingInvestment, 2),
                        currentEquityAed = decimal.Round(currentEquityAed, 2),
                        multiple,
                        milestoneReached,
                        neededForFourXAed = decimal.Round(neededForFourX, 2),
                    },
                    openPositionsCount = openPositions.Count,
                    openBuyCount,
                    studyLockActive = false,
                });
            })
            .WithName("GetKpi")
            .WithDescription("Returns today/weekly KPI metrics, session stats, and compounding data.");

        return group;
    }

    private static object ParseJsonOrRaw(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<object>(payload) ?? new { raw = payload };
        }
        catch
        {
            return new { raw = payload };
        }
    }

    private static string BuildTimelineMarkdown(IReadOnlyCollection<RuntimeTimelineEvent> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Runtime Timeline");
        sb.AppendLine();
        sb.AppendLine($"Total events: {events.Count}");
        sb.AppendLine();

        foreach (var item in events)
        {
            var utc = item.CreatedAtUtc;
            var ksa = utc.ToOffset(TimeSpan.FromHours(3));
            var dubai = utc.ToOffset(TimeSpan.FromHours(4));
            var india = utc.ToOffset(TimeSpan.FromMinutes(330));

            sb.AppendLine($"- Time UTC {utc:yyyy-MM-dd HH:mm:ss} | KSA(AST) {ksa:HH:mm:ss} | Dubai(GST) {dubai:HH:mm:ss} | India(IST) {india:HH:mm:ss}");
            sb.AppendLine($"  Event: {item.EventType} [{item.Stage}] source={item.Source} symbol={item.Symbol}");
            sb.AppendLine($"  Summary: {DescribeEvent(item)}");
            sb.AppendLine($"  Correlation: cycle_id={item.CycleId ?? "-"}, trade_id={item.TradeId ?? "-"}");
            sb.AppendLine($"  Data: {item.PayloadJson}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string DescribeEvent(RuntimeTimelineEvent item)
    {
        return item.EventType switch
        {
            "MT5_MARKET_SNAPSHOT_RECEIVED" => "MT5 sent a market snapshot to Brain.",
            "CYCLE_STARTED" => "Brain started a new decision cycle using latest market data.",
            "RULE_ENGINE_SETUP_CANDIDATE" => "Rule engine validated the setup candidate — cycle continues to news filter.",
            "RULE_ENGINE_ABORT" => "Rule engine rejected the setup — no structural basis for a trade.",
            "AI_SKIPPED_RULE_ENGINE_ABORT" => "AI analysis was skipped because the rule engine already rejected this setup.",
            "NEWS_CHECK" => "Economic news filter assessed nearby high-impact events.",
            "CYCLE_ABORTED" => "Cycle was aborted before reaching AI analysis.",
            "AI_ANALYZE_REQUEST" => "Brain sent snapshot + prompt policy to AI worker.",
            "TELEGRAM_INTERPRETED" => "AI worker interpreted Telegram channels into a market stance.",
            "AI_PRE_STAGE_COMPLETED" => "AI pre-stage completed and produced provider votes.",
            "AI_COMMITTEE_EVALUATED" => "AI committee compared provider outputs and checked agreement.",
            "AI_VALIDATION_EVALUATED" => "Validation stage compared candidate signal consistency.",
            "AI_ANALYZE_RESPONSE" => "Brain received the AI decision payload.",
            "AI_CONSENSUS_FAILED" => "Trade was blocked because AI quorum failed.",
            "TRADE_SCORE_CALCULATION" => "Trade scoring layer calculated setup quality across structure, momentum, execution, AI, and sentiment.",
            "DECISION_EVALUATED" => "Decision engine evaluated risk and trade permissions.",
            "TRADE_ROUTED" => "Trade was routed to MT5 queue or manual approval queue.",
            "FINAL_DECISION" => "Final cycle verdict: trade approved or rejected with primary reason.",
            "MT5_PENDING_TRADE_DEQUEUED" => "MT5 EA pulled a pending trade from Brain.",
            "MT5_TRADE_STATUS_RECEIVED" => "MT5 sent execution status back to Brain.",
            _ => $"Recorded event {item.EventType} at stage {item.Stage}.",
        };
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

public sealed record UpdateRuntimeSettingsRequest(string? Symbol, bool? AutoTradeEnabled = null, decimal? MinTradeGrams = null);

public sealed record AutoTradeToggleRequest(bool Enabled);

public sealed record MinTradeGramsRequest(decimal MinTradeGrams);

public sealed record MicroRotationToggleRequest(bool Enabled);

