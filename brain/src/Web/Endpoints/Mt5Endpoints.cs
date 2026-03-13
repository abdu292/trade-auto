using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Application.Common.Services;
using Brain.Domain.Entities;
using Brain.Web.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace Brain.Web.Endpoints;

public static class Mt5Endpoints
{
    public static IEndpointRouteBuilder MapMt5Endpoints(this IEndpointRouteBuilder app)
    {
        var mt5Group = app.MapGroup("/mt5")
            .AddEndpointFilter<TradeApiSecurityFilter>()
            .WithTags("MT5 Expert Advisor");

        mt5Group.MapGet(
            "/pending-trades",
            async Task<IResult> (IPendingTradeStore pendingTradeStore, ILogger<object> logger, IRuntimeTimelineWriter timeline, CancellationToken cancellationToken) =>
            {
                logger.LogInformation("→ GET /mt5/pending-trades");

                if (!pendingTradeStore.TryDequeue(out var trade) || trade is null)
                {
                    logger.LogInformation("← GET /mt5/pending-trades no pending trades");
                    return TypedResults.NoContent();
                }

                logger.LogInformation("← GET /mt5/pending-trades returns {TradeId} {Type} @ {Price}",
                    trade.Id, trade.Type, trade.Price);

                await timeline.WriteAsync(
                    eventType: "MT5_PENDING_TRADE_DEQUEUED",
                    stage: "mt5_bridge",
                    source: "brain",
                    symbol: trade.Symbol,
                    cycleId: trade.CycleId,
                    tradeId: trade.Id.ToString(),
                    payload: new
                    {
                        trade.Id,
                        trade.CycleId,
                        trade.Type,
                        trade.Symbol,
                        trade.Price,
                        trade.Tp,
                        trade.Expiry,
                        trade.Ml,
                        trade.Grams,
                        trade.EngineState,
                        trade.Cause,
                    },
                    cancellationToken);

                return TypedResults.Ok(new
                {
                    id = trade.Id,
                    cycleId = trade.CycleId,
                    type = trade.Type,
                    symbol = trade.Symbol,
                    price = trade.Price,
                    tp = trade.Tp,
                    expiry = trade.Expiry,
                    ml = trade.Ml,
                    grams = trade.Grams,
                    alignmentScore = trade.AlignmentScore,
                    regime = trade.Regime,
                    riskTag = trade.RiskTag,
                    engineState = trade.EngineState,
                    mode = trade.Mode,
                    cause = trade.Cause,
                    waterfallRisk = trade.WaterfallRisk,
                    bucket = trade.Bucket,
                    session = trade.Session,
                    sessionPhase = trade.SessionPhase,
                    regimeTag = trade.RegimeTag,
                    riskState = trade.RiskState,
                    sizeClass = trade.SizeClass,
                    telegramState = trade.TelegramState,
                    // Section 8.2: Full TABLE columns
                    shopBuy = trade.ShopBuy,
                    shopSell = trade.ShopSell,
                    expiryKsa = trade.ExpiryKSA,
                    expiryServer = trade.ExpiryServer,
                });
            })
            .WithName("GetPendingTrades")
            .WithDescription("Fetch pending trade orders for MT5 Expert Advisor");

        mt5Group.MapGet(
            "/control/cancel-pending/consume",
            IResult (IMt5ControlStore controlStore) =>
            {
                if (!controlStore.TryConsumeCancelPending(out var reason))
                {
                    return TypedResults.Ok(new { cancelPending = false, reason = string.Empty });
                }

                return TypedResults.Ok(new { cancelPending = true, reason });
            })
            .WithName("ConsumeCancelPendingControl")
            .WithDescription("EA consumes kill-switch command to cancel all broker pending orders.");

        mt5Group.MapGet(
            "/control/fetch-history/consume",
            IResult (IHistoryFetchStore fetchStore, ILogger<object> logger) =>
            {
                var request = fetchStore.TryConsume();
                if (request is null)
                {
                    return TypedResults.Ok(new { hasFetchRequest = false });
                }

                logger.LogInformation(
                    "[ReplayFetch] EA consumed fetch request Symbol={Symbol} From={From:o} To={To:o} Timeframes={Timeframes}",
                    request.Symbol,
                    request.From,
                    request.To,
                    string.Join(",", request.Timeframes));

                return TypedResults.Ok(new
                {
                    hasFetchRequest = true,
                    symbol = request.Symbol,
                    timeframes = request.Timeframes,
                    from = request.From.ToUnixTimeSeconds(),
                    to = request.To.ToUnixTimeSeconds(),
                    fromIso = request.From.UtcDateTime.ToString("o"),
                    toIso = request.To.UtcDateTime.ToString("o"),
                });
            })
            .WithName("ConsumeFetchHistoryControl")
            .WithDescription(
                "EA polls this endpoint to check for a pending history-fetch request. " +
                "If a request is pending, the EA fetches the specified candles using CopyRates " +
                "and POSTs them to POST /api/replay/mt5-history in batches.");


        mt5Group.MapPost(
            "/market-snapshot",
            async Task<IResult> (
                Mt5MarketSnapshotRequest request,
                ILatestMarketSnapshotStore snapshotStore,
                IChartDataStore chartDataStore,
                ITradeLedgerService ledger,
                IRuntimeTimelineWriter timeline,
                ILogger<object> logger,
                IHttpClientFactory httpClientFactory,
                CancellationToken cancellationToken) =>
            {
                var internalClockUtc = DateTimeOffset.UtcNow;
                var utcReferenceTime = request.Timestamp;
                var mt5ServerOffset = TimeSpan.FromMinutes(130); // MT5 server = UTC+2:10 per PRD relation (KSA-50m)
                var expectedMt5FromUtc = utcReferenceTime.ToOffset(mt5ServerOffset);
                var rawMt5ServerTime = request.Mt5ServerTime ?? expectedMt5FromUtc;
                var mt5ServerTime = rawMt5ServerTime.Offset == mt5ServerOffset
                    ? rawMt5ServerTime
                    : rawMt5ServerTime.ToOffset(mt5ServerOffset);
                var timeSkewMs = Math.Abs((decimal)(mt5ServerTime - expectedMt5FromUtc).TotalMilliseconds);

                var mt5ToKsaOffsetMinutes = request.Mt5ToKsaOffsetMinutes ?? 50;
                var ksaTime = mt5ServerTime.AddMinutes(mt5ToKsaOffsetMinutes).ToOffset(TimeSpan.FromHours(3));
                var uaeTime = ksaTime.ToOffset(TimeSpan.FromHours(4));
                var indiaTime = ksaTime.ToOffset(TimeSpan.FromMinutes(330));
                var volatilityExpansion = request.VolatilityExpansion ?? (request.Adr <= 0 ? 0 : request.Atr / request.Adr);
                // Canonical session from server time only (single session engine; do not use EA-provided session)
                var (resolvedSession, resolvedPhase) = TradingSessionClock.Resolve(mt5ServerTime);
                // London/NY overlap: Server 14:55–15:25 (spec). Derive from time so regime labels match.
                var serverTod = mt5ServerTime.TimeOfDay;
                var isLondonNyOverlap = serverTod >= TimeSpan.FromMinutes(14 * 60 + 55) && serverTod < TimeSpan.FromMinutes(15 * 60 + 25);

                var mt5Mid = request.Bid.HasValue && request.Ask.HasValue
                    ? (request.Bid.Value + request.Ask.Value) / 2m
                    : (request.TimeframeData.FirstOrDefault(x => string.Equals(x.Timeframe, "M15", StringComparison.OrdinalIgnoreCase))?.Close
                        ?? request.TimeframeData.FirstOrDefault()?.Close
                        ?? 0m);

                var systemFetchedGoldRate = await TryFetchSystemGoldRateAsync(httpClientFactory, cancellationToken);
                var rateDeltaUsd = (systemFetchedGoldRate.HasValue && mt5Mid > 0m)
                    ? Math.Abs(systemFetchedGoldRate.Value - mt5Mid)
                    : 0m;
                var rateAuthority = "MT5_FALLBACK";
                var authoritativeRate = mt5Mid;

                if (systemFetchedGoldRate.HasValue)
                {
                    if (rateDeltaUsd > 10m && mt5Mid > 0m)
                    {
                        rateAuthority = "MT5_REFERENCE";
                        authoritativeRate = mt5Mid;
                    }
                    else
                    {
                        rateAuthority = "SYSTEM_FETCHED";
                        authoritativeRate = systemFetchedGoldRate.Value;
                    }
                }

                var timeframeData = request.TimeframeData.Select(tf =>
                    new TimeframeDataContract(
                        Timeframe: tf.Timeframe,
                        Open: tf.Open,
                        High: tf.High,
                        Low: tf.Low,
                        Close: tf.Close,
                        Volume: tf.Volume,
                        CandleStartTime: tf.CandleStartTime,
                        CandleCloseTime: tf.CandleCloseTime,
                        CandleBodySize: tf.CandleBodySize ?? 0m,
                        UpperWickSize: tf.UpperWickSize ?? 0m,
                        LowerWickSize: tf.LowerWickSize ?? 0m,
                        CandleRange: tf.CandleRange ?? 0m,
                        Ma20Value: tf.Ma20Value ?? 0m,
                        Ma20Distance: tf.Ma20Distance ?? 0m,
                        Rsi: tf.Rsi ?? 0m,
                        Atr: tf.Atr ?? 0m)).ToArray();

                var pendingOrders = (request.PendingOrders ?? Array.Empty<Mt5PendingOrderRequest>())
                    .Select(item => new PendingOrderSnapshotContract(
                        Type: item.Type,
                        Price: item.Price,
                        Tp: item.Tp,
                        Expiry: item.Expiry,
                        VolumeGramsEquivalent: item.VolumeGramsEquivalent ?? 0m))
                    .ToArray();

                var openPositions = (request.OpenPositions ?? Array.Empty<Mt5OpenPositionRequest>())
                    .Select(item => new OpenPositionSnapshotContract(
                        EntryPrice: item.EntryPrice,
                        CurrentPnlPoints: item.CurrentPnlPoints,
                        Tp: item.Tp,
                        VolumeGramsEquivalent: item.VolumeGramsEquivalent ?? 0m))
                    .ToArray();

                var executionEvents = (request.OrderExecutionEvents ?? Array.Empty<Mt5OrderExecutionEventRequest>())
                    .Select(item => new OrderExecutionEventContract(
                        Status: item.Status,
                        Timestamp: item.Timestamp,
                        Price: item.Price ?? 0m,
                        VolumeGramsEquivalent: item.VolumeGramsEquivalent ?? 0m,
                        Ticket: item.Ticket ?? 0UL))
                    .ToArray();

                var snapshot = new MarketSnapshotContract(
                    Symbol: request.Symbol,
                    TimeframeData: timeframeData,
                    Atr: request.Atr,
                    Adr: request.Adr,
                    Ma20: request.Ma20,
                    Ma20H4: request.Ma20H4 ?? request.Ma20,
                    Ma20H1: request.Ma20H1 ?? request.Ma20,
                    Ma20M30: request.Ma20M30 ?? request.Ma20,
                    RsiH1: request.RsiH1 ?? 0m,
                    RsiM15: request.RsiM15 ?? 0m,
                    AtrH1: request.AtrH1 ?? request.Atr,
                    AtrM15: request.AtrM15 ?? request.Atr,
                    PreviousDayHigh: request.PreviousDayHigh ?? 0m,
                    PreviousDayLow: request.PreviousDayLow ?? 0m,
                    WeeklyHigh: request.WeeklyHigh ?? 0m,
                    WeeklyLow: request.WeeklyLow ?? 0m,
                    DayOpen: request.DayOpen ?? 0m,
                    WeekOpen: request.WeekOpen ?? 0m,
                    SessionHigh: request.SessionHigh ?? 0m,
                    SessionLow: request.SessionLow ?? 0m,
                    SessionHighJapan: request.SessionHighJapan ?? 0m,
                    SessionLowJapan: request.SessionLowJapan ?? 0m,
                    SessionHighIndia: request.SessionHighIndia ?? 0m,
                    SessionLowIndia: request.SessionLowIndia ?? 0m,
                    SessionHighLondon: request.SessionHighLondon ?? 0m,
                    SessionLowLondon: request.SessionLowLondon ?? 0m,
                    SessionHighNy: request.SessionHighNy ?? 0m,
                    SessionLowNy: request.SessionLowNy ?? 0m,
                    PreviousSessionHigh: request.PreviousSessionHigh ?? 0m,
                    PreviousSessionLow: request.PreviousSessionLow ?? 0m,
                    Ema50H1: request.Ema50H1 ?? 0m,
                    Ema200H1: request.Ema200H1 ?? 0m,
                    AdrUsedPct: request.AdrUsedPct ?? 0m,
                    Session: resolvedSession,
                    Timestamp: request.Timestamp,
                    VolatilityExpansion: volatilityExpansion,
                    DayOfWeek: mt5ServerTime.DayOfWeek,
                    Mt5ServerTime: mt5ServerTime,
                    KsaTime: ksaTime,
                    UaeTime: uaeTime,
                    IndiaTime: indiaTime,
                    InternalClockUtc: internalClockUtc,
                    UtcReferenceTime: utcReferenceTime,
                    TimeSkewMs: timeSkewMs,
                    Mt5ToKsaOffsetMinutes: mt5ToKsaOffsetMinutes,
                    TelegramImpactTag: NormalizeImpactTag(request.TelegramImpactTag),
                    TradingViewConfirmation: NormalizeConfirmationTag(request.TradingViewConfirmation),
                    IsCompression: request.IsCompression ?? false,
                    IsExpansion: request.IsExpansion ?? false,
                    IsAtrExpanding: request.IsAtrExpanding ?? false,
                    HasOverlapCandles: request.HasOverlapCandles ?? false,
                    HasImpulseCandles: request.HasImpulseCandles ?? false,
                    HasLiquiditySweep: request.HasLiquiditySweep ?? false,
                    HasPanicDropSequence: request.HasPanicDropSequence ?? false,
                    IsPostSpikePullback: request.IsPostSpikePullback ?? false,
                    IsLondonNyOverlap: request.IsLondonNyOverlap ?? isLondonNyOverlap,
                    IsBreakoutConfirmed: request.IsBreakoutConfirmed ?? false,
                    IsUsRiskWindow: request.IsUsRiskWindow ?? false,
                    IsFriday: mt5ServerTime.DayOfWeek == DayOfWeek.Friday,
                    Bid: request.Bid ?? 0m,
                    Ask: request.Ask ?? 0m,
                    Spread: request.Spread ?? 0m,
                    SpreadMedian60m: request.SpreadMedian60m ?? request.SpreadAvg5m ?? 0m,
                    SpreadMax60m: request.SpreadMax60m ?? request.SpreadMax5m ?? 0m,
                    CompressionCountM15: request.CompressionCountM15 ?? 0,
                    ExpansionCountM15: request.ExpansionCountM15 ?? 0,
                    ImpulseStrengthScore: request.ImpulseStrengthScore ?? 0m,
                    TelegramState: NormalizeTelegramState(request.TelegramState),
                    PanicSuspected: request.PanicSuspected ?? false,
                    TvAlertType: NormalizeTvAlertType(request.TvAlertType),
                    SessionPhase: resolvedPhase,
                    SpreadMin1m: request.SpreadMin1m ?? 0m,
                    SpreadAvg1m: request.SpreadAvg1m ?? 0m,
                    SpreadMax1m: request.SpreadMax1m ?? 0m,
                    SpreadMin5m: request.SpreadMin5m ?? 0m,
                    SpreadAvg5m: request.SpreadAvg5m ?? 0m,
                    SpreadMax5m: request.SpreadMax5m ?? 0m,
                    FreeMargin: request.FreeMargin ?? 0m,
                    Equity: request.Equity ?? 0m,
                    Balance: request.Balance ?? 0m,
                    TickRatePer30s: request.TickRatePer30s ?? 0m,
                    FreezeGapDetected: request.FreezeGapDetected ?? false,
                    SlippageEstimatePoints: request.SlippageEstimatePoints ?? 0m,
                    SessionVwap: request.SessionVwap ?? 0m,
                    SystemFetchedGoldRate: systemFetchedGoldRate ?? 0m,
                    RateDeltaUsd: rateDeltaUsd,
                    RateAuthority: rateAuthority,
                    AuthoritativeRate: authoritativeRate,
                    CompressionRangesM15: (request.CompressionRangesM15 ?? Array.Empty<decimal>()).ToArray(),
                    PendingOrders: pendingOrders,
                    OpenPositions: openPositions,
                    OrderExecutionEvents: executionEvents,
                    DxyBid: request.DxyBid,
                    SilverBid: request.SilverBid);

                snapshotStore.Upsert(snapshot);

                if (request.RecentCandlesM15 is { Count: > 0 })
                {
                    var candles = request.RecentCandlesM15
                        .Select(c => new CandlePoint(
                            Time: c.Time ?? DateTimeOffset.MinValue,
                            Open: c.Open,
                            High: c.High,
                            Low: c.Low,
                            Close: c.Close,
                            Volume: c.Volume))
                        .ToList();
                    chartDataStore.SetM15Candles(candles);
                }

                // Physical ledger is source of truth — only updated by user (Set Physical) or slips/deposits/withdrawals.
                // Do NOT overwrite ledger from MT5; MT5 is execution infrastructure only.

                await timeline.WriteAsync(
                    eventType: "MT5_MARKET_SNAPSHOT_RECEIVED",
                    stage: "mt5_ingest",
                    source: "mt5",
                    symbol: snapshot.Symbol,
                    cycleId: null,
                    tradeId: null,
                    payload: new
                    {
                        snapshot.Symbol,
                        snapshot.Timestamp,
                        snapshot.Mt5ServerTime,
                        snapshot.KsaTime,
                        snapshot.UaeTime,
                        snapshot.IndiaTime,
                        snapshot.Bid,
                        snapshot.Ask,
                        snapshot.Spread,
                        snapshot.TimeframeData,
                        snapshot.PendingOrders,
                        snapshot.OpenPositions,
                        snapshot.OrderExecutionEvents,
                        snapshot.TelegramState,
                        snapshot.Session,
                        snapshot.SessionPhase,
                        snapshot.RateAuthority,
                        snapshot.AuthoritativeRate,
                    },
                    cancellationToken);

                var tickTelemetry = snapshotStore.GetTickTelemetry(1);
                logger.LogInformation(
                    "→ POST /mt5/market-snapshot stored {Symbol} snapshot #{TickCount} ({TimeframeCount} TFs, session={Session}/{Phase}, regimeVol={VolatilityExpansion:0.00}, spread={Spread:0.000}, lagMs={LagMs:0}, mt5={Mt5Time}, ksa={KsaTime}, india={IndiaTime}, authority={RateAuthority}, rateDelta={RateDelta:0.00}, timeSkewMs={TimeSkew:0})",
                    snapshot.Symbol,
                    tickTelemetry.TotalIngested,
                    timeframeData.Length,
                    resolvedSession,
                    resolvedPhase,
                    volatilityExpansion,
                    snapshot.Spread,
                    tickTelemetry.LastIngestionLatencyMs,
                    mt5ServerTime,
                    ksaTime,
                    indiaTime,
                    rateAuthority,
                    rateDeltaUsd,
                    timeSkewMs);

                return TypedResults.Ok(new { received = true });
            })
            .WithName("PostMarketSnapshot")
            .WithDescription("Expert Advisor posts latest market snapshot for AI analysis");

        mt5Group.MapPost(
            "/trade-status",
            async Task<IResult> (
                HttpRequest httpRequest,
                ILogger<object> logger,
                ITradeLedgerService ledger,
                ILatestMarketSnapshotStore snapshotStore,
                IRuntimeTimelineWriter timeline,
                IApplicationDbContext db,
                INotificationService notification,
                IExpectedEntryStore expectedEntryStore,
                CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(httpRequest.Body);
                var rawBody = await reader.ReadToEndAsync(cancellationToken);
                var sanitizedBody = rawBody.TrimEnd('\0', ' ', '\r', '\n', '\t');

                Mt5TradeStatusRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<Mt5TradeStatusRequest>(
                        sanitizedBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Invalid JSON payload on /mt5/trade-status. RawBody={RawBody}", rawBody);
                    return TypedResults.BadRequest(new { error = "Invalid JSON payload" });
                }

                if (request is null || string.IsNullOrWhiteSpace(request.TradeId) || string.IsNullOrWhiteSpace(request.Status))
                {
                    return TypedResults.BadRequest(new { error = "tradeId and status are required" });
                }

                logger.LogInformation(
                    "→ POST /mt5/trade-status: TradeId={TradeId}, Status={Status}",
                    request.TradeId, request.Status);

                await timeline.WriteAsync(
                    eventType: "MT5_TRADE_STATUS_RECEIVED",
                    stage: "execution",
                    source: "mt5",
                    symbol: snapshotStore.TryGet(out var seenSnapshot) && seenSnapshot is not null ? seenSnapshot.Symbol : "XAUUSD.gram",
                    cycleId: null,
                    tradeId: request.TradeId,
                    payload: new
                    {
                        request.TradeId,
                        request.Status,
                        request.Mt5Price,
                        request.Grams,
                        request.Mt5Time,
                        request.Ticket,
                    },
                    cancellationToken);

                var normalizedStatus = request.Status.Trim().ToUpperInvariant();
                var mt5Time = request.Mt5Time ?? DateTimeOffset.UtcNow;
                if (!Guid.TryParse(request.TradeId, out var tradeId))
                {
                    return TypedResults.BadRequest(new { error = "Invalid tradeId" });
                }

                if (normalizedStatus is "BUY_TRIGGERED" or "BUY_FILLED" or "FILLED_BUY")
                {
                    var openedSession = snapshotStore.TryGet(out var buySnap) && buySnap is not null
                        ? buySnap.Session
                        : string.Empty;
                    var actualFill = request.Mt5Price ?? 0m;
                    var grams = request.Grams ?? 0m;
                    var expectedEntry = expectedEntryStore.TryGet(tradeId, out var expected) ? expected : (decimal?)null;
                    var slippagePips = expectedEntry.HasValue ? actualFill - expectedEntry.Value : (decimal?)null;
                    var slippageUsd = expectedEntry.HasValue && grams > 0m
                        ? (actualFill - expectedEntry.Value) * grams * 0.03215m
                        : (decimal?)null;

                    var buySlip = ledger.ApplyBuyFill(
                        tradeId,
                        grams,
                        actualFill,
                        mt5Time,
                        openedSession);

                    await timeline.WriteAsync(
                        eventType: "TRADE_TRIGGERED",
                        stage: "execution",
                        source: "mt5",
                        symbol: snapshotStore.TryGet(out var _s) && _s is not null ? _s.Symbol : "XAUUSD.gram",
                        cycleId: null,
                        tradeId: request.TradeId,
                        payload: new
                        {
                            request.TradeId,
                            ExpectedEntry = expectedEntry,
                            ActualFill = actualFill,
                            SlippagePips = slippagePips,
                            SlippageUSD = slippageUsd,
                            Grams = grams,
                            request.Mt5Time,
                            lifecycleEvent = "TradeActive",
                        },
                        cancellationToken);
                    if (expectedEntry.HasValue)
                    {
                        await timeline.WriteAsync(
                            eventType: "TABLE_COMPILER",
                            stage: "execution",
                            source: "mt5",
                            symbol: snapshotStore.TryGet(out var _s2) && _s2 is not null ? _s2.Symbol : "XAUUSD.gram",
                            cycleId: null,
                            tradeId: request.TradeId,
                            payload: new
                            {
                                EventType = "FILL",
                                Session = openedSession,
                                ExpectedEntry = expectedEntry.Value,
                                ActualFill = actualFill,
                                SlippageUSD = slippageUsd,
                                Outcome = "TRADE_ACTIVE",
                                Grams = grams,
                            },
                            cancellationToken);
                    }

                    await notification.NotifyAsync("BUY SLIP", buySlip.Message, cancellationToken);

                    if (snapshotStore.TryGet(out var latest) && latest is not null)
                    {
                        var signal = TelegramSignal.Create(
                            channelKey: $"consensus:{latest.TelegramState.ToLowerInvariant()}",
                            direction: latest.TelegramState.Contains("SELL", StringComparison.OrdinalIgnoreCase) ? "SELL" : "BUY",
                            confidence: 0.5m,
                            consensusState: latest.TelegramState,
                            panicSuspected: latest.PanicSuspected,
                            serverTimeUtc: DateTimeOffset.UtcNow,
                            rawMessage: $"mt5_status={normalizedStatus};tradeId={tradeId}");
                        db.TelegramSignals.Add(signal);

                        var consensusKey = $"consensus:{latest.TelegramState.ToLowerInvariant()}";
                        var consensusChannel = await db.TelegramChannels.FirstOrDefaultAsync(x => x.ChannelKey == consensusKey, cancellationToken);
                        if (consensusChannel is null)
                        {
                            consensusChannel = TelegramChannel.Create(
                                channelKey: consensusKey,
                                name: $"Consensus {latest.TelegramState}",
                                type: "MIXED",
                                weight: 1.0m);
                            db.TelegramChannels.Add(consensusChannel);
                        }
                        consensusChannel.TouchActive();

                        await db.SaveChangesAsync(cancellationToken);
                    }

                    return TypedResults.Ok(new
                    {
                        received = true,
                        slip = buySlip,
                        ledger = ledger.GetState(),
                    });
                }

                if (normalizedStatus is "TP_HIT" or "TP_FILLED" or "SELL_TP_FILLED")
                {
                    var closedSession = snapshotStore.TryGet(out var sellSnap) && sellSnap is not null
                        ? sellSnap.Session
                        : string.Empty;
                    var sellSlip = ledger.ApplySellFill(
                        tradeId,
                        request.Mt5Price ?? 0m,
                        mt5Time,
                        closedSession);

                    if (sellSlip is null)
                    {
                        return TypedResults.Ok(new
                        {
                            received = true,
                            duplicateIgnored = true,
                            ledger = ledger.GetState(),
                        });
                    }

                    await timeline.WriteAsync(
                        eventType: "TRADE_CLOSED",
                        stage: "execution",
                        source: "mt5",
                        symbol: snapshotStore.TryGet(out var _s3) && _s3 is not null ? _s3.Symbol : "XAUUSD.gram",
                        cycleId: null,
                        tradeId: request.TradeId,
                        payload: new
                        {
                            request.TradeId,
                            Status = normalizedStatus,
                            lifecycleEvent = "TradeClosed",
                            Outcome = "TP_HIT",
                            request.Mt5Price,
                            request.Mt5Time,
                        },
                        cancellationToken);
                    await timeline.WriteAsync(
                        eventType: "TABLE_COMPILER",
                        stage: "execution",
                        source: "mt5",
                        symbol: snapshotStore.TryGet(out var _s4) && _s4 is not null ? _s4.Symbol : "XAUUSD.gram",
                        cycleId: null,
                        tradeId: request.TradeId,
                        payload: new
                        {
                            EventType = "CLOSE",
                            Session = closedSession,
                            Outcome = "TP_HIT",
                            ClosePrice = request.Mt5Price,
                            request.Mt5Time,
                            NetProfitAed = sellSlip.NetProfitAed,
                        },
                        cancellationToken);

                    await notification.NotifyAsync("SELL SLIP", sellSlip.Message, cancellationToken);

                    if (snapshotStore.TryGet(out var latest) && latest is not null)
                    {
                        var signalWindowStart = DateTimeOffset.UtcNow.AddHours(-6);
                        var sourceChannelKeys = await db.TelegramSignals
                            .AsNoTracking()
                            .Where(x => x.ServerTimeUtc >= signalWindowStart)
                            .OrderByDescending(x => x.ServerTimeUtc)
                            .Select(x => x.ChannelKey)
                            .Distinct()
                            .OrderBy(x => x)
                            .Take(40)
                            .ToListAsync(cancellationToken);

                        var consensusKey = $"consensus:{latest.TelegramState.ToLowerInvariant()}";
                        if (!sourceChannelKeys.Contains(consensusKey))
                        {
                            sourceChannelKeys.Add(consensusKey);
                        }

                        foreach (var key in sourceChannelKeys)
                        {
                            var channel = await db.TelegramChannels.FirstOrDefaultAsync(x => x.ChannelKey == key, cancellationToken);
                            if (channel is null)
                            {
                                channel = TelegramChannel.Create(
                                    channelKey: key,
                                    name: key,
                                    type: key.StartsWith("consensus:", StringComparison.OrdinalIgnoreCase) ? "MIXED" : "INTRADAY",
                                    weight: 1.0m);
                                db.TelegramChannels.Add(channel);
                            }

                            channel.ApplyOutcome("GOOD");
                        }
                        await db.SaveChangesAsync(cancellationToken);
                    }

                    return TypedResults.Ok(new
                    {
                        received = true,
                        slip = sellSlip,
                        ledger = ledger.GetState(),
                    });
                }

                if (normalizedStatus is "FAILED" or "REJECTED_RISK_GUARD" or "CANCELED" or "CANCELLED")
                {
                    if (snapshotStore.TryGet(out var latest) && latest is not null)
                    {
                        var channelKey = $"consensus:{latest.TelegramState.ToLowerInvariant()}";
                        var channel = await db.TelegramChannels.FirstOrDefaultAsync(x => x.ChannelKey == channelKey, cancellationToken);
                        if (channel is not null)
                        {
                            channel.ApplyOutcome("BAD_CONTEXT");
                            await db.SaveChangesAsync(cancellationToken);
                        }
                    }
                }

                logger.LogInformation(
                    "← /mt5/trade-status: Status callback processed");
                return TypedResults.Ok(new { received = true });
            })
            .WithName("UpdateTradeStatus")
            .WithDescription("Expert Advisor sends trade execution status callback");

        return app;
    }

