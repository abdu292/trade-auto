using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class RuntimeTimelineEvent : BaseEntity<Guid>
{
    private const string DefaultSymbol = "XAUUSD.gram";

    private RuntimeTimelineEvent()
    {
    }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Stage { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;
    public string? CycleId { get; private set; }
    public string? TradeId { get; private set; }
    public string PayloadJson { get; private set; } = "{}";

    public static RuntimeTimelineEvent Create(
        string eventType,
        string stage,
        string source,
        string symbol,
        string? cycleId,
        string? tradeId,
        string payloadJson)
    {
        return new RuntimeTimelineEvent
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            EventType = string.IsNullOrWhiteSpace(eventType) ? "UNKNOWN" : eventType.Trim().ToUpperInvariant(),
            Stage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage.Trim().ToLowerInvariant(),
            Source = string.IsNullOrWhiteSpace(source) ? "system" : source.Trim().ToLowerInvariant(),
            Symbol = string.IsNullOrWhiteSpace(symbol) ? DefaultSymbol : symbol.Trim(),
            CycleId = string.IsNullOrWhiteSpace(cycleId) ? null : cycleId.Trim(),
            TradeId = string.IsNullOrWhiteSpace(tradeId) ? null : tradeId.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
        };
    }
}