using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 12: ANALYZE ENGINE
/// Purpose: Converts environment + structure + history into a TABLE-ready trade map.
/// 
/// Must output:
/// - Regime, WaterfallRisk, MID_AIR_STATUS
/// - Rail-A status + reason, Rail-B status + reason
/// - S1 / S2 / R1 / R2 / FAIL
/// - current-session anchors, next-session anchors
/// - nearest magnet
/// - primary trade concept
/// - rotation envelope
/// - trigger objects
/// - bottomType (CLASSIC_RECLAIM / FLUSH_ABSORPTION / PANIC_TO_REBUILD / INVALID)
/// - patternType (WATERFALL_CONTINUATION / FLUSH_REVERSAL_ATTEMPT)
/// - impulseHarvestScore
/// - sessionHistoricalModifier
/// </summary>
public static class AnalyzeEngine
{
    public static AnalyzeEngineResult Process(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure,
        VolatilityRegimeEngineResult volatility,
        WaterfallCrisisEngineResult waterfall,
        NewsEngineResult news,
        HistoricalPatternEngineResult historical,
        CandidateEngineResult candidate,
        IndicatorEngineResult indicators,
        SessionEngineResult session)
    {
        // Bottom grammar
        var bottomType = ClassifyBottomType(snapshot, structure, indicators);
        
        // Pattern classifier
        var patternType = waterfall.PatternType;
        
        // Rail status
        var (railAStatus, railAReason) = DetermineRailAStatus(news, structure, waterfall);
        var (railBStatus, railBReason) = DetermineRailBStatus(news, structure, waterfall, volatility);
        
        // Session anchors
        var currentSessionAnchors = DetermineCurrentSessionAnchors(structure, indicators, session);
        var nextSessionAnchors = DetermineNextSessionAnchors(structure, session);
        
        // Nearest magnet
        var nearestMagnet = DetermineNearestMagnet(snapshot, structure);
        
        // Primary trade concept
        var primaryTradeConcept = DeterminePrimaryTradeConcept(
            structure, 
            candidate, 
            bottomType, 
            patternType);
        
        // Rotation envelope
        var rotationEnvelope = DetermineRotationEnvelope(
            structure, 
            historical, 
            indicators);
        
        // Trigger objects
        var triggerObjects = DetermineTriggerObjects(
            structure, 
            candidate, 
            indicators);
        
        // Impulse harvest score
        var impulseHarvestScore = CalculateImpulseHarvestScore(
            historical, 
            structure, 
            volatility, 
            news);
        
        return new AnalyzeEngineResult(
            Regime: volatility.Regime,
            WaterfallRisk: waterfall.WaterfallRisk,
            MidAirStatus: structure.IsMidAir ? structure.MidAirZone : "NONE",
            RailAStatus: railAStatus,
            RailAReason: railAReason,
            RailBStatus: railBStatus,
            RailBReason: railBReason,
            S1: structure.S1,
            S2: structure.S2,
            R1: structure.R1,
            R2: structure.R2,
            Fail: structure.Fail,
            CurrentSessionAnchors: currentSessionAnchors,
            NextSessionAnchors: nextSessionAnchors,
            NearestMagnet: nearestMagnet,
            PrimaryTradeConcept: primaryTradeConcept,
            RotationEnvelope: rotationEnvelope,
            TriggerObjects: triggerObjects,
            BottomType: bottomType,
            PatternType: patternType,
            ImpulseHarvestScore: impulseHarvestScore,
            SessionHistoricalModifier: historical.SessionHistoricalModifier);
    }

    private static string ClassifyBottomType(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure,
        IndicatorEngineResult indicators)
    {
        // CLASSIC_RECLAIM_BOTTOM
        if (structure.HasReclaim && structure.HasShelf && indicators.IsCompression)
        {
            return "CLASSIC_RECLAIM_BOTTOM";
        }
        
        // FLUSH_ABSORPTION_BOTTOM
        if (structure.HasSweep && structure.HasShelf && !snapshot.HasPanicDropSequence)
        {
            return "FLUSH_ABSORPTION_BOTTOM";
        }
        
        // PANIC_TO_REBUILD_BOTTOM
        if (snapshot.HasPanicDropSequence && structure.HasShelf && indicators.RsiH1 < 45m)
        {
            return "PANIC_TO_REBUILD_BOTTOM";
        }
        
        return "INVALID";
    }

    private static (string Status, string Reason) DetermineRailAStatus(
        NewsEngineResult news,
        StructureEngineResult structure,
        WaterfallCrisisEngineResult waterfall)
    {
        if (news.RailAPermission == "NO" || waterfall.ShouldBlock)
        {
            return ("BLOCKED", $"Rail-A blocked: {news.RailAPermission}, waterfall={waterfall.WaterfallRisk}");
        }
        
        if (news.RailAPermission == "ONLY_AFTER_STRUCTURE" && !structure.HasShelf)
        {
            return ("WAIT", "Rail-A waiting for structure confirmation");
        }
        
        return ("ALLOWED", "Rail-A allowed");
    }

    private static (string Status, string Reason) DetermineRailBStatus(
        NewsEngineResult news,
        StructureEngineResult structure,
        WaterfallCrisisEngineResult waterfall,
        VolatilityRegimeEngineResult volatility)
    {
        if (news.RailBPermission == "NO" || waterfall.ShouldBlock)
        {
            return ("BLOCKED", $"Rail-B blocked: {news.RailBPermission}, waterfall={waterfall.WaterfallRisk}");
        }
        
        if (volatility.VolatilityClass == "EXTREME")
        {
            return ("BLOCKED", "Rail-B blocked: extreme volatility");
        }
        
        if (!structure.HasLid || !structure.HasShelf)
        {
            return ("WAIT", "Rail-B waiting for lid and compression");
        }
        
        return ("ALLOWED", "Rail-B allowed");
    }

