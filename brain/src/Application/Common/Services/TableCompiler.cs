using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// TABLE Compiler per spec/00_instructions and spec/12_table_compiler_spec.md
/// Position: after ANALYZE
/// Role: ONLY legal order builder
/// Hard law: TABLE is the only module allowed to create BUY_LIMIT/BUY_STOP orders
/// </summary>
public static class TableCompiler
{
    private const decimal MinimumProjectedMoveNetUsd = 8.0m;
    private const decimal StandardRotationMinUsd = 8.0m;
    private const decimal StandardRotationMaxUsd = 12.0m;
    private const decimal ImpulseHarvestMinUsd = 20.0m;

    public record TableResult(
        bool IsValid,
        string? OrderType,  // BUY_LIMIT, BUY_STOP, null if invalid
        decimal? EntryPrice,
        decimal? Tp1,
        decimal? Tp2,
        decimal? Tp3,
        decimal? StopLoss,
        decimal? Grams,
        DateTimeOffset? Expiry,
        string? Template,  // FLUSH_LIMIT_CAPTURE, IMPULSE_HARVEST_CAPTURE, STANDARD
        string? ReasonCode,
        string? RejectionReason
    );

    /// <summary>
    /// Compiles a legal order table. This is the ONLY module allowed to create orders.
    /// </summary>
    public static TableResult Compile(
        MarketSnapshotContract snapshot,
        AnalyzeResult analyzeResult,
        LedgerStateContract ledgerState,
        NewsEngineResult newsResult,
        CapitalUtilizationResult capitalResult,
        HistoricalPatternResult historicalResult,
        VerifyResult verifyResult,
        decimal minTradeGrams = 100m)
    {
        // Hard re-checks (TABLE must re-check everything)
        if (!RecheckSafety(snapshot, analyzeResult, newsResult))
        {
            return new TableResult(
                false,
                null, null, null, null, null, null, null, null, null,
                "SAFETY_BLOCK",
                "Hard safety blocker triggered"
            );
        }

        if (!RecheckCapitalLegality(ledgerState, capitalResult, minTradeGrams))
        {
            return new TableResult(
                false,
                null, null, null, null, null, null, null, null, null,
                "CAPITAL_BLOCK",
                "Capital legality failed"
            );
        }

        if (!RecheckStructure(analyzeResult))
        {
            return new TableResult(
                false,
                null, null, null, null, null, null, null, null, null,
                "STRUCTURE_BLOCK",
                "Structure validity failed"
            );
        }

        if (!RecheckRailPermissions(newsResult, analyzeResult))
        {
            return new TableResult(
                false,
                null, null, null, null, null, null, null, null, null,
                "RAIL_BLOCK",
                "Rail permissions do not allow this path"
            );
        }

        // Determine order type and template
        var (orderType, template) = DetermineOrderType(analyzeResult, newsResult, historicalResult);
        if (orderType == null)
        {
            return new TableResult(
                false,
                null, null, null, null, null, null, null, null, null,
                "NO_VALID_PATH",
                "No valid order type determined"
            );
        }

        // Calculate entry price
        var entryPrice = CalculateEntryPrice(snapshot, analyzeResult, orderType);
        if (!entryPrice.HasValue)
        {
            return new TableResult(
                false,
                null, null, null, null, null, null, null, null, null,
                "ENTRY_CALCULATION_FAILED",
                "Could not calculate valid entry price"
            );
        }

        // Calculate TP ladder
        var (tp1, tp2, tp3) = CalculateTpLadder(
            entryPrice.Value,
            analyzeResult,
            historicalResult,
            newsResult,
            snapshot);

        // Verify projected move meets minimum
        var projectedMoveNetUsd = CalculateProjectedMoveNetUsd(
            entryPrice.Value,
            tp1 ?? entryPrice.Value,
            snapshot.Spread);

        if (projectedMoveNetUsd < MinimumProjectedMoveNetUsd)
        {
            return new TableResult(
                false,
                null, null, null, null, null, null, null, null, null,
                "INSUFFICIENT_REWARD",
                $"Projected move {projectedMoveNetUsd:F2} USD is below minimum {MinimumProjectedMoveNetUsd} USD"
            );
        }

        // Calculate grams
        var grams = CalculateGrams(ledgerState, capitalResult, minTradeGrams);
        if (grams < minTradeGrams)
        {
            return new TableResult(
                false,
                null, null, null, null, null, null, null, null, null,
                "INSUFFICIENT_GRAMS",
                $"Calculated grams {grams:F2} is below minimum {minTradeGrams}"
            );
        }

        // Calculate expiry
        var expiry = CalculateExpiry(snapshot, newsResult, analyzeResult);

        // Calculate stop loss
        var stopLoss = CalculateStopLoss(entryPrice.Value, analyzeResult, snapshot);

        return new TableResult(
            true,
            orderType,
            entryPrice,
            tp1,
            tp2,
            tp3,
            stopLoss,
            grams,
            expiry,
            template,
            "TABLE_COMPILED",
            null
        );
    }

