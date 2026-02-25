using Brain.Domain.Common;
using Brain.Domain.ValueObjects;

namespace Brain.Domain.Entities;

public sealed class RiskProfile : BaseEntity<RiskProfileId>
{
    private RiskProfile()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public RiskLevel Level { get; private set; }
    public decimal MaxDrawdownPercent { get; private set; }
    public bool IsActive { get; private set; }

    public static RiskProfile Create(string name, RiskLevel level, decimal maxDrawdownPercent, bool isActive = false)
    {
        return new RiskProfile
        {
            Id = RiskProfileId.New(),
            Name = name.Trim(),
            Level = level,
            MaxDrawdownPercent = maxDrawdownPercent,
            IsActive = isActive
        };
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
