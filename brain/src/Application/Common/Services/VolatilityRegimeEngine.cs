using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 4: VOLATILITY/REGIME ENGINE
/// Purpose: Classifies market volatility and regime states (Range, Reload, Expansion, Exhaustion)
/// and classifies them as NORMAL/EXPANDED/EXTREME.
/// If volatility is EXTREME, new trades should normally be blocked.
/// </summary>
public static class VolatilityRegimeEngine
{
    public static VolatilityRegimeEngineResult Analyze(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        StructureEngineResult structure)
    {
        // Regime classification
        var regime = ClassifyRegime(snapshot, indicators, structure);
        
        // Volatility state
        var volatilityState = ClassifyVolatility(snapshot, indicators);
        
        // Volatility classification (NORMAL/EXPANDED/EXTREME)
        var volatilityClass = ClassifyVolatilityClass(indicators, volatilityState);
        
        return new VolatilityRegimeEngineResult(
            Regime: regime.Regime,
            RegimeReason: regime.Reason,
            VolatilityState: volatilityState,
            VolatilityClass: volatilityClass,
            ShouldBlockNewTrades: volatilityClass == "EXTREME" || regime.Regime == "EXHAUSTION");
    }

    private static (string Regime, string Reason) ClassifyRegime(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        StructureEngineResult structure)
    {
        // RANGE: Price consolidating, compression present, low ADR usage
        if (indicators.IsCompression 
            && indicators.CompressionCountM15 >= 3
            && indicators.AdrUsedRatio < 0.5m
            && !indicators.IsExpansion)
        {
            return ("RANGE", "Compression with low ADR usage");
        }
        
        // RANGE_RELOAD: Range with reload potential (compression + base forming)
        if (indicators.IsCompression
            && structure.HasShelf
            && indicators.AdrUsedRatio < 0.6m
            && snapshot.HasOverlapCandles)
        {
            return ("RANGE_RELOAD", "Range with shelf reload potential");
        }
        
        // CONTINUATION_REBUILD: Structure rebuilding after move
        if (structure.HasReclaim
            && indicators.HasOverlapCandles
            && indicators.CompressionCountM15 >= 2
            && indicators.AdrUsedRatio < 0.7m)
        {
            return ("CONTINUATION_REBUILD", "Structure rebuilding after reclaim");
        }
        
        // EXPANSION: ATR expanding, impulse candles, but not exhausted
        if (indicators.IsExpansion
            && indicators.IsAtrExpanding
            && snapshot.HasImpulseCandles
            && indicators.AdrUsedRatio < 0.85m
            && !snapshot.HasPanicDropSequence)
        {
            return ("EXPANSION", "ATR expanding with impulse, not exhausted");
        }
        
        // EXHAUSTION: ADR nearly used up, overextended
        if (indicators.AdrUsedRatio >= 0.85m
            || (indicators.RsiH1 >= 75m && indicators.IsAtrExpanding))
        {
            return ("EXHAUSTION", "ADR exhausted or extreme RSI with expansion");
        }
        
        // LIQUIDATION: Panic drop sequence or structural breakdown
        if (snapshot.HasPanicDropSequence
            || snapshot.PanicSuspected
            || (structure.Fail.HasValue && indicators.AdrUsedRatio >= 0.9m))
        {
            return ("LIQUIDATION", "Panic or structural breakdown");
        }
        
        // NEWS_SPIKE: News event with expansion
        if (snapshot.NewsEventFlag
            && indicators.IsExpansion
            && indicators.AdrUsedRatio > 0.5m)
        {
            return ("NEWS_SPIKE", "News event with expansion");
        }
        
        // SHOCK: Geopolitical or unexpected shock
        if (snapshot.GeoRiskFlag
            && (indicators.IsExpansion || snapshot.HasPanicDropSequence))
        {
            return ("SHOCK", "Geopolitical or unexpected shock");
        }
        
        // Default: RANGE
        return ("RANGE", "Default range classification");
    }

    private static string ClassifyVolatility(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators)
    {
        // COMPRESSED: Low ATR, compression present, low ADR usage
        if (indicators.IsCompression
            && indicators.CompressionCountM15 >= 4
            && indicators.AdrUsedRatio < 0.4m
            && !indicators.IsAtrExpanding)
        {
            return "COMPRESSED";
        }
        
        // NORMAL: Standard volatility, moderate ATR, normal ADR usage
        if (indicators.AdrUsedRatio >= 0.3m
            && indicators.AdrUsedRatio < 0.7m
            && !indicators.IsAtrExpanding
            && !indicators.IsExpansion)
        {
            return "NORMAL";
        }
        
        // EXPANSION: ATR expanding, impulse present
        if (indicators.IsAtrExpanding
            && indicators.IsExpansion
            && snapshot.HasImpulseCandles
            && indicators.AdrUsedRatio < 0.85m)
        {
            return "EXPANSION";
        }
        
        // EXHAUSTION: ADR nearly exhausted, extreme volatility
        if (indicators.AdrUsedRatio >= 0.85m
            || (indicators.RsiH1 >= 75m && indicators.IsAtrExpanding))
        {
            return "EXHAUSTION";
        }
        
        return "NORMAL";
    }

    private static string ClassifyVolatilityClass(
        IndicatorEngineResult indicators,
        string volatilityState)
    {
        if (volatilityState == "EXHAUSTION")
        {
            return "EXTREME";
        }
        
        if (volatilityState == "EXPANSION" && indicators.AdrUsedRatio >= 0.75m)
        {
            return "EXTREME";
        }
        
        if (volatilityState == "EXPANSION")
        {
            return "EXPANDED";
        }
        
        return "NORMAL";
    }
}

/// <summary>
/// Volatility/Regime Engine output contract
/// </summary>
public sealed record VolatilityRegimeEngineResult(
    string Regime,
    string RegimeReason,
    string VolatilityState,
    string VolatilityClass,
    bool ShouldBlockNewTrades);
