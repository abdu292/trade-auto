namespace Brain.Domain.ValueObjects;

public readonly record struct RiskLevel
{
    public static readonly RiskLevel Low = new("Low");
    public static readonly RiskLevel Medium = new("Medium");
    public static readonly RiskLevel High = new("High");

    public string Value { get; }

    public RiskLevel(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized is not ("Low" or "Medium" or "High"))
        {
            throw new ArgumentException("RiskLevel must be Low, Medium, or High.", nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}
