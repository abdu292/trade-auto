using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// STATE_06B_PATH_PROJECTION — converts deterministic engine output into displayable directional map.
/// For operator visibility only; not for execution. Feeds app UI (chart / where rates are heading).
/// </summary>
public static class PathProjectionEngine
{
    public static PathProjectionContract Project(
        MarketSnapshotContract snapshot,
        GoldEngineDecisionStackResult? decisionStackResult)
    {
        if (decisionStackResult is null)
        {
            return new PathProjectionContract(
                PathBias: "TWO_WAY",
                KeyMagnets: null,
                NextTestZone: null,
                InvalidationShelf: null,
                SessionTargetCorridor: null,
                ConfidenceBand: "LOW",
                SummaryLine: "No context — wait for structure.",
                NearestLegalBuyZone: null);
        }

        var pathState = decisionStackResult.PathState ?? string.Empty;
        var regime = decisionStackResult.MarketRegime?.Regime ?? string.Empty;
        var s1 = snapshot.SessionLow > 0m ? snapshot.SessionLow : (decimal?)null;
        var r1 = snapshot.SessionHigh > 0m ? snapshot.SessionHigh : (decimal?)null;
        var bid = snapshot.Bid;

        var pathBias = pathState switch
        {
            "BUY_LIMIT" => "UP",
            "BUY_STOP" => "UP",
            "WAIT_PULLBACK" => "TWO_WAY",
            "OVEREXTENDED" => "RANGE",
            "STAND_DOWN" => "TWO_WAY",
            _ => "TWO_WAY",
        };

        string? keyMagnets = null;
        if (s1.HasValue && r1.HasValue)
            keyMagnets = $"S1={s1.Value:0.0} R1={r1.Value:0.0}";

        string? nextTestZone = null;
        if (pathState == "BUY_LIMIT" && s1.HasValue)
            nextTestZone = $"Pullback to {s1.Value:0.0} zone";
        else if (pathState == "BUY_STOP" && r1.HasValue)
            nextTestZone = $"Break above {r1.Value:0.0}";

        string? invalidationShelf = null;
        if (s1.HasValue && pathState == "BUY_LIMIT")
            invalidationShelf = $"Below {s1.Value:0.0}";
        else if (r1.HasValue && pathState == "BUY_STOP")
            invalidationShelf = $"Fail above {r1.Value:0.0}";

        string? sessionTargetCorridor = null;
        if (r1.HasValue && s1.HasValue)
            sessionTargetCorridor = $"{s1.Value:0.0}-{r1.Value:0.0}";

        var confidenceBand = decisionStackResult.ConfidenceScore?.Tier ?? "WAIT";
        if (confidenceBand == "HIGH" || confidenceBand == "NORMAL") confidenceBand = "MEDIUM";
        else if (confidenceBand == "MICRO") confidenceBand = "LOW";

        var summaryLine = pathState switch
        {
            "BUY_LIMIT" when s1.HasValue => $"Likely drift to {s1.Value:0.0} then rejection risk",
            "BUY_STOP" when r1.HasValue => $"Reclaim path active above {r1.Value:0.0} shelf",
            "WAIT_PULLBACK" => "Wait pullback — no entry yet",
            "OVEREXTENDED" => "Late squeeze; continuation weak if lid not cleared",
            "STAND_DOWN" => "Stand down — no setup",
            _ => "Range — wait for structure",
        };

        var nearestLegalBuyZone = s1; // S1 base shelf = nearest legal buy zone for BUY_LIMIT path

        return new PathProjectionContract(
            PathBias: pathBias,
            KeyMagnets: keyMagnets,
            NextTestZone: nextTestZone,
            InvalidationShelf: invalidationShelf,
            SessionTargetCorridor: sessionTargetCorridor,
            ConfidenceBand: confidenceBand,
            SummaryLine: summaryLine,
            NearestLegalBuyZone: nearestLegalBuyZone);
    }
}
