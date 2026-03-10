using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v7 §3–§4 — Factor engine. Explicit factor classes and aggregation into
/// legalityState, biasState, pathState, sizeState, exitState.
/// </summary>
public static class FactorEngine
{
    /// <summary>Aggregate snapshot + overextension + sweep + regime + pretable into engine states.</summary>
    public static EngineStatesContract Aggregate(
        MarketSnapshotContract snapshot,
        OverextensionResult overextension,
        SweepReclaimResult sweepReclaim,
        RegimeClassificationContract regime,
        string pathState,
        string waterfallRisk,
        string? pretableRiskLevel = null,
        string? efficiencyState = null)
    {
        var (session, phase) = TradingSessionClock.Resolve(snapshot.KsaTime);
        var factors = new List<FactorImpactContract>();

        // Macro/risk (bias, caution, stand-down)
        factors.Add(MacroFactor(snapshot));
        // Session/time
        factors.Add(SessionFactor(session, phase, snapshot));
        // Volatility/liquidity
        factors.Add(VolatilityFactor(snapshot));
        // Structure (base, lid, sweep, reclaim)
        factors.Add(StructureFactor(snapshot, sweepReclaim));
        // Stretch/exhaustion
        factors.Add(StretchFactor(overextension, snapshot));
        // Execution (spread, pending-before-level)
        factors.Add(ExecutionFactor(snapshot));
        // Capital (deployable, exposure) — requires ledger; we don't have it here, skip or neutral
        factors.Add(new FactorImpactContract(
            "capital_deployable", "UNKNOWN", "neutral", 0m, "immediate",
            false, false, true, false, false, false));

        var legalityState = AggregateLegality(factors, regime, pretableRiskLevel, snapshot.Spread);
        var biasState = AggregateBias(factors, snapshot);
        var sizeState = AggregateSize(legalityState, pathState, factors);
        var exitState = pathState == PathState.StandDown || pathState == PathState.WaitPullback
            ? ExitState.StandDown
            : ExitState.StandardTp;

        return new EngineStatesContract(
            LegalityState: legalityState,
            BiasState: biasState,
            PathState: pathState,
            SizeState: sizeState,
            ExitState: exitState,
            OverextensionState: overextension.State,
            WaterfallRisk: waterfallRisk,
            Session: session,
            SessionPhase: phase,
            Factors: factors,
            EfficiencyState: efficiencyState ?? EfficiencyStates.Low);
    }

    private static FactorImpactContract MacroFactor(MarketSnapshotContract s)
    {
        var tag = s.TelegramImpactTag ?? "LOW";
        var dir = tag == "HIGH" ? "hazard" : (tag == "MEDIUM" ? "caution" : "neutral");
        return new FactorImpactContract(
            "macro_news", tag, dir, tag == "HIGH" ? 1m : 0.5m, "immediate",
            true, true, false, false, false, true);
    }

    private static FactorImpactContract SessionFactor(string session, string phase, MarketSnapshotContract s)
    {
        var value = $"{session}_{phase}";
        return new FactorImpactContract(
            "session_time", value, "neutral", 0.5m, "session",
            false, false, true, false, true, false);
    }

    private static FactorImpactContract VolatilityFactor(MarketSnapshotContract s)
    {
        var adrUsed = s.AdrUsedPct;
        var spread = s.Spread;
        var block = spread >= 0.7m;
        var caution = spread >= 0.5m || adrUsed > 0.9m;
        var dir = block ? "hazard" : (caution ? "caution" : "neutral");
        return new FactorImpactContract(
            "volatility_liquidity", $"ADR={adrUsed:0.00} spread={spread:0.00}", dir, block ? 1m : 0.5m, "intraday",
            true, false, true, false, true, block);
    }

    private static FactorImpactContract StructureFactor(MarketSnapshotContract s, SweepReclaimResult sweep)
    {
        var state = sweep.State;
        var bullish = state == SweepReclaimState.SweepReclaim ? "bullish" : "neutral";
        var strength = state == SweepReclaimState.SweepReclaim ? 0.9m : 0.3m;
        return new FactorImpactContract(
            "structure_sweep_reclaim", state, bullish, strength, "intraday",
            false, true, false, true, false, false);
    }

    private static FactorImpactContract StretchFactor(OverextensionResult o, MarketSnapshotContract s)
    {
        var dir = o.State == OverextensionState.Overextended ? "hazard" : (o.State == OverextensionState.Stretched ? "caution" : "neutral");
        return new FactorImpactContract(
            "stretch_exhaustion", o.State, dir, o.State == OverextensionState.Overextended ? 1m : 0.5m, "immediate",
            true, false, false, false, false, true);
    }

    private static FactorImpactContract ExecutionFactor(MarketSnapshotContract s)
    {
        var block = s.Spread >= 0.7m;
        return new FactorImpactContract(
            "execution_spread", $"spread={s.Spread:0.00}", block ? "hazard" : "neutral", block ? 1m : 0m, "immediate",
            true, false, true, false, false, block);
    }

    private static string AggregateLegality(
        List<FactorImpactContract> factors,
        RegimeClassificationContract regime,
        string? pretableRiskLevel,
        decimal spread)
    {
        if (regime.IsBlocked || pretableRiskLevel == "BLOCK" || spread >= 0.7m)
            return LegalityState.Block;
        var hazard = factors.Any(f => f.ImpactDirection == "hazard" && f.AffectsLegality);
        var caution = factors.Any(f => f.ImpactDirection == "caution" && f.AffectsLegality);
        if (hazard) return LegalityState.Block;
        if (caution || pretableRiskLevel == "CAUTION") return LegalityState.Caution;
        return LegalityState.Legal;
    }

    private static string AggregateBias(List<FactorImpactContract> factors, MarketSnapshotContract s)
    {
        if (s.TelegramImpactTag == "HIGH" && (s.TelegramState == "BUY" || s.TelegramState == "STRONG_BUY"))
            return BiasState.Shock;
        var bearish = factors.Any(f => f.ImpactDirection == "bearish");
        var bullish = factors.Any(f => f.ImpactDirection == "bullish");
        if (bearish && !bullish) return BiasState.Bearish;
        if (bullish) return BiasState.Bullish;
        return BiasState.Neutral;
    }

    private static string AggregateSize(string legality, string pathState, List<FactorImpactContract> factors)
    {
        if (legality == LegalityState.Block || pathState == PathState.StandDown || pathState == PathState.WaitPullback)
            return SizeState.Zero;
        if (legality == LegalityState.Caution) return SizeState.Micro;
        return SizeState.Full;
    }
}
