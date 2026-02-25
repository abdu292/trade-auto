using Brain.Domain.Common;

namespace Brain.Domain.Events;

public sealed record TradeRejected(TradeId TradeId, string Reason) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
