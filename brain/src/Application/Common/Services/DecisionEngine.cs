using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

public static class DecisionEngine
{
    public static DecisionResultContract Evaluate(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        TradeSignalContract aiSignal,
        LedgerStateContract ledgerState)
    {
        if (!string.Equals(snapshot.Symbol, "XAUUSD", StringComparison.OrdinalIgnoreCase))
        {
            return NoTrade("Only XAUUSD is permitted.", aiSignal.AlignmentScore);
        }

        if (regime.IsBlocked)
        {
            return NoTrade($"NO TRADE - HIGH RISK ({regime.Regime})", aiSignal.AlignmentScore);
        }

        if (string.Equals(aiSignal.SafetyTag, "BLOCK", StringComparison.OrdinalIgnoreCase))
        {
            return NoTrade("NO TRADE - HIGH RISK (AI safety block)", aiSignal.AlignmentScore);
        }

        var score = Math.Clamp(aiSignal.AlignmentScore, 0m, 1m);
        var threshold = string.Equals(regime.RiskTag, "SAFE", StringComparison.OrdinalIgnoreCase) ? 0.62m : 0.72m;
        if (score < threshold)
        {
            return NoTrade("NO TRADE - HIGH RISK (alignment below threshold)", score);
        }

        var primaryClose = snapshot.TimeframeData
            .FirstOrDefault(tf => string.Equals(tf.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.Close
            ?? snapshot.TimeframeData.First().Close;

        var entryDistance = regime.Regime switch
        {
            "COMPRESSION" => snapshot.Atr * 0.35m,
            "POST_SPIKE_PULLBACK" => snapshot.Atr * 0.80m,
            "EXPANSION" => snapshot.Atr * 0.55m,
            _ => snapshot.Atr * 0.45m
        };

        var rail = regime.Regime == "EXPANSION" ? "BUY_STOP" : "BUY_LIMIT";
        var entry = rail == "BUY_LIMIT"
            ? primaryClose - entryDistance
            : primaryClose + (entryDistance * 0.60m);

        var tpMultiplier = regime.Regime switch
        {
            "COMPRESSION" => 1.3m,
            "POST_SPIKE_PULLBACK" => 1.7m,
            "EXPANSION" => 1.5m,
            _ => 1.4m
        };

        var tp = entry + (snapshot.Atr * tpMultiplier);

        var baseGrams = regime.RiskTag == "SAFE" ? 18m : 9m;
        var volatilityPenalty = Math.Clamp(snapshot.VolatilityExpansion <= 0 ? 0.15m : snapshot.VolatilityExpansion / 2m, 0.10m, 0.55m);
        var exposurePenalty = Math.Clamp(ledgerState.OpenExposurePercent / 100m, 0m, 0.70m);
        var grams = Math.Max(2m, baseGrams * (1m - volatilityPenalty) * (1m - exposurePenalty));

        var expiry = snapshot.Timestamp.UtcDateTime.Add(GetSessionExpiry(snapshot.Session));

        return new DecisionResultContract(
            IsTradeAllowed: true,
            Status: "TABLE",
            Reason: $"{regime.Regime} aligned with AI {aiSignal.SafetyTag}/{aiSignal.DirectionBias}.",
            Rail: rail,
            Entry: decimal.Round(entry, 2),
            Tp: decimal.Round(tp, 2),
            Grams: decimal.Round(grams, 2),
            ExpiryUtc: new DateTimeOffset(expiry, TimeSpan.Zero),
            MaxLifeSeconds: (int)GetSessionExpiry(snapshot.Session).TotalSeconds,
            AlignmentScore: score);
    }

    private static TimeSpan GetSessionExpiry(string session) => session.ToUpperInvariant() switch
    {
        "ASIA" => TimeSpan.FromHours(6),
        "EUROPE" => TimeSpan.FromHours(4),
        "LONDON" => TimeSpan.FromHours(4),
        "NEW_YORK" => TimeSpan.FromHours(3),
        _ => TimeSpan.FromHours(4)
    };

    private static DecisionResultContract NoTrade(string reason, decimal score) =>
        new(
            IsTradeAllowed: false,
            Status: "NO TRADE - HIGH RISK",
            Reason: reason,
            Rail: string.Empty,
            Entry: 0m,
            Tp: 0m,
            Grams: 0m,
            ExpiryUtc: DateTimeOffset.UtcNow,
            MaxLifeSeconds: 0,
            AlignmentScore: score);
}
