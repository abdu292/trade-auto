namespace Brain.Application.Common.Models;

public sealed record MarketSimulationStartContract(
    decimal StartPrice = 2890m,
    decimal VolatilityUsd = 0.45m,
    decimal BaseSpread = 0.18m,
    int IntervalSeconds = 5,
    string? SessionOverride = null,
    bool EnableShockEvents = true);

public sealed record MarketSimulationStatusContract(
    bool IsRunning,
    decimal CurrentMid,
    decimal LastBid,
    decimal LastAsk,
    decimal LastSpread,
    string Session,
    DateTimeOffset? StartedUtc,
    long TickCount,
    int IntervalSeconds,
    decimal VolatilityUsd,
    decimal BaseSpread,
    bool EnableShockEvents,
    string SourceTag);
