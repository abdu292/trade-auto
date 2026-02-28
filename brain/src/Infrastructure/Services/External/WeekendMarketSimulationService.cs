using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

public sealed class WeekendMarketSimulationService(
    ILatestMarketSnapshotStore snapshotStore,
    ILogger<WeekendMarketSimulationService> logger) : IMarketSimulationService
{
    private readonly Lock _gate = new();
    private readonly Random _random = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    private bool _isRunning;
    private decimal _currentMid = 2890m;
    private decimal _lastBid;
    private decimal _lastAsk;
    private decimal _lastSpread = 0.18m;
    private decimal _lastAtr = 8m;
    private string _session = "LONDON";
    private DateTimeOffset? _startedUtc;
    private long _tickCount;
    private int _intervalSeconds = 5;
    private decimal _volatilityUsd = 0.45m;
    private decimal _baseSpread = 0.18m;
    private bool _enableShockEvents = true;
    private string? _sessionOverride;

    public MarketSimulationStatusContract GetStatus()
    {
        lock (_gate)
        {
            return new MarketSimulationStatusContract(
                IsRunning: _isRunning,
                CurrentMid: decimal.Round(_currentMid, 3),
                LastBid: decimal.Round(_lastBid, 3),
                LastAsk: decimal.Round(_lastAsk, 3),
                LastSpread: decimal.Round(_lastSpread, 3),
                Session: _session,
                StartedUtc: _startedUtc,
                TickCount: _tickCount,
                IntervalSeconds: _intervalSeconds,
                VolatilityUsd: decimal.Round(_volatilityUsd, 3),
                BaseSpread: decimal.Round(_baseSpread, 3),
                EnableShockEvents: _enableShockEvents,
                SourceTag: "SIMULATED_MT5");
        }
    }

    public void Start(MarketSimulationStartContract start)
    {
        lock (_gate)
        {
            StopInternal();

            _currentMid = Math.Max(1800m, start.StartPrice);
            _volatilityUsd = Math.Clamp(start.VolatilityUsd, 0.05m, 4.50m);
            _baseSpread = Math.Clamp(start.BaseSpread, 0.05m, 2.00m);
            _intervalSeconds = Math.Clamp(start.IntervalSeconds, 1, 30);
            _enableShockEvents = start.EnableShockEvents;
            _sessionOverride = string.IsNullOrWhiteSpace(start.SessionOverride)
                ? null
                : start.SessionOverride.Trim().ToUpperInvariant();
            _startedUtc = DateTimeOffset.UtcNow;
            _tickCount = 0;
            _lastAtr = 8m;

            _cts = new CancellationTokenSource();
            _isRunning = true;
            _loopTask = Task.Run(() => RunAsync(_cts.Token));
        }

        logger.LogInformation(
            "Weekend simulator started. start={Start}, vol={Vol}, spread={Spread}, interval={Interval}s, sessionOverride={SessionOverride}",
            start.StartPrice,
            start.VolatilityUsd,
            start.BaseSpread,
            start.IntervalSeconds,
            start.SessionOverride ?? "AUTO");
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopInternal();
        }

        logger.LogInformation("Weekend simulator stopped.");
    }

    public void StepOnce()
    {
        var snapshot = BuildNextSnapshot();
        snapshotStore.Upsert(snapshot);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = BuildNextSnapshot();
                snapshotStore.Upsert(snapshot);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Weekend simulator tick generation failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private MarketSnapshotContract BuildNextSnapshot()
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            _session = _sessionOverride ?? ResolveSession(now);

            var randomPulse = ((decimal)_random.NextDouble() - 0.5m) * 2m * _volatilityUsd;
            var cyclicalPulse = (decimal)Math.Sin(_tickCount / 22d) * (_volatilityUsd * 0.35m);
            var drift = SessionDrift(_session);

            var shock = 0m;
            if (_enableShockEvents && _tickCount > 0 && _tickCount % 90 == 0)
            {
                var chance = _random.NextDouble();
                if (chance < 0.22)
                {
                    shock = ((decimal)_random.NextDouble() - 0.5m) * (_volatilityUsd * 8m);
                }
            }

            var previousMid = _currentMid;
            _currentMid = Math.Max(1800m, _currentMid + randomPulse + cyclicalPulse + drift + shock);

            var spreadNoise = ((decimal)_random.NextDouble() - 0.5m) * (_baseSpread * 0.35m);
            _lastSpread = Math.Max(0.05m, _baseSpread + spreadNoise + (Math.Abs(shock) > 0 ? _baseSpread * 0.25m : 0m));
            _lastBid = _currentMid - (_lastSpread / 2m);
            _lastAsk = _currentMid + (_lastSpread / 2m);

            var move = Math.Abs(_currentMid - previousMid);
            var atr = Math.Max(4m, Math.Min(25m, (move * 2.2m) + 6m));
            var atrExpanding = atr > _lastAtr;
            _lastAtr = atr;

            var adr = Math.Max(12m, Math.Min(45m, atr * 2.4m));
            var isExpansion = atr >= 10m;
            var isCompression = atr <= 7m;
            var hasImpulse = move >= Math.Max(0.35m, _volatilityUsd * 1.4m);
            var panic = shock < -(_volatilityUsd * 2.8m);

            var rsiH1 = Math.Clamp(52m + ((_currentMid - 2890m) / 2.2m), 28m, 78m);
            var rsiM15 = Math.Clamp(rsiH1 + (((decimal)_random.NextDouble() - 0.5m) * 6m), 25m, 82m);

            var tvAlertType = hasImpulse
                ? (_currentMid >= previousMid ? "BREAKOUT" : "SESSION_BREAK")
                : "NONE";

            var telegramState = _currentMid >= previousMid
                ? (hasImpulse ? "BUY" : "QUIET")
                : (panic ? "SELL" : "MIXED");

            var timeframeData = BuildTimeframes(_currentMid, atr);
            var mt5ServerTime = now;

            _tickCount++;

            return new MarketSnapshotContract(
                Symbol: "XAUUSD",
                TimeframeData: timeframeData,
                Atr: decimal.Round(atr, 4),
                Adr: decimal.Round(adr, 4),
                Ma20: decimal.Round(_currentMid - 0.45m, 4),
                Ma20H4: decimal.Round(_currentMid - 1.20m, 4),
                Ma20H1: decimal.Round(_currentMid - 0.70m, 4),
                Ma20M30: decimal.Round(_currentMid - 0.30m, 4),
                RsiH1: decimal.Round(rsiH1, 2),
                RsiM15: decimal.Round(rsiM15, 2),
                AtrH1: decimal.Round(atr * 1.18m, 4),
                AtrM15: decimal.Round(atr * 0.78m, 4),
                PreviousDayHigh: decimal.Round(_currentMid + 12m, 2),
                PreviousDayLow: decimal.Round(_currentMid - 14m, 2),
                SessionHigh: decimal.Round(_currentMid + 6m, 2),
                SessionLow: decimal.Round(_currentMid - 6m, 2),
                Session: _session,
                Timestamp: now,
                VolatilityExpansion: decimal.Round(atr / adr, 4),
                DayOfWeek: mt5ServerTime.DayOfWeek,
                Mt5ServerTime: mt5ServerTime,
                KsaTime: mt5ServerTime.AddMinutes(50),
                Mt5ToKsaOffsetMinutes: 50,
                TelegramImpactTag: panic ? "HIGH" : (isExpansion ? "MODERATE" : "LOW"),
                TradingViewConfirmation: hasImpulse ? "CONFIRM" : "NEUTRAL",
                IsCompression: isCompression,
                IsExpansion: isExpansion,
                IsAtrExpanding: atrExpanding,
                HasOverlapCandles: isCompression,
                HasImpulseCandles: hasImpulse,
                HasLiquiditySweep: hasImpulse && ((decimal)_random.NextDouble() > 0.65m),
                HasPanicDropSequence: panic,
                IsPostSpikePullback: !hasImpulse && atr > 8m,
                IsLondonNyOverlap: _session == "LONDON" && now.Hour is >= 12 and <= 14,
                IsBreakoutConfirmed: hasImpulse,
                IsUsRiskWindow: now.Hour is >= 13 and <= 18,
                IsFriday: mt5ServerTime.DayOfWeek == DayOfWeek.Friday,
                Bid: decimal.Round(_lastBid, 4),
                Ask: decimal.Round(_lastAsk, 4),
                Spread: decimal.Round(_lastSpread, 4),
                SpreadMedian60m: decimal.Round(Math.Max(0.05m, _baseSpread), 4),
                SpreadMax60m: decimal.Round(Math.Max(_lastSpread, _baseSpread * 1.8m), 4),
                CompressionCountM15: isCompression ? 4 : 1,
                ExpansionCountM15: isExpansion ? 3 : 1,
                ImpulseStrengthScore: decimal.Round(Math.Min(1m, move / Math.Max(0.10m, _volatilityUsd)), 4),
                TelegramState: telegramState,
                PanicSuspected: panic,
                TvAlertType: tvAlertType);
        }
    }

    private static IReadOnlyCollection<TimeframeDataContract> BuildTimeframes(decimal mid, decimal atr)
    {
        static TimeframeDataContract Build(string tf, decimal basePrice, decimal range)
        {
            var open = basePrice - (range * 0.18m);
            var close = basePrice + (range * 0.08m);
            var high = Math.Max(open, close) + (range * 0.30m);
            var low = Math.Min(open, close) - (range * 0.28m);
            return new TimeframeDataContract(tf, decimal.Round(open, 4), decimal.Round(high, 4), decimal.Round(low, 4), decimal.Round(close, 4));
        }

        return
        [
            Build("M5", mid, Math.Max(0.25m, atr * 0.10m)),
            Build("M15", mid, Math.Max(0.35m, atr * 0.16m)),
            Build("M30", mid, Math.Max(0.45m, atr * 0.22m)),
            Build("H1", mid, Math.Max(0.65m, atr * 0.30m)),
            Build("H4", mid, Math.Max(1.20m, atr * 0.50m)),
        ];
    }

    private static string ResolveSession(DateTimeOffset now)
    {
        var hour = now.Hour;
        if (hour is >= 0 and < 4) return "JAPAN";
        if (hour is >= 4 and < 8) return "INDIA";
        if (hour is >= 8 and < 14) return "LONDON";
        return "NY";
    }

    private static decimal SessionDrift(string session) => session switch
    {
        "JAPAN" => 0.02m,
        "INDIA" => 0.03m,
        "LONDON" => 0.05m,
        "NY" => 0.04m,
        _ => 0m
    };

    private void StopInternal()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loopTask = null;
        _isRunning = false;
    }
}
