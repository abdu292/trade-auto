using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 11: CANDIDATE ENGINE
/// Purpose: Manages candidate lifecycle states from NONE to INVALIDATED.
/// 
/// States:
/// - NONE
/// - FORMING
/// - ZONE_WATCH_ACTIVE
/// - EARLY_FLUSH_CANDIDATE
/// - CANDIDATE
/// - ARMED
/// - PENDING_PLANTED
/// - FILLED
/// - PASSED
/// - OVEREXTENDED
/// - REQUALIFIED
/// - INVALIDATED
/// 
/// Important mapping rule:
/// - if pathState = OVEREXTENDED and reasonCode = WAITPULLBACKBASE but structure remains valid,
///   map this to ZONE_WATCH_ACTIVE rather than pure NOTRADE,
///   so the next flush can promote to EARLY_FLUSH_CANDIDATE instead of starting from zero
/// </summary>
public static class CandidateEngine
{
    public static CandidateEngineResult Process(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure,
        VolatilityRegimeEngineResult volatility,
        WaterfallCrisisEngineResult waterfall,
        NewsEngineResult news,
        CapitalUtilizationEngineResult capital,
        HistoricalPatternEngineResult historical,
        VerifyEngineResult verify,
        SessionEngineResult session)
    {
        // Get current candidate state (would be stored in state store)
        var currentState = "NONE"; // Default, actual would retrieve from store
        
        // Determine next state based on conditions
        var nextState = DetermineNextState(
            currentState,
            snapshot,
            structure,
            volatility,
            waterfall,
            news,
            capital,
            historical,
            verify,
            session);
        
        // Candidate freshness
        var freshness = DetermineFreshness(nextState, snapshot);
        
        return new CandidateEngineResult(
            CurrentState: currentState,
            NextState: nextState,
            CandidateFreshness: freshness,
            ZoneWatchActive: nextState == "ZONE_WATCH_ACTIVE",
            EarlyFlushCandidate: nextState == "EARLY_FLUSH_CANDIDATE",
            CanPromoteToArmed: nextState == "CANDIDATE" || nextState == "EARLY_FLUSH_CANDIDATE",
            Reason: $"State transition: {currentState} -> {nextState}");
    }

    private static string DetermineNextState(
        string currentState,
        MarketSnapshotContract snapshot,
        StructureEngineResult structure,
        VolatilityRegimeEngineResult volatility,
        WaterfallCrisisEngineResult waterfall,
        NewsEngineResult news,
        CapitalUtilizationEngineResult capital,
        HistoricalPatternEngineResult historical,
        VerifyEngineResult verify,
        SessionEngineResult session)
    {
        // Hard blockers: if waterfall/crisis blocks, invalidate
        if (waterfall.ShouldBlock || !capital.AffordableFlag)
        {
            return "INVALIDATED";
        }
        
        // Check for OVEREXTENDED -> ZONE_WATCH_ACTIVE mapping
        if (currentState == "OVEREXTENDED" && structure.StructureQuality != "WEAK")
        {
            // Structure remains valid, map to ZONE_WATCH_ACTIVE
            if (IsPriceApproachingZone(snapshot, structure))
            {
                return "ZONE_WATCH_ACTIVE";
            }
        }
        
        // FORMING: Structure beginning to form
        if (currentState == "NONE" && structure.HasShelf && !waterfall.ShouldBlock)
        {
            return "FORMING";
        }
        
        // ZONE_WATCH_ACTIVE: Price approaching valid S1/S2/S3 zone
        if ((currentState == "FORMING" || currentState == "NONE") 
            && IsPriceApproachingZone(snapshot, structure)
            && CanMeetRewardFloor(snapshot, structure))
        {
            return "ZONE_WATCH_ACTIVE";
        }
        
        // EARLY_FLUSH_CANDIDATE: Price flushes into defended deep shelf
        if ((currentState == "ZONE_WATCH_ACTIVE" || currentState == "FORMING")
            && structure.HasSweep
            && structure.HasShelf
            && !waterfall.ShouldBlock
            && structure.S2.HasValue)
        {
            return "EARLY_FLUSH_CANDIDATE";
        }
        
        // CANDIDATE: Next candles hold lows / rejection confirms
        if ((currentState == "EARLY_FLUSH_CANDIDATE" || currentState == "ZONE_WATCH_ACTIVE")
            && structure.HasReclaim
            && CanMeetRewardFloor(snapshot, structure))
        {
            return "CANDIDATE";
        }
        
        // ARMED: Only when rail permissions and TABLE legality pass
        // This is set by TABLE compiler, not here
        
        // OVEREXTENDED: Price moved too far from base
        if (currentState != "NONE" && currentState != "INVALIDATED")
        {
            if (IsOverextended(snapshot, structure))
            {
                return "OVEREXTENDED";
            }
        }
        
        // Keep current state if no transition
        return currentState;
    }

