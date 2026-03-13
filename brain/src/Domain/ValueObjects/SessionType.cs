namespace Brain.Domain.ValueObjects;

public readonly record struct SessionType
{
    public static readonly SessionType Japan = new("Japan");
    public static readonly SessionType India = new("India");
    public static readonly SessionType London = new("London");
    public static readonly SessionType NewYork = new("NewYork");
    public static readonly SessionType LateNewYork = new("LateNewYork");
    public static readonly SessionType OffHours = new("OffHours");

    public string Value { get; }

    public SessionType(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        Value = normalized switch
        {
            "JAPAN" => "Japan",
            "INDIA" => "India",
            "LONDON" => "London",
            "NY" => "NewYork",
            "NEWYORK" => "NewYork",
            "NEW_YORK" => "NewYork",
            "LATE_NY" => "LateNewYork",
            "LATENEWYORK" => "LateNewYork",
            "ASIA" => "Japan",
            "ASIAN" => "Japan",
            "EUROPE" => "London",
            "OFFHOURS" => "OffHours",
            "OFF_HOURS" => "OffHours",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(Value))
        {
            throw new ArgumentException("Invalid session type.", nameof(value));
        }
    }

    public override string ToString() => Value;
}
