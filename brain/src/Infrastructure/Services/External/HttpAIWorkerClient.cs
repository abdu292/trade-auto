using System.Text;
using System.Text.Json;
using System.Net.Http;
using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

/// <summary>
/// Real HTTP client for communicating with the AI Worker service.
/// </summary>
public sealed class HttpAIWorkerClient : IAIWorkerClient
{
    private const int MaxAnalyzeResponseBytes = 1_500_000;
    private const int MaxModeResponseBytes = 250_000;

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpAIWorkerClient> _logger;
    private readonly string _baseUrl;

    public HttpAIWorkerClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HttpAIWorkerClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = ResolveBaseUrl(configuration);
    }

    public async Task<TradeSignalContract> AnalyzeAsync(MarketSnapshotContract snapshot, string? cycleId, CancellationToken cancellationToken)
    {
        try
        {
            // Log outgoing request
            _logger.LogInformation(
                "→ [AIWorker] POST {BaseUrl}/analyze for {Symbol}, session={Session}",
                _baseUrl,
                snapshot.Symbol,
                snapshot.Session);

            // Convert C# contract to Python-compatible format (snake_case)
            var request = new
            {
                symbol = snapshot.Symbol,
                timeframeData = snapshot.TimeframeData.Select(tf => new
                {
                    timeframe = tf.Timeframe,
                    open = tf.Open,
                    high = tf.High,
                    low = tf.Low,
                    close = tf.Close,
                    volume = tf.Volume,
                    candleStartTime = tf.CandleStartTime,
                    candleCloseTime = tf.CandleCloseTime,
                    candleBodySize = tf.CandleBodySize,
                    upperWickSize = tf.UpperWickSize,
                    lowerWickSize = tf.LowerWickSize,
                    candleRange = tf.CandleRange,
                    ma20Value = tf.Ma20Value,
                    ma20Distance = tf.Ma20Distance,
                    rsi = tf.Rsi,
                    atr = tf.Atr,
                }),
                atr = snapshot.Atr,
                adr = snapshot.Adr,
                ma20 = snapshot.Ma20,
                ma20H4 = snapshot.Ma20H4,
                ma20H1 = snapshot.Ma20H1,
                ma20M30 = snapshot.Ma20M30,
                rsiH1 = snapshot.RsiH1,
                rsiM15 = snapshot.RsiM15,
                atrH1 = snapshot.AtrH1,
                atrM15 = snapshot.AtrM15,
                previousDayHigh = snapshot.PreviousDayHigh,
                previousDayLow = snapshot.PreviousDayLow,
                weeklyHigh = snapshot.WeeklyHigh,
                weeklyLow = snapshot.WeeklyLow,
                dayOpen = snapshot.DayOpen,
                weekOpen = snapshot.WeekOpen,
                sessionHigh = snapshot.SessionHigh,
                sessionLow = snapshot.SessionLow,
                sessionHighJapan = snapshot.SessionHighJapan,
                sessionLowJapan = snapshot.SessionLowJapan,
                sessionHighIndia = snapshot.SessionHighIndia,
                sessionLowIndia = snapshot.SessionLowIndia,
                sessionHighLondon = snapshot.SessionHighLondon,
                sessionLowLondon = snapshot.SessionLowLondon,
                sessionHighNy = snapshot.SessionHighNy,
                sessionLowNy = snapshot.SessionLowNy,
                previousSessionHigh = snapshot.PreviousSessionHigh,
                previousSessionLow = snapshot.PreviousSessionLow,
                ema50H1 = snapshot.Ema50H1,
                ema200H1 = snapshot.Ema200H1,
                adrUsedPct = snapshot.AdrUsedPct,
                session = snapshot.Session,
                sessionPhase = snapshot.SessionPhase,
                timestamp = snapshot.Timestamp,
                volatilityExpansion = snapshot.VolatilityExpansion,
                dayOfWeek = snapshot.DayOfWeek.ToString(),
                mt5ServerTime = snapshot.Mt5ServerTime,
                ksaTime = snapshot.KsaTime,
                uaeTime = snapshot.UaeTime,
                indiaTime = snapshot.IndiaTime,
                internalClockUtc = snapshot.InternalClockUtc,
                utcReferenceTime = snapshot.UtcReferenceTime,
                timeSkewMs = snapshot.TimeSkewMs,
                mt5ToKsaOffsetMinutes = snapshot.Mt5ToKsaOffsetMinutes,
                telegramImpactTag = snapshot.TelegramImpactTag,
                tradingViewConfirmation = snapshot.TradingViewConfirmation,
                isCompression = snapshot.IsCompression,
                isExpansion = snapshot.IsExpansion,
                isAtrExpanding = snapshot.IsAtrExpanding,
                hasOverlapCandles = snapshot.HasOverlapCandles,
                hasImpulseCandles = snapshot.HasImpulseCandles,
                hasLiquiditySweep = snapshot.HasLiquiditySweep,
                hasPanicDropSequence = snapshot.HasPanicDropSequence,
                isPostSpikePullback = snapshot.IsPostSpikePullback,
                isLondonNyOverlap = snapshot.IsLondonNyOverlap,
                isBreakoutConfirmed = snapshot.IsBreakoutConfirmed,
                isUsRiskWindow = snapshot.IsUsRiskWindow,
                isFriday = snapshot.IsFriday,
                bid = snapshot.Bid,
                ask = snapshot.Ask,
                spread = snapshot.Spread,
                spreadMedian60m = snapshot.SpreadMedian60m,
                spreadMax60m = snapshot.SpreadMax60m,
                compressionCountM15 = snapshot.CompressionCountM15,
                expansionCountM15 = snapshot.ExpansionCountM15,
                impulseStrengthScore = snapshot.ImpulseStrengthScore,
                telegramState = snapshot.TelegramState,
                panicSuspected = snapshot.PanicSuspected,
                tvAlertType = snapshot.TvAlertType,
                tickRatePer30s = snapshot.TickRatePer30s,
                freezeGapDetected = snapshot.FreezeGapDetected,
                slippageEstimatePoints = snapshot.SlippageEstimatePoints,
                sessionVwap = snapshot.SessionVwap,
                systemFetchedGoldRate = snapshot.SystemFetchedGoldRate,
                rateDeltaUsd = snapshot.RateDeltaUsd,
                rateAuthority = snapshot.RateAuthority,
                authoritativeRate = snapshot.AuthoritativeRate,
                cycleId = cycleId,
                compressionRangesM15 = snapshot.CompressionRangesM15 ?? [],
                freeMargin = snapshot.FreeMargin,
                equity = snapshot.Equity,
                balance = snapshot.Balance,
                pendingOrders = (snapshot.PendingOrders ?? []).Select(item => new
                {
                    type = item.Type,
                    price = item.Price,
                    tp = item.Tp,
                    expiry = item.Expiry,
                    volumeGramsEquivalent = item.VolumeGramsEquivalent,
                }),
                openPositions = (snapshot.OpenPositions ?? []).Select(item => new
                {
                    entryPrice = item.EntryPrice,
                    currentPnlPoints = item.CurrentPnlPoints,
                    tp = item.Tp,
                    volumeGramsEquivalent = item.VolumeGramsEquivalent,
                }),
                orderExecutionEvents = (snapshot.OrderExecutionEvents ?? []).Select(item => new
                {
                    status = item.Status,
                    timestamp = item.Timestamp,
                    price = item.Price,
                    volumeGramsEquivalent = item.VolumeGramsEquivalent,
                    ticket = item.Ticket,
                }),
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/analyze")
            {
                Content = jsonContent,
            };

            using var response = await _httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseBody = await ReadResponseBodyLimitedAsync(response, MaxAnalyzeResponseBytes, cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<TradeSignalResponse>(responseBody, options);

            if (result == null)
            {
                throw new InvalidOperationException("AI Worker returned null response");
            }

            var signal = new TradeSignalContract(
                Rail: result.Rail,
                Entry: Convert.ToDecimal(result.Entry),
                Tp: Convert.ToDecimal(result.Tp),
                Pe: result.Pe,
                Ml: result.Ml,
                Confidence: Convert.ToDecimal(result.Confidence),
                SafetyTag: result.SafetyTag ?? "CAUTION",
                DirectionBias: result.DirectionBias ?? "BULLISH",
                AlignmentScore: Convert.ToDecimal(result.AlignmentScore ?? result.Confidence),
                NewsImpactTag: result.NewsImpactTag ?? "LOW",
                TvConfirmationTag: result.TvConfirmationTag ?? "NEUTRAL",
                NewsTags: result.NewsTags ?? ["unknown_news_state"],
                Summary: result.Summary ?? "No AI summary provided.",
                ConsensusPassed: result.ConsensusPassed ?? true,
                AgreementCount: result.AgreementCount ?? 1,
                RequiredAgreement: result.RequiredAgreement ?? 1,
                DisagreementReason: result.DisagreementReason,
                ProviderVotes: result.ProviderVotes ?? [],
                ModeHint: result.ModeHint ?? "UNKNOWN",
                ModeConfidence: Convert.ToDecimal(result.ModeConfidence ?? 0.5),
                ModeTtlSeconds: result.ModeTtlSeconds ?? 900,
                ModeKeywords: result.ModeKeywords ?? [],
                RegimeTag: string.IsNullOrWhiteSpace(result.RegimeTag) ? "STANDARD" : result.RegimeTag,
                RiskState: string.IsNullOrWhiteSpace(result.RiskState) ? "CAUTION" : result.RiskState,
                GeoHeadline: result.GeoHeadline ?? "NONE",
                DxyBias: result.DxyBias ?? "NEUTRAL",
                YieldsBias: result.YieldsBias ?? "NEUTRAL",
                CrossMetalsBias: result.CrossMetalsBias ?? "NEUTRAL",
                CbFlow: result.CbFlow ?? "UNKNOWN",
                InstPositioning: result.InstPositioning ?? "UNKNOWN",
                EventRisk: result.EventRisk ?? "LOW",
                PromptRefs: result.PromptRefs ?? [],
                ProviderModels: result.ProviderModels ?? [],
                AiTraceJson: result.AiTraceJson,
                CycleId: result.CycleId ?? cycleId);

            _logger.LogInformation(
                "← [AIWorker] Analysis complete: {Signal} (confidence={Confidence})",
                signal.Rail,
                signal.Confidence);

            return signal;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "✗ [AIWorker] HTTP request failed. Verify External:AIWorkerBaseUrl is reachable: {BaseUrl}",
                _baseUrl);
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "✗ [AIWorker] Request timeout");
            throw;
        }
    }

    public async Task<ModeSignalContract?> GetModeAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                symbol = snapshot.Symbol,
                session = snapshot.Session,
                timestamp = snapshot.Timestamp,
                telegramState = snapshot.TelegramState,
                telegramImpactTag = snapshot.TelegramImpactTag,
                isExpansion = snapshot.IsExpansion,
                hasImpulseCandles = snapshot.HasImpulseCandles,
                hasPanicDropSequence = snapshot.HasPanicDropSequence,
                tvAlertType = snapshot.TvAlertType,
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/mode")
            {
                Content = jsonContent,
            };

            using var response = await _httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseBody = await ReadResponseBodyLimitedAsync(response, MaxModeResponseBytes, cancellationToken);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<ModeSignalResponse>(responseBody, options);
            if (result is null)
            {
                return null;
            }

            return new ModeSignalContract(
                Mode: string.IsNullOrWhiteSpace(result.Mode) ? "UNKNOWN" : result.Mode,
                Confidence: Convert.ToDecimal(result.Confidence),
                Keywords: result.Keywords ?? [],
                TtlSeconds: result.TtlSeconds <= 0 ? 900 : result.TtlSeconds,
                CapturedAtUtc: result.CapturedAtUtc ?? DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI worker mode endpoint unavailable; continuing with local mode hints.");
            return null;
        }
    }

    private static string ResolveBaseUrl(IConfiguration configuration)
    {
        var configured = (configuration["External:AIWorkerBaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return "http://127.0.0.1:8001";
    }

    private static async Task<string> ReadResponseBodyLimitedAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidOperationException($"AI worker response exceeded {maxBytes} bytes limit.");
            }

            ms.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// DTO for deserializing AI Worker response (matches Python TradeSignal model)
    /// </summary>
    private sealed record TradeSignalResponse(
        string Rail,
        double Entry,
        double Tp,
        DateTimeOffset Pe,
        int Ml,
        double Confidence,
        string? SafetyTag,
        string? DirectionBias,
        double? AlignmentScore,
        string? NewsImpactTag,
        string? TvConfirmationTag,
        IReadOnlyCollection<string>? NewsTags,
        string? Summary,
        bool? ConsensusPassed,
        int? AgreementCount,
        int? RequiredAgreement,
        string? DisagreementReason,
        IReadOnlyCollection<string>? ProviderVotes,
        string? ModeHint,
        double? ModeConfidence,
        int? ModeTtlSeconds,
        IReadOnlyCollection<string>? ModeKeywords,
        string? RegimeTag,
        string? RiskState,
        string? GeoHeadline,
        string? DxyBias,
        string? YieldsBias,
        string? CrossMetalsBias,
        string? CbFlow,
        string? InstPositioning,
        string? EventRisk,
        IReadOnlyCollection<string>? PromptRefs,
        IReadOnlyCollection<string>? ProviderModels,
        string? AiTraceJson,
        string? CycleId);

    private sealed record ModeSignalResponse(
        string Mode,
        double Confidence,
        IReadOnlyCollection<string>? Keywords,
        int TtlSeconds,
        DateTimeOffset? CapturedAtUtc);
}
