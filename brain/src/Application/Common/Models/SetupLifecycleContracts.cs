namespace Brain.Application.Common.Models;

/// <summary>
/// Spec v9/v10/v11 §1 — Setup lifecycle states.
/// Full candidate lifecycle: NONE → FORMING → CANDIDATE → ARMED → PENDING_PLANTED → FILLED | PASSED | INVALIDATED.
/// Re-qualification: OVEREXTENDED/PASSED can transition to REQUALIFIED then ARMED.
/// </summary>
public static class SetupLifecycleState
{
    /// <summary>No candidate exists.</summary>
    public const string None = "NONE";

    /// <summary>Structure is starting to form but not yet fully valid.</summary>
    public const string Forming = "FORMING";

    /// <summary>Base-opportunity window detected; candidate forming before full confirmation (spec v11 §7).</summary>
    public const string Candidate = "CANDIDATE";

    /// <summary>Structure is fully valid; pending order should be planted immediately.</summary>
    public const string Armed = "ARMED";

    /// <summary>Pending order was successfully placed into MT5 or approvals queue (spec v11: PENDING_PLANTED).</summary>
    public const string OrderPlanted = "ORDER_PLANTED";

    /// <summary>Alias for dashboard/spec v11 — same as OrderPlanted.</summary>
    public const string PendingPlanted = "PENDING_PLANTED";

    /// <summary>Order triggered and position opened.</summary>
    public const string Filled = "FILLED";

    /// <summary>
    /// Setup was armed but price expanded before the order could be planted — entry window missed.
    /// (Spec v11: PASSED.)
    /// </summary>
    public const string PassedOverextended = "PASSED_OVEREXTENDED";

    /// <summary>Alias for spec v11 — same as PassedOverextended.</summary>
    public const string Passed = "PASSED";

    /// <summary>Price overextended above base; waiting for pullback (spec v11 re-qualification source).</summary>
    public const string Overextended = "OVEREXTENDED";

    /// <summary>
    /// Previously overextended/passed setup cooled off and re-qualified; BUY_LIMIT may be re-armed (spec v11 §8).
    /// </summary>
    public const string Requalified = "REQUALIFIED";

    /// <summary>
    /// Setup was invalidated due to a safety condition change:
    /// waterfall, active hazard window, spread explosion, or structural shift.
    /// </summary>
    public const string Invalidated = "INVALIDATED";
}

/// <summary>
/// Spec v9/v10/v11 §2 — Armed candidate record.
/// Stored when structure becomes valid; supports re-qualification and full lifecycle.
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
    DateTimeOffset? PlantedAt = null,
    /// <summary>Spec v11 — When state is REQUALIFIED, the prior state (e.g. PASSED_OVEREXTENDED).</summary>
    string? RequalifiedFrom = null)
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
