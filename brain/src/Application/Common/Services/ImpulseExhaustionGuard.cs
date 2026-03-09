using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// CR11 — Impulse Exhaustion Guard.
/// Prevents late breakout chasing after vertical impulse moves.
///
/// Thresholds:
///   SAFE:    impulseDistancePoints ≤ 15  OR  impulseDistanceATR ≤ 1.2
///   CAUTION: 15 &lt; points ≤ 25           OR  1.2 &lt; ATR ≤ 2.0
///   BLOCK:   points > 25               OR  ATR > 2.0  OR  ≥3 expansion candles with weak momentum
///
/// Flags emitted: IMPULSE_EXHAUSTION_CAUTION, IMPULSE_EXHAUSTION_BLOCK.
/// </summary>
public static class ImpulseExhaustionGuard
{
    private const decimal SafePointsThreshold   = 15m;
    private const decimal CautionPointsMax      = 25m;
    private const decimal SafeAtrThreshold      = 1.2m;
    private const decimal CautionAtrMax         = 2.0m;
    private const int     ExpansionCandlesBlock = 3;

    // Multipliers used to approximate impulse distance from available snapshot data.
    // ImpulseStrength contributes 2× ATR; each expansion candle adds 0.5× ATR.
    private const decimal ImpulseStrengthAtrMultiplier  = 2.0m;
    private const decimal ExpansionCandleAtrContribution = 0.5m;

    // Fallback scale when ATR data is unavailable (maps ImpulseStrengthScore 0–1 to ≈0–20 pts)
    private const decimal FallbackScalePoints = 20m;

    // Minimum impulse strength score for a breakout to be considered strong
    private const decimal WeakBreakoutThreshold = 0.5m;

    /// <summary>
    /// Evaluates impulse exhaustion risk for the current market snapshot.
    /// </summary>
    public static ImpulseExhaustionResult Evaluate(MarketSnapshotContract snapshot)
    {
        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;

        // Impulse distance in USD points — approximated from expansion count × ATR contribution.
        // Each expansion candle contributes roughly one ATR_M15 worth of distance.
        // Capped approximation: use ImpulseStrengthScore × 2 × ATR_M15 as a proxy when
        // direct price-from-base data is not available in the snapshot.
        decimal impulseDistancePoints;
        if (atrM15 > 0m)
        {
            impulseDistancePoints = snapshot.ImpulseStrengthScore * ImpulseStrengthAtrMultiplier * atrM15
                + snapshot.ExpansionCountM15 * atrM15 * ExpansionCandleAtrContribution;
        }
        else
        {
            impulseDistancePoints = snapshot.ImpulseStrengthScore * FallbackScalePoints;
        }

        // Impulse distance expressed as multiples of ATR_M15
        var impulseDistanceAtr = atrM15 > 0m ? impulseDistancePoints / atrM15 : 0m;

        var expansionCandles = snapshot.ExpansionCountM15;

        var flags = new List<string>();

        // ── BLOCK conditions ─────────────────────────────────────────────────
        // ≥3 expansion candles with weak momentum confirmation
        var weakMomentum = !snapshot.IsBreakoutConfirmed && snapshot.ImpulseStrengthScore < WeakBreakoutThreshold;
        var blockByExpansion = expansionCandles >= ExpansionCandlesBlock && weakMomentum;

        if (impulseDistancePoints > CautionPointsMax || impulseDistanceAtr > CautionAtrMax || blockByExpansion)
        {
            flags.Add("IMPULSE_EXHAUSTION_BLOCK");
            var reason = impulseDistancePoints > CautionPointsMax
                ? $"BLOCK — ImpulsePoints={impulseDistancePoints:0.0} > {CautionPointsMax}"
                : impulseDistanceAtr > CautionAtrMax
                    ? $"BLOCK — ImpulseATR={impulseDistanceAtr:0.00} > {CautionAtrMax}"
                    : $"BLOCK — {expansionCandles} expansion candles with weak momentum (score={snapshot.ImpulseStrengthScore:0.00})";

            return new ImpulseExhaustionResult(
                Level: "BLOCK",
                ImpulseDistancePoints: impulseDistancePoints,
                ImpulseDistanceAtr: impulseDistanceAtr,
                ConsecutiveExpansionCandles: expansionCandles,
                Flags: flags,
                Reason: reason);
        }

        // ── SAFE conditions ──────────────────────────────────────────────────
        if (impulseDistancePoints <= SafePointsThreshold || impulseDistanceAtr <= SafeAtrThreshold)
        {
            return new ImpulseExhaustionResult(
                Level: "SAFE",
                ImpulseDistancePoints: impulseDistancePoints,
                ImpulseDistanceAtr: impulseDistanceAtr,
                ConsecutiveExpansionCandles: expansionCandles,
                Flags: flags,
                Reason: $"SAFE — ImpulsePoints={impulseDistancePoints:0.0}≤{SafePointsThreshold} or ATR={impulseDistanceAtr:0.00}≤{SafeAtrThreshold}");
        }

        // ── CAUTION ──────────────────────────────────────────────────────────
        flags.Add("IMPULSE_EXHAUSTION_CAUTION");
        return new ImpulseExhaustionResult(
            Level: "CAUTION",
            ImpulseDistancePoints: impulseDistancePoints,
            ImpulseDistanceAtr: impulseDistanceAtr,
            ConsecutiveExpansionCandles: expansionCandles,
            Flags: flags,
            Reason: $"CAUTION — ImpulsePoints={impulseDistancePoints:0.0} ({SafePointsThreshold}–{CautionPointsMax}), ATR={impulseDistanceAtr:0.00} ({SafeAtrThreshold}–{CautionAtrMax})");
    }
}
