using Brain.Domain.Common;
using Brain.Domain.ValueObjects;

namespace Brain.Domain.Entities;

public sealed class MarketSnapshot : BaseEntity<MarketSnapshotId>
{
    private readonly List<TimeframeCandle> _timeframeData = [];

    private MarketSnapshot()
    {
    }

    public string Symbol { get; private set; } = string.Empty;
    public decimal Atr { get; private set; }
    public decimal Adr { get; private set; }
    public decimal Ma20 { get; private set; }
    public SessionType Session { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public IReadOnlyCollection<TimeframeCandle> TimeframeData => _timeframeData.AsReadOnly();

    public static MarketSnapshot Create(
        string symbol,
        decimal atr,
        decimal adr,
        decimal ma20,
        SessionType session,
        DateTimeOffset timestamp,
        IEnumerable<TimeframeCandle> timeframeData)
    {
        var snapshot = new MarketSnapshot
        {
            Id = MarketSnapshotId.New(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            Atr = atr,
            Adr = adr,
            Ma20 = ma20,
            Session = session,
            Timestamp = timestamp
        };

        snapshot._timeframeData.AddRange(timeframeData);
        return snapshot;
    }
}

public sealed record TimeframeCandle(string Timeframe, decimal Open, decimal High, decimal Low, decimal Close);
