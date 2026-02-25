using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class StrategyProfile : BaseEntity<StrategyProfileId>
{
    private StrategyProfile()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public string Description { get; private set; } = string.Empty;

    public static StrategyProfile Create(string name, string description, bool isActive = false)
    {
        return new StrategyProfile
        {
            Id = StrategyProfileId.New(),
            Name = name.Trim(),
            Description = description.Trim(),
            IsActive = isActive
        };
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
