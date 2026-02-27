using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryLatestMarketSnapshotStore : ILatestMarketSnapshotStore
{
    private readonly Lock _gate = new();
    private MarketSnapshotContract? _latest;

    public void Upsert(MarketSnapshotContract snapshot)
    {
        lock (_gate)
        {
            _latest = snapshot;
        }
    }

    public bool TryGet(out MarketSnapshotContract? snapshot)
    {
        lock (_gate)
        {
            snapshot = _latest;
            return snapshot is not null;
        }
    }
}
