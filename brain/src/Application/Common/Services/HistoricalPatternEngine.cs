using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 10: HISTORICAL_PATTERN_ENGINE
/// Purpose: Injects 10+ year memory into today's decision.
/// 
/// Outputs:
/// - historicalPatternTag
/// - historicalContinuationScore
/// - historicalReversalRisk
/// - historicalExtensionBandUSD
/// - historicalTrapProbability
/// - historicalBestPath
/// - sessionHistoricalModifier
/// 
/// Meaning:
/// This engine decides whether today is more likely to:
/// - rotate normally +8 to +12,
/// - continue into +20 / +30 / +50,
/// - or trap/exhaust.
/// </summary>
public static class HistoricalPatternEngine
{
    public static HistoricalPatternEngineResult Process(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        StructureEngineResult structure,
        SessionEngineResult session)
    {
        // Historical pattern tag (simplified - actual would query 10+ year database)
        var patternTag = DetermineHistoricalPatternTag(snapshot, indicators, structure, session);
        
        // Historical continuation score (0-1)
        var continuationScore = CalculateContinuationScore(indicators, structure, session);
        
        // Historical reversal risk (0-1)
        var reversalRisk = CalculateReversalRisk(indicators, structure, session);
        
        // Historical extension band USD
        var extensionBand = CalculateExtensionBand(indicators, structure, session, continuationScore);
        
        // Historical trap probability (0-1)
        var trapProbability = CalculateTrapProbability(indicators, structure, session);
        
        // Historical best path
        var bestPath = DetermineBestPath(continuationScore, reversalRisk, trapProbability, structure);
        
        // Session historical modifier
        var sessionModifier = CalculateSessionHistoricalModifier(session, indicators, structure);
        
        return new HistoricalPatternEngineResult(
            HistoricalPatternTag: patternTag,
            HistoricalContinuationScore: continuationScore,
            HistoricalReversalRisk: reversalRisk,
            HistoricalExtensionBandUSD: extensionBand,
            HistoricalTrapProbability: trapProbability,
            HistoricalBestPath: bestPath,
            SessionHistoricalModifier: sessionModifier);
    }

    private static string DetermineHistoricalPatternTag(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        StructureEngineResult structure,
        SessionEngineResult session)
    {
        // Simplified pattern detection based on current structure and indicators
        // Actual implementation would query historical database
        
        if (structure.HasReclaim && indicators.IsCompression)
        {
            return "RECLAIM_COMPRESSION";
        }
        
        if (structure.HasSweep && !structure.HasReclaim)
        {
            return "SWEEP_NO_RECLAIM";
        }
        
        if (indicators.AdrUsedRatio >= 0.85m)
        {
            return "ADR_EXHAUSTION";
        }
        
        if (session.Session == "LONDON" && indicators.IsExpansion)
        {
            return "LONDON_EXPANSION";
        }
        
        return "STANDARD_ROTATION";
    }

    private static decimal CalculateContinuationScore(
        IndicatorEngineResult indicators,
        StructureEngineResult structure,
        SessionEngineResult session)
    {
        var score = 0.5m; // Base score
        
        // Boost if structure supports continuation
        if (structure.HasReclaim && structure.HasShelf)
        {
            score += 0.2m;
        }
        
        // Boost if compression present (energy building)
        if (indicators.IsCompression && indicators.CompressionCountM15 >= 3)
        {
            score += 0.15m;
        }
        
        // Boost for London session (strongest directional moves)
        if (session.Session == "LONDON")
        {
            score += 0.1m;
        }
        
        // Reduce if ADR exhausted
        if (indicators.AdrUsedRatio >= 0.85m)
        {
            score -= 0.3m;
        }
        
        return Math.Clamp(score, 0m, 1m);
    }

    private static decimal CalculateReversalRisk(
        IndicatorEngineResult indicators,
        StructureEngineResult structure,
        SessionEngineResult session)
    {
        var risk = 0.3m; // Base risk
        
        // Increase risk if no structure
        if (!structure.HasShelf && !structure.HasReclaim)
        {
            risk += 0.2m;
        }
        
        // Increase risk if overextended
        if (indicators.AdrUsedRatio >= 0.75m)
        {
            risk += 0.2m;
        }
        
        // Increase risk in transition windows
        if (session.IsTransition)
        {
            risk += 0.15m;
        }
        
        return Math.Clamp(risk, 0m, 1m);
    }

    private static decimal CalculateExtensionBand(
        IndicatorEngineResult indicators,
        StructureEngineResult structure,
        SessionEngineResult session,
        decimal continuationScore)
    {
        // Extension band based on continuation score and session
        if (continuationScore < 0.6m)
        {
            return 0m; // No extension
        }
        
        // Base extension for strong continuation
        var baseExtension = 12m; // Standard +8 to +12
        
        // Add extension for very strong continuation
        if (continuationScore >= 0.8m && session.Session == "LONDON")
        {
            baseExtension = 30m; // +20 to +30
        }
        
        if (continuationScore >= 0.9m && structure.HasReclaim && indicators.IsCompression)
        {
            baseExtension = 50m; // +30 to +50
        }
        
        return baseExtension;
    }

    private static decimal CalculateTrapProbability(
        IndicatorEngineResult indicators,
        StructureEngineResult structure,
        SessionEngineResult session)
    {
        var probability = 0.2m; // Base probability
        
        // Increase if mid-air
        if (structure.IsMidAir)
        {
            probability += 0.3m;
        }
        
        // Increase if no structure
        if (!structure.HasShelf && !structure.HasReclaim)
        {
            probability += 0.2m;
        }
        
        // Increase if exhausted
        if (indicators.AdrUsedRatio >= 0.85m)
        {
            probability += 0.25m;
        }
        
        return Math.Clamp(probability, 0m, 1m);
    }

    private static string DetermineBestPath(
        decimal continuationScore,
        decimal reversalRisk,
        decimal trapProbability,
        StructureEngineResult structure)
    {
        if (trapProbability > 0.6m)
        {
            return "AVOID";
        }
        
        if (continuationScore >= 0.7m && reversalRisk < 0.4m)
        {
            return structure.HasShelf ? "BUY_LIMIT" : "BUY_STOP";
        }
        
        if (structure.HasShelf)
        {
            return "BUY_LIMIT";
        }
        
        return "WAIT";
    }

    private static decimal CalculateSessionHistoricalModifier(
        SessionEngineResult session,
        IndicatorEngineResult indicators,
        StructureEngineResult structure)
    {
        // Session-specific historical behavior modifier
        var modifier = 1.0m;
        
        switch (session.Session)
        {
            case "JAPAN":
                modifier = 0.8m; // Softer moves
                break;
            case "INDIA":
                modifier = 0.9m; // Moderate
                break;
            case "LONDON":
                modifier = 1.2m; // Strongest directional expansion
                break;
            case "NEW_YORK":
                modifier = 0.7m; // High event/spike potential but also trap risk
                break;
        }
        
        // Adjust based on structure quality
        if (structure.StructureQuality == "STRONG")
        {
            modifier *= 1.1m;
        }
        else if (structure.StructureQuality == "WEAK")
        {
            modifier *= 0.9m;
        }
        
        return modifier;
    }
}

/// <summary>
/// Historical Pattern Engine output contract
/// </summary>
public sealed record HistoricalPatternEngineResult(
    string HistoricalPatternTag,
    decimal HistoricalContinuationScore,
    decimal HistoricalReversalRisk,
    decimal HistoricalExtensionBandUSD,
    decimal HistoricalTrapProbability,
    string HistoricalBestPath,
    decimal SessionHistoricalModifier);
