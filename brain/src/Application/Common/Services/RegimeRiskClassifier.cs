using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

public static class RegimeRiskClassifier
{
    private const decimal ExpansionThreshold = 1.15m;
    private const decimal CompressionThreshold = 0.82m;

    public static RegimeClassificationContract Classify(MarketSnapshotContract snapshot)
    {
        var volatilityExpansion = snapshot.VolatilityExpansion > 0m
            ? snapshot.VolatilityExpansion
            : (snapshot.Adr <= 0m ? 0m : snapshot.Atr / snapshot.Adr);

        var isExpansion = snapshot.IsExpansion || snapshot.IsAtrExpanding || volatilityExpansion >= ExpansionThreshold;
        var isCompression = snapshot.IsCompression || (snapshot.HasOverlapCandles && volatilityExpansion <= CompressionThreshold);
        var isNewsHigh = string.Equals(snapshot.TelegramImpactTag, "HIGH", StringComparison.OrdinalIgnoreCase);
        var isFridayOverlapExpansion = snapshot.IsFriday && snapshot.IsLondonNyOverlap && isExpansion;
        var isPanicPattern = snapshot.HasPanicDropSequence;
        var isWaterfall = (isExpansion && snapshot.HasImpulseCandles && isNewsHigh) || isFridayOverlapExpansion || isPanicPattern;
        var isPostSpikePullback = snapshot.IsPostSpikePullback || (volatilityExpansion >= 1.00m && volatilityExpansion < ExpansionThreshold && !snapshot.HasImpulseCandles);

        if (isFridayOverlapExpansion)
        {
            return new RegimeClassificationContract(
                Regime: "FRIDAY_HIGH_RISK",
                RiskTag: "BLOCK",
                IsBlocked: true,
                IsWaterfall: true,
                Reason: "Friday London/NY overlap with expansion is blocked.");
        }

        if (isWaterfall || (isExpansion && snapshot.HasImpulseCandles && isNewsHigh))
        {
            return new RegimeClassificationContract(
                Regime: "NEWS_SPIKE",
                RiskTag: "BLOCK",
                IsBlocked: true,
                IsWaterfall: true,
                Reason: "Waterfall guard triggered (expansion + impulse + HIGH news / panic pattern).");
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

            if (isCompression)
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
