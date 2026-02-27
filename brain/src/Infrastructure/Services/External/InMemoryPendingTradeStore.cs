using System.Collections.Concurrent;
using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryPendingTradeStore : IPendingTradeStore
{
    private readonly ConcurrentQueue<PendingTradeContract> _queue = new();

    public void Enqueue(PendingTradeContract trade) => _queue.Enqueue(trade);

    public bool TryDequeue(out PendingTradeContract? trade)
    {
        var ok = _queue.TryDequeue(out var item);
        trade = item;
        return ok;
    }
}
