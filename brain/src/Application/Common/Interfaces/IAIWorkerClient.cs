using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface IAIWorkerClient
{
    Task<TradeSignalContract> AnalyzeAsync(MarketSnapshotContract snapshot, string? cycleId, CancellationToken cancellationToken);
    Task<ModeSignalContract?> GetModeAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken);
}
