using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Application.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;

namespace Brain.Infrastructure.Services.External;

/// <summary>
/// Implements the historical replay engine described in PRD3 sections 18–25.
/// Imports MT5-exported CSV candle data and replays it through the same decision
/// pipeline used for live trading. Real trade execution is always disabled.
/// </summary>
public sealed class HistoricalReplayService : IHistoricalReplayService
{
    // Candle storage keyed by "SYMBOL_TIMEFRAME"
    private readonly ConcurrentDictionary<string, List<ReplayCandle>> _candles = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HistoricalReplayService> _logger;

    // Replay state
    private volatile bool _isRunning;
    private volatile bool _isPaused;
    private CancellationTokenSource? _replayCts;

    // initial ledger balance (configurable via start request)
    private decimal _initialCashAed = 50000m;

    private string _symbol = string.Empty;
    private int _totalCandles;
    private int _processedCandles;
    private int _cyclesTriggered;
    private int _setupCandidatesFound;
    private int _tradesArmed;
    private DateTimeOffset? _replayFrom;
    private DateTimeOffset? _replayTo;
    private DateTimeOffset? _startedUtc;
    private string _driverTimeframe = "M5";
    private readonly SemaphoreSlim _pauseGate = new(1, 1);

    private const decimal DefaultAtrEstimate = 10m; // Default fallback ATR estimate for gold (USD/oz)

    public HistoricalReplayService(IServiceProvider serviceProvider, ILogger<HistoricalReplayService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Import
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<int> ImportCandlesAsync(
        string symbol,
        string timeframe,
        Stream csvStream,
        CancellationToken cancellationToken)
    {
        var key = BuildKey(symbol, timeframe);
        var candles = new List<ReplayCandle>();
        var symUpper = symbol.Trim().ToUpperInvariant();
        var tfUpper = timeframe.Trim().ToUpperInvariant();

        using var reader = new StreamReader(csvStream);
        var lineNumber = 0;
        var skipped = 0;

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line)) continue;

            if (lineNumber == 1 && IsHeaderLine(line))
            {
                skipped++;
                continue;
            }

            if (!TryParseCandle(line, symUpper, tfUpper, out var candle))
            {
                skipped++;
                _logger.LogDebug("Replay import: skipped line {Line}: {Content}", lineNumber, line);
                continue;
            }

