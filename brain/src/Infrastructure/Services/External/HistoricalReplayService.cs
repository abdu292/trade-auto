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
    private const decimal ReplayCapitalFloorAed = 350000m;
    private const string DefaultReplaySymbol = "XAUUSD.gram";

    // Candle storage keyed by "SYMBOL_TIMEFRAME"
    private readonly ConcurrentDictionary<string, List<ReplayCandle>> _candles = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HistoricalReplayService> _logger;

    // Replay state
    private volatile bool _isRunning;
    private volatile bool _isPaused;
    private CancellationTokenSource? _replayCts;

    // initial ledger balance (configurable via start request)
    private decimal _initialCashAed = 100000m;

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
    private string _phase = "IDLE";

    private const decimal DefaultAtrEstimate = 10m; // Default fallback ATR estimate for gold (USD/oz)
    private const int CompressionWindowM15 = 8;
    private const int CompressionWindowM5 = 10;

    public HistoricalReplayService(IServiceProvider serviceProvider, ILogger<HistoricalReplayService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Phase / pending-symbol helpers
    // ──────────────────────────────────────────────────────────────────────────

    public void SetPhase(string phase)
    {
        _phase = phase;
        _logger.LogInformation("Replay phase → {Phase}", phase);
    }

    public void SetPendingSymbol(string symbol)
    {
        _symbol = string.IsNullOrWhiteSpace(symbol) ? DefaultReplaySymbol : symbol.Trim();
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
        var sym = string.IsNullOrWhiteSpace(symbol) ? DefaultReplaySymbol : symbol.Trim();
        var tfUpper = timeframe.Trim().ToUpperInvariant();
        var key = BuildKey(sym, tfUpper);
        var candles = new List<ReplayCandle>();

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

            if (!TryParseCandle(line, sym, tfUpper, out var candle))
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
            sym, tfUpper, candles.Count, skipped);

        return candles.Count;
    }

    public int ImportCandlesDirect(string symbol, string timeframe, IEnumerable<ReplayCandle> candles)
    {
        var sym = string.IsNullOrWhiteSpace(symbol) ? DefaultReplaySymbol : symbol.Trim();
        var tfUpper = timeframe.Trim().ToUpperInvariant();
        var key = BuildKey(sym, tfUpper);

        var list = candles.OrderBy(c => c.Timestamp).ToList();
        _candles[key] = list;

        _logger.LogInformation(
            "Replay direct import: {Symbol} {Timeframe} — {Count} candles stored",
            sym, tfUpper, list.Count);

        return list.Count;
    }



    public async Task StartAsync(ReplayStartRequest request, CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("Replay is already running. Stop it first.");
        }

        var symbol = string.IsNullOrWhiteSpace(request.Symbol)
            ? DefaultReplaySymbol
            : request.Symbol.Trim();

        // Prefer M5 as driver timeframe; fall back to whatever is imported
        var driverTf = ChooseDriverTimeframe(symbol);
        if (driverTf is null)
        {
            throw new InvalidOperationException(
            $"No candles imported for symbol '{symbol}'. " +
                $"Use POST /api/replay/import to upload CSV data first.");
        }

        var driverKey = BuildKey(symbol, driverTf);
        var driverCandles = _candles[driverKey];

        var filtered = driverCandles
            .Where(c => (request.From is null || c.Timestamp >= request.From)
                     && (request.To is null || c.Timestamp <= request.To))
            .ToList();

        if (filtered.Count == 0)
        {
            throw new InvalidOperationException(
                $"No candles in the requested date range for {symbol}/{driverTf}.");
        }

        _symbol = symbol;
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
        _phase = "RUNNING";

        _replayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _replayCts.Token;

        // Capture delay to avoid closure issues
        var speedMultiplier = Math.Max(0, request.SpeedMultiplier);
        var useAI = request.UseMockAI ? false : request.UseAI;
        var ignoreNewsGate = request.IgnoreNewsGate;
        var replayTelegramState = NormalizeReplayTelegramState(request.TelegramReplayState);
        _initialCashAed = Math.Max(request.InitialCashAed, ReplayCapitalFloorAed);
        if (_initialCashAed > request.InitialCashAed)
        {
            _logger.LogInformation(
                "Replay capital normalized from {RequestedCash} to {NormalizedCash} AED to preserve 100g minimum-size parity under replay size caps.",
                request.InitialCashAed,
                _initialCashAed);
        }

        // Ensure each replay run starts from a clean risk state.
        DecisionEngine.ResetRuntimeGuards();

        _ = Task.Run(async () =>
        {
            try
            {
                await RunReplayLoopAsync(filtered, symbol, useAI, ignoreNewsGate, replayTelegramState, speedMultiplier, token);
                _phase = "DONE";
            }
            catch (OperationCanceledException)
            {
                _phase = "IDLE";
            }
            catch (Exception)
            {
                _phase = "ERROR";
                throw;
            }
            finally
            {
                _isRunning = false;
                _isPaused = false;
                _logger.LogInformation(
                    "Replay finished. {Symbol} {Tf}: {Processed}/{Total} candles, {Cycles} cycles, {Setups} setup candidates, {Trades} trades armed.",
                    symbol, driverTf, _processedCandles, _totalCandles, _cyclesTriggered, _setupCandidatesFound, _tradesArmed);
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
        _phase = "IDLE";
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
        DriverTimeframe: _driverTimeframe,
        Phase: _phase);


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
        bool ignoreNewsGate,
        string replayTelegramState,
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

            var snapshot = BuildSnapshot(symbol, candle) with
            {
                TelegramState = replayTelegramState,
            };
            _processedCandles++;
            _cyclesTriggered++;

            await ProcessReplayCycleAsync(snapshot, useAI, ignoreNewsGate, ct);

            if (candleIntervalMs > 0)
                await Task.Delay(candleIntervalMs, ct);
        }
    }

    private async Task ProcessReplayCycleAsync(
        MarketSnapshotContract snapshot,
        bool useAI,
        bool ignoreNewsGate,
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
        // Log market regime as a dedicated event before rule engine evaluation.
        var marketRegimePrecheck = MarketRegimeDetector.Detect(snapshot);
        await timeline.WriteAsync(
            eventType: "MARKET_REGIME_DETECTED",
            stage: "regime",
            source: "replay_engine",
            symbol: snapshot.Symbol,
            cycleId: cycleId,
            tradeId: null,
            payload: new
            {
                regime = marketRegimePrecheck.Regime,
                isTradeable = marketRegimePrecheck.IsTradeable,
                reason = marketRegimePrecheck.Reason,
                ema50H1 = snapshot.Ema50H1,
                ema200H1 = snapshot.Ema200H1,
                rsiH1 = snapshot.RsiH1,
            },
            cancellationToken: ct);

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
                marketRegime = setup.MarketRegime,
                h1Context = setup.H1Context,
                m15Setup = setup.M15Setup,
                m5Entry = setup.M5Entry,
                impulseConfirmation = setup.ImpulseConfirmation,
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

        // ── Pattern Detector (CR8): mirrors the live path — runs after regime/rule-engine ──
        // Produces structured pattern intelligence for ANALYZE/TABLE/MANAGE/STUDY feeds.
        // Non-executing: no trades placed here, only intelligence emitted.
        try
        {
            var patterns = PatternDetector.Detect(snapshot);
            if (patterns.Count > 0)
            {
                await timeline.WriteAsync(
                    eventType: "PATTERN_DETECTOR_RESULTS",
                    stage: "pattern",
                    source: "replay_engine",
                    symbol: snapshot.Symbol,
                    cycleId: cycleId,
                    tradeId: null,
                    payload: new
                    {
                        patternCount = patterns.Count,
                        replayMode = true,
                        patterns = patterns.Select(p => new
                        {
                            patternId = p.PatternId,
                            patternVersion = p.PatternVersion,
                            detectionMode = p.DetectionMode.ToString(),
                            patternType = p.PatternType.ToString(),
                            subtype = p.Subtype,
                            confidence = p.Confidence,
                            session = p.Session,
                            timeframePrimary = p.TimeframePrimary,
                            entrySafety = p.EntrySafety,
                            waterfallRisk = p.WaterfallRisk,
                            failThreatened = p.FailThreatened,
                            recommendedAction = p.RecommendedAction.ToString(),
                        }).ToList(),
                    },
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Replay [{CycleId}] Pattern detector failed; continuing cycle.", cycleId);
        }

        if (ignoreNewsGate)
        {
            await timeline.WriteAsync(
                eventType: "REPLAY_NEWS_CHECK",
                stage: "news",
                source: "replay_engine",
                symbol: snapshot.Symbol,
                cycleId: cycleId,
                tradeId: null,
                payload: new
                {
                    blocked = false,
                    reason = "News gate bypassed by replay configuration (ignoreNewsGate=true).",
                    nearbyEvents = Array.Empty<object>(),
                    replayMode = true,
                    ignored = true,
                },
                cancellationToken: ct);
        }
        else
        {
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
                    replayMode = true,
                    ignored = false,
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

            aiSignal = NormalizeAiSignalForReplay(aiSignal, snapshot, ignoreNewsGate);

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
        else if (string.Equals(decision.Cause, "BOTTOMPERMISSION_FALSE", StringComparison.Ordinal))
        {
            // BLOCKED_VALID_SETUP_CANDIDATE (CR8): mirrors the live path — tag study candidates
            // when a setup passed scoring but was blocked by the bottom-permission gate.
            await timeline.WriteAsync(
                eventType: "BLOCKED_VALID_SETUP_CANDIDATE",
                stage: "study",
                source: "replay_engine",
                symbol: snapshot.Symbol,
                cycleId: cycleId,
                tradeId: null,
                payload: new
                {
                    cause = decision.Cause,
                    bottomPermissionReason = decision.Reason,
                    tradeScore = tradeScore.TotalScore,
                    session = snapshot.Session,
                    sessionPhase = snapshot.SessionPhase,
                    waterfallRisk = decision.WaterfallRisk,
                    replayMode = true,
                    note = "Replay study candidate: passed scoring but blocked by BottomPermission. " +
                           "STUDY should determine if block saved from waterfall or if rule is too strict.",
                },
                cancellationToken: ct);
        }
    }

    private static TradeSignalContract NormalizeAiSignalForReplay(TradeSignalContract signal, MarketSnapshotContract snapshot, bool ignoreNewsGate)
    {
        if (!ignoreNewsGate)
            return signal;

        var pretableBlocked = string.Equals(signal.DisagreementReason, "RISK_BLOCKED_PRETABLE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(signal.Rail, "NO_TRADE", StringComparison.OrdinalIgnoreCase)
            || (signal.Summary?.Contains("PRETABLE_BLOCK", StringComparison.OrdinalIgnoreCase) ?? false);

        if (pretableBlocked)
        {
            var fallback = BuildMockAiSignal(snapshot);
            return fallback with
            {
                Summary = "Replay override: bypassed AI pretable news/sentiment block because ignoreNewsGate=true.",
            };
        }

        var normalizedSafetyTag = string.Equals(signal.SafetyTag, "BLOCK", StringComparison.OrdinalIgnoreCase)
            ? "CAUTION"
            : signal.SafetyTag;

        return signal with
        {
            SafetyTag = normalizedSafetyTag,
            NewsImpactTag = "LOW",
            NewsTags = Array.Empty<string>(),
            ModeHint = "UNKNOWN",
            ModeConfidence = Math.Min(signal.ModeConfidence, 0.45m),
            ModeTtlSeconds = 900,
            ModeKeywords = Array.Empty<string>(),
            RegimeTag = "STANDARD",
            RiskState = "SAFE",
            GeoHeadline = "NONE",
            EventRisk = "LOW",
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private MarketSnapshotContract BuildSnapshot(string symbol, ReplayCandle primary)
    {
        // Build multi-timeframe data from all imported candles for this symbol
        var timeframeData = new List<TimeframeDataContract>();
        var indicatorByTimeframe = new Dictionary<string, IndicatorPack>(StringComparer.OrdinalIgnoreCase);

        foreach (var tf in new[] { "H1", "M15", "M5" })
        {
            var key = BuildKey(symbol, tf);
            if (!_candles.TryGetValue(key, out var list)) continue;

               var uptoIdx = BinarySearchLastIndexLtEq(list, primary.Timestamp);
               if (uptoIdx < 0) continue;
           
               var candle = list[uptoIdx];
               var indicators = BuildIndicatorPackOptimized(list, uptoIdx);
            indicatorByTimeframe[tf] = indicators;

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
                CandleRange: range,
                Ma20Value: indicators.Ma20,
                Ma20Distance: indicators.Ma20 > 0m ? candle.Close - indicators.Ma20 : 0m,
                Rsi: indicators.Rsi14,
                Atr: indicators.Atr14));
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
        var h1Indicators = indicatorByTimeframe.GetValueOrDefault("H1");
        var m15Indicators = indicatorByTimeframe.GetValueOrDefault("M15");
        var m5Indicators = indicatorByTimeframe.GetValueOrDefault("M5");
        var compressionCountM15 = CountCompressionCandles(symbol, primary.Timestamp);
        var compressionCountM5 = CountCompressionCandles(symbol, "M5", primary.Timestamp, CompressionWindowM5);
        var expansionCountM15 = CountExpansionCandles(symbol, primary.Timestamp);
        var hasLiquiditySweep = DetectLiquiditySweep(symbol, primary.Timestamp);
        var m5Range = timeframeData.FirstOrDefault(t => string.Equals(t.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.CandleRange ?? 0m;
        var m5Body = timeframeData.FirstOrDefault(t => string.Equals(t.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.CandleBodySize ?? 0m;
        var atrM15 = m15Indicators.Atr14 > 0m ? m15Indicators.Atr14 : atr;
        var impulseScore = atrM15 > 0m ? Clamp01(m5Body / atrM15) : 0m;
        var hasImpulse = impulseScore >= 0.40m;
        var isExpansion = atrM15 > 0m && m5Range >= atrM15 * 1.15m;

        return new MarketSnapshotContract(
            Symbol: symbol,
            TimeframeData: timeframeData,
            Atr: atr,
            Adr: atr * 2.5m,
            Ma20: h1Indicators.Ma20 > 0m ? h1Indicators.Ma20 : (m5Indicators.Ma20 > 0m ? m5Indicators.Ma20 : close),
            Session: session,
            Timestamp: primary.Timestamp,
            Ma20H1: h1Indicators.Ma20,
            RsiH1: h1Indicators.Rsi14,
            RsiM15: m15Indicators.Rsi14,
            AtrH1: h1Indicators.Atr14,
            AtrM15: m15Indicators.Atr14,
            Ema50H1: h1Indicators.Ema50,
            Ema200H1: h1Indicators.Ema200,
            Bid: close - 0.10m,
            Ask: close + 0.10m,
            Spread: 0.20m,
            AuthoritativeRate: close,
            RateAuthority: "REPLAY_CANDLE",
            IsCompression: DetectCompression(symbol, primary.Timestamp),
            CompressionCountM15: compressionCountM15,
            ExpansionCountM15: expansionCountM15,
            HasImpulseCandles: hasImpulse,
            IsExpansion: isExpansion,
            IsAtrExpanding: expansionCountM15 > 0,
            ImpulseStrengthScore: impulseScore,
            HasOverlapCandles: DetectBase(symbol, primary.Timestamp),
                HasLiquiditySweep: hasLiquiditySweep,
            DayOfWeek: primary.Timestamp.DayOfWeek,
            Mt5ServerTime: primary.Timestamp,
            KsaTime: primary.Timestamp.AddMinutes(50),
            SessionPhase: phase,
            IsFriday: primary.Timestamp.DayOfWeek == DayOfWeek.Friday,
                CompressionCountM5: compressionCountM5,
            CycleId: $"replay_{primary.Timestamp:yyyyMMddHHmmss}");
    }

    private readonly record struct IndicatorPack(decimal Ma20, decimal Rsi14, decimal Atr14, decimal Ema50, decimal Ema200);

    private static IndicatorPack BuildIndicatorPack(IReadOnlyList<ReplayCandle> candles)
    {
        if (candles.Count == 0)
            return default;

        var closes = candles.Select(c => c.Close).ToList();
        var ma20 = CalculateSma(closes, 20);
        var rsi14 = CalculateRsi(closes, 14);
        var atr14 = CalculateAtr(candles, 14);
        var ema50 = CalculateEma(closes, 50);
        var ema200 = CalculateEma(closes, 200);
        return new IndicatorPack(ma20, rsi14, atr14, ema50, ema200);
    }

    private static IndicatorPack BuildIndicatorPackOptimized(List<ReplayCandle> allCandles, int upToIndex)
    {
        if (upToIndex < 0)
            return default;

        var closes = new List<decimal>(upToIndex + 1);
        for (int i = 0; i <= upToIndex; i++)
        {
            closes.Add(allCandles[i].Close);
        }

        var ma20 = CalculateSma(closes, 20);
        var rsi14 = CalculateRsi(closes, 14);
        var atr14 = CalculateAtrOptimized(allCandles, upToIndex, 14);
        var ema50 = CalculateEma(closes, 50);
        var ema200 = CalculateEma(closes, 200);
        return new IndicatorPack(ma20, rsi14, atr14, ema50, ema200);
    }

    private static int BinarySearchLastIndexLtEq(List<ReplayCandle> candles, DateTimeOffset target)
    {
        int left = 0, right = candles.Count - 1;
        int result = -1;
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            if (candles[mid].Timestamp <= target)
            {
                result = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        return result;
    }

    private static decimal CalculateAtrOptimized(List<ReplayCandle> allCandles, int upToIndex, int period)
    {
        if (upToIndex < 1 || period <= 0)
            return 0m;

        var startIdx = Math.Max(1, upToIndex - 50);
        var trueRanges = new List<decimal>();
        for (int i = startIdx; i <= upToIndex; i++)
        {
            var current = allCandles[i];
            var prev = allCandles[i - 1];
            var tr = Math.Max(
                current.High - current.Low,
                Math.Max(Math.Abs(current.High - prev.Close), Math.Abs(current.Low - prev.Close)));
            trueRanges.Add(tr);
        }
        if (trueRanges.Count == 0)
            return 0m;
        var atr = trueRanges.Take(period).Average();
        for (var i = period; i < trueRanges.Count; i++)
        {
            atr = ((atr * (period - 1)) + trueRanges[i]) / period;
        }
        return atr;
    }

    private static decimal CalculateSma(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period || period <= 0)
            return 0m;

        return values.TakeLast(period).Average();
    }

    private static decimal CalculateEma(IReadOnlyList<decimal> values, int period)
    {
        if (values.Count < period || period <= 0)
            return 0m;

        var seed = values.Take(period).Average();
        var k = 2m / (period + 1m);
        var ema = seed;
        for (var i = period; i < values.Count; i++)
        {
            ema = ((values[i] - ema) * k) + ema;
        }

        return ema;
    }

    private static decimal CalculateRsi(IReadOnlyList<decimal> closes, int period)
    {
        if (closes.Count <= period || period <= 0)
            return 0m;

        decimal gains = 0m;
        decimal losses = 0m;

        for (var i = 1; i <= period; i++)
        {
            var delta = closes[i] - closes[i - 1];
            if (delta > 0m)
                gains += delta;
            else
                losses += -delta;
        }

        var avgGain = gains / period;
        var avgLoss = losses / period;

        for (var i = period + 1; i < closes.Count; i++)
        {
            var delta = closes[i] - closes[i - 1];
            var gain = delta > 0m ? delta : 0m;
            var loss = delta < 0m ? -delta : 0m;
            avgGain = ((avgGain * (period - 1)) + gain) / period;
            avgLoss = ((avgLoss * (period - 1)) + loss) / period;
        }

        if (avgLoss == 0m)
            return avgGain == 0m ? 50m : 100m;

        var rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }

    private static decimal CalculateAtr(IReadOnlyList<ReplayCandle> candles, int period)
    {
        if (candles.Count < 2 || period <= 0)
            return 0m;

        var trueRanges = new List<decimal>(candles.Count - 1);
        for (var i = 1; i < candles.Count; i++)
        {
            var current = candles[i];
            var previousClose = candles[i - 1].Close;
            var highLow = current.High - current.Low;
            var highClose = Math.Abs(current.High - previousClose);
            var lowClose = Math.Abs(current.Low - previousClose);
            trueRanges.Add(Math.Max(highLow, Math.Max(highClose, lowClose)));
        }

        if (trueRanges.Count == 0)
            return 0m;

        var take = Math.Min(period, trueRanges.Count);
        return trueRanges.TakeLast(take).Average();
    }

    private int CountCompressionCandles(string symbol, DateTimeOffset at)
        => CountCompressionCandles(symbol, "M15", at, CompressionWindowM15);

    private int CountCompressionCandles(string symbol, string timeframe, DateTimeOffset at, int lookbackCandles)
    {
        var key = BuildKey(symbol, timeframe);
        if (!_candles.TryGetValue(key, out var list)) return 0;

        var window = list
            .Where(c => c.Timestamp <= at)
            .TakeLast(lookbackCandles)
            .ToList();

        if (window.Count < 3) return 0;

        var averageRange = window.Average(c => c.High - c.Low);
        var threshold = averageRange * 0.80m;
        return window.Count(c => (c.High - c.Low) <= threshold);
    }

    private bool DetectLiquiditySweep(string symbol, DateTimeOffset at)
    {
        var key = BuildKey(symbol, "H1");
        if (!_candles.TryGetValue(key, out var list))
            return false;

        var uptoIdx = BinarySearchLastIndexLtEq(list, at);
        if (uptoIdx < 2)
            return false;

        // Compare current H1 candle against the most recent 24 prior H1 candles.
        var current = list[uptoIdx];
        var priorStart = Math.Max(0, uptoIdx - 24);
        var priorCount = uptoIdx - priorStart;
        if (priorCount < 3)
            return false;

        var priorSwingLow = list
            .Skip(priorStart)
            .Take(priorCount)
            .Min(c => c.Low);

        if (current.Low >= priorSwingLow)
            return false;

        var h1Range = current.High - current.Low;
        var reclaimBuffer = h1Range > 0m ? h1Range * 0.20m : 0m;
        return current.Close >= priorSwingLow + reclaimBuffer;
    }

    private int CountExpansionCandles(string symbol, DateTimeOffset at)
    {
        var key = BuildKey(symbol, "M15");
        if (!_candles.TryGetValue(key, out var list)) return 0;

        var window = list
            .Where(c => c.Timestamp <= at)
            .TakeLast(8)
            .ToList();

        if (window.Count < 3) return 0;

        var averageRange = window.Average(c => c.High - c.Low);
        var threshold = averageRange * 1.20m;
        return window.Count(c => (c.High - c.Low) >= threshold);
    }

    private static decimal Clamp01(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 1m) return 1m;
        return value;
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

    private static string NormalizeReplayTelegramState(string? state)
    {
        var normalized = (state ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "STRONG_BUY" => "STRONG_BUY",
            "BUY" => "BUY",
            "BULLISH" => "BULLISH",
            "QUIET" => "QUIET",
            "MIXED" => "MIXED",
            "BEARISH" => "BEARISH",
            "SELL" => "SELL",
            "STRONG_SELL" => "STRONG_SELL",
            "PANIC" => "PANIC",
            _ => "QUIET",
        };
    }

    private string? ChooseDriverTimeframe(string symbol)
    {
        foreach (var tf in new[] { "M5", "M15", "H1" })
        {
            if (_candles.TryGetValue(BuildKey(symbol, tf), out var candles) && candles.Count > 0)
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
