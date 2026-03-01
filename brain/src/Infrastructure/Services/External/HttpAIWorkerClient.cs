using System.Text;
using System.Text.Json;
using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

/// <summary>
/// Real HTTP client for communicating with the AI Worker service at http://localhost:8001
/// </summary>
public sealed class HttpAIWorkerClient : IAIWorkerClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpAIWorkerClient> _logger;
    private const string BaseUrl = "http://localhost:8001";

    public HttpAIWorkerClient(HttpClient httpClient, ILogger<HttpAIWorkerClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TradeSignalContract> AnalyzeAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken)
    {
        try
        {
            // Log outgoing request
            _logger.LogInformation(
                "→ [AIWorker] POST /analyze for {Symbol}, session={Session}",
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
                    close = tf.Close
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
                ema50H1 = snapshot.Ema50H1,
                ema200H1 = snapshot.Ema200H1,
                adrUsedPct = snapshot.AdrUsedPct,
                session = snapshot.Session,
                timestamp = snapshot.Timestamp,
                volatilityExpansion = snapshot.VolatilityExpansion,
                dayOfWeek = snapshot.DayOfWeek.ToString(),
                mt5ServerTime = snapshot.Mt5ServerTime,
                ksaTime = snapshot.KsaTime,
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
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/analyze",
                jsonContent,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
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
                ModeKeywords: result.ModeKeywords ?? []);

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
                "✗ [AIWorker] HTTP request failed. Is aiworker running on http://localhost:8001?");
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

            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/mode",
                jsonContent,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
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
        IReadOnlyCollection<string>? ModeKeywords);

    private sealed record ModeSignalResponse(
        string Mode,
        double Confidence,
        IReadOnlyCollection<string>? Keywords,
        int TtlSeconds,
        DateTimeOffset? CapturedAtUtc);
}