    private static bool IsPriceApproachingZone(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure)
    {
        var currentPrice = snapshot.Bid > 0m ? snapshot.Bid : 
            snapshot.TimeframeData.FirstOrDefault()?.Close ?? 0m;
        
        if (currentPrice <= 0m) return false;
        
        // Check if price is approaching S1, S2, or S3
        var atr = snapshot.Atr > 0m ? snapshot.Atr : 10m; // Default ATR
        var proximity = atr * 0.5m; // Within 0.5 ATR
        
        if (structure.S1 > 0m && Math.Abs(currentPrice - structure.S1) <= proximity)
        {
            return true;
        }
        
        if (structure.S2.HasValue && Math.Abs(currentPrice - structure.S2.Value) <= proximity)
        {
            return true;
        }
        
        if (structure.S3.HasValue && Math.Abs(currentPrice - structure.S3.Value) <= proximity)
        {
            return true;
        }
        
        return false;
    }

    private static bool CanMeetRewardFloor(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure)
    {
        var currentPrice = snapshot.Bid > 0m ? snapshot.Bid : 
            snapshot.TimeframeData.FirstOrDefault()?.Close ?? 0m;
        
        if (currentPrice <= 0m) return false;
        
        // Check if projected move from entry zone to TP can meet +8 USD minimum
        var entryZone = structure.S2.HasValue && structure.S2.Value < structure.S1 
            ? structure.S2.Value 
            : structure.S1;
        
        if (entryZone <= 0m) return false;
        
        // Estimate TP (would use R1 or structure-based TP)
        var tp = structure.R1 > 0m ? structure.R1 : currentPrice + 12m;
        
        // Projected move (accounting for spread and bullion handicap)
        var projectedMove = tp - entryZone;
        var netMove = projectedMove - 0.80m - 0.80m; // Shop spread both ways
        
        return netMove >= 8m; // Minimum +8 USD
    }

    private static bool IsOverextended(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure)
    {
        var currentPrice = snapshot.Bid > 0m ? snapshot.Bid : 
            snapshot.TimeframeData.FirstOrDefault()?.Close ?? 0m;
        
        if (currentPrice <= 0m || structure.S1 <= 0m) return false;
        
        var atr = snapshot.Atr > 0m ? snapshot.Atr : 10m;
        var distanceFromBase = currentPrice - structure.S1;
        
        // Overextended if price is more than 1.0 ATR above base
        return distanceFromBase > atr * 1.0m;
    }

    private static string DetermineFreshness(string state, MarketSnapshotContract snapshot)
    {
        // FRESH: Recently formed
        // AGING: State held for some time
        // STALE: State held too long
        
        if (state == "NONE" || state == "INVALIDATED")
        {
            return "NONE";
        }
        
        // Simplified: would check timestamp of state creation
        // For now, assume FRESH for new states
        return "FRESH";
    }
}

/// <summary>
/// Candidate Engine output contract
/// </summary>
public sealed record CandidateEngineResult(
    string CurrentState,
    string NextState,
    string CandidateFreshness,
    bool ZoneWatchActive,
    bool EarlyFlushCandidate,
    bool CanPromoteToArmed,
    string Reason);