    private static bool RecheckSafety(
        MarketSnapshotContract snapshot,
        AnalyzeResult analyzeResult,
        NewsEngineResult newsResult)
    {
        // Re-check FAIL safety
        if (analyzeResult.FailThreatened || analyzeResult.FailBroken)
        {
            return false;
        }

        // Re-check waterfall risk
        if (newsResult.WaterfallRisk == "HIGH" || analyzeResult.WaterfallRisk == "HIGH")
        {
            return false;
        }

        // Re-check hazard timing
        if (newsResult.HazardWindowActive && newsResult.NextTier1UltraWithin45m)
        {
            return false;
        }

        // Re-check spread
        if (snapshot.Spread > snapshot.SpreadMax60m * 1.5m)
        {
            return false;
        }

        return true;
    }

    private static bool RecheckCapitalLegality(
        LedgerStateContract ledgerState,
        CapitalUtilizationResult capitalResult,
        decimal minTradeGrams)
    {
        if (!capitalResult.AffordableFlag)
        {
            return false;
        }

        if (capitalResult.ExposureState == "MAXED")
        {
            return false;
        }

        if (capitalResult.SlotCount >= 2)
        {
            return false;
        }

        return true;
    }

    private static bool RecheckStructure(AnalyzeResult analyzeResult)
    {
        if (analyzeResult.BottomType == "INVALID")
        {
            return false;
        }

        if (analyzeResult.PatternType == "WATERFALL_CONTINUATION")
        {
            return false;
        }

        if (analyzeResult.MidAirStatus == "MID_AIR_FAIL")
        {
            return false;
        }

        return true;
    }

    private static bool RecheckRailPermissions(
        NewsEngineResult newsResult,
        AnalyzeResult analyzeResult)
    {
        var requestedRail = analyzeResult.PrimaryTradeConcept == "FLUSH_LIMIT_CAPTURE" ? "A" : "B";

        if (requestedRail == "A")
        {
            if (newsResult.RailAPermission == "NO")
            {
                return false;
            }
            if (newsResult.RailAPermission == "ONLY_AFTER_STRUCTURE" && !analyzeResult.StructureValid)
            {
                return false;
            }
        }
        else if (requestedRail == "B")
        {
            if (newsResult.RailBPermission == "NO")
            {
                return false;
            }
            if (newsResult.RailBPermission == "STRICT" && analyzeResult.ConfidenceScore < 0.75m)
            {
                return false;
            }
        }

        return true;
    }

    private static (string? OrderType, string? Template) DetermineOrderType(
        AnalyzeResult analyzeResult,
        NewsEngineResult newsResult,
        HistoricalPatternResult historicalResult)
    {
        // FLUSH_LIMIT_CAPTURE template
        if (analyzeResult.PrimaryTradeConcept == "FLUSH_LIMIT_CAPTURE" ||
            (analyzeResult.BottomType != "INVALID" &&
             analyzeResult.PatternType == "FLUSH_REVERSAL_ATTEMPT" &&
             newsResult.WaterfallRisk != "HIGH" &&
             analyzeResult.FailProtected))
        {
            return ("BUY_LIMIT", "FLUSH_LIMIT_CAPTURE");
        }

        // IMPULSE_HARVEST_CAPTURE template
        if (analyzeResult.ImpulseHarvestScore > 0.7m &&
            historicalResult.HistoricalContinuationScore > 0.7m &&
            historicalResult.HistoricalExtensionBandUsd >= ImpulseHarvestMinUsd &&
            newsResult.WaterfallRisk != "HIGH")
        {
            if (analyzeResult.PrimaryTradeConcept == "IMPULSE_BREAKOUT_CAPTURE" ||
                analyzeResult.PrimaryTradeConcept == "IMPULSE_CONTINUATION_REBUILD")
            {
                return ("BUY_STOP", "IMPULSE_HARVEST_CAPTURE");
            }
        }

        // Standard templates
        if (analyzeResult.PrimaryTradeConcept == "SHELF_RECLAIM" ||
            analyzeResult.PrimaryTradeConcept == "BASE_RETEST")
        {
            return ("BUY_LIMIT", "STANDARD");
        }

        if (analyzeResult.PrimaryTradeConcept == "LID_BREAKOUT" ||
            analyzeResult.PrimaryTradeConcept == "COMPRESSION_BREAKOUT")
        {
            return ("BUY_STOP", "STANDARD");
        }

        return (null, null);
    }

