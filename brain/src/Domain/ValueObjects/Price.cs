namespace Brain.Domain.ValueObjects;

public readonly record struct Price
{
    public decimal Value { get; }

    public Price(decimal value)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Price must be positive.");
        }

        Value = decimal.Round(value, 5, MidpointRounding.AwayFromZero);
    }

    public static implicit operator decimal(Price price) => price.Value;
    public override string ToString() => Value.ToString("0.#####");
}