    private static async Task<decimal?> TryFetchSystemGoldRateAsync(IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(4);

        var stooq = await TryFetchStooqAsync(client, cancellationToken);
        if (stooq.HasValue && stooq.Value > 0m)
        {
            return stooq.Value;
        }

        var goldApi = await TryFetchGoldApiAsync(client, cancellationToken);
        if (goldApi.HasValue && goldApi.Value > 0m)
        {
            return goldApi.Value;
        }

        return null;
    }

    private static async Task<decimal?> TryFetchStooqAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync("https://stooq.com/q/l/?s=xauusd&i=1", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var lines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                return null;
            }

            var values = lines[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < 7)
            {
                return null;
            }

            if (decimal.TryParse(values[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var close) && close > 0m)
            {
                return close;
            }
        }
        catch
        {
        }

        return null;
    }

    private static async Task<decimal?> TryFetchGoldApiAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync("https://api.gold-api.com/price/XAU", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.TryGetProperty("price", out var priceProp)
                && priceProp.ValueKind == JsonValueKind.Number
                && priceProp.TryGetDecimal(out var price)
                && price > 0m)
            {
                return price;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string NormalizeImpactTag(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "HIGH" => "HIGH",
            "MODERATE" => "MODERATE",
            _ => "LOW",
        };
    }

    private static string NormalizeConfirmationTag(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "CONFIRM" => "CONFIRM",
            "CONTRADICT" => "CONTRADICT",
            _ => "NEUTRAL",
        };
    }

    private static string NormalizeTelegramState(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "STRONG_BUY" => "STRONG_BUY",
            "BUY" => "BUY",
            "MIXED" => "MIXED",
            "SELL" => "SELL",
            "STRONG_SELL" => "STRONG_SELL",
            _ => "QUIET",
        };
    }

    private static string NormalizeTvAlertType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "BREAKOUT" => "BREAKOUT",
            "LID_BREAK" => "LID_BREAK",
            "RETEST_HOLD" => "RETEST_HOLD",
            "SHELF_RECLAIM" => "SHELF_RECLAIM",
            "ADR_EXHAUSTION" => "ADR_EXHAUSTION",
            "EXHAUSTION" => "EXHAUSTION",
            "RSI_OVERHEAT" => "RSI_OVERHEAT",
            "RSI_DIVERGENCE" => "RSI_DIVERGENCE",
            "ADR_EXTREME" => "ADR_EXTREME",
            "SESSION_BREAK" => "SESSION_BREAK",
            _ => "NONE",
        };
    }
}

