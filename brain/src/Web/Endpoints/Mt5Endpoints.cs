using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Application.Common.Services;
using Brain.Domain.Entities;
using Brain.Web.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            IResult (IPendingTradeStore pendingTradeStore, ILogger<object> logger) =>
            {
                logger.LogInformation("→ GET /mt5/pending-trades");

                if (!pendingTradeStore.TryDequeue(out var trade) || trade is null)
                {
                    logger.LogInformation("← GET /mt5/pending-trades no pending trades");
                    return TypedResults.NoContent();
                }

                logger.LogInformation("← GET /mt5/pending-trades returns {TradeId} {Type} @ {Price}",
                    trade.Id, trade.Type, trade.Price);

                return TypedResults.Ok(new
                {
                    id = trade.Id,
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

        mt5Group.MapPost(
            "/market-snapshot",
            (Mt5MarketSnapshotRequest request, ILatestMarketSnapshotStore snapshotStore, ILogger<object> logger) =>
            {
                var mt5ServerTime = request.Mt5ServerTime ?? request.Timestamp;
                var ksaTime = mt5ServerTime.AddMinutes(request.Mt5ToKsaOffsetMinutes ?? 50);
                var volatilityExpansion = request.VolatilityExpansion ?? (request.Adr <= 0 ? 0 : request.Atr / request.Adr);
                var (resolvedSession, resolvedPhase) = TradingSessionClock.Resolve(ksaTime);

                var timeframeData = request.TimeframeData.Select(tf =>
                    new TimeframeDataContract(tf.Timeframe, tf.Open, tf.High, tf.Low, tf.Close)).ToArray();

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
                    Ema50H1: request.Ema50H1 ?? 0m,
                    Ema200H1: request.Ema200H1 ?? 0m,
                    AdrUsedPct: request.AdrUsedPct ?? 0m,
                    Session: resolvedSession,
                    Timestamp: request.Timestamp,
                    VolatilityExpansion: volatilityExpansion,
                    DayOfWeek: mt5ServerTime.DayOfWeek,
                    Mt5ServerTime: mt5ServerTime,
                    KsaTime: ksaTime,
                    Mt5ToKsaOffsetMinutes: request.Mt5ToKsaOffsetMinutes ?? 50,
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
                    IsLondonNyOverlap: request.IsLondonNyOverlap ?? false,
                    IsBreakoutConfirmed: request.IsBreakoutConfirmed ?? false,
                    IsUsRiskWindow: request.IsUsRiskWindow ?? false,
                    IsFriday: mt5ServerTime.DayOfWeek == DayOfWeek.Friday,
                    Bid: request.Bid ?? 0m,
                    Ask: request.Ask ?? 0m,
                    Spread: request.Spread ?? 0m,
                    SpreadMedian60m: request.SpreadMedian60m ?? 0m,
                    SpreadMax60m: request.SpreadMax60m ?? 0m,
                    CompressionCountM15: request.CompressionCountM15 ?? 0,
                    ExpansionCountM15: request.ExpansionCountM15 ?? 0,
                    ImpulseStrengthScore: request.ImpulseStrengthScore ?? 0m,
                    TelegramState: NormalizeTelegramState(request.TelegramState),
                    PanicSuspected: request.PanicSuspected ?? false,
                    TvAlertType: NormalizeTvAlertType(request.TvAlertType),
                    SessionPhase: resolvedPhase);

                snapshotStore.Upsert(snapshot);
                var tickTelemetry = snapshotStore.GetTickTelemetry(1);
                logger.LogInformation(
                    "→ POST /mt5/market-snapshot stored {Symbol} snapshot #{TickCount} ({TimeframeCount} TFs, session={Session}/{Phase}, regimeVol={VolatilityExpansion:0.00}, spread={Spread:0.000}, lagMs={LagMs:0}, mt5={Mt5Time}, ksa={KsaTime})",
                    snapshot.Symbol,
                    tickTelemetry.TotalIngested,
                    timeframeData.Length,
                    resolvedSession,
                    resolvedPhase,
                    volatilityExpansion,
                    snapshot.Spread,
                    tickTelemetry.LastIngestionLatencyMs,
                    mt5ServerTime,
                    ksaTime);

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
                IApplicationDbContext db,
                INotificationService notification,
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

                var normalizedStatus = request.Status.Trim().ToUpperInvariant();
                var mt5Time = request.Mt5Time ?? DateTimeOffset.UtcNow;
                if (!Guid.TryParse(request.TradeId, out var tradeId))
                {
                    return TypedResults.BadRequest(new { error = "Invalid tradeId" });
                }

                if (normalizedStatus is "BUY_TRIGGERED" or "BUY_FILLED" or "FILLED_BUY")
                {
                    var buySlip = ledger.ApplyBuyFill(
                        tradeId,
                        request.Grams ?? 0m,
                        request.Mt5Price ?? 0m,
                        mt5Time);

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
                    var sellSlip = ledger.ApplySellFill(
                        tradeId,
                        request.Mt5Price ?? 0m,
                        mt5Time);

                    if (sellSlip is null)
                    {
                        return TypedResults.Ok(new
                        {
                            received = true,
                            duplicateIgnored = true,
                            ledger = ledger.GetState(),
                        });
                    }

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
    string? TvAlertType);

public sealed record Mt5TimeframeDataRequest(
    string Timeframe,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close);
