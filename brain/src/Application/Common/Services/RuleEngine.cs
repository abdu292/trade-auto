using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Deterministic three-layer rule engine. Evaluates H1 context, M15 setup, and M5 entry
/// to produce a setup candidate before AI analysis is performed.
/// AI must never generate trades independently — this engine always runs first.
/// </summary>
public static class RuleEngine
{
    // ── Thresholds ──────────────────────────────────────────────────────────
    private const decimal RangeContractionThreshold = 0.75m;  // M15 ATR must be < this × H1 ATR
    private const decimal MinimumImpulseStrength = 0.40m;     // minimum impulse score for momentum entry
    private const decimal RsiCompressionLowerBound = 35m;     // RSI floor for valid compression entry
    private const decimal RsiCompressionUpperBound = 72m;     // RSI ceiling for valid compression entry

    /// <summary>
    /// Evaluates all three layers and returns a setup candidate (or abort result).
    /// Market regime detection runs first; if the regime is not tradeable the cycle
    /// is aborted before H1/M15/M5 evaluation is performed.
    /// If any layer fails, the result is invalid and the decision cycle must abort.
    /// </summary>
    public static SetupCandidateResult Evaluate(MarketSnapshotContract snapshot)
    {
        // ── Layer 0: Market Regime Detection ────────────────────────────────────
        // Filters out structurally poor market conditions (dead, choppy, bear trend)
        // before performing the more expensive H1/M15/M5 structural analysis.
        var marketRegime = MarketRegimeDetector.Detect(snapshot);
        if (!marketRegime.IsTradeable)
        {
            return SetupCandidateResult.AbortedByRegime(marketRegime);
        }

        var h1 = EvaluateH1Context(snapshot);

        if (h1.Context == "NEUTRAL")
        {
            return SetupCandidateResult.Aborted(h1, $"H1 context is neutral — no directional bias. {h1.Reason}", marketRegime);
        }

        var m15 = EvaluateM15Setup(snapshot);
        if (!m15.IsValid)
        {
            return SetupCandidateResult.Aborted(h1, $"M15 setup not found: {m15.Reason}", marketRegime);
        }

        var m5 = EvaluateM5Entry(snapshot);
        if (!m5.IsValid)
        {
            return SetupCandidateResult.Aborted(h1, m15, $"M5 entry not confirmed: {m5.Reason}", marketRegime);
        }

        return SetupCandidateResult.Valid(h1, m15, m5, marketRegime);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Layer 1 — H1 Context
    // Evaluates the broader market state using trend alignment and momentum.
    // Returns BULLISH, BEARISH, or NEUTRAL.
    // ──────────────────────────────────────────────────────────────────────────
    private static H1ContextResult EvaluateH1Context(MarketSnapshotContract snapshot)
    {
        var h1Candle = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "H1", StringComparison.OrdinalIgnoreCase));

        var close = h1Candle?.Close
            ?? snapshot.TimeframeData.FirstOrDefault()?.Close
            ?? 0m;

        var ma = snapshot.Ma20H1 > 0m ? snapshot.Ma20H1 : snapshot.Ma20;
        var rsiH1 = snapshot.RsiH1;
        var hasLiquiditySweep = snapshot.HasLiquiditySweep;

        // Trend alignment: price position relative to MA
        var isBullishTrend = ma > 0m && close > ma;
        var isBearishTrend = ma > 0m && close < ma;

        // Directional momentum via RSI (allow zero when RSI not available)
        var rsiAvailable = rsiH1 > 0m;
        var isBullishMomentum = !rsiAvailable || rsiH1 >= 50m;
        var isBearishMomentum = !rsiAvailable || rsiH1 < 50m;

        // Reclaim: liquidity sweep followed by recovery above/below a level
        var hasReclaim = hasLiquiditySweep && (isBullishTrend || isBearishTrend);

        string context;
        if (isBullishTrend && isBullishMomentum)
        {
            context = "BULLISH";
        }
        else if (isBearishTrend && isBearishMomentum)
        {
            context = "BEARISH";
        }
        else if (hasLiquiditySweep && isBullishTrend)
        {
            context = "BULLISH";
        }
        else if (hasLiquiditySweep && isBearishTrend)
        {
            context = "BEARISH";
        }
        else if (ma == 0m)
        {
            // No MA data available — use RSI alone
            if (rsiH1 > 55m) context = "BULLISH";
            else if (rsiH1 > 0m && rsiH1 < 45m) context = "BEARISH";
            else context = "NEUTRAL";
        }
        else
        {
            context = "NEUTRAL";
        }

