namespace Brain.Application.Common.Interfaces;

/// <summary>
/// Stores a pending MT5 history-fetch request so the EA can poll and fulfil it.
/// </summary>
public sealed record Mt5HistoryFetchRequest(
    string Symbol,
    string[] Timeframes,
    DateTimeOffset From,
    DateTimeOffset To);

public interface IHistoryFetchStore
{
    /// <summary>Queues a history-fetch request for the MT5 EA.</summary>
    void Queue(Mt5HistoryFetchRequest request);

    /// <summary>
    /// Returns the pending request (without consuming it) so the status endpoint
    /// can report that a fetch is queued.
    /// </summary>
    Mt5HistoryFetchRequest? GetPending();

    /// <summary>
    /// Atomically removes and returns the pending request.
    /// Returns null if nothing is queued.
    /// </summary>
    Mt5HistoryFetchRequest? TryConsume();
}
