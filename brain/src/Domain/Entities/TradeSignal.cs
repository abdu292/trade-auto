using Brain.Domain.Common;
using Brain.Domain.ValueObjects;

namespace Brain.Domain.Entities;

public sealed class TradeSignal : BaseEntity<TradeSignalId>
{
    private TradeSignal()
    {
    }

    public string Symbol { get; private set; } = string.Empty;
    public RailType Rail { get; private set; }
    public Price Entry { get; private set; }
    public Price TakeProfit { get; private set; }
    public DateTimeOffset PendingExpirationUtc { get; private set; }
    public int MaxLifeSeconds { get; private set; }
    public decimal Confidence { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static TradeSignal Create(
        string symbol,
        RailType rail,
        Price entry,
        Price takeProfit,
        DateTimeOffset pendingExpirationUtc,
        int maxLifeSeconds,
        decimal confidence)
    {
        return new TradeSignal
        {
            Id = TradeSignalId.New(),
            Symbol = symbol.Trim(),
            Rail = rail,
            Entry = entry,
            TakeProfit = takeProfit,
            PendingExpirationUtc = pendingExpirationUtc,
            MaxLifeSeconds = maxLifeSeconds,
            Confidence = confidence,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