    private static decimal? CalculateEntryPrice(
        MarketSnapshotContract snapshot,
        AnalyzeResult analyzeResult,
        string orderType)
    {
        if (orderType == "BUY_LIMIT")
        {
            // For flush captures, entry in upper half of deep zone
            if (analyzeResult.S2.HasValue)
            {
                var zoneRange = analyzeResult.S2.Value - (analyzeResult.S3 ?? analyzeResult.S2.Value * 0.998m);
                return analyzeResult.S2.Value - (zoneRange * 0.3m);  // Upper 30% of zone
            }
            if (analyzeResult.S1.HasValue)
            {
                return analyzeResult.S1.Value * 0.9995m;  // Slightly below S1
            }
        }
        else if (orderType == "BUY_STOP")
        {
            // For breakouts, entry above lid/compression
            if (analyzeResult.R1.HasValue)
            {
                return analyzeResult.R1.Value * 1.0005m;  // Slightly above R1
            }
            if (analyzeResult.LidPrice.HasValue)
            {
                return analyzeResult.LidPrice.Value * 1.0005m;
            }
        }

        return null;
    }

    private static (decimal? Tp1, decimal? Tp2, decimal? Tp3) CalculateTpLadder(
        decimal entryPrice,
        AnalyzeResult analyzeResult,
        HistoricalPatternResult historicalResult,
        NewsEngineResult newsResult,
        MarketSnapshotContract snapshot)
    {
        decimal? tp1 = null;
        decimal? tp2 = null;
        decimal? tp3 = null;

        // Determine if impulse harvest mode is allowed
        var impulseHarvestAllowed = analyzeResult.ImpulseHarvestScore > 0.7m &&
                                    historicalResult.HistoricalContinuationScore > 0.7m &&
                                    historicalResult.HistoricalExtensionBandUsd >= ImpulseHarvestMinUsd &&
                                    newsResult.WaterfallRisk != "HIGH";

        if (impulseHarvestAllowed)
        {
            // Impulse harvest ladder
            tp1 = entryPrice + StandardRotationMinUsd;
            tp2 = entryPrice + 20.0m;
            if (historicalResult.HistoricalExtensionBandUsd >= 30.0m)
            {
                tp3 = entryPrice + 30.0m;
            }
            if (historicalResult.HistoricalExtensionBandUsd >= 50.0m)
            {
                tp3 = entryPrice + 50.0m;
            }
        }
        else
        {
            // Standard rotation ladder
            tp1 = entryPrice + StandardRotationMinUsd;
            tp2 = entryPrice + StandardRotationMaxUsd;
        }

        return (tp1, tp2, tp3);
    }

    private static decimal CalculateProjectedMoveNetUsd(
        decimal entryPrice,
        decimal tpPrice,
        decimal spread)
    {
        // Calculate gross move
        var grossMove = Math.Abs(tpPrice - entryPrice);

        // Subtract spread (half on entry, half on exit)
        var netMove = grossMove - spread;

        // Subtract bullion handicap (0.80 USD per oz = ~0.026 USD per gram)
        // For 100g trade: ~2.6 USD handicap
        const decimal BullionHandicapPerGram = 0.026m;
        const decimal TypicalTradeGrams = 100m;
        var bullionHandicap = BullionHandicapPerGram * TypicalTradeGrams;

        return netMove - bullionHandicap;
    }

