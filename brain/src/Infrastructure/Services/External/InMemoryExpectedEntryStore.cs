using System.Collections.Concurrent;
using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryExpectedEntryStore : IExpectedEntryStore
{
    private readonly ConcurrentDictionary<Guid, decimal> _expected = new();

    public void Set(Guid tradeId, decimal expectedEntryPrice) =>
        _expected[tradeId] = expectedEntryPrice;

    public bool TryGet(Guid tradeId, out decimal expectedEntryPrice) =>
        _expected.TryRemove(tradeId, out expectedEntryPrice);
}
