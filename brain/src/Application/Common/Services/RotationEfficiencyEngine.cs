using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v8 §11 — Rotation Efficiency Engine.
/// Evaluates whether a potential trade is capital-efficient before placement.
/// Trades likely to trap capital across sessions must be rejected (CAPITAL_SLEEP_RISK).
///
/// Metrics:
///   • SameSessionTpProbability  — likelihood TP is reached within current session
///   • ExpectedAedReturn         — estimated AED profit given entry/TP/grams/rate
///   • ExpectedHoldTimeMinutes   — estimated time in trade (ATR-based projection)
///   • AedPerMinute              — capital efficiency ratio
///   • SleepRisk                 — true when trade is likely to extend beyond session
///
/// Efficiency states (§11):
///   HIGH              — fast rotation, high probability same-session close, strong AED/min
///   MEDIUM            — acceptable return, some risk of extension but manageable
///   LOW               — poor return or slow rotation but within acceptable bounds
///   CAPITAL_SLEEP_RISK — trade expected to extend beyond session; blocks entry
/// </summary>
public static class RotationEfficiencyEngine
{
    // AED per minute thresholds for efficiency classification
    private const decimal HighAedPerMinute = 0.8m;
    private const decimal MediumAedPerMinute = 0.3m;

    // Same-session TP probability thresholds
    private const decimal HighSameSessionProb = 0.70m;
    private const decimal MediumSameSessionProb = 0.40m;

    // Maximum acceptable hold time (minutes) before sleep risk activates
    private const int SleepRiskHoldMinutes = 180;

