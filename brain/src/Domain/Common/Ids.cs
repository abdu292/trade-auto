namespace Brain.Domain.Common;

public readonly record struct TradeId(Guid Value)
{
    public static TradeId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct TradeSignalId(Guid Value)
{
    public static TradeSignalId New() => new(Guid.NewGuid());
}

public readonly record struct StrategyProfileId(Guid Value)
{
    public static StrategyProfileId New() => new(Guid.NewGuid());
}

public readonly record struct RiskProfileId(Guid Value)
{
    public static RiskProfileId New() => new(Guid.NewGuid());
}

public readonly record struct SessionStateId(Guid Value)
{
    public static SessionStateId New() => new(Guid.NewGuid());
}

public readonly record struct MarketSnapshotId(Guid Value)
{
    public static MarketSnapshotId New() => new(Guid.NewGuid());
}
