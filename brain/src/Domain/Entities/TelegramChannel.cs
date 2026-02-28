using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class TelegramChannel : BaseEntity<Guid>
{
    private TelegramChannel()
    {
    }

    public string ChannelKey { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = "MIXED";
    public string ReliabilityFlags { get; private set; } = "unknown";
    public decimal Weight { get; private set; } = 1.0m;
    public decimal WinRateRolling { get; private set; }
    public decimal ImpactScore { get; private set; }
    public decimal ConflictScore { get; private set; }
    public DateTimeOffset LastActiveTimeUtc { get; private set; } = DateTimeOffset.MinValue;

    public static TelegramChannel Create(string channelKey, string name, string type, decimal weight = 1.0m)
    {
        return new TelegramChannel
        {
            Id = Guid.NewGuid(),
            ChannelKey = channelKey.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Type = string.IsNullOrWhiteSpace(type) ? "MIXED" : type.Trim().ToUpperInvariant(),
            Weight = weight <= 0 ? 1.0m : weight,
            LastActiveTimeUtc = DateTimeOffset.UtcNow,
        };
    }

    public void TouchActive() => LastActiveTimeUtc = DateTimeOffset.UtcNow;

    public void ApplyOutcome(string outcome)
    {
        var normalized = outcome.Trim().ToUpperInvariant();
        if (normalized == "GOOD")
        {
            Weight = Math.Min(2.5m, Weight + 0.03m);
            ImpactScore = Math.Min(1.0m, ImpactScore + 0.02m);
            ConflictScore = Math.Max(0m, ConflictScore - 0.01m);
        }
        else if (normalized is "HOLD" or "BAD_CONTEXT")
        {
            Weight = Math.Max(0.3m, Weight - 0.04m);
            ConflictScore = Math.Min(1.0m, ConflictScore + 0.02m);
        }

        TouchActive();
    }
}
