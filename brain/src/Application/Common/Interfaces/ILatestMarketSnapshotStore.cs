using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface ILatestMarketSnapshotStore
{
    void Upsert(MarketSnapshotContract snapshot);
    bool TryGet(out MarketSnapshotContract? snapshot);
    TickIngestionTelemetryContract GetTickTelemetry(int take = 20);
}
