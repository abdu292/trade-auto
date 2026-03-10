using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

/// <summary>Spec v7 §10 — Store last engine state for dashboard factor panel and trade-map.</summary>
public interface ILastGoldEngineStateStore
{
    void SetLast(EngineStatesContract? engineStates, PathRoutingResult? pathRouting, MarketSnapshotContract? snapshot);
    (EngineStatesContract? EngineStates, PathRoutingResult? PathRouting, MarketSnapshotContract? Snapshot) GetLast();
}
