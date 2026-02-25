namespace Brain.Domain.ValueObjects;

public readonly record struct SessionType
{
    public static readonly SessionType Asian = new("Asian");
    public static readonly SessionType London = new("London");
    public static readonly SessionType NewYork = new("NewYork");
    public static readonly SessionType OffHours = new("OffHours");

    public string Value { get; }

    public SessionType(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized is not ("Asian" or "London" or "NewYork" or "OffHours"))
        {
            throw new ArgumentException("Invalid session type.", nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}
