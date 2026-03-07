using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Brain.Web.Endpoints;

public static class ReplayEndpoints
{
    public static IEndpointRouteBuilder MapReplayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/replay")
            .WithTags("Historical Replay");

        // ── Import CSV candle data ──────────────────────────────────────────
        group.MapPost(
            "/import",
            async Task<IResult> (
                IHistoricalReplayService replay,
                IFormFile file,
                string symbol,
                string timeframe,
                CancellationToken cancellationToken) =>
            {
                if (file == null || file.Length == 0)
                {
                    return TypedResults.BadRequest(new { error = "A CSV file must be uploaded using the 'file' form field." });
                }

                var symNorm = (symbol ?? string.Empty).Trim().ToUpperInvariant();
                var tfNorm = (timeframe ?? string.Empty).Trim().ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(symNorm))
                    return TypedResults.BadRequest(new { error = "symbol query parameter is required." });

                if (string.IsNullOrWhiteSpace(tfNorm))
                    return TypedResults.BadRequest(new { error = "timeframe query parameter is required (M5, M15, H1, etc.)." });

                try
                {
                    using var stream = file.OpenReadStream();
                    var count = await replay.ImportCandlesAsync(symNorm, tfNorm, stream, cancellationToken);
                    var totals = replay.GetImportedCounts(symNorm);

                    return TypedResults.Ok(new
                    {
                        symbol = symNorm,
                        timeframe = tfNorm,
                        imported = count,
                        totalImportedPerTimeframe = totals,
                        hint = count == 0
                            ? "No candles were parsed. Check CSV format: timestamp,open,high,low,close[,volume]"
                            : $"Successfully imported {count} candles. Use POST /api/replay/start to begin replay.",
                    });
                }
                catch (Exception ex)
                {
                    return TypedResults.BadRequest(new { error = ex.Message });
                }
            })
            .DisableAntiforgery()
            .WithName("ImportReplayCandles")
            .WithDescription(
                "Import MT5-exported CSV candle data for historical replay. " +
                "Upload the file using multipart/form-data with field name 'file'. " +
                "Accepted formats: MT5 (Date,Time,Open,High,Low,Close,Volume) or ISO (timestamp,open,high,low,close[,volume]). " +
                "Call once per timeframe (M5, M15, H1) before starting replay.");

        // ── Receive MT5 candle batch (posted by EA after a fetch-history request) ─
        group.MapPost(
            "/mt5-history",
            IResult (
                IHistoricalReplayService replay,
                ILogger<object> logger,
                Mt5HistoryBatchRequest request) =>
            {
                if (string.IsNullOrWhiteSpace(request.Symbol))
                    return TypedResults.BadRequest(new { error = "Symbol is required." });

                if (string.IsNullOrWhiteSpace(request.Timeframe))
                    return TypedResults.BadRequest(new { error = "Timeframe is required." });

                var sym = request.Symbol.Trim().ToUpperInvariant();
                var tf = request.Timeframe.Trim().ToUpperInvariant();

                var candles = request.Candles
                    .Select(c => new ReplayCandle(
                        Symbol: sym,
                        Timeframe: tf,
                        Timestamp: DateTimeOffset.FromUnixTimeSeconds(c.TimestampUnix),
                        Open: (decimal)c.Open,
                        High: (decimal)c.High,
                        Low: (decimal)c.Low,
                        Close: (decimal)c.Close,
                        Volume: c.Volume))
                    .ToList();

                var count = replay.ImportCandlesDirect(sym, tf, candles);

                logger.LogInformation(
                    "[ReplayImport] MT5 batch received Symbol={Symbol} TF={Tf} Candles={Count} IsFinalBatch={IsFinalBatch}",
                    sym,
                    tf,
                    count,
                    request.IsFinalBatch);

                if (request.IsFinalBatch)
                {
                    replay.SetPhase("MT5_FETCH_RECEIVED");
                    logger.LogInformation("[ReplayImport] Final batch received; phase moved to MT5_FETCH_RECEIVED for {Symbol}", sym);
                }

                return TypedResults.Ok(new
                {
                    symbol = sym,
                    timeframe = tf,
                    stored = count,
                    isFinalBatch = request.IsFinalBatch,
                });
            })
            .WithName("ReceiveMt5HistoryBatch")
            .WithDescription(
                "Called by the MT5 EA to deliver historical candle batches after a fetch-history request. " +
                "Set isFinalBatch=true on the last timeframe batch so the brain can auto-start replay.");

        // ── Run replay (MT5 fetch → import → start in one click) ───────────
        group.MapPost(
            "/run",
            IResult (
                IHistoricalReplayService replay,
                IHistoryFetchStore fetchStore,
                ILogger<object> logger,
                RunReplayRequest request) =>
            {
                if (replay.GetStatus().IsRunning)
                    return TypedResults.BadRequest(new { error = "A replay is already running. Stop it first." });

                var sym = (request.Symbol ?? "XAUUSD").Trim().ToUpperInvariant();
                var from = request.From ?? DateTimeOffset.UtcNow.AddDays(-7);
                var to = request.To ?? DateTimeOffset.UtcNow;

                if (to <= from)
                    return TypedResults.BadRequest(new { error = "'to' must be after 'from'." });

                // Store pending replay settings so the mt5-history endpoint can auto-start
                replay.Stop(); // clear any previous state
                replay.SetPendingSymbol(sym);
                replay.SetPhase("MT5_FETCH_QUEUED");

                // Queue fetch for EA
                fetchStore.Queue(new Mt5HistoryFetchRequest(
                    Symbol: sym,
                    Timeframes: ["M5", "M15", "H1"],
                    From: from,
                    To: to));

                logger.LogInformation(
                    "[ReplayRun] Fetch queued Symbol={Symbol} From={From:o} To={To:o} Timeframes=M5,M15,H1",
                    sym,
                    from,
                    to);

                return TypedResults.Ok(new
                {
                    message = "MT5 history fetch queued. The EA will fetch candles and return them. Poll GET /api/replay/status to track progress.",
                    symbol = sym,
                    from,
                    to,
                    status = replay.GetStatus(),
                });
            })
            .WithName("RunReplay")
            .WithDescription(
                "One-click replay trigger: queues a history-fetch request for the MT5 EA (M5/M15/H1 candles), " +
                "the EA delivers the data, and replay starts automatically. " +
                "Poll GET /api/replay/status to watch the phase: MT5_FETCH_QUEUED → MT5_FETCH_RECEIVED → RUNNING → DONE.");

        // ── Auto-start after all MT5 batches are received ──────────────────
        // This is an internal helper endpoint called after /mt5-history sets
        // phase=MT5_FETCH_RECEIVED.  The caller (typically the EA's final POST)
        // can also trigger it.  Kept separate so it can be tested independently.
        group.MapPost(
            "/start-after-fetch",
            async Task<IResult> (
                IHistoricalReplayService replay,
                ILogger<object> logger,
                RunReplayRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    replay.SetPhase("IMPORTING");
                    var startReq = new ReplayStartRequest(
                        Symbol: (request.Symbol ?? "XAUUSD").Trim().ToUpperInvariant(),
                        From: request.From,
                        To: request.To,
                        SpeedMultiplier: request.SpeedMultiplier,
                        UseAI: !request.UseMockAI,
                        UseMockAI: request.UseMockAI,
                        InitialCashAed: request.InitialCashAed,
                        IgnoreNewsGate: request.IgnoreNewsGate,
                        TelegramReplayState: request.TelegramReplayState);

                    await replay.StartAsync(startReq, cancellationToken);
                    logger.LogInformation(
                        "[ReplayRun] start-after-fetch succeeded Symbol={Symbol} Speed={SpeedMultiplier} UseMockAI={UseMockAI}",
                        startReq.Symbol,
                        startReq.SpeedMultiplier,
                        startReq.UseMockAI);
                    return TypedResults.Ok(new { message = "Replay started.", status = replay.GetStatus() });
                }
                catch (InvalidOperationException ex)
                {
                    replay.SetPhase("ERROR");
                    logger.LogWarning(ex, "[ReplayRun] start-after-fetch failed for Symbol={Symbol}", request.Symbol);
                    return TypedResults.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("StartAfterFetch")
            .WithDescription("Starts replay using already-imported candles (called automatically after MT5 data arrives).");

        // ── Start replay ───────────────────────────────────────────────────
        group.MapPost(
            "/start",
            async Task<IResult> (
                IHistoricalReplayService replay,
                ReplayStartRequest request,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    await replay.StartAsync(request, cancellationToken);
                    var status = replay.GetStatus();

                    return TypedResults.Ok(new
                    {
                        message = "Replay started.",
                        status,
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return TypedResults.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("StartReplay")
            .WithDescription(
                "Start historical candle replay. Replay drives the full decision pipeline (rule engine → AI → decision engine) " +
                "using imported CSV data. Real trade execution is always disabled. " +
                "Real AI is used by default; set useMockAI=true for explicit mock mode. " +
                "You can also override the starting ledger cash via InitialCashAed. " +
                "For deterministic backtests, ignoreNewsGate defaults to true and telegramReplayState defaults to QUIET. " +
                "Decision timeline events are recorded in the database and viewable at GET /api/monitoring/timeline.");

        // ── Pause replay ───────────────────────────────────────────────────
        group.MapPost(
            "/pause",
            IResult (IHistoricalReplayService replay) =>
            {
                replay.Pause();
                return TypedResults.Ok(new { message = "Replay paused.", status = replay.GetStatus() });
            })
            .WithName("PauseReplay")
            .WithDescription("Pause the running replay at the current candle. Use /resume to continue.");

        // ── Resume replay ──────────────────────────────────────────────────
        group.MapPost(
            "/resume",
            IResult (IHistoricalReplayService replay) =>
            {
                replay.Resume();
                return TypedResults.Ok(new { message = "Replay resumed.", status = replay.GetStatus() });
            })
            .WithName("ResumeReplay")
            .WithDescription("Resume a paused replay.");

        // ── Stop replay ────────────────────────────────────────────────────
        group.MapPost(
            "/stop",
            IResult (IHistoricalReplayService replay) =>
            {
                replay.Stop();
                return TypedResults.Ok(new { message = "Replay stopped.", status = replay.GetStatus() });
            })
            .WithName("StopReplay")
            .WithDescription("Stop and reset the running replay.");

        // ── Replay status ──────────────────────────────────────────────────
        group.MapGet(
            "/status",
            IResult (IHistoricalReplayService replay, IHistoryFetchStore fetchStore, string? symbol) =>
            {
                var status = replay.GetStatus();

                var symNorm = (symbol ?? status.Symbol ?? "XAUUSD").Trim().ToUpperInvariant();
                var importedCounts = replay.GetImportedCounts(symNorm);
                var pendingFetch = fetchStore.GetPending();

                return TypedResults.Ok(new
                {
                    status,
                    importedCandles = importedCounts,
                    pendingFetch = pendingFetch is null ? null : new
                    {
                        symbol = pendingFetch.Symbol,
                        timeframes = pendingFetch.Timeframes,
                        from = pendingFetch.From,
                        to = pendingFetch.To,
                    },
                });
            })
            .WithName("GetReplayStatus")
            .WithDescription(
                "Returns current replay state including progress counters, phase, setup candidates found, and trades armed. " +
                "Also returns the number of imported candles per timeframe and any pending MT5 fetch request.");

        return app;
    }
}

