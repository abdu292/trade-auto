using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// CR11 Layer B — PRETABLE Risk Intelligence.
/// Determines trade aggressiveness before execution.
///
/// Outputs:
///   RiskLevel:    SAFE | CAUTION | BLOCK
///   RiskScore:    0.0 – 1.0 (higher = riskier)
///   RiskFlags[]:  named risk contributors
///   Session:      current trading session
///   SizeModifier: multiplier applied to computed gram size
///
/// PRETABLE cannot override WATERFALL_RISK, FAIL laws, hazard windows,
/// exposure limits, capital limits, or panic interrupts.
///
/// If LiquiditySweepResult.IsConfirmed the risk level may improve one tier:
///   CAUTION → SAFE  (but never BLOCK → SAFE/CAUTION)
/// </summary>
public static class PretableService
{
    /// <summary>Evaluates PRETABLE risk given the current market context.</summary>
    public static PretableResult Evaluate(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        ImpulseExhaustionResult impulseExhaustion,
        LiquiditySweepResult liquiditySweep,
        string session)
    {
        var flags = new List<string>();
        var riskScore = 0m;

        // ── Hard BLOCK conditions (regime-level overrides) ──────────────────
        if (regime.IsBlocked || regime.IsWaterfall)
        {
            flags.Add("REGIME_BLOCK");
            return BuildResult("BLOCK", 1.0m, flags, session, 0m,
                $"Regime hard block: {regime.Reason}");
        }

        // ── Impulse Exhaustion contributes to score ─────────────────────────
        if (impulseExhaustion.Level == "BLOCK")
        {
            flags.Add("IMPULSE_EXHAUSTION_BLOCK");
            riskScore += 0.4m;
        }
        else if (impulseExhaustion.Level == "CAUTION")
        {
            flags.Add("IMPULSE_EXHAUSTION_CAUTION");
            riskScore += 0.2m;
        }

        // ── Volatility / spread risk ─────────────────────────────────────────
        var spreadInstability = snapshot.SpreadMedian60m > 0m
            && snapshot.SpreadMax60m > 0m
            && snapshot.SpreadMax60m >= snapshot.SpreadMedian60m * 2.0m;
        if (spreadInstability)
        {
            flags.Add("SPREAD_INSTABILITY");
            riskScore += 0.15m;
        }

        // ── ADR exhaustion ───────────────────────────────────────────────────
        if (snapshot.AdrUsedPct >= 85m)
        {
            flags.Add("ADR_EXHAUSTED");
            riskScore += 0.15m;
        }
        else if (snapshot.AdrUsedPct >= 70m)
        {
            flags.Add("ADR_HIGH_USAGE");
            riskScore += 0.08m;
        }

        // ── Panic / news ────────────────────────────────────────────────────
        if (snapshot.HasPanicDropSequence || snapshot.PanicSuspected)
        {
            flags.Add("PANIC_SUSPECTED");
            riskScore += 0.25m;
        }

        if (string.Equals(snapshot.TelegramImpactTag, "HIGH", StringComparison.OrdinalIgnoreCase))
        {
            flags.Add("HIGH_IMPACT_NEWS");
            riskScore += 0.15m;
        }

        // ── Friday overlap risk ──────────────────────────────────────────────
        if (snapshot.IsFriday && snapshot.IsLondonNyOverlap)
        {
            flags.Add("FRIDAY_OVERLAP");
            riskScore += 0.10m;
        }

        // ── ATR expansion ────────────────────────────────────────────────────
        if (snapshot.IsAtrExpanding && snapshot.IsExpansion)
        {
            flags.Add("ATR_EXPANSION");
            riskScore += 0.10m;
        }

        riskScore = Math.Clamp(riskScore, 0m, 1.0m);

        // ── Determine base risk level from score ─────────────────────────────
        string riskLevel;
        if (riskScore >= 0.65m || impulseExhaustion.Level == "BLOCK")
        {
            riskLevel = "BLOCK";
        }
        else if (riskScore >= 0.30m || impulseExhaustion.Level == "CAUTION")
        {
            riskLevel = "CAUTION";
        }
        else
        {
            riskLevel = "SAFE";
        }

        // ── Liquidity sweep upgrade: CAUTION → SAFE ──────────────────────────
        // A confirmed liquidity sweep indicates a controlled reversal entry.
        // It may improve risk by one tier — only from CAUTION to SAFE,
        // never from BLOCK to anything better.
        if (riskLevel == "CAUTION" && liquiditySweep.IsConfirmed)
        {
            flags.Add("SWEEP_CONFIRMED_UPGRADE");
            riskLevel = "SAFE";
        }

        // ── Compute size modifier ────────────────────────────────────────────
        var sizeModifier = riskLevel switch
        {
            "SAFE"    => 1.0m,
            "CAUTION" => 0.60m,
            "BLOCK"   => 0.0m,
            _          => 0.0m,
        };

        var reason = $"PRETABLE {riskLevel}: score={riskScore:0.00}, flags=[{string.Join(",", flags)}], sweep={liquiditySweep.IsConfirmed}, impulse={impulseExhaustion.Level}";
        return BuildResult(riskLevel, riskScore, flags, session, sizeModifier, reason);
    }

    private static PretableResult BuildResult(
        string riskLevel,
        decimal riskScore,
        List<string> flags,
        string session,
        decimal sizeModifier,
        string reason) =>
        new(
            RiskLevel: riskLevel,
            RiskScore: riskScore,
            RiskFlags: flags,
            Session: session,
            SizeModifier: sizeModifier,
            Reason: reason);
}
