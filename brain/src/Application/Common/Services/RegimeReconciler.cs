using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// PATCH 2 — MASTER REGIME RECONCILIATION
/// Single reconciler after all regime engines and before candidate/path logic.
/// Precedence: 1) legality/hazard/crisis/waterfall 2) structure 3) session-quality 4) candidate/path.
/// </summary>
public static class RegimeReconciler
{
    /// <summary>
    /// Reconciles session regime gate, market regime, gold regime, pattern detector (entrySafety BLOCKED),
    /// and volatility into one master regime and tradeability.
    /// </summary>
    public static RegimeReconciledResult Reconcile(
        RegimeClassificationContract sessionRegime,
        MarketRegimeResult marketRegime,
        GoldRegimeResult goldRegime,
        IReadOnlyCollection<PatternDetectionResult> patternResults,
        bool volatilityShouldBlock,
        string waterfallRiskFromRegime,
        bool hazardWindowBlocked)
    {
        var downgradedSignals = new List<string>();
        string? dominantBlocker = null;
        string masterRegime = sessionRegime.Regime;
        bool finalTradeability = sessionRegime.IsBlocked == false && marketRegime.IsTradeable && goldRegime.IsTradeable;

        // 1) Hazard / legality
        if (hazardWindowBlocked)
        {
            dominantBlocker = "HAZARD_WINDOW";
            finalTradeability = false;
            downgradedSignals.Add("session_regime");
            downgradedSignals.Add("market_regime");
            downgradedSignals.Add("gold_regime");
        }

        // 2) Session regime block (e.g. Friday late NY)
        if (sessionRegime.IsBlocked)
        {
            dominantBlocker ??= "SESSION_REGIME";
            finalTradeability = false;
            masterRegime = sessionRegime.Regime;
            downgradedSignals.Add("market_regime");
            downgradedSignals.Add("gold_regime");
        }

        // 3) Pattern detector: any BLOCKED entrySafety is authoritative
        var patternBlocked = patternResults.Any(p =>
            string.Equals(p.PatternType.ToString(), "WaterfallRisk", StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.EntrySafety, "BLOCKED", StringComparison.OrdinalIgnoreCase));
        if (patternBlocked)
        {
            dominantBlocker ??= "WATERFALL_PATTERN";
            finalTradeability = false;
            downgradedSignals.Add("market_regime");
            downgradedSignals.Add("gold_regime");
        }

        // 4) Waterfall risk HIGH from regime layer
        if (string.Equals(waterfallRiskFromRegime, "HIGH", StringComparison.OrdinalIgnoreCase))
        {
            dominantBlocker ??= "WATERFALL_REGIME";
            finalTradeability = false;
        }

        // 5) Market regime not tradeable
        if (!marketRegime.IsTradeable && dominantBlocker == null)
        {
            dominantBlocker = "MARKET_REGIME";
            finalTradeability = false;
            downgradedSignals.Add("gold_regime");
        }

        // 6) Gold regime not tradeable
        if (!goldRegime.IsTradeable && dominantBlocker == null)
        {
            dominantBlocker = "GOLD_REGIME";
            finalTradeability = false;
        }

        // 7) Volatility block
        if (volatilityShouldBlock && dominantBlocker == null)
        {
            dominantBlocker = "VOLATILITY";
            finalTradeability = false;
        }

        var finalReasonCode = dominantBlocker ?? (finalTradeability ? "ALLOWED" : "CAUTION");
        return new RegimeReconciledResult(
            MasterRegime: masterRegime,
            FinalTradeability: finalTradeability,
            DominantBlocker: dominantBlocker,
            DowngradedSignals: downgradedSignals,
            FinalReasonCode: finalReasonCode,
            PatternBlocked: patternBlocked,
            HazardBlocked: hazardWindowBlocked);
    }
}

/// <summary>Result of regime reconciliation for use by candidate/path and ENGINE_STATES_ACTIVE.</summary>
public sealed record RegimeReconciledResult(
    string MasterRegime,
    bool FinalTradeability,
    string? DominantBlocker,
    IReadOnlyList<string> DowngradedSignals,
    string FinalReasonCode,
    bool PatternBlocked,
    bool HazardBlocked);
