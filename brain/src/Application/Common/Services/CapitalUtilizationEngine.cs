using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 9: CAPITAL UTILIZATION ENGINE
/// Purpose: Enforces ledger truth, C1/C2, capacity clamp, slot discipline, sizeState, exposureState, affordableFlag.
/// 
/// Hard law:
/// - no ARMED candidate if capital is illegal
/// - TABLE must reject unaffordable trades
/// </summary>
public static class CapitalUtilizationEngine
{
    public static CapitalUtilizationEngineResult Process(
        MarketSnapshotContract snapshot,
        LedgerStateContract? ledgerState,
        IndicatorEngineResult indicators)
    {
        if (ledgerState == null)
        {
            return new CapitalUtilizationEngineResult(
                C1Capacity: 0m,
                C2Capacity: 0m,
                CapacityClamp: false,
                SizeState: "ZERO",
                ExposureState: "BLOCKED",
                AffordableFlag: false,
                MaxAffordableGrams: 0m);
        }
        
        // C1/C2 capacity (C1 = 80%, C2 = 20% of deployable)
        var c1Capacity = ledgerState.BucketC1Aed > 0m 
            ? ledgerState.BucketC1Aed 
            : ledgerState.DeployableCashAed * 0.80m;
        var c2Capacity = ledgerState.BucketC2Aed > 0m 
            ? ledgerState.BucketC2Aed 
            : ledgerState.DeployableCashAed * 0.20m;
        
        // Capacity clamp check
        var currentPrice = snapshot.AuthoritativeRate > 0m 
            ? snapshot.AuthoritativeRate 
            : snapshot.Bid > 0m ? snapshot.Bid : 0m;
        
        var capacityClamp = currentPrice > 0m && c1Capacity > 0m;
        
        // Size state
        var sizeState = DetermineSizeState(ledgerState, c1Capacity, currentPrice);
        
        // Exposure state
        var exposureState = DetermineExposureState(ledgerState);
        
        // Affordable flag and max grams
        var (affordableFlag, maxGrams) = CheckAffordability(
            ledgerState, 
            c1Capacity, 
            currentPrice);
        
        return new CapitalUtilizationEngineResult(
            C1Capacity: c1Capacity,
            C2Capacity: c2Capacity,
            CapacityClamp: capacityClamp,
            SizeState: sizeState,
            ExposureState: exposureState,
            AffordableFlag: affordableFlag,
            MaxAffordableGrams: maxGrams);
    }

    private static string DetermineSizeState(
        LedgerStateContract ledgerState,
        decimal c1Capacity,
        decimal currentPrice)
    {
        if (currentPrice <= 0m || c1Capacity <= 0m)
        {
            return "ZERO";
        }
        
        // Check if we can afford minimum trade (100g)
        var minGrams = 100m;
        var result = CapitalUtilizationService.Check(c1Capacity, currentPrice, minGrams);
        
        if (!result.ApprovedByCapacityGate)
        {
            return "ZERO";
        }
        
        // Check exposure level
        if (ledgerState.OpenExposurePercent >= 65m)
        {
            return "ZERO";
        }
        
        if (ledgerState.OpenExposurePercent >= 45m)
        {
            return "REDUCED";
        }
        
        // Check capacity level
        if (result.MaxLegalGrams >= minGrams * 2m)
        {
            return "FULL";
        }
        
        if (result.MaxLegalGrams >= minGrams)
        {
            return "MICRO";
        }
        
        return "ZERO";
    }

    private static string DetermineExposureState(LedgerStateContract ledgerState)
    {
        if (ledgerState.OpenExposurePercent >= 65m)
        {
            return "BLOCKED";
        }
        
        if (ledgerState.OpenExposurePercent >= 45m)
        {
            return "CAUTION";
        }
        
        return "SAFE";
    }

    private static (bool AffordableFlag, decimal MaxGrams) CheckAffordability(
        LedgerStateContract ledgerState,
        decimal c1Capacity,
        decimal currentPrice)
    {
        if (currentPrice <= 0m || c1Capacity <= 0m)
        {
            return (false, 0m);
        }
        
        var minGrams = 100m;
        var result = CapitalUtilizationService.Check(c1Capacity, currentPrice, minGrams);
        
        return (result.ApprovedByCapacityGate, result.MaxLegalGrams);
    }
}

/// <summary>
/// Capital Utilization Engine output contract
/// </summary>
public sealed record CapitalUtilizationEngineResult(
    decimal C1Capacity,
    decimal C2Capacity,
    bool CapacityClamp,
    string SizeState,
    string ExposureState,
    bool AffordableFlag,
    decimal MaxAffordableGrams);
