using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 5: WATERFALL/CRISIS ENGINE
/// Purpose: Distinguishes WATERFALL_CONTINUATION from FLUSH_REVERSAL_ATTEMPT.
/// Triggers crisis veto when needed.
/// This is a hard blocker. If waterfall/crisis says NO, nothing below may force a trade.
/// </summary>
public static class WaterfallCrisisEngine
{
    public static WaterfallCrisisEngineResult Analyze(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        StructureEngineResult structure,
        VolatilityRegimeEngineResult volatility)
    {
        // Classify waterfall vs flush
        var pattern = ClassifyPattern(snapshot, indicators, structure);
        
        // Crisis veto check
        var crisisVeto = CheckCrisisVeto(snapshot, indicators, structure, volatility, pattern);
        
        // Waterfall risk level
        var waterfallRisk = DetermineWaterfallRisk(snapshot, indicators, structure, pattern);
        
        return new WaterfallCrisisEngineResult(
            PatternType: pattern.PatternType,
            PatternReason: pattern.Reason,
            CrisisVeto: crisisVeto.IsVetoed,
            CrisisReason: crisisVeto.Reason,
            WaterfallRisk: waterfallRisk,
            ShouldBlock: crisisVeto.IsVetoed || waterfallRisk == "HIGH" || pattern.PatternType == "WATERFALL_CONTINUATION");
    }

    private static (string PatternType, string Reason) ClassifyPattern(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        StructureEngineResult structure)
    {
        // WATERFALL_CONTINUATION signals:
        // - Repeated bearish closes near lows
        // - Shelf destruction continues
        // - No real rebound
        // - Volatility expansion in continuation direction
        
        var hasWaterfallSignals = 0;
        var hasFlushSignals = 0;
        
        // Check for waterfall continuation signals
        if (snapshot.HasPanicDropSequence)
        {
            hasWaterfallSignals++;
        }
        
        if (indicators.IsExpansion && indicators.IsAtrExpanding && indicators.RsiH1 < 40m)
        {
            hasWaterfallSignals++;
        }
        
        if (!structure.HasReclaim && structure.HasSweep && indicators.AdrUsedRatio > 0.7m)
        {
            hasWaterfallSignals++;
        }
        
        if (structure.Fail.HasValue && indicators.AdrUsedRatio >= 0.85m)
        {
            hasWaterfallSignals++;
        }
        
        // Check for flush reversal attempt signals
        if (structure.HasSweep && structure.HasReclaim)
        {
            hasFlushSignals++;
        }
        
        if (structure.HasShelf && indicators.HasOverlapCandles && !indicators.IsAtrExpanding)
        {
            hasFlushSignals++;
        }
        
        if (indicators.IsCompression && indicators.CompressionCountM15 >= 3 && indicators.RsiH1 > 35m)
        {
            hasFlushSignals++;
        }
        
        // Determine pattern
        if (hasWaterfallSignals >= 2)
        {
            return ("WATERFALL_CONTINUATION", 
                $"Waterfall continuation: panic={snapshot.HasPanicDropSequence}, expansion={indicators.IsExpansion}, no_reclaim={!structure.HasReclaim}");
        }
        
        if (hasFlushSignals >= 2)
        {
            return ("FLUSH_REVERSAL_ATTEMPT",
                $"Flush reversal attempt: sweep={structure.HasSweep}, reclaim={structure.HasReclaim}, shelf={structure.HasShelf}");
        }
        
        // Default: cannot determine, treat as caution
        return ("UNKNOWN", "Insufficient signals to classify pattern");
    }

    private static (bool IsVetoed, string Reason) CheckCrisisVeto(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        StructureEngineResult structure,
        VolatilityRegimeEngineResult volatility,
        (string PatternType, string Reason) pattern)
    {
        // Crisis veto conditions:
        // 1. WATERFALL_CONTINUATION pattern
        if (pattern.PatternType == "WATERFALL_CONTINUATION")
        {
            return (true, "Crisis veto: WATERFALL_CONTINUATION pattern detected");
        }
        
        // 2. FAIL threatened or broken
        if (structure.Fail.HasValue && indicators.AdrUsedRatio >= 0.85m)
        {
            return (true, "Crisis veto: FAIL threatened or broken");
        }
        
        // 3. Panic drop sequence
        if (snapshot.HasPanicDropSequence || snapshot.PanicSuspected)
        {
            return (true, "Crisis veto: Panic drop sequence detected");
        }
        
        // 4. Extreme volatility with structural breakdown
        if (volatility.VolatilityClass == "EXTREME" && !structure.HasShelf && !structure.HasReclaim)
        {
            return (true, "Crisis veto: Extreme volatility with structural breakdown");
        }
        
        // 5. Geopolitical shock with expansion
        if (snapshot.GeoRiskFlag && indicators.IsExpansion && indicators.AdrUsedRatio > 0.7m)
        {
            return (true, "Crisis veto: Geopolitical shock with expansion");
        }
        
        return (false, "No crisis veto conditions met");
    }

    private static string DetermineWaterfallRisk(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        StructureEngineResult structure,
        (string PatternType, string Reason) pattern)
    {
        var riskScore = 0;
        
        // HIGH risk signals
        if (pattern.PatternType == "WATERFALL_CONTINUATION")
        {
            riskScore += 3;
        }
        
        if (structure.Fail.HasValue && indicators.AdrUsedRatio >= 0.85m)
        {
            riskScore += 2;
        }
        
        if (snapshot.HasPanicDropSequence)
        {
            riskScore += 2;
        }
        
        // MEDIUM risk signals
        if (indicators.IsExpansion && indicators.IsAtrExpanding && indicators.RsiH1 < 40m)
        {
            riskScore += 1;
        }
        
        if (!structure.HasReclaim && structure.HasSweep)
        {
            riskScore += 1;
        }
        
        if (indicators.AdrUsedRatio > 0.75m && !structure.HasShelf)
        {
            riskScore += 1;
        }
        
        // Determine risk level
        if (riskScore >= 3)
        {
            return "HIGH";
        }
        
        if (riskScore >= 1)
        {
            return "MEDIUM";
        }
        
        return "LOW";
    }
}

/// <summary>
/// Waterfall/Crisis Engine output contract
/// </summary>
public sealed record WaterfallCrisisEngineResult(
    string PatternType,
    string PatternReason,
    bool CrisisVeto,
    string CrisisReason,
    string WaterfallRisk,
    bool ShouldBlock);