    // Session duration estimates in minutes (usable trading window)
    private static readonly Dictionary<string, int> SessionMinutes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "JAPAN", 240 },
        { "INDIA", 300 },
        { "LONDON", 360 },
        { "NY", 300 },
    };

    // AED conversion rate (1 USD ≈ 3.674 AED)
    private const decimal UsdToAed = 3.674m;

    // Troy ounce to gram conversion
    private const decimal TroyOzPerGram = 1m / 31.1035m;

    /// <summary>
    /// Compute the rotation efficiency for a proposed trade setup.
    /// </summary>
    /// <param name="snapshot">Current market snapshot with session and price data.</param>
    /// <param name="entry">Proposed entry price in USD.</param>
    /// <param name="tp">Proposed take-profit in USD.</param>
    /// <param name="grams">Proposed size in grams.</param>
    public static RotationEfficiencyResult Evaluate(
        MarketSnapshotContract snapshot,
        decimal entry,
        decimal tp,
        decimal grams)
    {
        if (entry <= 0m || tp <= 0m || grams <= 0m)
        {
            return new RotationEfficiencyResult(
                EfficiencyState: EfficiencyStates.Low,
                EfficiencyScore: 0,
                SameSessionTpProbability: 0m,
                ExpectedAedReturn: 0m,
                ExpectedHoldTimeMinutes: 0,
                AedPerMinute: 0m,
                SleepRisk: false,
                Reason: "No valid entry/TP/grams — efficiency cannot be computed.");
        }

        var session = (snapshot.Session ?? "NY").ToUpperInvariant();
        var atrH1 = snapshot.AtrH1 > 0m ? snapshot.AtrH1 : (snapshot.Atr > 0m ? snapshot.Atr : 3m);

        // ── Expected AED return ──────────────────────────────────────────────
        // TP distance (USD per oz) → USD per gram → AED
        var tpDistanceUsdPerOz = Math.Abs(tp - entry);
        var tpDistanceUsdPerGram = tpDistanceUsdPerOz * TroyOzPerGram;
        var expectedAedReturn = tpDistanceUsdPerGram * grams * UsdToAed;

        // ── Expected hold time ───────────────────────────────────────────────
        // Estimated as: (TP distance / ATR_H1) × 60 minutes per H1 candle
        var tpInAtrs = atrH1 > 0m ? tpDistanceUsdPerOz / atrH1 : 2m;
        var expectedHoldMinutes = (int)Math.Round(tpInAtrs * 60m);
        expectedHoldMinutes = Math.Clamp(expectedHoldMinutes, 5, 480);

        // ── AED per minute ───────────────────────────────────────────────────
        var aedPerMinute = expectedHoldMinutes > 0m
            ? expectedAedReturn / expectedHoldMinutes
            : 0m;

        // ── Sleep risk ───────────────────────────────────────────────────────
        // True when expected hold time is longer than session-specific threshold
        // or when hold time exceeds the remaining session window estimate.
        var sleepRisk = expectedHoldMinutes >= SleepRiskHoldMinutes;

        // Cross-session extension check: if ADR is near exhaustion, trades have
        // higher probability of holding overnight.
        if (snapshot.AdrUsedPct >= 0.85m)
            sleepRisk = true;

        // Friday + London/NY overlap increases sleep risk significantly
        if (snapshot.IsFriday && snapshot.IsLondonNyOverlap)
            sleepRisk = true;

        // ── Same-session TP probability ──────────────────────────────────────
        var sessionMinutesTotal = SessionMinutes.GetValueOrDefault(session, 240);
        var sameSessionTpProb = sleepRisk
            ? 0.15m
            : Math.Clamp(1m - (tpInAtrs / 3m), 0.1m, 0.95m);

        // Adjust for ADR: if most of the day's range is consumed, probability drops
        if (snapshot.AdrUsedPct > 0.8m)
            sameSessionTpProb *= 0.7m;

        // Adjust for compression: compressed market has higher continuation probability
        if (snapshot.IsCompression && snapshot.CompressionCountM15 >= 3)
            sameSessionTpProb = Math.Min(sameSessionTpProb + 0.10m, 0.95m);

        sameSessionTpProb = Math.Clamp(sameSessionTpProb, 0m, 1m);

        // ── Score ────────────────────────────────────────────────────────────
        var score = ComputeScore(aedPerMinute, sameSessionTpProb, sleepRisk, snapshot);

        // ── State ────────────────────────────────────────────────────────────
        string state;
        string reason;

        if (sleepRisk)
        {
            state = EfficiencyStates.CapitalSleepRisk;
            reason = $"CAPITAL_SLEEP_RISK: expectedHold={expectedHoldMinutes}min exceeds safe window or ADR exhausted. AedReturn={expectedAedReturn:0.00} AED.";
        }
        else if (aedPerMinute >= HighAedPerMinute && sameSessionTpProb >= HighSameSessionProb)
        {
            state = EfficiencyStates.High;
            reason = $"HIGH: AED/min={aedPerMinute:0.00}, sameSessionProb={sameSessionTpProb:0.00}, hold={expectedHoldMinutes}min. Efficient same-session rotation.";
        }
        else if (aedPerMinute >= MediumAedPerMinute && sameSessionTpProb >= MediumSameSessionProb)
        {
            state = EfficiencyStates.Medium;
            reason = $"MEDIUM: AED/min={aedPerMinute:0.00}, sameSessionProb={sameSessionTpProb:0.00}, hold={expectedHoldMinutes}min. Acceptable efficiency.";
        }
        else
        {
            state = EfficiencyStates.Low;
            reason = $"LOW: AED/min={aedPerMinute:0.00}, sameSessionProb={sameSessionTpProb:0.00}, hold={expectedHoldMinutes}min. Below efficiency threshold.";
        }

        return new RotationEfficiencyResult(
            EfficiencyState: state,
            EfficiencyScore: score,
            SameSessionTpProbability: Math.Round(sameSessionTpProb, 3),
            ExpectedAedReturn: Math.Round(expectedAedReturn, 2),
            ExpectedHoldTimeMinutes: expectedHoldMinutes,
            AedPerMinute: Math.Round(aedPerMinute, 3),
            SleepRisk: sleepRisk,
            Reason: reason);
    }

    private static int ComputeScore(
        decimal aedPerMinute,
        decimal sameSessionTpProb,
        bool sleepRisk,
        MarketSnapshotContract snapshot)
    {
        if (sleepRisk)
        {
            // Sleep-risk trades receive a hard-capped score of 5–20 to reflect capital efficiency
            // risk. A non-zero floor (5 pts) avoids ambiguity with completely invalid trades (0 pts).
            // Same-session probability contributes proportionally in case some chance still exists.
            return Math.Max(5, (int)(sameSessionTpProb * 20));
        }

        // Score components (total 100):
        // AED/min quality:          0–40 pts
        // Same-session probability: 0–40 pts
        // Session quality bonus:    0–10 pts
        // No ADR exhaustion bonus:  0–10 pts

        var aedPts = aedPerMinute >= HighAedPerMinute ? 40
            : aedPerMinute >= MediumAedPerMinute ? (int)(20 + (aedPerMinute - MediumAedPerMinute) / (HighAedPerMinute - MediumAedPerMinute) * 20)
            : (int)(aedPerMinute / MediumAedPerMinute * 20);

        var probPts = (int)(sameSessionTpProb * 40);

        var session = (snapshot.Session ?? "NY").ToUpperInvariant();
        var sessionPts = session is "LONDON" or "NY" ? 10 : (session is "INDIA" ? 7 : 5);

        var adrPts = snapshot.AdrUsedPct <= 0.7m ? 10
            : snapshot.AdrUsedPct <= 0.9m ? 5
            : 0;

        return Math.Clamp(aedPts + probPts + sessionPts + adrPts, 0, 100);
    }
}