public sealed record Mt5TradeStatusRequest(
    string TradeId,
    string Status,
    decimal? Mt5Price,
    decimal? Grams,
    DateTimeOffset? Mt5Time,
    long? Ticket);
public sealed record Mt5MarketSnapshotRequest(
    string Symbol,
    IReadOnlyCollection<Mt5TimeframeDataRequest> TimeframeData,
    decimal Atr,
    decimal Adr,
    decimal Ma20,
    decimal? Ma20H4,
    decimal? Ma20H1,
    decimal? Ma20M30,
    decimal? RsiH1,
    decimal? RsiM15,
    decimal? AtrH1,
    decimal? AtrM15,
    decimal? PreviousDayHigh,
    decimal? PreviousDayLow,
    decimal? WeeklyHigh,
    decimal? WeeklyLow,
    decimal? DayOpen,
    decimal? WeekOpen,
    decimal? SessionHigh,
    decimal? SessionLow,
    decimal? SessionHighJapan,
    decimal? SessionLowJapan,
    decimal? SessionHighIndia,
    decimal? SessionLowIndia,
    decimal? SessionHighLondon,
    decimal? SessionLowLondon,
    decimal? SessionHighNy,
    decimal? SessionLowNy,
    decimal? PreviousSessionHigh,
    decimal? PreviousSessionLow,
    decimal? Ema50H1,
    decimal? Ema200H1,
    decimal? AdrUsedPct,
    string Session,
    DateTimeOffset Timestamp,
    decimal? VolatilityExpansion,
    DateTimeOffset? Mt5ServerTime,
    int? Mt5ToKsaOffsetMinutes,
    string? TelegramImpactTag,
    string? TradingViewConfirmation,
    bool? IsCompression,
    bool? IsExpansion,
    bool? IsAtrExpanding,
    bool? HasOverlapCandles,
    bool? HasImpulseCandles,
    bool? HasLiquiditySweep,
    bool? HasPanicDropSequence,
    bool? IsPostSpikePullback,
    bool? IsLondonNyOverlap,
    bool? IsBreakoutConfirmed,
    bool? IsUsRiskWindow,
    decimal? Bid,
    decimal? Ask,
    decimal? Spread,
    decimal? SpreadMedian60m,
    decimal? SpreadMax60m,
    int? CompressionCountM15,
    int? ExpansionCountM15,
    decimal? ImpulseStrengthScore,
    string? TelegramState,
    bool? PanicSuspected,
    string? TvAlertType,
    // Spread stats for 1m and 5m windows (spec_v5.md A1)
    decimal? SpreadMin1m,
    decimal? SpreadAvg1m,
    decimal? SpreadMax1m,
    decimal? SpreadMin5m,
    decimal? SpreadAvg5m,
    decimal? SpreadMax5m,
    // Account state (spec_v5.md A1)
    decimal? FreeMargin,
    decimal? Equity,
    decimal? Balance,
    // Tick/market quality (PRD)
    decimal? TickRatePer30s,
    bool? FreezeGapDetected,
    decimal? SlippageEstimatePoints,
    decimal? SessionVwap,
    IReadOnlyCollection<decimal>? CompressionRangesM15,
    IReadOnlyCollection<Mt5PendingOrderRequest>? PendingOrders,
    IReadOnlyCollection<Mt5OpenPositionRequest>? OpenPositions,
    IReadOnlyCollection<Mt5OrderExecutionEventRequest>? OrderExecutionEvents,
    decimal? DxyBid = null,
    decimal? SilverBid = null,
    IReadOnlyCollection<Mt5CandleRequest>? RecentCandlesM15 = null);

public sealed record Mt5CandleRequest(
    DateTimeOffset? Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume = 0L);

public sealed record Mt5TimeframeDataRequest(
    string Timeframe,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume = 0L,
    DateTimeOffset? CandleStartTime = null,
    DateTimeOffset? CandleCloseTime = null,
    decimal? CandleBodySize = null,
    decimal? UpperWickSize = null,
    decimal? LowerWickSize = null,
    decimal? CandleRange = null,
    decimal? Ma20Value = null,
    decimal? Ma20Distance = null,
    decimal? Rsi = null,
    decimal? Atr = null);

public sealed record Mt5PendingOrderRequest(
    string Type,
    decimal Price,
    decimal Tp,
    DateTimeOffset? Expiry,
    decimal? VolumeGramsEquivalent = null);

public sealed record Mt5OpenPositionRequest(
    decimal EntryPrice,
    decimal CurrentPnlPoints,
    decimal Tp,
    decimal? VolumeGramsEquivalent = null);

public sealed record Mt5OrderExecutionEventRequest(
    string Status,
    DateTimeOffset Timestamp,
    decimal? Price = null,
    decimal? VolumeGramsEquivalent = null,
    ulong? Ticket = null);
