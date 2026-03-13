using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// HISTORICAL_PATTERN_ENGINE per spec/00_instructions
/// Position: before candidate promotion and before ANALYZE/TABLE decisioning
/// Role: inject 10+ year memory into today's decision
/// </summary>
public static class HistoricalPatternEngine
{
    public record HistoricalPatternResult(
        string HistoricalPatternTag,  // Pattern identifier
        decimal HistoricalContinuationScore,  // 0.0 to 1.0
        decimal HistoricalReversalRisk,  // 0.0 to 1.0
        decimal HistoricalExtensionBandUsd,  // USD extension potential
        decimal HistoricalTrapProbability,  // 0.0 to 1.0
        string HistoricalBestPath,  // BUY_LIMIT, BUY_STOP, WAIT
        decimal SessionHistoricalModifier  // -1.0 to +1.0
    );

    /// <summary>
    /// Analyzes historical patterns and returns memory-based insights.
    /// In production, this would query a 10+ year historical database.
    /// </summary>
    public static HistoricalPatternResult AnalyzeHistoricalPattern(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        string session,
        string sessionPhase,
        DayOfWeek dayOfWeek,
        IReadOnlyCollection<HistoricalPatternMatch>? historicalMatches = null)
    {
        var historicalPatternTag = "UNKNOWN";
        var historicalContinuationScore = 0.5m;
        var historicalReversalRisk = 0.5m;
        var historicalExtensionBandUsd = 12.0m;  // Default to standard rotation
        var historicalTrapProbability = 0.3m;
        var historicalBestPath = "WAIT";
        var sessionHistoricalModifier = 0.0m;

        // Analyze session-specific historical behavior
        var sessionBehavior = AnalyzeSessionBehavior(session, sessionPhase, dayOfWeek);
        sessionHistoricalModifier = sessionBehavior.Modifier;
        historicalPatternTag = sessionBehavior.PatternTag;

        // Analyze regime-specific patterns
        var regimePattern = AnalyzeRegimePattern(regime, snapshot);
        historicalContinuationScore = regimePattern.ContinuationScore;
        historicalReversalRisk = regimePattern.ReversalRisk;
        historicalExtensionBandUsd = regimePattern.ExtensionBandUsd;
        historicalTrapProbability = regimePattern.TrapProbability;

        // If historical matches provided, use them for more accurate scoring
        if (historicalMatches != null && historicalMatches.Any())
        {
            var avgContinuation = historicalMatches.Average(m => m.ContinuationScore);
            var avgReversal = historicalMatches.Average(m => m.ReversalRisk);
            var avgExtension = historicalMatches.Average(m => m.ExtensionBandUsd);
            var avgTrap = historicalMatches.Average(m => m.TrapProbability);

            // Weighted blend: 70% historical matches, 30% session/regime defaults
            historicalContinuationScore = (avgContinuation * 0.7m) + (historicalContinuationScore * 0.3m);
            historicalReversalRisk = (avgReversal * 0.7m) + (historicalReversalRisk * 0.3m);
            historicalExtensionBandUsd = (avgExtension * 0.7m) + (historicalExtensionBandUsd * 0.3m);
            historicalTrapProbability = (avgTrap * 0.7m) + (historicalTrapProbability * 0.3m);

            // Use most common pattern tag from matches
            var mostCommonTag = historicalMatches
                .GroupBy(m => m.PatternTag)
                .OrderByDescending(g => g.Count())
                .First().Key;
            historicalPatternTag = mostCommonTag;
        }

        // Determine best path based on scores
        if (historicalContinuationScore > 0.7m && historicalReversalRisk < 0.3m && historicalTrapProbability < 0.3m)
        {
            // Strong continuation, low reversal risk, low trap probability
            if (snapshot.HasLiquiditySweep && snapshot.IsCompression)
            {
                historicalBestPath = "BUY_STOP";
            }
            else
            {
                historicalBestPath = "BUY_LIMIT";
            }
        }
        else if (historicalContinuationScore > 0.6m && historicalReversalRisk < 0.4m)
        {
            historicalBestPath = "BUY_LIMIT";
        }

        // Clamp values
        historicalContinuationScore = Math.Clamp(historicalContinuationScore, 0.0m, 1.0m);
        historicalReversalRisk = Math.Clamp(historicalReversalRisk, 0.0m, 1.0m);
        historicalTrapProbability = Math.Clamp(historicalTrapProbability, 0.0m, 1.0m);
        sessionHistoricalModifier = Math.Clamp(sessionHistoricalModifier, -1.0m, 1.0m);

        return new HistoricalPatternResult(
            historicalPatternTag,
            historicalContinuationScore,
            historicalReversalRisk,
            historicalExtensionBandUsd,
            historicalTrapProbability,
            historicalBestPath,
            sessionHistoricalModifier
        );
    }

