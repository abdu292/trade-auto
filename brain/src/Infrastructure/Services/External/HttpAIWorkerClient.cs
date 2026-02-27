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
                sessionHigh = snapshot.SessionHigh,
                sessionLow = snapshot.SessionLow,
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
                Summary: result.Summary ?? "No AI summary provided.");

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
        string? Summary);
}
