using Brain.Domain.Common;

namespace Brain.Domain.Events;

public sealed record TradeCreated(TradeId TradeId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