            candles.Add(candle!);
        }

        candles.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        _candles[key] = candles;

        _logger.LogInformation(
            "Replay import: {Symbol} {Timeframe} — {Count} candles loaded ({Skipped} skipped)",
            symUpper, tfUpper, candles.Count, skipped);

        return candles.Count;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Control
    // ──────────────────────────────────────────────────────────────────────────

    public async Task StartAsync(ReplayStartRequest request, CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("Replay is already running. Stop it first.");
        }

        var symUpper = (request.Symbol ?? "XAUUSD").Trim().ToUpperInvariant();

        // Prefer M5 as driver timeframe; fall back to whatever is imported
        var driverTf = ChooseDriverTimeframe(symUpper);
        if (driverTf is null)
        {
            throw new InvalidOperationException(
                $"No candles imported for symbol '{symUpper}'. " +
                $"Use POST /api/replay/import to upload CSV data first.");
        }

        var driverKey = BuildKey(symUpper, driverTf);
        var driverCandles = _candles[driverKey];

        var filtered = driverCandles
            .Where(c => (request.From is null || c.Timestamp >= request.From)
                     && (request.To is null || c.Timestamp <= request.To))
            .ToList();

        if (filtered.Count == 0)
        {
            throw new InvalidOperationException(
                $"No candles in the requested date range for {symUpper}/{driverTf}.");
        }

        _symbol = symUpper;
        _driverTimeframe = driverTf;
        _totalCandles = filtered.Count;
        _processedCandles = 0;
        _cyclesTriggered = 0;
        _setupCandidatesFound = 0;
        _tradesArmed = 0;
        _replayFrom = filtered.First().Timestamp;
        _replayTo = filtered.Last().Timestamp;
        _startedUtc = DateTimeOffset.UtcNow;
        _isRunning = true;
        _isPaused = false;

        _replayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _replayCts.Token;

        // Capture delay to avoid closure issues
        var speedMultiplier = Math.Max(1, request.SpeedMultiplier);
        var useAI = request.UseMockAI ? false : request.UseAI;
        _initialCashAed = request.InitialCashAed;

        _ = Task.Run(async () =>
        {
            try
            {
                await RunReplayLoopAsync(filtered, symUpper, useAI, speedMultiplier, token);
            }
            finally
            {
                _isRunning = false;
                _isPaused = false;
                _logger.LogInformation(
                    "Replay finished. {Symbol} {Tf}: {Processed}/{Total} candles, {Cycles} cycles, {Setups} setup candidates, {Trades} trades armed.",
                    symUpper, driverTf, _processedCandles, _totalCandles, _cyclesTriggered, _setupCandidatesFound, _tradesArmed);
            }
        }, token);

        await Task.CompletedTask;
    }

    public void Pause()
    {
        if (_isRunning && !_isPaused)
        {
            _isPaused = true;
            _pauseGate.Wait(); // acquire semaphore so the replay loop blocks at WaitAsync
            _logger.LogInformation("Replay paused at candle {Processed}/{Total}.", _processedCandles, _totalCandles);
        }
    }

    public void Resume()
    {
        if (_isRunning && _isPaused)
        {
            _isPaused = false;
            if (_pauseGate.CurrentCount == 0)
                _pauseGate.Release();
            _logger.LogInformation("Replay resumed at candle {Processed}/{Total}.", _processedCandles, _totalCandles);
        }
    }

    public void Stop()
    {
        _replayCts?.Cancel();
        _isRunning = false;
        _isPaused = false;
        if (_pauseGate.CurrentCount == 0)
            _pauseGate.Release();
        _logger.LogInformation("Replay stopped.");
    }

    public ReplayStatusContract GetStatus() => new(
        IsRunning: _isRunning,
        IsPaused: _isPaused,
        Symbol: _symbol,
        TotalCandles: _totalCandles,
        ProcessedCandles: _processedCandles,
        CyclesTriggered: _cyclesTriggered,
        SetupCandidatesFound: _setupCandidatesFound,
        TradesArmed: _tradesArmed,
        ReplayFrom: _replayFrom,
        ReplayTo: _replayTo,
        StartedUtc: _startedUtc,
        DriverTimeframe: _driverTimeframe);

    public IReadOnlyDictionary<string, int> GetImportedCounts(string symbol)
    {
        var prefix = symbol.Trim().ToUpperInvariant() + "_";
        return _candles
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(
                kv => kv.Key[prefix.Length..],
                kv => kv.Value.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Replay loop
    // ──────────────────────────────────────────────────────────────────────────

    private async Task RunReplayLoopAsync(
        List<ReplayCandle> driverCandles,
        string symbol,
        bool useAI,
        int speedMultiplier,
        CancellationToken ct)
    {
        // Inter-candle delay: 0 = no delay (as fast as possible)
        var candleIntervalMs = speedMultiplier == 0
            ? 0
            : (int)(1000 / (double)speedMultiplier);

        foreach (var candle in driverCandles)
        {
            ct.ThrowIfCancellationRequested();

            // Pause support
            if (_isPaused)
            {
                _logger.LogDebug("Replay paused; waiting to resume...");
                await _pauseGate.WaitAsync(ct);
                _pauseGate.Release();
            }

            var snapshot = BuildSnapshot(symbol, candle);
            _processedCandles++;
            _cyclesTriggered++;

            await ProcessReplayCycleAsync(snapshot, useAI, ct);

            if (candleIntervalMs > 0)
                await Task.Delay(candleIntervalMs, ct);
        }
    }

    private async Task ProcessReplayCycleAsync(
        MarketSnapshotContract snapshot,
        bool useAI,
        CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var timeline = scope.ServiceProvider.GetRequiredService<IRuntimeTimelineWriter>();
        var aiWorker = scope.ServiceProvider.GetRequiredService<IAIWorkerClient>();
        var economicNews = scope.ServiceProvider.GetRequiredService<IEconomicNewsService>();

        var cycleId = $"replay_{snapshot.Timestamp:yyyyMMddHHmmss}_{snapshot.Symbol}";

        await timeline.WriteAsync(
            eventType: "REPLAY_CYCLE_STARTED",
            stage: "replay",
            source: "replay_engine",
            symbol: snapshot.Symbol,
            cycleId: cycleId,
            tradeId: null,
            payload: new { snapshot.Symbol, snapshot.Timestamp, snapshot.Session, snapshot.Bid, snapshot.Ask },
            cancellationToken: ct);

        // ── Step 1: Rule engine ──
        var setup = RuleEngine.Evaluate(snapshot);

        await timeline.WriteAsync(
            eventType: setup.IsValid ? "RULE_ENGINE_SETUP_CANDIDATE" : "RULE_ENGINE_ABORT",
            stage: "rule_engine",
            source: "replay_engine",
            symbol: snapshot.Symbol,
            cycleId: cycleId,
            tradeId: null,
            payload: new
            {
                setup.IsValid,
                h1Context = setup.H1Context,
                m15Setup = setup.M15Setup,
                m5Entry = setup.M5Entry,
                abortReason = setup.AbortReason,
            },
            cancellationToken: ct);

        if (!setup.IsValid)
        {
            _logger.LogDebug(
                "Replay [{CycleId}] Rule engine abort: {Reason}",
                cycleId, setup.AbortReason);
            return;
        }

        Interlocked.Increment(ref _setupCandidatesFound);

        var newsRisk = await economicNews.AssessAsync(snapshot.Timestamp, ct);
        await timeline.WriteAsync(
            eventType: "REPLAY_NEWS_CHECK",
            stage: "news",
            source: "forexfactory",
            symbol: snapshot.Symbol,
            cycleId: cycleId,
            tradeId: null,
            payload: new
            {
                blocked = newsRisk.IsBlocked,
                newsRisk.Reason,
                newsRisk.NearbyEvents,
                newsRisk.RefreshedAtUtc,
                newsRisk.IsStale,
            },
            cancellationToken: ct);

        if (newsRisk.IsBlocked)
        {
            await timeline.WriteAsync(
                eventType: "CYCLE_ABORTED",
                stage: "news",
                source: "replay_engine",
                symbol: snapshot.Symbol,
                cycleId: cycleId,
                tradeId: null,
                payload: new { reason = newsRisk.Reason, newsRisk.NearbyEvents },
                cancellationToken: ct);
            return;
        }

        // ── Step 2: AI analysis (or mock) ──
        TradeSignalContract aiSignal;
        if (useAI)
        {
            try
            {
                aiSignal = await aiWorker.AnalyzeAsync(snapshot, cycleId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Replay [{CycleId}] AI analysis failed; using mock pass-through.", cycleId);
                aiSignal = BuildMockAiSignal(snapshot);
            }
        }
        else
        {
            aiSignal = BuildMockAiSignal(snapshot);
        }

        await timeline.WriteAsync(
            eventType: "REPLAY_AI_RESPONSE",
            stage: "ai",
            source: useAI ? "aiworker" : "replay_mock",
            symbol: snapshot.Symbol,
            cycleId: cycleId,
            tradeId: null,
            payload: new
            {
                aiSignal.Rail,
                aiSignal.Entry,
                aiSignal.Tp,
                aiSignal.Confidence,
                aiSignal.ConsensusPassed,
                aiSignal.Summary,
                isMock = !useAI,
            },
            cancellationToken: ct);

        if (!aiSignal.ConsensusPassed)
        {
            await timeline.WriteAsync(
                eventType: "CYCLE_ABORTED",
                stage: "ai",
                source: "replay_engine",
                symbol: snapshot.Symbol,
                cycleId: cycleId,
                tradeId: null,
                payload: new { reason = "AI consensus failed", aiSignal.DisagreementReason },
                cancellationToken: ct);
            return;
        }

        // ── Step 3: Decision engine ──
        var regime = RegimeRiskClassifier.Classify(snapshot);

        // Trade scoring: ranks setup quality after all gates passed
        var tradeScore = TradeScoreCalculator.Calculate(snapshot, setup, aiSignal);

        await timeline.WriteAsync(
            eventType: "TRADE_SCORE_CALCULATION",
            stage: "scoring",
            source: "replay_engine",
            symbol: snapshot.Symbol,
            cycleId: cycleId,
            tradeId: null,
            payload: new
            {
                structureScore = tradeScore.StructureScore,
                momentumScore = tradeScore.MomentumScore,
                executionScore = tradeScore.ExecutionScore,
                aiScore = tradeScore.AiScore,
                sentimentScore = tradeScore.SentimentScore,
                totalScore = tradeScore.TotalScore,
                decisionTier = tradeScore.DecisionTier,
                threshold = TradeScoreCalculator.NoTradeThreshold,
                replayMode = true,
            },
            cancellationToken: ct);

        if (tradeScore.TotalScore < TradeScoreCalculator.NoTradeThreshold)
        {
            await timeline.WriteAsync(
                eventType: "CYCLE_ABORTED",
                stage: "scoring",
                source: "replay_engine",
                symbol: snapshot.Symbol,
                cycleId: cycleId,
                tradeId: null,
                payload: new
                {
                    reason = $"Trade score {tradeScore.TotalScore} below threshold {TradeScoreCalculator.NoTradeThreshold}",
                    tradeScore.DecisionTier,
                    replayMode = true,
                },
                cancellationToken: ct);
            return;
        }

        // ledger start balance may be overridden by the start request
        var startingCash = _initialCashAed;
        var ledger = new LedgerStateContract(
            CashAed: startingCash,
            GoldGrams: 0m,
            OpenExposurePercent: 0m,
            DeployableCashAed: startingCash,
            OpenBuyCount: 0);

        var decision = DecisionEngine.Evaluate(snapshot, regime, aiSignal, ledger);

        await timeline.WriteAsync(
            eventType: decision.IsTradeAllowed ? "REPLAY_TRADE_ARMED" : "REPLAY_CYCLE_NO_TRADE",
            stage: "decision",
            source: "replay_engine",
            symbol: snapshot.Symbol,
            cycleId: cycleId,
            tradeId: null,
            payload: new
            {
                decision.IsTradeAllowed,
                decision.Status,
                decision.EngineState,
                decision.Cause,
                decision.Reason,
                decision.Rail,
                decision.Entry,
                decision.Tp,
                decision.Grams,
                decision.WaterfallRisk,
                replayMode = true,
                executionBlocked = true,
            },
            cancellationToken: ct);

        if (decision.IsTradeAllowed)
        {
            Interlocked.Increment(ref _tradesArmed);
            _logger.LogInformation(
                "Replay [{CycleId}] Trade candidate: {Rail} {Symbol} @ {Entry} TP={Tp} grams={Grams} (NOT executed — replay mode)",
                cycleId, decision.Rail, snapshot.Symbol, decision.Entry, decision.Tp, decision.Grams);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private MarketSnapshotContract BuildSnapshot(string symbol, ReplayCandle primary)
    {
        // Build multi-timeframe data from all imported candles for this symbol
        var timeframeData = new List<TimeframeDataContract>();

        foreach (var tf in new[] { "H1", "M15", "M5" })
        {
            var key = BuildKey(symbol, tf);
            if (!_candles.TryGetValue(key, out var list)) continue;

            // Find the latest candle at or before the primary candle's timestamp
            var candle = list.LastOrDefault(c => c.Timestamp <= primary.Timestamp);
            if (candle is null) continue;

            var range = candle.High - candle.Low;
            var body = Math.Abs(candle.Close - candle.Open);
            var upper = candle.High - Math.Max(candle.Open, candle.Close);
            var lower = Math.Min(candle.Open, candle.Close) - candle.Low;

            timeframeData.Add(new TimeframeDataContract(
                Timeframe: tf,
                Open: candle.Open,
                High: candle.High,
                Low: candle.Low,
                Close: candle.Close,
                Volume: candle.Volume,
                CandleStartTime: candle.Timestamp,
                CandleCloseTime: candle.Timestamp + TimeframeToSpan(tf),
                CandleBodySize: body,
                UpperWickSize: upper,
                LowerWickSize: lower,
                CandleRange: range));
        }

        // Fallback: use the primary candle timeframe if no matching TFs found
        if (timeframeData.Count == 0)
        {
            var range = primary.High - primary.Low;
            var body = Math.Abs(primary.Close - primary.Open);
            timeframeData.Add(new TimeframeDataContract(
                Timeframe: primary.Timeframe,
                Open: primary.Open,
                High: primary.High,
                Low: primary.Low,
                Close: primary.Close,
                Volume: primary.Volume,
                CandleStartTime: primary.Timestamp,
                CandleBodySize: body,
                CandleRange: range));
        }

        var (session, phase) = TradingSessionClock.Resolve(primary.Timestamp);
        var close = primary.Close;
        var atr = EstimateAtr(symbol, primary.Timestamp);

        return new MarketSnapshotContract(
            Symbol: symbol,
            TimeframeData: timeframeData,
            Atr: atr,
            Adr: atr * 2.5m,
            Ma20: close,
            Session: session,
            Timestamp: primary.Timestamp,
            Bid: close - 0.10m,
            Ask: close + 0.10m,
            Spread: 0.20m,
            IsCompression: DetectCompression(symbol, primary.Timestamp),
            HasOverlapCandles: DetectBase(symbol, primary.Timestamp),
            DayOfWeek: primary.Timestamp.DayOfWeek,
            Mt5ServerTime: primary.Timestamp,
            KsaTime: primary.Timestamp.AddMinutes(50),
            SessionPhase: phase,
            IsFriday: primary.Timestamp.DayOfWeek == DayOfWeek.Friday,
            CycleId: $"replay_{primary.Timestamp:yyyyMMddHHmmss}");
    }

    private bool DetectCompression(string symbol, DateTimeOffset at)
    {
        var key = BuildKey(symbol, "M15");
        if (!_candles.TryGetValue(key, out var list)) return false;

        var window = list
            .Where(c => c.Timestamp <= at && c.Timestamp >= at.AddHours(-2))
            .TakeLast(6)
            .ToList();

        if (window.Count < 3) return false;

        var ranges = window.Select(c => c.High - c.Low).ToList();
        var first = ranges.Take(3).Average();
        var last = ranges.TakeLast(3).Average();
        return last < first * 0.85m;
    }

    private bool DetectBase(string symbol, DateTimeOffset at)
    {
        var key = BuildKey(symbol, "M15");
        if (!_candles.TryGetValue(key, out var list)) return false;

        var window = list
            .Where(c => c.Timestamp <= at && c.Timestamp >= at.AddHours(-3))
            .TakeLast(8)
            .ToList();

        if (window.Count < 4) return false;

        var high = window.Max(c => c.High);
        var low = window.Min(c => c.Low);
        var range = high - low;
        var avgCandle = window.Average(c => c.High - c.Low);

        // Overlapping candles: overall range is less than 3x average candle = tight base
        return range < avgCandle * 3m;
    }

    private decimal EstimateAtr(string symbol, DateTimeOffset at)
    {
        var key = BuildKey(symbol, "H1");
        if (_candles.TryGetValue(key, out var list))
        {
            var window = list
                .Where(c => c.Timestamp <= at)
                .TakeLast(14)
                .ToList();

            if (window.Count >= 2)
                return window.Average(c => c.High - c.Low);
        }

        // Fall back to M15
        var m15Key = BuildKey(symbol, "M15");
        if (_candles.TryGetValue(m15Key, out var m15list))
        {
            var window = m15list
                .Where(c => c.Timestamp <= at)
                .TakeLast(14)
                .ToList();

            if (window.Count >= 2)
                return window.Average(c => c.High - c.Low) * 4m;
        }

        return DefaultAtrEstimate; // Default ATR estimate for gold
    }

    private static TradeSignalContract BuildMockAiSignal(MarketSnapshotContract snapshot)
    {
        var close = snapshot.TimeframeData.FirstOrDefault(x => x.Timeframe == "M5")?.Close
            ?? snapshot.TimeframeData.FirstOrDefault()?.Close
            ?? 0m;

        return new TradeSignalContract(
            Rail: "BUY_LIMIT",
            Entry: close - (snapshot.Atr * 0.5m),
            Tp: close + (snapshot.Atr * 1.0m),
            Pe: snapshot.Timestamp.AddMinutes(30),
            Ml: 1800,
            Confidence: 0.75m,
            SafetyTag: "CAUTION",
            DirectionBias: "BULLISH",
            AlignmentScore: 0.72m,
            NewsImpactTag: "LOW",
            TvConfirmationTag: "NEUTRAL",
            Summary: "Mock AI pass-through for replay mode",
            ConsensusPassed: true,
            AgreementCount: 1,
            RequiredAgreement: 1);
    }

    private string? ChooseDriverTimeframe(string symbol)
    {
        foreach (var tf in new[] { "M5", "M15", "H1" })
        {
            if (_candles.ContainsKey(BuildKey(symbol, tf)))
                return tf;
        }
        return null;
    }

    private static string BuildKey(string symbol, string timeframe)
        => $"{symbol.Trim().ToUpperInvariant()}_{timeframe.Trim().ToUpperInvariant()}";

    private static bool IsHeaderLine(string line)
    {
        var lower = line.ToLowerInvariant();
        return lower.Contains("date") || lower.Contains("open") || lower.Contains("time");
    }

    private static bool TryParseCandle(string line, string symbol, string timeframe, out ReplayCandle? candle)
    {
        candle = null;
        var parts = line.Split([',', '\t', ';'], StringSplitOptions.TrimEntries);

        if (parts.Length < 5) return false;

        try
        {
            DateTimeOffset timestamp;

            // Try combined timestamp column first (ISO 8601 or MT5 "2024.01.02 00:05")
            if (parts.Length >= 6
                && TryParseMt5Date(parts[0], parts[1], out var dt))
            {
                // MT5 format: Date, Time, Open, High, Low, Close[, Volume]
                timestamp = dt;
                var open = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
                var high = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
                var low = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
                var close = decimal.Parse(parts[5], CultureInfo.InvariantCulture);
                var volume = parts.Length > 6 ? long.Parse(parts[6], CultureInfo.InvariantCulture) : 0L;
                candle = new ReplayCandle(symbol, timeframe, timestamp, open, high, low, close, volume);
                return true;
            }

            // Single timestamp column
            if (DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var ts))
            {
                timestamp = ts;
                var open = decimal.Parse(parts[1], CultureInfo.InvariantCulture);
                var high = decimal.Parse(parts[2], CultureInfo.InvariantCulture);
                var low = decimal.Parse(parts[3], CultureInfo.InvariantCulture);
                var close = decimal.Parse(parts[4], CultureInfo.InvariantCulture);
                var volume = parts.Length > 5 ? long.Parse(parts[5], CultureInfo.InvariantCulture) : 0L;
                candle = new ReplayCandle(symbol, timeframe, timestamp, open, high, low, close, volume);
                return true;
            }
        }
        catch
        {
            // Fall through to return false
        }

        return false;
    }

    private static bool TryParseMt5Date(string datePart, string timePart, out DateTimeOffset result)
    {
        result = default;
        // MT5 exports as "2024.01.02" or "2024-01-02"
        var combined = $"{datePart} {timePart}";
        if (DateTimeOffset.TryParseExact(
                combined,
                ["yyyy.MM.dd HH:mm", "yyyy.MM.dd HH:mm:ss",
                 "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out result))
        {
            return true;
        }

        return false;
    }

    private static TimeSpan TimeframeToSpan(string tf) => tf.ToUpperInvariant() switch
    {
        "M1" => TimeSpan.FromMinutes(1),
        "M5" => TimeSpan.FromMinutes(5),
        "M15" => TimeSpan.FromMinutes(15),
        "M30" => TimeSpan.FromMinutes(30),
        "H1" => TimeSpan.FromHours(1),
        "H4" => TimeSpan.FromHours(4),
        "D1" => TimeSpan.FromDays(1),
        _ => TimeSpan.FromMinutes(5),
    };
}
