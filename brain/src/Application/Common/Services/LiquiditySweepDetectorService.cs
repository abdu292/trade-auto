using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// CR11 — Liquidity Sweep Detector.
/// Detects stop-hunt sweeps that create safe reversal entries.
///
/// Conditions required for a confirmed sweep:
///   1. Price sweeps a prior swing high or low (HasLiquiditySweep)
///   2. Candle closes back inside the prior range (reclaim)
///   3. Range expansion or volume spike is present
///   4. Sweep occurs near a structural liquidity zone
///
/// When IsConfirmed=true, the PRETABLE risk level may improve one tier
/// (CAUTION → SAFE), but NEVER overrides WATERFALL_RISK.
/// </summary>
public static class LiquiditySweepDetectorService
{
    public static LiquiditySweepResult Detect(MarketSnapshotContract snapshot)
    {
        // Condition 1: Price swept a prior swing level (pre-computed by MT5 EA or market data provider)
        var priceSwept = snapshot.HasLiquiditySweep;

        // Condition 2: Candle closed back inside the range after the sweep (reclaim).
        // Proxied by: HasOverlapCandles (at least one consolidation candle above the swept level)
        // OR TvAlertType SHELF_RECLAIM / RETEST_HOLD indicating price recovered.
        var closedBackInside = snapshot.HasOverlapCandles
            || snapshot.TvAlertType is "SHELF_RECLAIM" or "RETEST_HOLD";

        // Condition 3: Volume or range spike during the sweep candle.
        // Proxied by: IsExpansion or ATR expanding or impulse candles present during the sweep.
        var volumeOrRangeSpike = snapshot.IsExpansion || snapshot.IsAtrExpanding || snapshot.HasImpulseCandles;

        // Condition 4: Sweep occurred near structural liquidity.
        // Proxied by: price near session highs/lows or prior structure boundaries.
        var nearStructuralLiquidity = IsNearStructuralLiquidity(snapshot);

        var isConfirmed = priceSwept && closedBackInside && volumeOrRangeSpike && nearStructuralLiquidity;

        var reason = isConfirmed
            ? $"Sweep confirmed: Swept={priceSwept}, Reclaim={closedBackInside}, Spike={volumeOrRangeSpike}, NearLiquidity={nearStructuralLiquidity}"
            : $"Sweep not confirmed: Swept={priceSwept}, Reclaim={closedBackInside}, Spike={volumeOrRangeSpike}, NearLiquidity={nearStructuralLiquidity}";

        return new LiquiditySweepResult(
            IsConfirmed: isConfirmed,
            PriceSweptPriorLevel: priceSwept,
            CandleClosedBackInside: closedBackInside,
            VolumeOrRangeSpike: volumeOrRangeSpike,
            NearStructuralLiquidity: nearStructuralLiquidity,
            Reason: reason);
    }

    private static bool IsNearStructuralLiquidity(MarketSnapshotContract snapshot)
    {
        var close = snapshot.AuthoritativeRate > 0m
            ? snapshot.AuthoritativeRate
            : snapshot.Bid > 0m ? snapshot.Bid : 0m;

        if (close <= 0m)
            return true; // insufficient data — assume near structure

        // Proximity buffer: within 0.5% of any session high/low counts as near structural liquidity
        const decimal ProximityFraction = 0.005m;

        bool Near(decimal level) => level > 0m && Math.Abs(close - level) / level <= ProximityFraction;

        return Near(snapshot.SessionHigh)
            || Near(snapshot.SessionLow)
            || Near(snapshot.SessionHighJapan)
            || Near(snapshot.SessionLowJapan)
            || Near(snapshot.SessionHighIndia)
            || Near(snapshot.SessionLowIndia)
            || Near(snapshot.SessionHighLondon)
            || Near(snapshot.SessionLowLondon)
            || Near(snapshot.SessionHighNy)
            || Near(snapshot.SessionLowNy)
            || Near(snapshot.PreviousSessionHigh)
            || Near(snapshot.PreviousSessionLow)
            || Near(snapshot.PreviousDayHigh)
            || Near(snapshot.PreviousDayLow);
    }
}
