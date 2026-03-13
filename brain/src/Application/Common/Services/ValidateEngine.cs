using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// VALIDATE Engine per spec/00_instructions
/// Position: after TABLE
/// Role: scrub realism and coherence
/// May downgrade, resize, reject but never invent a trade
/// </summary>
public static class ValidateEngine
{
    public record ValidateResult(
        bool IsValid,
        string? OrderType,
        decimal? EntryPrice,
        decimal? Tp1,
        decimal? Tp2,
        decimal? Tp3,
        decimal? StopLoss,
        decimal? Grams,
        DateTimeOffset? Expiry,
        string? ReasonCode,
        string? RejectionReason,
        string? DowngradeReason
    );

    /// <summary>
    /// Validates TABLE output for realism and coherence.
    /// </summary>
    public static ValidateResult Validate(
        TableCompiler.TableResult tableResult,
        MarketSnapshotContract snapshot,
        NewsEngineResult newsResult,
        AnalyzeResult analyzeResult,
        HistoricalPatternResult historicalResult)
    {
        if (!tableResult.IsValid)
        {
            return new ValidateResult(
                false,
                null, null, null, null, null, null, null, null, null,
                "TABLE_INVALID",
                tableResult.RejectionReason,
                null
            );
        }

        // Check expiry realism
        if (tableResult.Expiry.HasValue)
        {
            var expiryMinutes = (tableResult.Expiry.Value - snapshot.Timestamp).TotalMinutes;
            
            // Expiry too short
            if (expiryMinutes < 30)
            {
                return new ValidateResult(
                    false,
                    null, null, null, null, null, null, null, null, null,
                    "EXPIRY_TOO_SHORT",
                    $"Expiry {expiryMinutes:F0} minutes is too short",
                    null
                );
            }

            // Expiry too long for session phase
            if (snapshot.SessionPhase == "END" && expiryMinutes > 90)
            {
                return new ValidateResult(
                    false,
                    tableResult.OrderType,
                    tableResult.EntryPrice,
                    tableResult.Tp1,
                    tableResult.Tp2,
                    tableResult.Tp3,
                    tableResult.StopLoss,
                    tableResult.Grams,
                    snapshot.Timestamp.AddMinutes(60),  // Downgrade expiry
                    "EXPIRY_DOWNGRADED",
                    null,
                    "Expiry too long for END phase, reduced to 60 minutes"
                );
            }
        }

        // Check size realism
        if (tableResult.Grams.HasValue)
        {
            var grams = tableResult.Grams.Value;
            
            // Size too large for current exposure
            if (grams > 500m)  // Arbitrary large size threshold
            {
                return new ValidateResult(
                    false,
                    tableResult.OrderType,
                    tableResult.EntryPrice,
                    tableResult.Tp1,
                    tableResult.Tp2,
                    tableResult.Tp3,
                    tableResult.StopLoss,
                    300m,  // Downgrade size
                    tableResult.Expiry,
                    "SIZE_DOWNGRADED",
                    null,
                    "Size too large, reduced to 300g"
                );
            }
        }

        // Check session-phase realism
        if (snapshot.SessionPhase == "START")
        {
            // START phase: structure forming, be cautious
            if (tableResult.Template == "IMPULSE_HARVEST_CAPTURE")
            {
                return new ValidateResult(
                    false,
                    tableResult.OrderType,
                    tableResult.EntryPrice,
                    tableResult.Tp1,
                    null,  // Remove TP3
                    null,
                    tableResult.StopLoss,
                    tableResult.Grams,
                    tableResult.Expiry,
                    "AGGRESSIVENESS_DOWNGRADED",
                    null,
                    "START phase: removed extended TP targets"
                );
            }
        }

        // Check aggressiveness vs confidence coherence
        if (tableResult.Template == "IMPULSE_HARVEST_CAPTURE")
        {
            if (analyzeResult.ConfidenceScore < 0.75m || historicalResult.HistoricalContinuationScore < 0.7m)
            {
                // Downgrade to standard rotation
                return new ValidateResult(
                    true,
                    tableResult.OrderType,
                    tableResult.EntryPrice,
                    tableResult.Tp1,
                    tableResult.Tp2,
                    null,  // Remove TP3
                    tableResult.StopLoss,
                    tableResult.Grams,
                    tableResult.Expiry,
                    "IMPULSE_DOWNGRADED",
                    null,
                    "Confidence/continuation score insufficient for impulse harvest, downgraded to standard rotation"
                );
            }
        }

        // Check net-edge realism
        if (tableResult.EntryPrice.HasValue && tableResult.Tp1.HasValue)
        {
            var netMove = Math.Abs(tableResult.Tp1.Value - tableResult.EntryPrice.Value) - snapshot.Spread;
            
            // After spread, move should still be meaningful
            if (netMove < 5.0m)
            {
                return new ValidateResult(
                    false,
                    null, null, null, null, null, null, null, null, null,
                    "NET_EDGE_TOO_SMALL",
                    $"Net move after spread {netMove:F2} USD is too small",
                    null
                );
            }
        }

        // Check impulse mode coherence
        if (tableResult.Template == "IMPULSE_HARVEST_CAPTURE")
        {
            if (newsResult.WaterfallRisk == "HIGH" || 
                analyzeResult.WaterfallRisk == "HIGH" ||
                historicalResult.HistoricalTrapProbability > 0.5m)
            {
                return new ValidateResult(
                    false,
                    null, null, null, null, null, null, null, null, null,
                    "IMPULSE_INCOHERENT",
                    "Impulse harvest not coherent with high waterfall risk or trap probability",
                    null
                );
            }
        }

        // All checks passed
        return new ValidateResult(
            true,
            tableResult.OrderType,
            tableResult.EntryPrice,
            tableResult.Tp1,
            tableResult.Tp2,
            tableResult.Tp3,
            tableResult.StopLoss,
            tableResult.Grams,
            tableResult.Expiry,
            "VALIDATED",
            null,
            null
        );
    }
}