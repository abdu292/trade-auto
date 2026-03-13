using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 3: STRUCTURE ENGINE
/// Purpose: Builds S1, S2, R1, R2, FAIL levels and detects shelf, lid, sweep, reclaim, mid-air zones.
/// This is the geometric truth layer of the system.
/// </summary>
public static class StructureEngine
{
    public static StructureEngineResult Analyze(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        SessionEngineResult session)
    {
        var currentPrice = snapshot.Bid > 0m ? snapshot.Bid : 
            snapshot.TimeframeData.FirstOrDefault()?.Close ?? 0m;

        // S1 = Base shelf (session low or previous session low)
        var s1 = CalculateS1(snapshot, indicators, session);
        
        // S2 = Deeper sweep pocket (previous session low or previous day low)
        var s2 = CalculateS2(snapshot, s1);
        
        // S3 = Deepest exhaustion pocket (previous day low or weekly low)
        var s3 = CalculateS3(snapshot, s2 ?? s1);
        
        // R1 = First resistance (session high or previous session high)
        var r1 = CalculateR1(snapshot, indicators, session);
        
        // R2 = Second resistance (previous session high or previous day high)
        var r2 = CalculateR2(snapshot, r1);
        
        // FAIL = ADR exhaustion level (when ADR used >= 85%)
        var fail = CalculateFail(snapshot, indicators, currentPrice);
        
        // Shelf detection (defended base level)
        var shelf = DetectShelf(snapshot, indicators, s1, s2);
        
        // Lid detection (compression resistance level)
        var lid = DetectLid(snapshot, indicators, r1, r2);
        
        // Sweep detection (liquidity sweep)
        var sweep = DetectSweep(snapshot, indicators, s1, s2);
        
        // Reclaim detection (price reclaims after sweep)
        var reclaim = DetectReclaim(snapshot, indicators, sweep, s1);
        
        // Mid-air zone detection (price between structure levels, no clear base)
        var midAir = DetectMidAir(snapshot, indicators, currentPrice, s1, r1);
        
        return new StructureEngineResult(
            S1: s1,
            S2: s2,
            S3: s3,
            R1: r1,
            R2: r2,
            Fail: fail,
            HasShelf: shelf.HasShelf,
            ShelfLevel: shelf.ShelfLevel,
            HasLid: lid.HasLid,
            LidLevel: lid.LidLevel,
            HasSweep: sweep.HasSweep,
            SweepLevel: sweep.SweepLevel,
            HasReclaim: reclaim.HasReclaim,
            ReclaimLevel: reclaim.ReclaimLevel,
            IsMidAir: midAir.IsMidAir,
            MidAirZone: midAir.MidAirZone,
            StructureQuality: DetermineStructureQuality(shelf, lid, sweep, reclaim, midAir));
    }

    private static decimal CalculateS1(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        SessionEngineResult session)
    {
        // S1 = Base shelf: current session low or previous session low
        var sessionLow = indicators.SessionLow;
        var previousSessionLow = indicators.PreviousSessionLow;
        
        // Prefer current session low if valid, otherwise previous
        if (sessionLow > 0m)
        {
            return sessionLow;
        }
        
        if (previousSessionLow > 0m)
        {
            return previousSessionLow;
        }
        
        // Fallback: use previous day low
        return snapshot.PreviousDayLow > 0m ? snapshot.PreviousDayLow : 0m;
    }

    private static decimal? CalculateS2(MarketSnapshotContract snapshot, decimal s1)
    {
        // S2 = Deeper sweep pocket: previous session low or previous day low (must be below S1)
        var candidates = new List<decimal>();
        
        if (snapshot.PreviousSessionLow > 0m && snapshot.PreviousSessionLow < s1)
        {
            candidates.Add(snapshot.PreviousSessionLow);
        }
        
        if (snapshot.PreviousDayLow > 0m && snapshot.PreviousDayLow < s1)
        {
            candidates.Add(snapshot.PreviousDayLow);
        }
        
        return candidates.Count > 0 ? candidates.Min() : null;
    }

    private static decimal? CalculateS3(MarketSnapshotContract snapshot, decimal s2)
    {
        // S3 = Deepest exhaustion pocket: previous day low or weekly low (must be below S2)
        var candidates = new List<decimal>();
        
        if (snapshot.PreviousDayLow > 0m && snapshot.PreviousDayLow < s2)
        {
            candidates.Add(snapshot.PreviousDayLow);
        }
        
        if (snapshot.WeeklyLow > 0m && snapshot.WeeklyLow < s2)
        {
            candidates.Add(snapshot.WeeklyLow);
        }
        
        return candidates.Count > 0 ? candidates.Min() : null;
    }

    private static decimal CalculateR1(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        SessionEngineResult session)
    {
        // R1 = First resistance: current session high or previous session high
        var sessionHigh = indicators.SessionHigh;
        var previousSessionHigh = indicators.PreviousSessionHigh;
        
        if (sessionHigh > 0m)
        {
            return sessionHigh;
        }
        
        if (previousSessionHigh > 0m)
        {
            return previousSessionHigh;
        }
        
        // Fallback: use previous day high
        return snapshot.PreviousDayHigh > 0m ? snapshot.PreviousDayHigh : 0m;
    }

    private static decimal? CalculateR2(MarketSnapshotContract snapshot, decimal r1)
    {
        // R2 = Second resistance: previous session high or previous day high (must be above R1)
        var candidates = new List<decimal>();
        
        if (snapshot.PreviousSessionHigh > 0m && snapshot.PreviousSessionHigh > r1)
        {
            candidates.Add(snapshot.PreviousSessionHigh);
        }
        
        if (snapshot.PreviousDayHigh > 0m && snapshot.PreviousDayHigh > r1)
        {
            candidates.Add(snapshot.PreviousDayHigh);
        }
        
        return candidates.Count > 0 ? candidates.Max() : null;
    }

