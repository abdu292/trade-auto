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
}
