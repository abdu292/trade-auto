using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Candidate Engine per spec/00_instructions and spec/03_decision_state_machine.md
/// Manages candidate lifecycle: NONE, FORMING, ZONE_WATCH_ACTIVE, EARLY_FLUSH_CANDIDATE, CANDIDATE, ARMED, etc.
/// </summary>
public static class CandidateEngine
{
    public record CandidateResult(
        string State,  // NONE, FORMING, ZONE_WATCH_ACTIVE, EARLY_FLUSH_CANDIDATE, CANDIDATE, ARMED, etc.
        string? PreviousState,
        bool CanPromote,
        string? PromotionReason,
        string? BlockReason
    );

    /// <summary>
    /// Updates candidate state based on current market conditions.
    /// </summary>
    public static CandidateResult UpdateCandidate(
        string? currentState,
        MarketSnapshotContract snapshot,
        RuleEngine.StructureResult structureResult,
        string waterfallRisk,
        VerifyEngine.VerifyResult verifyResult,
        HistoricalPatternEngine.HistoricalPatternResult historicalResult)
    {
        var state = currentState ?? "NONE";

        // Hard blockers: cannot promote if blocked
        if (waterfallRisk == "HIGH" || structureResult.FailBroken)
        {
            return new CandidateResult(
                "INVALIDATED",
                state,
                false,
                null,
                waterfallRisk == "HIGH" ? "Waterfall risk HIGH" : "FAIL broken");
        }

        // State transitions
        if (state == "NONE")
        {
            // Check if structure is forming
            if (structureResult.S1.HasValue || structureResult.S2.HasValue)
            {
                return new CandidateResult(
                    "FORMING",
                    state,
                    true,
                    "Structure detected",
                    null);
            }
        }
        else if (state == "FORMING")
        {
            // Check if price is approaching zone
            if (structureResult.S1.HasValue && snapshot.Bid <= structureResult.S1.Value * 1.002m)
            {
                return new CandidateResult(
                    "ZONE_WATCH_ACTIVE",
                    state,
                    true,
                    "Price approaching S1 zone",
                    null);
            }
            if (structureResult.S2.HasValue && snapshot.Bid <= structureResult.S2.Value * 1.002m)
            {
                return new CandidateResult(
                    "ZONE_WATCH_ACTIVE",
                    state,
                    true,
                    "Price approaching S2 zone",
                    null);
            }
        }
        else if (state == "ZONE_WATCH_ACTIVE")
        {
            // Check if flush into shelf detected
            if (snapshot.HasLiquiditySweep &&
                structureResult.S2.HasValue &&
                snapshot.Bid <= structureResult.S2.Value &&
                !structureResult.FailBroken &&
                waterfallRisk != "HIGH")
            {
                return new CandidateResult(
                    "EARLY_FLUSH_CANDIDATE",
                    state,
                    true,
                    "Flush into shelf detected without FAIL break",
                    null);
            }
        }
        else if (state == "EARLY_FLUSH_CANDIDATE")
        {
            // Check if next candles hold lows / rejection confirms
            if (snapshot.IsCompression && snapshot.HasOverlapCandles)
            {
                // Calculate projected move
                var projectedMove = structureResult.S2.HasValue
                    ? (structureResult.R1 ?? snapshot.Bid * 1.01m) - structureResult.S2.Value
                    : 0m;

                if (projectedMove >= 8.0m)
                {
                    return new CandidateResult(
                        "CANDIDATE",
                        state,
                        true,
                        "Rejection confirmed, projected move >= 8 USD",
                        null);
                }
            }
        }
        else if (state == "CANDIDATE")
        {
            // Can promote to ARMED if all conditions met
            // (This would be done after TABLE compilation)
            return new CandidateResult(
                state,
                state,
                false,
                null,
                "Waiting for TABLE compilation");
        }
        else if (state == "OVEREXTENDED")
        {
            // Check if can requalify
            if (snapshot.IsCompression &&
                structureResult.S1.HasValue &&
                !structureResult.FailBroken &&
                waterfallRisk != "HIGH")
            {
                var projectedMove = (structureResult.R1 ?? snapshot.Bid * 1.01m) - structureResult.S1.Value;
                if (projectedMove >= 8.0m)
                {
                    return new CandidateResult(
                        "REQUALIFIED",
                        state,
                        true,
                        "Price cooled, shelf rebuilt, reward >= 8 USD",
                        null);
                }
            }
        }

        // Default: maintain current state
        return new CandidateResult(
            state,
            state,
            false,
            null,
            null);
    }
}