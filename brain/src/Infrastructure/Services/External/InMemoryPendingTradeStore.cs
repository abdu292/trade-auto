using System.Collections.Concurrent;
using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryPendingTradeStore : IPendingTradeStore
{
    private readonly ConcurrentQueue<PendingTradeContract> _queue = new();

    public void Enqueue(PendingTradeContract trade) => _queue.Enqueue(trade);

    public int Count() => _queue.Count;

    public int Clear()
    {
        var cleared = 0;
        while (_queue.TryDequeue(out _))
        {
            cleared++;
        }

        return cleared;
    }

    public IReadOnlyCollection<PendingTradeContract> Snapshot() => _queue.ToArray();

    public bool TryDequeue(out PendingTradeContract? trade)
    {
        var ok = _queue.TryDequeue(out var item);
        trade = item;
        return ok;
    }
}
