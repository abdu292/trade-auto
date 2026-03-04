using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

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
            IResult (IHistoricalReplayService replay, string? symbol) =>
            {
                var status = replay.GetStatus();

                var symNorm = (symbol ?? status.Symbol ?? "XAUUSD").Trim().ToUpperInvariant();
                var importedCounts = replay.GetImportedCounts(symNorm);

                return TypedResults.Ok(new
                {
                    status,
                    importedCandles = importedCounts,
                });
            })
            .WithName("GetReplayStatus")
            .WithDescription(
                "Returns current replay state including progress counters, setup candidates found, and trades armed. " +
                "Also returns the number of imported candles per timeframe.");

        return app;
    }
}
