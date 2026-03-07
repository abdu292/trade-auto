using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services.External;

/// <summary>
/// Simple in-memory store for a single pending MT5 history-fetch request.
/// Only one request can be queued at a time; a new queue call replaces any
/// existing pending request.
/// </summary>
public sealed class InMemoryHistoryFetchStore : IHistoryFetchStore
{
    private volatile Mt5HistoryFetchRequest? _pending;

    public void Queue(Mt5HistoryFetchRequest request)
        => _pending = request;

    public Mt5HistoryFetchRequest? GetPending() => _pending;

    public Mt5HistoryFetchRequest? TryConsume()
        => Interlocked.Exchange(ref _pending, null);
}
