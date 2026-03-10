namespace Brain.Application.Common.Models;

/// <summary>
/// Spec v9/v10 §1 — Setup lifecycle states.
/// Tracks progression from structure detection through to order placement or invalidation.
/// </summary>
public static class SetupLifecycleState
{
    /// <summary>Structure is starting to form but not yet fully valid.</summary>
    public const string Forming = "FORMING";

    /// <summary>Structure is fully valid; pending order should be planted immediately.</summary>
    public const string Armed = "ARMED";

    /// <summary>Pending order was successfully placed into MT5 or approvals queue.</summary>
    public const string OrderPlanted = "ORDER_PLANTED";

    /// <summary>
    /// Setup was armed but price expanded before the order could be planted — entry window missed.
    /// Subsequent cycles that see OVEREXTENDED_ABOVE_BASE after an ARMED candidate was stored
    /// will be tagged with this state to distinguish "missed earlier window" from "no setup at all".
    /// </summary>
    public const string PassedOverextended = "PASSED_OVEREXTENDED";

    /// <summary>
    /// Setup was invalidated due to a safety condition change:
    /// waterfall, active hazard window, spread explosion, or structural shift.
    /// </summary>
    public const string Invalidated = "INVALIDATED";
}

/// <summary>
/// Spec v9/v10 §2 — Armed candidate record.
/// Stored when structure becomes valid so the engine can track timing gaps
/// between arming and order placement, and diagnose missed entry windows.
/// </summary>
public sealed record ArmedSetupCandidate(
    /// <summary>BUY_LIMIT or BUY_STOP per spec §4.</summary>
    string PathType,
    /// <summary>Structural base or reclaim level used as entry reference.</summary>
    decimal BaseLevel,
    /// <summary>Breakout lid or structural high used as invalidation reference.</summary>
    decimal LidLevel,
    /// <summary>Human-readable summary of the trigger condition (e.g. "M15_BASE_RECLAIM").</summary>
    string TriggerCondition,
    /// <summary>UTC time after which this candidate is considered expired and should be invalidated.</summary>
    DateTimeOffset ExpiryWindow,
    /// <summary>Condition that would invalidate this candidate (e.g. "WATERFALL_HIGH|HAZARD|SPREAD_BLOCK").</summary>
    string InvalidationCondition,
    /// <summary>UTC time the candidate was created.</summary>
    DateTimeOffset CreatedAt,
    /// <summary>Cycle ID that produced this candidate.</summary>
    string CycleId,
    /// <summary>Confidence score at the time of arming.</summary>
    int ConfidenceScore,
    /// <summary>Current lifecycle state.</summary>
    string LifecycleState = SetupLifecycleState.Armed,
    /// <summary>Reason for invalidation or overextension (null when still armed).</summary>
    string? InvalidationReason = null,
    /// <summary>UTC time the order was planted (null until ORDER_PLANTED).</summary>
    DateTimeOffset? PlantedAt = null)
{
    /// <summary>Returns true when the expiry window has passed.</summary>
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiryWindow;
}

/// <summary>
/// Spec v9/v10 §5 — Lifecycle diagnostic snapshot for monitoring and timeline logging.
/// </summary>
public sealed record SetupLifecycleStatusContract(
    string Symbol,
    string LifecycleState,
    string? PathType,
    decimal? BaseLevel,
    decimal? LidLevel,
    string? TriggerCondition,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiryWindow,
    string? InvalidationReason,
    DateTimeOffset? PlantedAt);
