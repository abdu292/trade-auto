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
        LedgerStateContract? ledgerState = null,
        string? pretableRiskLevel = null,
        string? efficiencyState = null,
        ConfidenceScoreResult? confidence = null,
        bool hazardWindowActive = false,
        string? reasonCode = null)
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
        factors.Add(ExecutionFactor(snapshot, pathState));
        // Capital/business
        factors.Add(CapitalFactor(ledgerState));
        if (hazardWindowActive)
        {
            factors.Add(new FactorImpactContract(
                "hazard_window",
                "ACTIVE_BLOCK",
                "hazard",
                1m,
                "immediate",
                true,
                false,
                true,
                false,
                true,
                true));
        }

        var legalityState = AggregateLegality(factors, regime, pretableRiskLevel, snapshot.Spread, hazardWindowActive);
        var biasState = AggregateBias(factors, snapshot);
        var sizeState = AggregateSize(legalityState, pathState, factors, confidence, session);
        var exitState = AggregateExitState(pathState, sweepReclaim, confidence, hazardWindowActive);

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
            EfficiencyState: efficiencyState ?? EfficiencyStates.Low,
            ConfidenceScore: confidence?.Score ?? 0,
            ConfidenceTier: confidence?.Tier ?? "WAIT",
            ConfidenceReason: confidence?.Reason,
            ReasonCode: reasonCode,
            HazardWindowActive: hazardWindowActive);
    }

    private static FactorImpactContract MacroFactor(MarketSnapshotContract s)
    {
        var tag = s.NewsEventFlag || s.GeoRiskFlag ? "HIGH" : (s.TelegramImpactTag ?? "LOW");
        var dir = s.GeoRiskFlag ? "hazard" : (tag == "HIGH" ? "hazard" : (tag == "MEDIUM" ? "caution" : "neutral"));
        return new FactorImpactContract(
            "macro_news", tag, dir, tag == "HIGH" ? 1m : 0.5m, "immediate",
            true, true, false, false, false, true);
    }

    private static FactorImpactContract SessionFactor(string session, string phase, MarketSnapshotContract s)
    {
        var value = $"{session}_{phase}";
        var direction = phase is "START" or "END" ? "caution" : "neutral";
        return new FactorImpactContract(
            "session_time", value, direction, phase is "START" or "END" ? 0.6m : 0.5m, "session",
            false, false, true, false, true, false);
    }

    private static FactorImpactContract VolatilityFactor(MarketSnapshotContract s)
    {
        var adrUsed = s.AdrUsedPct;
        var spread = s.Spread;
        var vci = ComputeVci(s);
        var block = spread >= 0.7m;
        var caution = spread >= 0.5m || adrUsed > 0.9m || vci > 1.3m;
        var dir = block ? "hazard" : (caution ? "caution" : "neutral");
        return new FactorImpactContract(
            "volatility_liquidity", $"ADR={adrUsed:0.00} spread={spread:0.00} VCI={vci:0.00}", dir, block ? 1m : 0.5m, "intraday",
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

    private static FactorImpactContract ExecutionFactor(MarketSnapshotContract s, string pathState)
    {
        var block = s.Spread >= 0.7m;
        var pendingLegal = pathState switch
        {
            var x when x == PathState.BuyStop => s.Ask <= 0m || s.AuthoritativeRate <= 0m || s.AuthoritativeRate > s.Ask,
            var x when x == PathState.BuyLimit => s.Bid <= 0m || s.AuthoritativeRate <= 0m || s.AuthoritativeRate < s.Bid,
            _ => true,
        };
        return new FactorImpactContract(
            "execution_spread", $"spread={s.Spread:0.00};pendingLegal={pendingLegal}", (!pendingLegal || block) ? "hazard" : "neutral", (!pendingLegal || block) ? 1m : 0m, "immediate",
            true, false, true, false, false, !pendingLegal || block);
    }

    private static FactorImpactContract CapitalFactor(LedgerStateContract? ledgerState)
    {
        if (ledgerState is null)
        {
            return new FactorImpactContract(
                "capital_business",
                "UNKNOWN",
                "neutral",
                0m,
                "immediate",
                false,
                false,
                true,
                false,
                false,
                false);
        }

        var state = $"deployable={ledgerState.DeployableCashAed:0.00};exposure={ledgerState.OpenExposurePercent:0.##}";
        var blocked = ledgerState.DeployableCashAed <= 0m || ledgerState.OpenExposurePercent >= 65m;
        var caution = !blocked && (ledgerState.DeployableCashAed < 1000m || ledgerState.OpenExposurePercent >= 45m);
        var direction = blocked ? "hazard" : (caution ? "caution" : "neutral");
        return new FactorImpactContract(
            "capital_business",
            state,
            direction,
            blocked ? 1m : (caution ? 0.5m : 0m),
            "immediate",
            true,
            false,
            true,
            false,
            false,
            blocked);
    }

    private static string AggregateLegality(
        List<FactorImpactContract> factors,
        RegimeClassificationContract regime,
        string? pretableRiskLevel,
        decimal spread,
        bool hazardWindowActive)
    {
        if (regime.IsBlocked || pretableRiskLevel == "BLOCK" || spread >= 0.7m || hazardWindowActive)
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

    private static string AggregateSize(
        string legality,
        string pathState,
        List<FactorImpactContract> factors,
        ConfidenceScoreResult? confidence,
        string session)
    {
        if (legality == LegalityState.Block || pathState is PathState.StandDown or PathState.WaitPullback or PathState.Overextended)
            return SizeState.Zero;
        if (confidence?.Tier == "WAIT") return SizeState.Zero;
        if (confidence?.Tier == "MICRO" || legality == LegalityState.Caution) return SizeState.Micro;
        var cautionSize = factors.Any(f => f.AffectsSize && f.ImpactDirection == "caution");
        if (cautionSize || session is "JAPAN" or "NY") return SizeState.Reduced;
        return SizeState.Full;
    }

    private static string AggregateExitState(
        string pathState,
        SweepReclaimResult sweepReclaim,
        ConfidenceScoreResult? confidence,
        bool hazardWindowActive)
    {
        if (hazardWindowActive || pathState is PathState.StandDown or PathState.WaitPullback or PathState.Overextended)
        {
            return ExitState.StandDown;
        }

        if (confidence?.Tier == "MICRO")
        {
            return ExitState.TightExpiry;
        }

        if (pathState == PathState.BuyLimit && sweepReclaim.State == SweepReclaimState.SweepReclaim)
        {
            return ExitState.MagnetTp;
        }

        return ExitState.StandardTp;
    }

    private static decimal ComputeVci(MarketSnapshotContract snapshot)
    {
        var ranges = snapshot.CompressionRangesM15;
        if (ranges is null || ranges.Count < 50)
        {
            return 1m;
        }

        var last10 = ranges.TakeLast(10).Average();
        var last50 = ranges.TakeLast(50).Average();
        return last50 > 0m ? last10 / last50 : 1m;
    }
}
