using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class DecisionLog : BaseEntity<Guid>
{
    private const string DefaultSymbol = "XAUUSD.gram";

    private DecisionLog()
    {
    }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string Symbol { get; private set; } = DefaultSymbol;
    public string Status { get; private set; } = string.Empty;
    public string EngineState { get; private set; } = string.Empty;
    public string Mode { get; private set; } = string.Empty;
    public string Cause { get; private set; } = string.Empty;
    public string WaterfallRisk { get; private set; } = string.Empty;
    public string TelegramState { get; private set; } = string.Empty;
    public string RailPermissionA { get; private set; } = string.Empty;
    public string RailPermissionB { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public decimal Entry { get; private set; }
    public decimal Tp { get; private set; }
    public decimal Grams { get; private set; }
    public int RotationCapThisSession { get; private set; }
    public bool ForceWhereToTrade { get; private set; }
    public string SnapshotHash { get; private set; } = string.Empty;

    public static DecisionLog Create(
        string symbol,
        string status,
        string engineState,
        string mode,
        string cause,
        string waterfallRisk,
        string telegramState,
        string railPermissionA,
        string railPermissionB,
        string reason,
        decimal entry,
        decimal tp,
        decimal grams,
        int rotationCapThisSession,
        bool forceWhereToTrade,
        string snapshotHash)
    {
        return new DecisionLog
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Symbol = string.IsNullOrWhiteSpace(symbol) ? DefaultSymbol : symbol.Trim(),
            Status = status,
            EngineState = engineState,
            Mode = mode,
            Cause = cause,
            WaterfallRisk = waterfallRisk,
            TelegramState = telegramState,
            RailPermissionA = railPermissionA,
            RailPermissionB = railPermissionB,
            Reason = reason,
            Entry = entry,
            Tp = tp,
            Grams = grams,
            RotationCapThisSession = rotationCapThisSession,
            ForceWhereToTrade = forceWhereToTrade,
            SnapshotHash = snapshotHash,
        };
    }
}
