using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class MockMarketDataProvider : IMarketDataProvider
{
    public Task<MarketSnapshotContract> GetSnapshotAsync(string symbol, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var mt5ServerTime = now;

        var snapshot = new MarketSnapshotContract(
            Symbol: symbol.ToUpperInvariant(),
            TimeframeData:
            [
                new TimeframeDataContract("M5", 2940.00m, 2945.00m, 2937.00m, 2942.00m),
                new TimeframeDataContract("M15", 2938.00m, 2948.00m, 2935.00m, 2942.00m),
                new TimeframeDataContract("M30", 2935.00m, 2950.00m, 2932.00m, 2942.00m),
                new TimeframeDataContract("H1", 2928.00m, 2952.00m, 2920.00m, 2942.00m),
                new TimeframeDataContract("H4", 2908.00m, 2961.00m, 2898.00m, 2942.00m)
            ],
            Atr: 18.50m,
            Adr: 24.80m,
            Ma20: 2939.00m,
            Session: "London",
            Timestamp: now,
            VolatilityExpansion: 0.75m,
            DayOfWeek: mt5ServerTime.DayOfWeek,
            Mt5ServerTime: mt5ServerTime,
            KsaTime: mt5ServerTime.AddMinutes(50),
            Mt5ToKsaOffsetMinutes: 50,
            IsUsRiskWindow: now.Hour is >= 12 and < 17,
            IsFriday: mt5ServerTime.DayOfWeek == DayOfWeek.Friday);

        return Task.FromResult(snapshot);
    }
}
