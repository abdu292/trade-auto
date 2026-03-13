using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 14: VALIDATE ENGINE
/// Purpose: Reality scrub and coherence checks.
/// 
/// Checks:
/// - expiry realism
/// - size realism
/// - session-phase realism
/// - aggressiveness vs confidence
/// - impulse mode coherence
/// - net-edge realism
/// 
/// VALIDATE may:
/// - downgrade
/// - resize
/// - reject
/// but never invent a trade.
/// </summary>
public static class ValidateEngine
{
    public static ValidateEngineResult Validate(
        MarketSnapshotContract snapshot,
        TableCompilerResult table,
        StructureEngineResult structure,
        VolatilityRegimeEngineResult volatility,
        NewsEngineResult news,
        SessionEngineResult session)
    {
        if (!table.IsValid)
        {
            return new ValidateEngineResult(
                IsValid: false,
                Reason: table.Reason,
                ValidatedOrder: null,
                Downgrades: Array.Empty<string>(),
                ResizeApplied: false);
        }
        
        var downgrades = new List<string>();
        var validatedOrder = table;
        var resizeApplied = false;
        
        // Check expiry realism
        if (table.ExpiryUtc.HasValue)
        {
            var expiryCheck = CheckExpiryRealism(table.ExpiryUtc.Value, session, news);
            if (!expiryCheck.IsValid)
            {
                return new ValidateEngineResult(
                    IsValid: false,
                    Reason: expiryCheck.Reason,
                    ValidatedOrder: null,
                    Downgrades: Array.Empty<string>(),
                    ResizeApplied: false);
            }
            
            if (expiryCheck.Downgrade != null)
            {
                downgrades.Add(expiryCheck.Downgrade);
            }
        }
        
        // Check size realism
        if (table.Grams.HasValue)
        {
            var sizeCheck = CheckSizeRealism(table.Grams.Value, snapshot, volatility);
            if (!sizeCheck.IsValid)
            {
                return new ValidateEngineResult(
                    IsValid: false,
                    Reason: sizeCheck.Reason,
                    ValidatedOrder: null,
                    Downgrades: downgrades,
                    ResizeApplied: false);
            }
            
            if (sizeCheck.ResizeRequired)
            {
                resizeApplied = true;
                validatedOrder = validatedOrder with { Grams = sizeCheck.ResizedGrams };
                downgrades.Add($"Size resized from {table.Grams.Value:0.##}g to {sizeCheck.ResizedGrams:0.##}g");
            }
        }
        
        // Check session-phase realism
        var phaseCheck = CheckSessionPhaseRealism(table, session, news);
        if (!phaseCheck.IsValid)
        {
            downgrades.Add(phaseCheck.Reason);
        }
        
        // Check aggressiveness vs confidence
        var aggressivenessCheck = CheckAggressivenessCoherence(table, structure, volatility);
        if (!aggressivenessCheck.IsValid)
        {
            downgrades.Add(aggressivenessCheck.Reason);
        }
        
        // Check impulse mode coherence
        if (table.Template == "FLUSH_LIMIT_CAPTURE" || table.ProjectedMoveNetUSD > 20m)
        {
            var impulseCheck = CheckImpulseModeCoherence(table, volatility, news);
            if (!impulseCheck.IsValid)
            {
                downgrades.Add(impulseCheck.Reason);
            }
        }
        
        // Check net-edge realism
        var netEdgeCheck = CheckNetEdgeRealism(table, snapshot);
        if (!netEdgeCheck.IsValid)
        {
            return new ValidateEngineResult(
                IsValid: false,
                Reason: netEdgeCheck.Reason,
                ValidatedOrder: null,
                Downgrades: downgrades,
                ResizeApplied: resizeApplied);
        }
        
        return new ValidateEngineResult(
            IsValid: true,
            Reason: downgrades.Count > 0 
                ? $"Validated with downgrades: {string.Join(", ", downgrades)}"
                : "Validated",
            ValidatedOrder: validatedOrder,
            Downgrades: downgrades,
            ResizeApplied: resizeApplied);
    }

