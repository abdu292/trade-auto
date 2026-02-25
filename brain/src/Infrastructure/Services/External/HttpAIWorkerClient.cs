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
                session = snapshot.Session,
                timestamp = snapshot.Timestamp
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
                Confidence: Convert.ToDecimal(result.Confidence));

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
        double Confidence);
}
