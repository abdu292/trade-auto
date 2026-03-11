using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v11 §9 — Pending Order Supervision. Every pending order must remain under supervision
/// until trigger, expiry, cancellation, or safe replacement. Every cycle we check whether
/// any pending should be cancelled (FAIL threatened, structure broken, waterfall, hazard, etc.).
/// </summary>
public static class PendingOrderSupervision
{
    /// <summary>
    /// Determine if any pending order should be cancelled this cycle based on current market/safety state.
    /// Returns (shouldCancelAll, reason). When true, caller should RequestCancelPending(reason) and clear queue.
    /// </summary>
    public static (bool ShouldCancel, string Reason) Supervise(
        IReadOnlyCollection<PendingTradeContract> pendingOrders,
        MarketSnapshotContract snapshot,
        string waterfallRisk,
        bool hazardWindowBlocked,
        string? regimeBlockedReason,
        decimal spreadBlockThreshold = 0.7m,
        decimal adrFailThresholdPct = 85m)
    {
        if (pendingOrders.Count == 0)
            return (false, string.Empty);

        // FAIL threatened
        if (snapshot.AdrUsedPct >= adrFailThresholdPct)
            return (true, "FAIL_THREATENED: ADR used too high.");

        // Structure / safety
        if (waterfallRisk == "HIGH")
            return (true, "WATERFALL_RISK_HIGH");
        if (hazardWindowBlocked)
            return (true, "HAZARD_WINDOW_ACTIVE");
        if (!string.IsNullOrEmpty(regimeBlockedReason))
            return (true, $"REGIME_BLOCKED: {regimeBlockedReason}");
        if (snapshot.Spread >= spreadBlockThreshold)
            return (true, "SPREAD_BLOWOUT");

        // Stale / session: optional checks per order (e.g. order expiry passed, session deteriorated)
        foreach (var order in pendingOrders)
        {
            if (order.Expiry < DateTimeOffset.UtcNow)
                return (true, "PENDING_EXPIRED_STALE");
        }

        return (false, string.Empty);
    }
}