    private static (bool IsValid, string? Reason, string? Downgrade) CheckExpiryRealism(
        DateTimeOffset expiry,
        SessionEngineResult session,
        NewsEngineResult news)
    {
        var minutesToExpiry = (expiry - DateTimeOffset.UtcNow).TotalMinutes;
        
        // Check if expiry is too short
        if (minutesToExpiry < 30)
        {
            return (false, "Expiry too short (< 30 minutes)", null);
        }
        
        // Check if expiry extends into next hazard window
        if (news.NextTier1UltraWithin90m && minutesToExpiry > 90)
        {
            return (false, "Expiry extends into next hazard window", null);
        }
        
        // Check session-appropriate expiry
        var sessionExpiryRanges = session.Session switch
        {
            "JAPAN" => (90, 120),
            "INDIA" => (90, 150),
            "LONDON" => (60, 90),
            "NEW_YORK" => (45, 60),
            _ => (60, 90)
        };
        
        if (minutesToExpiry < sessionExpiryRanges.Item1 || minutesToExpiry > sessionExpiryRanges.Item2)
        {
            return (true, null, $"Expiry {minutesToExpiry:0} minutes outside session range {sessionExpiryRanges.Item1}-{sessionExpiryRanges.Item2}");
        }
        
        return (true, null, null);
    }

    private static (bool IsValid, string? Reason, bool ResizeRequired, decimal? ResizedGrams) CheckSizeRealism(
        decimal grams,
        MarketSnapshotContract snapshot,
        VolatilityRegimeEngineResult volatility)
    {
        // Minimum grams check
        if (grams < 100m)
        {
            return (false, $"Grams {grams:0.##} below 100g minimum", false, null);
        }
        
        // Check if size is too large for volatility
        if (volatility.VolatilityClass == "EXTREME" && grams > 200m)
        {
            return (true, null, true, 200m); // Resize to 200g max
        }
        
        return (true, null, false, null);
    }

    private static (bool IsValid, string Reason) CheckSessionPhaseRealism(
        TableCompilerResult table,
        SessionEngineResult session,
        NewsEngineResult news)
    {
        // Check if order is appropriate for session phase
        if (session.Phase == "END" && table.Template == "FLUSH_LIMIT_CAPTURE")
        {
            return (true, "FLUSH_LIMIT_CAPTURE in session END phase - caution");
        }
        
        if (session.IsTransition && table.OrderType == "BUY_STOP")
        {
            return (true, "BUY_STOP in transition window - caution");
        }
        
        return (true, "Session phase appropriate");
    }

    private static (bool IsValid, string Reason) CheckAggressivenessCoherence(
        TableCompilerResult table,
        StructureEngineResult structure,
        VolatilityRegimeEngineResult volatility)
    {
        // Check if order aggressiveness matches structure quality
        if (table.OrderType == "BUY_STOP" && structure.StructureQuality == "WEAK")
        {
            return (true, "BUY_STOP with weak structure - caution");
        }
        
        if (table.ProjectedMoveNetUSD > 20m && volatility.VolatilityClass == "NORMAL")
        {
            return (true, "Large TP target in normal volatility - verify impulse harvest mode");
        }
        
        return (true, "Aggressiveness coherent");
    }

    private static (bool IsValid, string Reason) CheckImpulseModeCoherence(
        TableCompilerResult table,
        VolatilityRegimeEngineResult volatility,
        NewsEngineResult news)
    {
        // Impulse harvest mode requires expansion volatility
        if (table.ProjectedMoveNetUSD > 20m && volatility.VolatilityState != "EXPANSION")
        {
            return (true, "Impulse harvest target but volatility not expansion - verify");
        }
        
        // Impulse harvest requires news not blocking
        if (table.ProjectedMoveNetUSD > 20m && news.OverallMODE == "BLOCK")
        {
            return (false, "Impulse harvest blocked by news mode");
        }
        
        return (true, "Impulse mode coherent");
    }

    private static (bool IsValid, string? Reason) CheckNetEdgeRealism(
        TableCompilerResult table,
        MarketSnapshotContract snapshot)
    {
        // Net edge must be >= 8 USD
        if (table.ProjectedMoveNetUSD < 8m)
        {
            return (false, $"Projected move net {table.ProjectedMoveNetUSD:0.00} USD < 8 USD minimum");
        }
        
        // Check if net edge is realistic (not too large)
        if (table.ProjectedMoveNetUSD > 100m)
        {
            return (false, $"Projected move net {table.ProjectedMoveNetUSD:0.00} USD unrealistically large");
        }
        
        return (true, null);
    }
}

/// <summary>
/// Validate Engine output contract
/// </summary>
public sealed record ValidateEngineResult(
    bool IsValid,
    string Reason,
    TableCompilerResult? ValidatedOrder,
    IReadOnlyCollection<string> Downgrades,
    bool ResizeApplied);
