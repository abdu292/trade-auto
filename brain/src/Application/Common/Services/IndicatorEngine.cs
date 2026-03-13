using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Indicator Engine per spec/00_instructions
/// Position: after Session Engine
/// Purpose: calculate ATR, RSI, MA20, Bands, ADR used, overlap/compression behavior, session highs/lows
/// </summary>
public static class IndicatorEngine
{
    public record IndicatorResult(
        decimal Atr,
        decimal Rsi,
        decimal Ma20,
        decimal? UpperBand,
        decimal? LowerBand,
        decimal AdrUsed,
        decimal AdrUsedPct,
        bool IsCompression,
        bool HasOverlapCandles,
        decimal SessionHigh,
        decimal SessionLow,
        decimal CompressionCountM15,
        decimal CompressionCountM5,
        decimal ExpansionCountM15,
        decimal VolatilityExpansion
    );

    /// <summary>
    /// Calculates all technical indicators from market snapshot.
    /// </summary>
    public static IndicatorResult Calculate(MarketSnapshotContract snapshot)
    {
        // Extract values from snapshot (these should already be calculated by MT5/data provider)
        var atr = snapshot.Atr;
        var rsi = snapshot.RsiH1;  // Use H1 RSI as primary
        var ma20 = snapshot.Ma20;
        var adrUsed = snapshot.Adr;
        var adrUsedPct = snapshot.AdrUsedPct;

        // Calculate bands (simple: MA20 ± 2*ATR)
        var upperBand = ma20 + (2 * atr);
        var lowerBand = ma20 - (2 * atr);

        // Compression/overlap flags
        var isCompression = snapshot.IsCompression;
        var hasOverlapCandles = snapshot.HasOverlapCandles;

        // Session highs/lows
        var sessionHigh = GetSessionHigh(snapshot);
        var sessionLow = GetSessionLow(snapshot);

        // Compression/expansion counts
        var compressionCountM15 = snapshot.CompressionCountM15;
        var compressionCountM5 = snapshot.CompressionCountM5;
        var expansionCountM15 = snapshot.ExpansionCountM15;
        var volatilityExpansion = snapshot.VolatilityExpansion;

        return new IndicatorResult(
            atr,
            rsi,
            ma20,
            upperBand,
            lowerBand,
            adrUsed,
            adrUsedPct,
            isCompression,
            hasOverlapCandles,
            sessionHigh,
            sessionLow,
            compressionCountM15,
            compressionCountM5,
            expansionCountM15,
            volatilityExpansion
        );
    }

    private static decimal GetSessionHigh(MarketSnapshotContract snapshot)
    {
        return snapshot.Session switch
        {
            "JAPAN" => snapshot.SessionHighJapan > 0 ? snapshot.SessionHighJapan : snapshot.SessionHigh,
            "INDIA" => snapshot.SessionHighIndia > 0 ? snapshot.SessionHighIndia : snapshot.SessionHigh,
            "LONDON" => snapshot.SessionHighLondon > 0 ? snapshot.SessionHighLondon : snapshot.SessionHigh,
            "NEW_YORK" => snapshot.SessionHighNy > 0 ? snapshot.SessionHighNy : snapshot.SessionHigh,
            _ => snapshot.SessionHigh
        };
    }

    private static decimal GetSessionLow(MarketSnapshotContract snapshot)
    {
        return snapshot.Session switch
        {
            "JAPAN" => snapshot.SessionLowJapan > 0 ? snapshot.SessionLowJapan : snapshot.SessionLow,
            "INDIA" => snapshot.SessionLowIndia > 0 ? snapshot.SessionLowIndia : snapshot.SessionLow,
            "LONDON" => snapshot.SessionLowLondon > 0 ? snapshot.SessionLowLondon : snapshot.SessionLow,
            "NEW_YORK" => snapshot.SessionLowNy > 0 ? snapshot.SessionLowNy : snapshot.SessionLow,
            _ => snapshot.SessionLow
        };
    }
}