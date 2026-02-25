namespace Brain.Domain.ValueObjects;

public readonly record struct RailType
{
    public static readonly RailType BuyLimit = new("BUY_LIMIT");
    public static readonly RailType BuyStop = new("BUY_STOP");

    public string Value { get; }

    public RailType(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized is not ("BUY_LIMIT" or "BUY_STOP"))
        {
            throw new ArgumentException("RailType must be BUY_LIMIT or BUY_STOP.", nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}
