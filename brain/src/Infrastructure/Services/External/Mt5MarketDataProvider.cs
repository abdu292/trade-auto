using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class Mt5MarketDataProvider(ILatestMarketSnapshotStore snapshotStore) : IMarketDataProvider
{
    public Task<MarketSnapshotContract> GetSnapshotAsync(string symbol, CancellationToken cancellationToken)
    {
        if (!snapshotStore.TryGet(out var latest) || latest is null)
        {
            throw new InvalidOperationException("No MT5 snapshot available yet.");
        }

        if (!string.Equals(latest.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Latest snapshot symbol '{latest.Symbol}' does not match requested symbol '{symbol}'.");
        }

        if (DateTimeOffset.UtcNow - latest.Timestamp > TimeSpan.FromMinutes(5))
        {
            throw new InvalidOperationException("Latest MT5 snapshot is stale.");
        }

        return Task.FromResult(latest);
    }
}
