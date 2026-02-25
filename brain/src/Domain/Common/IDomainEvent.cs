namespace Brain.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
