using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Waterfall Detection Engine per spec/13_waterfall_detection_engine.md
/// Distinguishes WATERFALL_CONTINUATION from FLUSH_REVERSAL_ATTEMPT
/// </summary>
public static class WaterfallDetectionEngine
{
    /// <summary>
    /// Detects waterfall risk level: LOW, MEDIUM, HIGH
    /// </summary>
    public static string Detect(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime)
    {
        // WATERFALL_CONTINUATION signals
        var waterfallSignals = 0;

        // Repeated bearish closes near lows
        if (snapshot.HasPanicDropSequence)
        {
            waterfallSignals += 3;
        }

        // Shelf breaks continue
        if (regime.Regime == "LIQUIDATION" || regime.Regime == "SHOCK")
        {
            waterfallSignals += 2;
        }

        // No meaningful rebound
        if (snapshot.IsPostSpikePullback && snapshot.VolatilityExpansion > 0.5m)
        {
            waterfallSignals += 1;
        }

        // Volatility expansion in continuation direction
        if (snapshot.IsAtrExpanding && snapshot.VolatilityExpansion > 0.7m)
        {
            waterfallSignals += 2;
        }

        // Structure deterioration persists
        if (regime.IsBlocked)
        {
            waterfallSignals += 2;
        }

        // Classify risk level
        if (waterfallSignals >= 5)
        {
            return "HIGH";
        }
        if (waterfallSignals >= 3)
        {
            return "MEDIUM";
        }

        // Check for FLUSH_REVERSAL_ATTEMPT (opposite of waterfall)
        var flushReversalSignals = 0;

        // Sharp drop into known shelf
        if (snapshot.HasLiquiditySweep && snapshot.IsCompression)
        {
            flushReversalSignals += 2;
        }

        // Lower rejection (price bounces from lows)
        if (snapshot.SessionLow > 0 && snapshot.Bid > snapshot.SessionLow * 1.001m)
        {
            flushReversalSignals += 1;
        }

        // No decisive lower extension
        if (!snapshot.HasPanicDropSequence && snapshot.VolatilityExpansion < 0.3m)
        {
            flushReversalSignals += 1;
        }

        // Zone starts holding
        if (snapshot.IsCompression && snapshot.HasOverlapCandles)
        {
            flushReversalSignals += 1;
        }

        // If flush reversal signals are strong, reduce waterfall risk
        if (flushReversalSignals >= 3)
        {
            waterfallSignals = Math.Max(0, waterfallSignals - 2);
        }

        // Final classification
        if (waterfallSignals >= 5)
        {
            return "HIGH";
        }
        if (waterfallSignals >= 3)
        {
            return "MEDIUM";
        }

        return "LOW";
    }
}