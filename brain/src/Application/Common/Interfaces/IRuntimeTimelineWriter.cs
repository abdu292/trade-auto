namespace Brain.Application.Common.Interfaces;

public interface IRuntimeTimelineWriter
{
    Task WriteAsync(
        string eventType,
        string stage,
        string source,
        string symbol,
        string? cycleId,
        string? tradeId,
        object payload,
        CancellationToken cancellationToken);
}