        var reason = context == "NEUTRAL"
            ? $"Conflicting signals. MA={ma:0.00}, Close={close:0.00}, RSI={rsiH1:0.0}, Sweep={hasLiquiditySweep}"
            : $"H1 {context}: MA={ma:0.00}, Close={close:0.00}, RSI={rsiH1:0.0}, Sweep={hasLiquiditySweep}, Reclaim={hasReclaim}";

        return new H1ContextResult(
            Context: context,
            HasLiquiditySweep: hasLiquiditySweep,
            HasReclaim: hasReclaim,
            IsTrendAligned: isBullishTrend || isBearishTrend,
            Reason: reason);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Layer 2 — M15 Setup
    // Identifies structural trade opportunities via compression or base formation.
    // ──────────────────────────────────────────────────────────────────────────
    private static M15SetupResult EvaluateM15Setup(MarketSnapshotContract snapshot)
    {
        // Volatility compression: explicit flag or at least 2 compression candles
        var isCompression = snapshot.IsCompression || snapshot.CompressionCountM15 >= 2;

        // Base formation: multiple overlapping candles (stable price structure)
        var hasBase = snapshot.HasOverlapCandles;

        // Range contraction: M15 ATR is tighter than H1 ATR (setup tightening before breakout)
        var atrH1 = snapshot.AtrH1 > 0m ? snapshot.AtrH1 : snapshot.Atr;
        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;
        var hasRangeContraction = atrH1 > 0m && atrM15 > 0m && atrM15 < atrH1 * RangeContractionThreshold;

        var isValid = isCompression || hasBase || hasRangeContraction;

        var reason = isValid
            ? $"M15 setup valid. Compression={isCompression}, Base={hasBase}, RangeContraction={hasRangeContraction}, ComprCount={snapshot.CompressionCountM15}, AtrM15={atrM15:0.00}"
            : $"No M15 setup. IsCompression={isCompression}, HasOverlapCandles={hasBase}, ComprCount={snapshot.CompressionCountM15}, AtrM15={atrM15:0.00}";

        return new M15SetupResult(
            IsValid: isValid,
            IsCompression: isCompression,
            HasBase: hasBase,
            Reason: reason);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Layer 3 — M5 Entry
    // Determines whether the setup is actionable at M5 granularity.
    // ──────────────────────────────────────────────────────────────────────────
    private static M5EntryResult EvaluateM5Entry(MarketSnapshotContract snapshot)
    {
        // Compression break: confirmed breakout candle
        var isBreakout = snapshot.IsBreakoutConfirmed;

        // Momentum shift: impulse candle with sufficient strength
        var isMomentumShift = snapshot.HasImpulseCandles && snapshot.ImpulseStrengthScore >= MinimumImpulseStrength;

        // Retest after reclaim: pullback to support after a spike
        var isRetest = snapshot.IsPostSpikePullback;

        // Compression entry: actively in compression + RSI in mid-range (not exhausted)
        // RSI between 35–72 indicates the move hasn't fully played out
        var rsiM15 = snapshot.RsiM15;
        var rsiInRange = rsiM15 == 0m || (rsiM15 >= RsiCompressionLowerBound && rsiM15 <= RsiCompressionUpperBound);
        var isCompressionEntry = snapshot.IsCompression && rsiInRange;

        var isValid = isBreakout || isMomentumShift || isRetest || isCompressionEntry;

        var reason = isValid
            ? $"M5 entry valid. Breakout={isBreakout}, Momentum={isMomentumShift}, Retest={isRetest}, CompressionEntry={isCompressionEntry}, ImpulseScore={snapshot.ImpulseStrengthScore:0.00}, RsiM15={rsiM15:0.0}"
            : $"No M5 entry. Breakout={isBreakout}, Momentum={isMomentumShift}, Retest={isRetest}, CompressionEntry={isCompressionEntry}, RsiM15={rsiM15:0.0} (needs {RsiCompressionLowerBound}–{RsiCompressionUpperBound} in compression)";

        return new M5EntryResult(
            IsValid: isValid,
            IsBreakout: isBreakout,
            IsMomentumShift: isMomentumShift,
            IsRetest: isRetest,
            Reason: reason);
    }
}