    private static decimal CalculateGrams(
        LedgerStateContract ledgerState,
        CapitalUtilizationResult capitalResult,
        decimal minTradeGrams)
    {
        // Use capital utilization result
        if (capitalResult.RecommendedGrams.HasValue)
        {
            return Math.Max(minTradeGrams, capitalResult.RecommendedGrams.Value);
        }

        // Fallback calculation
        var deployableAed = ledgerState.DeployableAed;
        var currentPrice = ledgerState.CurrentGoldPriceUsd;
        if (currentPrice <= 0)
        {
            return minTradeGrams;
        }

        // Convert AED to USD (1 AED = 0.272 USD)
        var deployableUsd = deployableAed * 0.272m;
        var maxGrams = deployableUsd / currentPrice;

        // Use 30% of deployable for safety
        var recommendedGrams = maxGrams * 0.3m;

        return Math.Max(minTradeGrams, recommendedGrams);
    }

    private static DateTimeOffset CalculateExpiry(
        MarketSnapshotContract snapshot,
        NewsEngineResult newsResult,
        AnalyzeResult analyzeResult)
    {
        var baseExpiryMinutes = 90;  // Default 90 minutes

        // Adjust based on session
        if (snapshot.Session == "JAPAN")
        {
            baseExpiryMinutes = 120;  // Wider for Japan
        }
        else if (snapshot.Session == "LONDON")
        {
            baseExpiryMinutes = 60;  // Tighter for London
        }
        else if (snapshot.Session == "NEW_YORK")
        {
            baseExpiryMinutes = 45;  // Tightest for NY
        }

        // Adjust based on hazard window
        if (newsResult.HazardWindowActive)
        {
            baseExpiryMinutes = Math.Min(baseExpiryMinutes, newsResult.MinutesToNextCleanWindow);
        }

        // Adjust based on session phase
        if (snapshot.SessionPhase == "END")
        {
            baseExpiryMinutes = Math.Min(baseExpiryMinutes, 60);
        }

        return snapshot.Timestamp.AddMinutes(baseExpiryMinutes);
    }

    private static decimal? CalculateStopLoss(
        decimal entryPrice,
        AnalyzeResult analyzeResult,
        MarketSnapshotContract snapshot)
    {
        // SL should be below FAIL or below S2/S3
        if (analyzeResult.FailPrice.HasValue)
        {
            return analyzeResult.FailPrice.Value * 0.999m;  // Slightly below FAIL
        }
        if (analyzeResult.S3.HasValue)
        {
            return analyzeResult.S3.Value * 0.999m;
        }
        if (analyzeResult.S2.HasValue)
        {
            return analyzeResult.S2.Value * 0.998m;  // 0.2% below S2
        }

        // Fallback: 1% below entry
        return entryPrice * 0.99m;
    }
}

// Supporting contracts
public record AnalyzeResult(
    string Regime,
    string WaterfallRisk,
    string MidAirStatus,
    string RailAStatus,
    string RailBStatus,
    string? RailAReason,
    string? RailBReason,
    decimal? S1,
    decimal? S2,
    decimal? S3,
    decimal? R1,
    decimal? R2,
    decimal? FailPrice,
    bool FailThreatened,
    bool FailBroken,
    bool FailProtected,
    bool StructureValid,
    string? CurrentSessionAnchor,
    string? NextSessionAnchor,
    string? NearestMagnet,
    string PrimaryTradeConcept,
    string RotationEnvelope,
    string? TriggerObject,
    string BottomType,  // CLASSIC_RECLAIM_BOTTOM, FLUSH_ABSORPTION_BOTTOM, PANIC_TO_REBUILD_BOTTOM, INVALID
    string PatternType,  // WATERFALL_CONTINUATION, FLUSH_REVERSAL_ATTEMPT
    decimal ImpulseHarvestScore,
    decimal SessionHistoricalModifier,
    decimal ConfidenceScore,
    decimal? LidPrice);

public record CapitalUtilizationResult(
    decimal? RecommendedGrams,
    string SizeState,  // MINIMUM, STANDARD, LARGE
    string ExposureState,  // SAFE, MODERATE, MAXED
    bool AffordableFlag,
    int SlotCount,
    decimal C1Available,
    decimal C2Available);