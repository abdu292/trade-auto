using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface IHistoricalReplayService
{
    /// <summary>
    /// Imports candles from a CSV stream for the given symbol and timeframe.
    /// Returns the number of candles successfully imported.
    /// </summary>
    Task<int> ImportCandlesAsync(
        string symbol,
        string timeframe,
        Stream csvStream,
        CancellationToken cancellationToken);

    /// <summary>
    /// Imports candles directly from an in-memory list (used by the MT5 history
    /// fetch flow where the EA POSTs OHLCV data directly).
    /// Returns the number of candles stored.
    /// </summary>
    int ImportCandlesDirect(string symbol, string timeframe, IEnumerable<ReplayCandle> candles);

    /// <summary>Starts the replay loop using previously imported candles.</summary>
    Task StartAsync(ReplayStartRequest request, CancellationToken cancellationToken);

    /// <summary>Pauses the running replay at the current candle position.</summary>
    void Pause();

    /// <summary>Resumes a paused replay.</summary>
    void Resume();

    /// <summary>Stops and resets the replay.</summary>
    void Stop();

    /// <summary>Returns the current replay status and counters.</summary>
    ReplayStatusContract GetStatus();

    /// <summary>Returns the number of imported candles per timeframe for a symbol.</summary>
    IReadOnlyDictionary<string, int> GetImportedCounts(string symbol);

    /// <summary>
    /// Sets the replay phase string (e.g. MT5_FETCH_QUEUED, MT5_FETCH_RECEIVED,
    /// IMPORTING, RUNNING, DONE, ERROR, IDLE).
    /// </summary>
    void SetPhase(string phase);

    /// <summary>Sets the symbol that the next run will target (needed before fetch).</summary>
    void SetPendingSymbol(string symbol);
}

