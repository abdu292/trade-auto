using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 6: HARD LEGALITY ENGINE
/// Purpose: Enforces exposure law, slots law, ledger legality, pre-block windows, hazard veto, affordability.
/// If legality fails, the path ends there.
/// </summary>
public static class HardLegalityEngine
{
    public static HardLegalityEngineResult Check(
        MarketSnapshotContract snapshot,
        LedgerStateContract? ledgerState,
        WaterfallCrisisEngineResult waterfall,
        StructureEngineResult structure,
        bool hazardWindowBlocked)
    {
        var checks = new List<LegalityCheck>();
        
        // 1. Exposure law (max 65% open exposure)
        var exposureCheck = CheckExposure(ledgerState);
        checks.Add(exposureCheck);
        
        // 2. Slots law (max 2 logical slots)
        var slotsCheck = CheckSlots(ledgerState);
        checks.Add(slotsCheck);
        
        // 3. Ledger legality (deployable cash > 0, no negative balances)
        var ledgerCheck = CheckLedgerLegality(ledgerState);
        checks.Add(ledgerCheck);
        
        // 4. Pre-block windows (hazard windows, news events)
        var preBlockCheck = CheckPreBlocks(snapshot, hazardWindowBlocked);
        checks.Add(preBlockCheck);
        
        // 5. Hazard veto (fatal hazard conditions)
        var hazardCheck = CheckHazardVeto(snapshot, waterfall, structure, hazardWindowBlocked);
        checks.Add(hazardCheck);
        
        // 6. Affordability (capacity clamp)
        var affordabilityCheck = CheckAffordability(snapshot, ledgerState);
        checks.Add(affordabilityCheck);
        
        // Aggregate result
        var allPassed = checks.All(c => c.Passed);
        var blockingChecks = checks.Where(c => !c.Passed).ToList();
        
        return new HardLegalityEngineResult(
            IsLegal: allPassed,
            BlockingChecks: blockingChecks,
            ExposureCheck: exposureCheck,
            SlotsCheck: slotsCheck,
            LedgerCheck: ledgerCheck,
            PreBlockCheck: preBlockCheck,
            HazardCheck: hazardCheck,
            AffordabilityCheck: affordabilityCheck);
    }

    private static LegalityCheck CheckExposure(LedgerStateContract? ledgerState)
    {
        if (ledgerState == null)
        {
            return new LegalityCheck("EXPOSURE", false, "Ledger state not available");
        }
        
        // Max 65% open exposure
        if (ledgerState.OpenExposurePercent >= 65m)
        {
            return new LegalityCheck("EXPOSURE", false, 
                $"Exposure {ledgerState.OpenExposurePercent:0.##}% exceeds 65% limit");
        }
        
        return new LegalityCheck("EXPOSURE", true, 
            $"Exposure {ledgerState.OpenExposurePercent:0.##}% within limit");
    }

    private static LegalityCheck CheckSlots(LedgerStateContract? ledgerState)
    {
        if (ledgerState == null)
        {
            return new LegalityCheck("SLOTS", false, "Ledger state not available");
        }
        
        // Max 2 logical slots (open + pending positions)
        if (ledgerState.OpenBuyCount >= 2)
        {
            return new LegalityCheck("SLOTS", false, 
                $"Open positions {ledgerState.OpenBuyCount} exceeds 2 slot limit");
        }
        
        return new LegalityCheck("SLOTS", true, 
            $"Open positions {ledgerState.OpenBuyCount} within 2 slot limit");
    }

    private static LegalityCheck CheckLedgerLegality(LedgerStateContract? ledgerState)
    {
        if (ledgerState == null)
        {
            return new LegalityCheck("LEDGER", false, "Ledger state not available");
        }
        
        // Deployable cash must be > 0
        if (ledgerState.DeployableCashAed <= 0m)
        {
            return new LegalityCheck("LEDGER", false, 
                $"Deployable cash {ledgerState.DeployableCashAed:0.##} AED is zero or negative");
        }
        
        // Cash balance must be >= 0 (no negative balances)
        if (ledgerState.CashAed < 0m)
        {
            return new LegalityCheck("LEDGER", false, 
                $"Cash balance {ledgerState.CashAed:0.##} AED is negative");
        }
        
        return new LegalityCheck("LEDGER", true, 
            $"Ledger legal: Deployable={ledgerState.DeployableCashAed:0.##} AED, Cash={ledgerState.CashAed:0.##} AED");
    }

