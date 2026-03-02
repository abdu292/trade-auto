namespace Brain.Application.Common.Models;

public sealed record TickIngestionEventContract(
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset Mt5ServerTime,
    DateTimeOffset KsaTime,
    string Symbol,
    string Session,
    decimal Bid,
    decimal Ask,
    decimal Spread,
    decimal VolatilityExpansion,
    int TimeframeCount,
    double EstimatedIngestionLatencyMs);

public sealed record TickIngestionTelemetryContract(
    long TotalIngested,
    DateTimeOffset? LastReceivedAtUtc,
    DateTimeOffset? LastMt5ServerTime,
    DateTimeOffset? LastKsaTime,
    double LastIngestionLatencyMs,
    double TicksPerMinuteLast1m,
    double TicksPerMinuteLast5m,
    IReadOnlyList<TickIngestionEventContract> RecentTicks);
