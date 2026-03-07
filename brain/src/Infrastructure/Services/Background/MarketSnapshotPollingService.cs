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
            var signal = await aiWorkerClient.AnalyzeAsync(snapshot, cycleId: null, cancellationToken);

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

        var basePrice = 2940.00m;
        var variance = (decimal)((random.NextDouble() - 0.5) * 8.0);
        var closePrice = basePrice + variance;

        var timeframeData = new[]
        {
            new TimeframeDataContract(
                Timeframe: "M5",
                Open: basePrice - 1.20m,
                High: closePrice + 1.60m,
                Low: closePrice - 1.80m,
                Close: closePrice),
            new TimeframeDataContract(
                Timeframe: "M15",
                Open: basePrice - 1.80m,
                High: closePrice + 2.10m,
                Low: closePrice - 2.30m,
                Close: closePrice),
            new TimeframeDataContract(
                Timeframe: "M30",
                Open: basePrice - 2.20m,
                High: closePrice + 2.80m,
                Low: closePrice - 2.70m,
                Close: closePrice),
            new TimeframeDataContract(
                Timeframe: "H1",
                Open: basePrice - 4.20m,
                High: closePrice + 4.80m,
                Low: closePrice - 4.50m,
                Close: closePrice),
            new TimeframeDataContract(
                Timeframe: "H4",
                Open: basePrice - 10.20m,
                High: basePrice + 12.20m,
                Low: basePrice - 11.50m,
                Close: closePrice)
        };

        var mt5ServerTime = now;
        var ksaTime = mt5ServerTime.AddMinutes(50);

        return new MarketSnapshotContract(
            Symbol: "XAUUSD.gram",
            TimeframeData: timeframeData,
            Atr: 14.2m,
            Adr: 18.0m,
            Ma20: basePrice,
            Ma20H4: basePrice - 6.5m,
            Ma20H1: basePrice - 2.6m,
            Ma20M30: basePrice - 1.1m,
            RsiH1: 52m,
            RsiM15: 49m,
            AtrH1: 14.2m,
            AtrM15: 5.8m,
            PreviousDayHigh: basePrice + 16m,
            PreviousDayLow: basePrice - 14m,
            SessionHigh: basePrice + 7m,
            SessionLow: basePrice - 6m,
            Session: DetermineSession(now),
            Timestamp: now,
            VolatilityExpansion: 0.79m,
            DayOfWeek: mt5ServerTime.DayOfWeek,
            Mt5ServerTime: mt5ServerTime,
            KsaTime: ksaTime,
            Mt5ToKsaOffsetMinutes: 50,
            TelegramImpactTag: "LOW",
            TradingViewConfirmation: "NEUTRAL",
            IsCompression: true,
            IsExpansion: false,
            IsAtrExpanding: false,
            HasOverlapCandles: true,
            HasImpulseCandles: false,
            HasLiquiditySweep: false,
            HasPanicDropSequence: false,
            IsPostSpikePullback: false,
            IsLondonNyOverlap: false,
            IsBreakoutConfirmed: false,
            IsUsRiskWindow: now.Hour is >= 12 and < 17,
            IsFriday: mt5ServerTime.DayOfWeek == DayOfWeek.Friday);
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
