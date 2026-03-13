using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 15: FINAL DECISION ENGINE
/// Purpose: Produces only YES, NO, or WAIT with reason codes.
/// 
/// Aims for:
/// - Capital protected
/// - Armed
/// - Notrade
/// </summary>
public static class FinalDecisionEngine
{
    public static FinalDecisionEngineResult Decide(
        CandidateEngineResult candidate,
        TableCompilerResult table,
        ValidateEngineResult validate,
        HardLegalityEngineResult legality,
        WaterfallCrisisEngineResult waterfall,
        NewsEngineResult news)
    {
        // Hard blockers: if any hard blocker is active, NO
        if (!legality.IsLegal)
        {
            return new FinalDecisionEngineResult(
                Decision: "NO",
                Reason: $"Hard legality blocked: {string.Join(", ", legality.BlockingChecks.Select(c => c.Reason))}",
                ReasonCode: "HARD_LEGALITY_BLOCK",
                EngineState: "CAPITAL_PROTECTED",
                DecisionResult: null);
        }
        
        if (waterfall.ShouldBlock)
        {
            return new FinalDecisionEngineResult(
                Decision: "NO",
                Reason: $"Waterfall/crisis blocked: {waterfall.CrisisReason}",
                ReasonCode: "WATERFALL_BLOCK",
                EngineState: "CAPITAL_PROTECTED",
                DecisionResult: null);
        }
        
        if (news.OverallMODE == "BLOCK")
        {
            return new FinalDecisionEngineResult(
                Decision: "NO",
                Reason: "News mode blocked",
                ReasonCode: "NEWS_BLOCK",
                EngineState: "CAPITAL_PROTECTED",
                DecisionResult: null);
        }
        
        // Validation check
        if (!validate.IsValid)
        {
            return new FinalDecisionEngineResult(
                Decision: "NO",
                Reason: $"Validation failed: {validate.Reason}",
                ReasonCode: "VALIDATION_FAILED",
                EngineState: "CAPITAL_PROTECTED",
                DecisionResult: null);
        }
        
        // Table check
        if (!table.IsValid || validate.ValidatedOrder == null)
        {
            return new FinalDecisionEngineResult(
                Decision: "NO",
                Reason: "Table compilation failed",
                ReasonCode: "TABLE_FAILED",
                EngineState: "CAPITAL_PROTECTED",
                DecisionResult: null);
        }
        
        // Candidate check
        if (!candidate.CanPromoteToArmed && candidate.NextState != "ARMED")
        {
            return new FinalDecisionEngineResult(
                Decision: "WAIT",
                Reason: $"Candidate not ready: {candidate.NextState}",
                ReasonCode: "CANDIDATE_NOT_READY",
                EngineState: "WAITING",
                DecisionResult: null);
        }
        
        // All checks passed: YES
        var decisionResult = BuildDecisionResult(validate.ValidatedOrder, candidate, news);
        
        return new FinalDecisionEngineResult(
            Decision: "YES",
            Reason: "All gates passed, trade allowed",
            ReasonCode: "ALL_GATES_PASSED",
            EngineState: "ARMED",
            DecisionResult: decisionResult);
    }

    private static DecisionResultContract BuildDecisionResult(
        TableCompilerResult table,
        CandidateEngineResult candidate,
        NewsEngineResult news)
    {
        // Build DecisionResultContract from validated table
        // This is a simplified version - actual would map all fields properly
        
        return new DecisionResultContract(
            IsTradeAllowed: true,
            Status: "ARMED",
            EngineState: "ARMED",
            Mode: table.ProjectedMoveNetUSD > 20m ? "IMPULSE" : "STANDARD",
            Cause: "TABLE_COMPILED",
            WaterfallRisk: news.WaterfallRisk,
            Reason: "Final decision: YES",
            Bucket: "C1",
            Rail: table.OrderType ?? "BUY_LIMIT",
            Session: "UNKNOWN", // Would be set from session engine
            SessionPhase: "UNKNOWN", // Would be set from session engine
            RegimeTag: "STANDARD",
            RiskState: "SAFE",
            SizeClass: "STANDARD",
            Entry: table.Entry ?? 0m,
            Tp: table.Tp ?? 0m,
            Grams: table.Grams ?? 0m,
            ExpiryUtc: table.ExpiryUtc ?? DateTimeOffset.UtcNow,
            MaxLifeSeconds: table.ExpiryUtc.HasValue 
                ? (int)(table.ExpiryUtc.Value - DateTimeOffset.UtcNow).TotalSeconds 
                : 0,
            AlignmentScore: 0.75m, // Would be calculated
            TelegramState: "QUIET",
            RailPermissionA: news.RailAPermission,
            RailPermissionB: news.RailBPermission,
            RotationCapThisSession: 2);
    }
}

/// <summary>
/// Final Decision Engine output contract
/// </summary>
public sealed record FinalDecisionEngineResult(
    string Decision, // YES, NO, WAIT
    string Reason,
    string ReasonCode,
    string EngineState, // CAPITAL_PROTECTED, ARMED, WAITING, NOTRADE
    DecisionResultContract? DecisionResult);
