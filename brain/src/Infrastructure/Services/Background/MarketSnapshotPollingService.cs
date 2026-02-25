using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.Background;

/// <summary>
/// Background service that generates a market snapshot every 30 seconds
/// and sends it to the AI Worker for analysis.
/// </summary>
public sealed class MarketSnapshotPollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MarketSnapshotPollingService> _logger;
    private const int PollIntervalSeconds = 30;

    public MarketSnapshotPollingService(IServiceProvider serviceProvider, ILogger<MarketSnapshotPollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔄 MarketSnapshotPolling service starting (poll interval: {Seconds}s)", PollIntervalSeconds);

        // Initial delay before first poll
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndAnalyzeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "✗ Error during market snapshot polling");
                // Continue polling despite errors
            }

            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("🛑 MarketSnapshotPolling service stopped");
    }

    private async Task PollAndAnalyzeAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateAsyncScope();
        var aiWorkerClient = scope.ServiceProvider.GetRequiredService<IAIWorkerClient>();

        // Generate mock market snapshot
        var snapshot = GenerateMockSnapshot();

        _logger.LogInformation(
            "📊 [Poll] Sending {Symbol} snapshot to AI Worker (Session={Session}, Ma20={Ma20}, ATR={Atr})",
            snapshot.Symbol,
            snapshot.Session,
            snapshot.Ma20,
            snapshot.Atr);

        try
        {
            var signal = await aiWorkerClient.AnalyzeAsync(snapshot, cancellationToken);

            _logger.LogInformation(
                "✅ [Poll] Analysis received: {Signal} @ {Entry} (TP={Tp}, Confidence={Confidence})",
                signal.Rail,
                signal.Entry,
                signal.Tp,
                signal.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "⚠️ [Poll] Analysis failed for {Symbol}. Check if aiworker is running on http://localhost:8001",
                snapshot.Symbol);
        }
    }

    /// <summary>
    /// Generates a mock market snapshot with realistic but varying data
    /// </summary>
    private static MarketSnapshotContract GenerateMockSnapshot()
    {
        var random = new Random();
        var now = DateTimeOffset.UtcNow;

        // Simulate EURUSD price around 1.1025 ± variance
        var basePrice = 1.1025m;
        var variance = (decimal)((random.NextDouble() - 0.5) * 0.001);
        var closePrice = basePrice + variance;

        var timeframeData = new[]
        {
            new TimeframeDataContract(
                Timeframe: "H1",
                Open: basePrice - 0.0005m,
                High: closePrice + 0.0008m,
                Low: closePrice - 0.0006m,
                Close: closePrice),
            new TimeframeDataContract(
                Timeframe: "H4",
                Open: basePrice - 0.001m,
                High: basePrice + 0.002m,
                Low: basePrice - 0.0015m,
                Close: closePrice)
        };

        return new MarketSnapshotContract(
            Symbol: "EURUSD",
            TimeframeData: timeframeData,
            Atr: 0.00095m,
            Adr: 0.0012m,
            Ma20: basePrice,
            Session: DetermineSession(now),
            Timestamp: now);
    }

    /// <summary>
    /// Determines trading session based on UTC time
    /// </summary>
    private static string DetermineSession(DateTimeOffset utcTime)
    {
        var hour = utcTime.Hour;

        return hour switch
        {
            >= 0 and < 8 => "ASIA",        // 00:00-08:00
            >= 8 and < 16 => "EUROPE",    // 08:00-16:00
            >= 16 and < 22 => "LONDON",   // 16:00-22:00
            _ => "NEW_YORK"                // 22:00-23:59
        };
    }
}
