using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 2: INDICATOR ENGINE
/// Purpose: Calculates and uses various technical indicators (ATR, RSI, MA20, Bands, ADR used),
/// along with Compression/overlap and session highs-lows.
/// Produces local deterministic features used by structure and volatility engines.
/// </summary>
public static class IndicatorEngine
{
    public static IndicatorEngineResult Calculate(MarketSnapshotContract snapshot, SessionEngineResult session)
    {
        // ATR values
        var atr = snapshot.Atr;
        var atrH1 = snapshot.AtrH1 > 0m ? snapshot.AtrH1 : atr;
        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : atr;
        var atrM5 = snapshot.TimeframeData
            .FirstOrDefault(tf => string.Equals(tf.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.Atr ?? 0m;

        // RSI values
        var rsiH1 = snapshot.RsiH1;
        var rsiM15 = snapshot.RsiM15;
        var rsiM5 = snapshot.TimeframeData
            .FirstOrDefault(tf => string.Equals(tf.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.Rsi ?? 0m;

        // MA20 values
        var ma20 = snapshot.Ma20;
        var ma20H4 = snapshot.Ma20H4;
        var ma20H1 = snapshot.Ma20H1;
        var ma20M30 = snapshot.Ma20M30;
        var ma20M5 = snapshot.Ma20M5;

        // ADR used
        var adrUsed = snapshot.AdrUsedPct / 100m; // Convert percentage to ratio
        var adr = snapshot.Adr;
        var adrUsedRatio = adr > 0m && snapshot.AdrUsedPct > 0m 
            ? snapshot.AdrUsedPct / 100m 
            : (adr > 0m ? atr / adr : 0m);

        // Compression/overlap behavior
        var compressionCountM15 = snapshot.CompressionCountM15;
        var compressionCountM5 = snapshot.CompressionCountM5;
        var isCompression = snapshot.IsCompression;
        var hasOverlapCandles = snapshot.HasOverlapCandles;
        var compressionRangesM15 = snapshot.CompressionRangesM15 ?? Array.Empty<decimal>();

        // Session highs-lows
        var sessionHigh = GetSessionHigh(snapshot, session.Session);
        var sessionLow = GetSessionLow(snapshot, session.Session);
        var previousSessionHigh = snapshot.PreviousSessionHigh;
        var previousSessionLow = snapshot.PreviousSessionLow;

        // Bands calculation (using MA20 as middle band, ATR for width)
        var upperBand = ma20 + (2m * atr);
        var lowerBand = ma20 - (2m * atr);
        var currentPrice = snapshot.Bid > 0m ? snapshot.Bid : 
            snapshot.TimeframeData.FirstOrDefault()?.Close ?? 0m;
        var bandPosition = currentPrice > 0m && ma20 > 0m
            ? (currentPrice - ma20) / (upperBand - lowerBand) * 2m // Normalized position: -1 to +1
            : 0m;

        // Volatility expansion/compression metrics
        var volatilityExpansion = snapshot.VolatilityExpansion;
        var isAtrExpanding = snapshot.IsAtrExpanding;
        var isExpansion = snapshot.IsExpansion;

        // Distance from MA20 (normalized by ATR)
        var ma20DistanceH1 = ma20H1 > 0m && currentPrice > 0m
            ? (currentPrice - ma20H1) / (atrH1 > 0m ? atrH1 : atr)
            : 0m;

        var ma20DistanceM15 = ma20M30 > 0m && currentPrice > 0m
            ? (currentPrice - ma20M30) / (atrM15 > 0m ? atrM15 : atr)
            : 0m;

        return new IndicatorEngineResult(
            Atr: atr,
            AtrH1: atrH1,
            AtrM15: atrM15,
            AtrM5: atrM5,
            RsiH1: rsiH1,
            RsiM15: rsiM15,
            RsiM5: rsiM5,
            Ma20: ma20,
            Ma20H4: ma20H4,
            Ma20H1: ma20H1,
            Ma20M30: ma20M30,
            Ma20M5: ma20M5,
            Adr: adr,
            AdrUsedRatio: adrUsedRatio,
            CompressionCountM15: compressionCountM15,
            CompressionCountM5: compressionCountM5,
            IsCompression: isCompression,
            HasOverlapCandles: hasOverlapCandles,
            CompressionRangesM15: compressionRangesM15,
            SessionHigh: sessionHigh,
            SessionLow: sessionLow,
            PreviousSessionHigh: previousSessionHigh,
            PreviousSessionLow: previousSessionLow,
            UpperBand: upperBand,
            LowerBand: lowerBand,
            BandPosition: bandPosition,
            VolatilityExpansion: volatilityExpansion,
            IsAtrExpanding: isAtrExpanding,
            IsExpansion: isExpansion,
            Ma20DistanceH1: ma20DistanceH1,
            Ma20DistanceM15: ma20DistanceM15);
    }

    private static decimal GetSessionHigh(MarketSnapshotContract snapshot, string session)
    {
        return session.ToUpperInvariant() switch
        {
            "JAPAN" => snapshot.SessionHighJapan > 0m ? snapshot.SessionHighJapan : snapshot.SessionHigh,
            "INDIA" => snapshot.SessionHighIndia > 0m ? snapshot.SessionHighIndia : snapshot.SessionHigh,
            "LONDON" => snapshot.SessionHighLondon > 0m ? snapshot.SessionHighLondon : snapshot.SessionHigh,
            "NEW_YORK" or "NY" => snapshot.SessionHighNy > 0m ? snapshot.SessionHighNy : snapshot.SessionHigh,
            _ => snapshot.SessionHigh
        };
    }

    private static decimal GetSessionLow(MarketSnapshotContract snapshot, string session)
    {
        return session.ToUpperInvariant() switch
        {
            "JAPAN" => snapshot.SessionLowJapan > 0m ? snapshot.SessionLowJapan : snapshot.SessionLow,
            "INDIA" => snapshot.SessionLowIndia > 0m ? snapshot.SessionLowIndia : snapshot.SessionLow,
            "LONDON" => snapshot.SessionLowLondon > 0m ? snapshot.SessionLowLondon : snapshot.SessionLow,
            "NEW_YORK" or "NY" => snapshot.SessionLowNy > 0m ? snapshot.SessionLowNy : snapshot.SessionLow,
            _ => snapshot.SessionLow
        };
    }
}

/// <summary>
/// Indicator Engine output contract
/// </summary>
public sealed record IndicatorEngineResult(
    decimal Atr,
    decimal AtrH1,
    decimal AtrM15,
    decimal AtrM5,
    decimal RsiH1,
    decimal RsiM15,
    decimal RsiM5,
    decimal Ma20,
    decimal Ma20H4,
    decimal Ma20H1,
    decimal Ma20M30,
    decimal Ma20M5,
    decimal Adr,
    decimal AdrUsedRatio,
    int CompressionCountM15,
    int CompressionCountM5,
    bool IsCompression,
    bool HasOverlapCandles,
    IReadOnlyCollection<decimal> CompressionRangesM15,
    decimal SessionHigh,
    decimal SessionLow,
    decimal PreviousSessionHigh,
    decimal PreviousSessionLow,
    decimal UpperBand,
    decimal LowerBand,
    decimal BandPosition,
    decimal VolatilityExpansion,
    bool IsAtrExpanding,
    bool IsExpansion,
    decimal Ma20DistanceH1,
    decimal Ma20DistanceM15);