    private static decimal? CalculateFail(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        decimal currentPrice)
    {
        // FAIL = ADR exhaustion level (when ADR used >= 85%)
        // FAIL is threatened when price has moved too far relative to ADR
        if (indicators.AdrUsedRatio >= 0.85m && indicators.Adr > 0m)
        {
            // FAIL level is approximately at the extreme of the ADR range
            // For buy-only system, FAIL is typically above current price (overextension)
            var failDistance = indicators.Adr * indicators.AdrUsedRatio;
            return currentPrice + failDistance;
        }
        
        return null;
    }

    private static (bool HasShelf, decimal ShelfLevel) DetectShelf(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        decimal s1,
        decimal? s2)
    {
        // Shelf = defended base level with compression/overlap candles
        var hasShelf = (indicators.HasOverlapCandles || indicators.IsCompression)
            && (snapshot.HasLiquiditySweep || snapshot.TvAlertType is "SHELF_RECLAIM" or "RETEST_HOLD");
        
        var shelfLevel = s2.HasValue && s2.Value < s1 ? s2.Value : s1;
        
        return (hasShelf && shelfLevel > 0m, shelfLevel);
    }

    private static (bool HasLid, decimal LidLevel) DetectLid(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        decimal r1,
        decimal? r2)
    {
        // Lid = compression resistance level with breakout potential
        var hasLid = indicators.IsCompression 
            && indicators.CompressionCountM15 >= 3
            && (snapshot.IsBreakoutConfirmed || snapshot.TvAlertType is "LID_BREAK" or "BREAKOUT");
        
        var lidLevel = r1;
        
        return (hasLid && lidLevel > 0m, lidLevel);
    }

    private static (bool HasSweep, decimal SweepLevel) DetectSweep(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        decimal s1,
        decimal? s2)
    {
        // Sweep = liquidity sweep into support level
        var hasSweep = snapshot.HasLiquiditySweep;
        var sweepLevel = s2.HasValue && s2.Value < s1 ? s2.Value : s1;
        
        return (hasSweep && sweepLevel > 0m, sweepLevel);
    }

    private static (bool HasReclaim, decimal ReclaimLevel) DetectReclaim(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        (bool HasSweep, decimal SweepLevel) sweep,
        decimal s1)
    {
        // Reclaim = price reclaims after sweep (closes back above swept level)
        var hasReclaim = sweep.HasSweep 
            && (indicators.HasOverlapCandles || snapshot.TvAlertType is "SHELF_RECLAIM" or "RETEST_HOLD");
        
        var reclaimLevel = sweep.SweepLevel > 0m ? sweep.SweepLevel : s1;
        
        return (hasReclaim && reclaimLevel > 0m, reclaimLevel);
    }

    private static (bool IsMidAir, string MidAirZone) DetectMidAir(
        MarketSnapshotContract snapshot,
        IndicatorEngineResult indicators,
        decimal currentPrice,
        decimal s1,
        decimal r1)
    {
        // Mid-air = price between structure levels with no clear base or resistance
        if (currentPrice <= 0m || s1 <= 0m || r1 <= 0m)
        {
            return (false, "NONE");
        }
        
        // Price is mid-air if:
        // 1. Between S1 and R1 (no clear base or lid)
        // 2. No compression/base structure
        // 3. No sweep/reclaim
        var isBetweenLevels = currentPrice > s1 && currentPrice < r1;
        var noStructure = !indicators.HasOverlapCandles 
            && !indicators.IsCompression 
            && !snapshot.HasLiquiditySweep;
        
        if (isBetweenLevels && noStructure)
        {
            var distanceToS1 = currentPrice - s1;
            var distanceToR1 = r1 - currentPrice;
            var totalRange = r1 - s1;
            
            if (totalRange > 0m)
            {
                var position = distanceToS1 / totalRange;
                var zone = position < 0.33m ? "LOWER_MID_AIR" 
                    : position > 0.67m ? "UPPER_MID_AIR" 
                    : "MID_AIR";
                
                return (true, zone);
            }
        }
        
        return (false, "NONE");
    }

    private static string DetermineStructureQuality(
        (bool HasShelf, decimal ShelfLevel) shelf,
        (bool HasLid, decimal LidLevel) lid,
        (bool HasSweep, decimal SweepLevel) sweep,
        (bool HasReclaim, decimal ReclaimLevel) reclaim,
        (bool IsMidAir, string MidAirZone) midAir)
    {
        if (midAir.IsMidAir)
        {
            return "MID_AIR";
        }
        
        if (reclaim.HasReclaim && shelf.HasShelf)
        {
            return "STRONG";
        }
        
        if (shelf.HasShelf || lid.HasLid)
        {
            return "PROVISIONAL";
        }
        
        if (sweep.HasSweep)
        {
            return "SWEEP_ONLY";
        }
        
        return "WEAK";
    }
}

/// <summary>
/// Structure Engine output contract
/// </summary>
public sealed record StructureEngineResult(
    decimal S1,
    decimal? S2,
    decimal? S3,
    decimal R1,
    decimal? R2,
    decimal? Fail,
    bool HasShelf,
    decimal ShelfLevel,
    bool HasLid,
    decimal LidLevel,
    bool HasSweep,
    decimal SweepLevel,
    bool HasReclaim,
    decimal ReclaimLevel,
    bool IsMidAir,
    string MidAirZone,
    string StructureQuality);
