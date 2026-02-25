using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface IMarketDataProvider
{
    Task<MarketSnapshotContract> GetSnapshotAsync(string symbol, CancellationToken cancellationToken);
}
