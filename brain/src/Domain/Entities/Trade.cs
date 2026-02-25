using Brain.Domain.Common;
using Brain.Domain.Events;
using Brain.Domain.ValueObjects;

namespace Brain.Domain.Entities;

public sealed class Trade : BaseEntity<TradeId>
{
    private Trade()
    {
    }

    public string Symbol { get; private set; } = string.Empty;
    public RailType Rail { get; private set; }
    public Price Entry { get; private set; }
    public Price TakeProfit { get; private set; }
    public DateTimeOffset ExpiryUtc { get; private set; }
    public int MaxLifeSeconds { get; private set; }
    public string Status { get; private set; } = "Pending";
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Trade Create(
        string symbol,
        RailType rail,
        Price entry,
        Price takeProfit,
        DateTimeOffset expiryUtc,
        int maxLifeSeconds)
    {
        var entity = new Trade
        {
            Id = TradeId.New(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            Rail = rail,
            Entry = entry,
            TakeProfit = takeProfit,
            ExpiryUtc = expiryUtc,
            MaxLifeSeconds = maxLifeSeconds,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        entity.AddDomainEvent(new TradeCreated(entity.Id));
        return entity;
    }

    public void MarkExecuted()
    {
        Status = "Executed";
        AddDomainEvent(new TradeExecuted(Id));
    }

    public void MarkRejected(string reason)
    {
        Status = $"Rejected:{reason}";
        AddDomainEvent(new TradeRejected(Id, reason));
    }
}
