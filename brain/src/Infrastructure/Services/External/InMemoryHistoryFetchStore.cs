using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services.External;

/// <summary>
/// Simple in-memory store for a single pending MT5 history-fetch request.
/// Only one request can be queued at a time; a new queue call replaces any
/// existing pending request.
/// </summary>
public sealed class InMemoryHistoryFetchStore : IHistoryFetchStore
{
    private readonly object _lock = new();
    private Mt5HistoryFetchRequest? _pending;

    public void Queue(Mt5HistoryFetchRequest request)
    {
        lock (_lock)
        {
            _pending = request;
        }
    }

    public Mt5HistoryFetchRequest? GetPending()
    {
        lock (_lock)
        {
            return _pending;
        }
    }

    public Mt5HistoryFetchRequest? TryConsume()
    {
        lock (_lock)
        {
            var value = _pending;
            _pending = null;
            return value;
        }
    }
}
