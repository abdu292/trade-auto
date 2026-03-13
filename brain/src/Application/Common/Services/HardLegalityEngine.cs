using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Hard Legality Engine per spec/00_instructions
/// Enforces: exposure law, slots law, ledger legality, pre-blocks, hazard veto
/// </summary>
public static class HardLegalityEngine
{
    public record LegalityResult(
        bool IsLegal,
        string? BlockReason,
        string? BlockCode
    );

    /// <summary>
    /// Checks all hard legality constraints.
    /// </summary>
    public static LegalityResult Check(
        MarketSnapshotContract snapshot,
        LedgerStateContract ledgerState,
        RegimeClassificationContract regime,
        string waterfallRisk)
    {
        // Exposure law: max 2 slots
        if (ledgerState.OpenPositionsCount >= 2)
        {
            return new LegalityResult(
                false,
                "Maximum exposure slots (2) reached",
                "EXPOSURE_MAXED");
        }

        // Slots law: check pending orders count
        var pendingCount = snapshot.PendingOrders?.Count ?? 0;
        if (pendingCount >= 2)
        {
            return new LegalityResult(
                false,
                "Maximum pending slots (2) reached",
                "SLOTS_MAXED");
        }

        // Ledger legality: check deployable capital
        if (ledgerState.DeployableAed <= 0)
        {
            return new LegalityResult(
                false,
                "No deployable capital available",
                "CAPITAL_INSUFFICIENT");
        }

        // Pre-blocks: check spread
        if (snapshot.Spread > snapshot.SpreadMax60m * 1.5m)
        {
            return new LegalityResult(
                false,
                $"Spread {snapshot.Spread:F4} exceeds maximum threshold",
                "SPREAD_BLOCK");
        }

        // Hazard veto: waterfall risk
        if (waterfallRisk == "HIGH")
        {
            return new LegalityResult(
                false,
                "Waterfall risk is HIGH",
                "WATERFALL_VETO");
        }

        // Hazard veto: regime blocked
        if (regime.IsBlocked)
        {
            return new LegalityResult(
                false,
                $"Regime {regime.RegimeTag} is blocked",
                "REGIME_BLOCK");
        }

        // Mid-air filter
        if (snapshot.FreezeGapDetected)
        {
            return new LegalityResult(
                false,
                "Freeze gap detected (mid-air risk)",
                "MID_AIR_BLOCK");
        }

        // All checks passed
        return new LegalityResult(true, null, null);
    }
}