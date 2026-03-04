namespace Brain.Application.Common.Models;

public sealed record H1ContextResult(
    string Context,
    bool HasLiquiditySweep,
    bool HasReclaim,
    bool IsTrendAligned,
    string Reason);

public sealed record M15SetupResult(
    bool IsValid,
    bool IsCompression,
    bool HasBase,
    string Reason);

public sealed record M5EntryResult(
    bool IsValid,
    bool IsBreakout,
    bool IsMomentumShift,
    bool IsRetest,
    string Reason);

public sealed record SetupCandidateResult(
    bool IsValid,
    H1ContextResult H1Context,
    M15SetupResult? M15Setup,
    M5EntryResult? M5Entry,
    string AbortReason)
{
    public static SetupCandidateResult Valid(H1ContextResult h1, M15SetupResult m15, M5EntryResult m5)
        => new(true, h1, m15, m5, string.Empty);

    public static SetupCandidateResult Aborted(H1ContextResult h1, string reason)
        => new(false, h1, null, null, reason);

    public static SetupCandidateResult Aborted(H1ContextResult h1, M15SetupResult m15, string reason)
        => new(false, h1, m15, null, reason);
}

public sealed record ReplayCandle(
    string Symbol,
    string Timeframe,
    DateTimeOffset Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public sealed record ReplayStartRequest(
    string Symbol = "XAUUSD",
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int SpeedMultiplier = 100,
    bool UseAI = true,
    bool UseMockAI = false);

public sealed record ReplayStatusContract(
    bool IsRunning,
    bool IsPaused,
    string Symbol,
    int TotalCandles,
    int ProcessedCandles,
    int CyclesTriggered,
    int SetupCandidatesFound,
    int TradesArmed,
    DateTimeOffset? ReplayFrom,
    DateTimeOffset? ReplayTo,
    DateTimeOffset? StartedUtc,
    string DriverTimeframe);