    private static (string PatternTag, decimal Modifier) AnalyzeSessionBehavior(
        string session,
        string sessionPhase,
        DayOfWeek dayOfWeek)
    {
        // Session-specific historical modifiers
        return session switch
        {
            "JAPAN" => ("CLEAN_RANGE_RELOAD", 0.1m),  // Cleaner range/reload
            "INDIA" => ("CONTINUATION_REBUILD", 0.2m),  // Continuation/retest friendly
            "LONDON" => ("STRONGEST_EXPANSION", 0.3m),  // Strongest directional expansion
            "NEW_YORK" => ("HIGHEST_EVENT_POTENTIAL", -0.1m),  // Highest event/spike potential but also trap risk
            "TRANSITION" => ("REGIME_HANDOVER_RISK", -0.2m),  // Regime handover risk
            _ => ("UNKNOWN", 0.0m)
        };
    }

    private static (decimal ContinuationScore, decimal ReversalRisk, decimal ExtensionBandUsd, decimal TrapProbability) AnalyzeRegimePattern(
        RegimeClassificationContract regime,
        MarketSnapshotContract snapshot)
    {
        var continuationScore = 0.5m;
        var reversalRisk = 0.5m;
        var extensionBandUsd = 12.0m;
        var trapProbability = 0.3m;

        // Regime-specific patterns
        if (regime.RegimeTag == "RANGE" || regime.RegimeTag == "RANGE_RELOAD")
        {
            continuationScore = 0.6m;
            reversalRisk = 0.3m;
            extensionBandUsd = 10.0m;  // Standard rotation
            trapProbability = 0.2m;
        }
        else if (regime.RegimeTag == "CONTINUATION_REBUILD")
        {
            continuationScore = 0.7m;
            reversalRisk = 0.3m;
            extensionBandUsd = 15.0m;
            trapProbability = 0.25m;
        }
        else if (regime.RegimeTag == "EXPANSION")
        {
            continuationScore = 0.75m;
            reversalRisk = 0.4m;
            extensionBandUsd = 20.0m;  // Potential for impulse harvest
            trapProbability = 0.35m;
        }
        else if (regime.RegimeTag == "EXHAUSTION")
        {
            continuationScore = 0.3m;
            reversalRisk = 0.7m;
            extensionBandUsd = 8.0m;  // Reduced extension
            trapProbability = 0.6m;
        }
        else if (regime.RegimeTag == "LIQUIDATION" || regime.RegimeTag == "SHOCK")
        {
            continuationScore = 0.2m;
            reversalRisk = 0.8m;
            extensionBandUsd = 8.0m;
            trapProbability = 0.7m;
        }

        // Adjust based on market state
        if (snapshot.HasPanicDropSequence)
        {
            reversalRisk = Math.Min(1.0m, reversalRisk + 0.2m);
            trapProbability = Math.Min(1.0m, trapProbability + 0.2m);
        }

        if (snapshot.IsCompression && snapshot.HasLiquiditySweep)
        {
            continuationScore = Math.Min(1.0m, continuationScore + 0.1m);
            trapProbability = Math.Max(0.0m, trapProbability - 0.1m);
        }

        return (continuationScore, reversalRisk, extensionBandUsd, trapProbability);
    }
}

// Supporting contract for historical pattern matches
public record HistoricalPatternMatch(
    string PatternTag,
    decimal ContinuationScore,
    decimal ReversalRisk,
    decimal ExtensionBandUsd,
    decimal TrapProbability,
    DateTimeOffset MatchDate,
    string Session,
    string RegimeTag);