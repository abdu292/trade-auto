using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Final Decision Engine per spec/00_instructions
/// Position: after VALIDATE
/// Role: produce only YES, NO, WAIT with reason codes
/// </summary>
public static class FinalDecisionEngine
{
    public record FinalDecisionResult(
        string Decision,  // YES, NO, WAIT
        string ReasonCode,
        string? Message
    );

    /// <summary>
    /// Makes the final trade decision.
    /// </summary>
    public static FinalDecisionResult MakeDecision(
        ValidateEngine.ValidateResult validateResult,
        MarketSnapshotContract snapshot,
        NewsEngine.NewsEngineResult newsResult,
        AnalyzeResult analyzeResult,
        HistoricalPatternEngine.HistoricalPatternResult historicalResult)
    {
        // If validation failed, decision is NO
        if (!validateResult.IsValid)
        {
            return new FinalDecisionResult(
                "NO",
                validateResult.ReasonCode ?? "VALIDATION_FAILED",
                validateResult.RejectionReason);
        }

        // Check if we should WAIT instead of YES
        if (newsResult.HazardWindowActive && newsResult.NextTier1UltraWithin45m)
        {
            return new FinalDecisionResult(
                "WAIT",
                "HAZARD_WINDOW_ACTIVE",
                $"Hazard window active, next Tier1 event in {newsResult.MinutesToNextCleanWindow} minutes");
        }

        if (snapshot.SessionPhase == "START" && analyzeResult.ConfidenceScore < 0.7m)
        {
            return new FinalDecisionResult(
                "WAIT",
                "START_PHASE_LOW_CONFIDENCE",
                "START phase with low confidence, waiting for better structure");
        }

        if (historicalResult.HistoricalTrapProbability > 0.6m)
        {
            return new FinalDecisionResult(
                "WAIT",
                "HIGH_TRAP_PROBABILITY",
                $"Historical trap probability {historicalResult.HistoricalTrapProbability:F2} is too high");
        }

        // All checks passed: YES
        return new FinalDecisionResult(
            "YES",
            "ALL_CHECKS_PASSED",
            "All safety, legality, structure, and validation checks passed");
    }
}