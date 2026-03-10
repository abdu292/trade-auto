using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryLastGoldEngineStateStore : ILastGoldEngineStateStore
{
    private volatile EngineStatesContract? _engineStates;
    private volatile PathRoutingResult? _pathRouting;
    private volatile MarketSnapshotContract? _snapshot;
    private readonly Lock _lock = new();

    public void SetLast(EngineStatesContract? engineStates, PathRoutingResult? pathRouting, MarketSnapshotContract? snapshot)
    {
        lock (_lock)
        {
            _engineStates = engineStates;
            _pathRouting = pathRouting;
            _snapshot = snapshot;
        }
    }

    public (EngineStatesContract? EngineStates, PathRoutingResult? PathRouting, MarketSnapshotContract? Snapshot) GetLast()
    {
        lock (_lock)
        {
            return (_engineStates, _pathRouting, _snapshot);
        }
    }
}