    private static IReadOnlyCollection<string> DetermineCurrentSessionAnchors(
        StructureEngineResult structure,
        IndicatorEngineResult indicators,
        SessionEngineResult session)
    {
        var anchors = new List<string>();
        
        if (structure.S1 > 0m)
        {
            anchors.Add($"S1={structure.S1:0.00}");
        }
        
        if (structure.S2.HasValue)
        {
            anchors.Add($"S2={structure.S2.Value:0.00}");
        }
        
        if (indicators.SessionHigh > 0m)
        {
            anchors.Add($"SessionHigh={indicators.SessionHigh:0.00}");
        }
        
        if (indicators.SessionLow > 0m)
        {
            anchors.Add($"SessionLow={indicators.SessionLow:0.00}");
        }
        
        return anchors;
    }

    private static IReadOnlyCollection<string> DetermineNextSessionAnchors(
        StructureEngineResult structure,
        SessionEngineResult session)
    {
        // Simplified: would predict next session anchors
        var anchors = new List<string>();
        
        // Use current structure as next session reference
        if (structure.R1 > 0m)
        {
            anchors.Add($"NextR1={structure.R1:0.00}");
        }
        
        return anchors;
    }

    private static decimal DetermineNearestMagnet(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure)
    {
        var currentPrice = snapshot.Bid > 0m ? snapshot.Bid : 
            snapshot.TimeframeData.FirstOrDefault()?.Close ?? 0m;
        
        if (currentPrice <= 0m) return 0m;
        
        // Find nearest structure level
        var levels = new List<decimal> { structure.S1, structure.R1 };
        if (structure.S2.HasValue) levels.Add(structure.S2.Value);
        if (structure.R2.HasValue) levels.Add(structure.R2.Value);
        
        var validLevels = levels.Where(l => l > 0m).ToList();
        if (validLevels.Count == 0) return 0m;
        
        return validLevels.OrderBy(l => Math.Abs(currentPrice - l)).First();
    }

    private static string DeterminePrimaryTradeConcept(
        StructureEngineResult structure,
        CandidateEngineResult candidate,
        string bottomType,
        string patternType)
    {
        if (candidate.EarlyFlushCandidate && bottomType != "INVALID")
        {
            return "FLUSH_LIMIT_CAPTURE";
        }
        
        if (structure.HasShelf && bottomType != "INVALID")
        {
            return "SHELF_BUY_LIMIT";
        }
        
        if (structure.HasLid && patternType == "FLUSH_REVERSAL_ATTEMPT")
        {
            return "LID_BREAKOUT_BUY_STOP";
        }
        
        return "STANDARD_ROTATION";
    }

    private static (decimal Min, decimal Max) DetermineRotationEnvelope(
        StructureEngineResult structure,
        HistoricalPatternEngineResult historical,
        IndicatorEngineResult indicators)
    {
        // Standard rotation: +8 to +12 USD
        var min = 8m;
        var max = 12m;
        
        // Extend if impulse harvest mode supported
        if (historical.HistoricalExtensionBandUSD > 0m && historical.HistoricalContinuationScore >= 0.7m)
        {
            max = Math.Max(max, historical.HistoricalExtensionBandUSD);
        }
        
        return (min, max);
    }

    private static IReadOnlyCollection<string> DetermineTriggerObjects(
        StructureEngineResult structure,
        CandidateEngineResult candidate,
        IndicatorEngineResult indicators)
    {
        var triggers = new List<string>();
        
        if (candidate.ZoneWatchActive)
        {
            triggers.Add("ZONE_WATCH");
        }
        
        if (structure.HasSweep)
        {
            triggers.Add("SWEEP");
        }
        
        if (structure.HasReclaim)
        {
            triggers.Add("RECLAIM");
        }
        
        if (indicators.IsCompression)
        {
            triggers.Add("COMPRESSION");
        }
        
        return triggers;
    }

    private static decimal CalculateImpulseHarvestScore(
        HistoricalPatternEngineResult historical,
        StructureEngineResult structure,
        VolatilityRegimeEngineResult volatility,
        NewsEngineResult news)
    {
        var score = 0m;
        
        // Base score from historical continuation
        score += historical.HistoricalContinuationScore * 0.4m;
        
        // Structure quality boost
        if (structure.StructureQuality == "STRONG")
        {
            score += 0.2m;
        }
        
        // Volatility must be expansion, not exhaustion
        if (volatility.VolatilityState == "EXPANSION" && volatility.VolatilityClass != "EXTREME")
        {
            score += 0.2m;
        }
        
        // News must not block
        if (news.OverallMODE != "BLOCK")
        {
            score += 0.2m;
        }
        
        return Math.Clamp(score, 0m, 1m);
    }
}

/// <summary>
/// Analyze Engine output contract
/// </summary>
public sealed record AnalyzeEngineResult(
    string Regime,
    string WaterfallRisk,
    string MidAirStatus,
    string RailAStatus,
    string RailAReason,
    string RailBStatus,
    string RailBReason,
    decimal S1,
    decimal? S2,
    decimal R1,
    decimal? R2,
    decimal? Fail,
    IReadOnlyCollection<string> CurrentSessionAnchors,
    IReadOnlyCollection<string> NextSessionAnchors,
    decimal NearestMagnet,
    string PrimaryTradeConcept,
    (decimal Min, decimal Max) RotationEnvelope,
    IReadOnlyCollection<string> TriggerObjects,
    string BottomType,
    string PatternType,
    decimal ImpulseHarvestScore,
    decimal SessionHistoricalModifier);
