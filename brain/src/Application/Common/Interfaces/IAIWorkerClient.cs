using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface IAIWorkerClient
{
    Task<TradeSignalContract> AnalyzeAsync(MarketSnapshotContract snapshot, CancellationToken cancellationToken);
}
