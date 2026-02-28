using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class MacroCacheState : BaseEntity<Guid>
{
    private MacroCacheState()
    {
    }

    public string MacroBias { get; private set; } = "UNKNOWN";
    public string InstitutionalBias { get; private set; } = "UNKNOWN";
    public string CbFlowFlag { get; private set; } = "UNKNOWN";
    public string PositioningFlag { get; private set; } = "UNKNOWN";
    public string Source { get; private set; } = "HEURISTIC";
    public DateTimeOffset LastRefreshedUtc { get; private set; } = DateTimeOffset.MinValue;

    public static MacroCacheState CreateDefault() =>
        new()
        {
            Id = Guid.NewGuid(),
            MacroBias = "UNKNOWN",
            InstitutionalBias = "UNKNOWN",
            CbFlowFlag = "UNKNOWN",
            PositioningFlag = "UNKNOWN",
            Source = "HEURISTIC",
            LastRefreshedUtc = DateTimeOffset.UtcNow,
        };

    public void Refresh(string macroBias, string institutionalBias, string cbFlowFlag, string positioningFlag, string source)
    {
        MacroBias = macroBias;
        InstitutionalBias = institutionalBias;
        CbFlowFlag = cbFlowFlag;
        PositioningFlag = positioningFlag;
        Source = source;
        LastRefreshedUtc = DateTimeOffset.UtcNow;
    }
}
