using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class TelegramSignal : BaseEntity<Guid>
{
    private TelegramSignal()
    {
    }

    public string ChannelKey { get; private set; } = string.Empty;
    public string Direction { get; private set; } = "UNKNOWN";
    public decimal Confidence { get; private set; }
    public string ConsensusState { get; private set; } = "QUIET";
    public bool PanicSuspected { get; private set; }
    public DateTimeOffset ServerTimeUtc { get; private set; }
    public string RawMessage { get; private set; } = string.Empty;
    public string OutcomeTag { get; private set; } = string.Empty;

    public static TelegramSignal Create(
        string channelKey,
        string direction,
        decimal confidence,
        string consensusState,
        bool panicSuspected,
        DateTimeOffset serverTimeUtc,
        string rawMessage)
    {
        return new TelegramSignal
        {
            Id = Guid.NewGuid(),
            ChannelKey = channelKey.Trim().ToLowerInvariant(),
            Direction = string.IsNullOrWhiteSpace(direction) ? "UNKNOWN" : direction.Trim().ToUpperInvariant(),
            Confidence = Math.Clamp(confidence, 0m, 1m),
            ConsensusState = string.IsNullOrWhiteSpace(consensusState) ? "QUIET" : consensusState.Trim().ToUpperInvariant(),
            PanicSuspected = panicSuspected,
            ServerTimeUtc = serverTimeUtc,
            RawMessage = rawMessage,
        };
    }

    public void MarkOutcome(string outcomeTag) => OutcomeTag = outcomeTag.Trim().ToUpperInvariant();
}
