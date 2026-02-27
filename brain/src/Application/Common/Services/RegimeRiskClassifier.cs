using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

public static class RegimeRiskClassifier
{
    public static RegimeClassificationContract Classify(MarketSnapshotContract snapshot)
    {
        var volatilityExpansion = snapshot.VolatilityExpansion > 0m
            ? snapshot.VolatilityExpansion
            : (snapshot.Adr <= 0m ? 0m : snapshot.Atr / snapshot.Adr);

        var isFridayRisk = snapshot.IsFriday && snapshot.IsUsRiskWindow;
        var isWaterfall = volatilityExpansion >= 1.45m;
        var isNewsSpike = snapshot.IsUsRiskWindow && volatilityExpansion >= 1.20m;
        var isPostSpikePullback = volatilityExpansion >= 1.05m && volatilityExpansion < 1.20m;

        if (isFridayRisk)
        {
            return new RegimeClassificationContract(
                Regime: "FRIDAY_HIGH_RISK",
                RiskTag: "BLOCK",
                IsBlocked: true,
                IsWaterfall: isWaterfall,
                Reason: "Friday US-risk window is blocked for new deployment.");
        }

        if (isWaterfall || isNewsSpike)
        {
            return new RegimeClassificationContract(
                Regime: "NEWS_SPIKE",
                RiskTag: "BLOCK",
                IsBlocked: true,
                IsWaterfall: isWaterfall,
                Reason: "Volatility/news spike detected. No-trade protection enabled.");
        }

        if (isPostSpikePullback)
        {
            return new RegimeClassificationContract(
                Regime: "POST_SPIKE_PULLBACK",
                RiskTag: "CAUTION",
                IsBlocked: false,
                IsWaterfall: false,
                Reason: "Post-spike pullback; allow only reduced sizing and deeper entries.");
        }

        if (volatilityExpansion <= 0.75m)
        {
            return new RegimeClassificationContract(
                Regime: "COMPRESSION",
                RiskTag: "SAFE",
                IsBlocked: false,
                IsWaterfall: false,
                Reason: "Compression regime supports controlled accumulation.");
        }

        return new RegimeClassificationContract(
            Regime: "EXPANSION",
            RiskTag: "CAUTION",
            IsBlocked: false,
            IsWaterfall: false,
            Reason: "Expansion regime requires tighter risk controls.");
    }
}