    private static LegalityCheck CheckPreBlocks(MarketSnapshotContract snapshot, bool hazardWindowBlocked)
    {
        // Pre-block windows: hazard windows, news events
        if (hazardWindowBlocked)
        {
            return new LegalityCheck("PRE_BLOCK", false, "Hazard window blocked");
        }
        
        if (snapshot.IsUsRiskWindow && snapshot.NewsEventFlag)
        {
            return new LegalityCheck("PRE_BLOCK", false, "US risk window with news event");
        }
        
        return new LegalityCheck("PRE_BLOCK", true, "No pre-block windows active");
    }

    private static LegalityCheck CheckHazardVeto(
        MarketSnapshotContract snapshot,
        WaterfallCrisisEngineResult waterfall,
        StructureEngineResult structure,
        bool hazardWindowBlocked)
    {
        // Hazard veto conditions
        if (hazardWindowBlocked)
        {
            return new LegalityCheck("HAZARD", false, "Fatal hazard window active");
        }
        
        if (waterfall.CrisisVeto)
        {
            return new LegalityCheck("HAZARD", false, $"Crisis veto: {waterfall.CrisisReason}");
        }
        
        if (waterfall.WaterfallRisk == "HIGH")
        {
            return new LegalityCheck("HAZARD", false, "High waterfall risk");
        }
        
        if (structure.Fail.HasValue)
        {
            return new LegalityCheck("HAZARD", false, "FAIL threatened or broken");
        }
        
        if (snapshot.Spread >= 0.7m) // Spread block threshold
        {
            return new LegalityCheck("HAZARD", false, $"Spread {snapshot.Spread:0.000} exceeds 0.7 block threshold");
        }
        
        return new LegalityCheck("HAZARD", true, "No hazard veto conditions");
    }

    private static LegalityCheck CheckAffordability(
        MarketSnapshotContract snapshot,
        LedgerStateContract? ledgerState)
    {
        if (ledgerState == null)
        {
            return new LegalityCheck("AFFORDABILITY", false, "Ledger state not available");
        }
        
        var currentPrice = snapshot.AuthoritativeRate > 0m 
            ? snapshot.AuthoritativeRate 
            : snapshot.Bid > 0m ? snapshot.Bid : 0m;
        
        if (currentPrice <= 0m)
        {
            return new LegalityCheck("AFFORDABILITY", false, "Invalid current price");
        }
        
        // Check if we can afford minimum trade (100g default, but configurable)
        var minGrams = 100m; // Configurable minimum
        var result = CapitalUtilizationService.Check(
            ledgerState.DeployableCashAed,
            currentPrice,
            minGrams);
        
        if (!result.ApprovedByCapacityGate)
        {
            return new LegalityCheck("AFFORDABILITY", false, 
                $"Cannot afford minimum {minGrams}g trade. MaxLegalGrams={result.MaxLegalGrams:0.##}g");
        }
        
        return new LegalityCheck("AFFORDABILITY", true, 
            $"Affordable: MaxLegalGrams={result.MaxLegalGrams:0.##}g");
    }
}

/// <summary>
/// Hard Legality Engine output contract
/// </summary>
public sealed record HardLegalityEngineResult(
    bool IsLegal,
    IReadOnlyCollection<LegalityCheck> BlockingChecks,
    LegalityCheck ExposureCheck,
    LegalityCheck SlotsCheck,
    LegalityCheck LedgerCheck,
    LegalityCheck PreBlockCheck,
    LegalityCheck HazardCheck,
    LegalityCheck AffordabilityCheck);

/// <summary>
/// Individual legality check result
/// </summary>
public sealed record LegalityCheck(
    string CheckType,
    bool Passed,
    string Reason);
