using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class MockMarketDataProvider : IMarketDataProvider
{
    public Task<MarketSnapshotContract> GetSnapshotAsync(string symbol, CancellationToken cancellationToken)
    {
        var snapshot = new MarketSnapshotContract(
            Symbol: symbol.ToUpperInvariant(),
            TimeframeData:
            [
                new TimeframeDataContract("M5", 1.10200m, 1.10320m, 1.10180m, 1.10290m),
                new TimeframeDataContract("M15", 1.10110m, 1.10400m, 1.10090m, 1.10370m)
            ],
            Atr: 0.00120m,
            Adr: 0.00650m,
            Ma20: 1.10250m,
            Session: "London",
            Timestamp: DateTimeOffset.UtcNow);

        return Task.FromResult(snapshot);
    }
}
