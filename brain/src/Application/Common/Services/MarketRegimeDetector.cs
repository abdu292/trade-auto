using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Detects the structural market regime before rule-engine evaluation.
/// This is a light, deterministic pre-filter that reduces trades taken in poor
/// market conditions (choppy, dead, or strong counter-trend environments).
///
/// Regime taxonomy:
///   TRENDING_BULL — EMA golden cross, price above EMA50, RSI ≥ 50. Optimal for scalp entries.
///   TRENDING_BEAR — EMA death cross, price below EMA50, RSI below 50. Buy-only system: block.
///   RANGING       — No clear cross or compressed EMA spread. Good for BUY_LIMIT/compression.
///   CHOPPY        — ADR exhausted or rapid expansion with no compression structure. Avoid.
///   DEAD          — ATR near zero or tick rate frozen. Execution risk: avoid.
///
/// All inputs come from MarketSnapshotContract; no external calls are made.
/// IsTradeable = false for TRENDING_BEAR, CHOPPY, and DEAD.
/// </summary>
public static class MarketRegimeDetector
{
    // ATR/ADR ratio below which the market is considered frozen (no intraday range)
    private const decimal AtrDeadRatioThreshold = 0.25m;

    // Minimum tick rate (ticks per 30 s); below this the feed may be near-dead
    private const decimal MinTickRate = 0.5m;

    // ADR utilisation % above which the day has no room for new scalp moves
    private const decimal AdrExhaustedPct = 85m;

    // Expansion candle count that, with no compression, signals a choppy tape
    private const int ChoppyExpansionCount = 5;

    // EMA spread ratio: if |Ema50 − Ema200| / Ema200 < this, cross is ambiguous
    private const decimal EmaSpreadMinRatio = 0.002m;

    // Minimum M5 candle range relative to M15 ATR to consider price actively moving
    private const decimal MinM5RangeAtrRatio = 0.20m;

    public static MarketRegimeResult Detect(MarketSnapshotContract snapshot)
    {
        var atrH1 = snapshot.AtrH1 > 0m ? snapshot.AtrH1 : snapshot.Atr;
        var adr = snapshot.Adr;

        // ── DEAD: near-zero volatility or frozen tick feed ──────────────────────
        if (IsDeadMarket(snapshot, atrH1, adr))
        {
            return new MarketRegimeResult(
                Regime: "DEAD",
                IsTradeable: false,
                Reason: $"Dead market — AtrH1={atrH1:0.00}, Adr={adr:0.00}, TickRate={snapshot.TickRatePer30s:0.0}/30s.");
        }

        // ── CHOPPY: ADR range consumed or erratic expansion without structure ────
        if (IsChoppyMarket(snapshot))
        {
            return new MarketRegimeResult(
                Regime: "CHOPPY",
                IsTradeable: false,
                Reason: $"Choppy market — AdrUsedPct={snapshot.AdrUsedPct:0.0}%, ExpansionM15={snapshot.ExpansionCountM15}, CompressionM15={snapshot.CompressionCountM15}.");
        }

        var close = ResolveClose(snapshot);

        // ── TRENDING_BEAR: death cross, price below EMA50, RSI bearish ───────────
        if (IsTrendingBear(snapshot, close))
        {
            return new MarketRegimeResult(
                Regime: "TRENDING_BEAR",
                IsTradeable: false,
                Reason: $"Trending bear — Ema50H1={snapshot.Ema50H1:0.00} < Ema200H1={snapshot.Ema200H1:0.00}, Close={close:0.00}, RsiH1={snapshot.RsiH1:0.0}.");
        }

        // ── TRENDING_BULL: golden cross, price above EMA50, RSI not bearish ──────
        if (IsTrendingBull(snapshot, close))
        {
            return new MarketRegimeResult(
                Regime: "TRENDING_BULL",
                IsTradeable: true,
                Reason: $"Trending bull — Ema50H1={snapshot.Ema50H1:0.00} > Ema200H1={snapshot.Ema200H1:0.00}, Close={close:0.00}, RsiH1={snapshot.RsiH1:0.0}.");
        }

        // ── RANGING: all other conditions — consolidation / accumulation forming ─
        return new MarketRegimeResult(
            Regime: "RANGING",
            IsTradeable: true,
            Reason: $"Ranging/accumulation — Ema50H1={snapshot.Ema50H1:0.00}, Ema200H1={snapshot.Ema200H1:0.00}, Close={close:0.00}, ComprM15={snapshot.CompressionCountM15}.");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ────────────────────────────────────────────────────────────────────────────

    private static bool IsDeadMarket(MarketSnapshotContract snapshot, decimal atrH1, decimal adr)
    {
        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;
        var m5Candle = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "M5", StringComparison.OrdinalIgnoreCase));
        var m5Range = m5Candle?.CandleRange ?? 0m;
        var priceIsMoving = atrM15 > 0m && m5Range >= atrM15 * MinM5RangeAtrRatio;

        // Primary candle-derived signal: H1 ATR is less than 25 % of the average daily range.
        // Guard with M5 activity to avoid false DEAD tags during active impulses.
        if (atrH1 > 0m && adr > 0m && atrH1 < adr * AtrDeadRatioThreshold && !priceIsMoving)
            return true;

        // Secondary candle-derived confirmation: tick rate is frozen.
        // A low tick rate alone can be an unreliable indicator on some broker feeds
        // (e.g. feed pauses that do not reflect actual market inactivity).
        // Therefore, when tick rate is the only trigger, verify with a candle-based
        // activity check: if the M5 candle is showing meaningful range movement the
        // market is not truly dead, regardless of the tick count.
        if (snapshot.TickRatePer30s > 0m && snapshot.TickRatePer30s < MinTickRate)
        {
            // If M5 candle range shows meaningful activity relative to M15 ATR,
            // do not classify as dead — price is actively moving despite low tick count.
            if (!priceIsMoving)
                return true;
        }

        return false;
    }

    private static bool IsChoppyMarket(MarketSnapshotContract snapshot)
    {
        // Day range fully consumed — no room for a clean intraday scalp
        if (snapshot.AdrUsedPct >= AdrExhaustedPct)
            return true;

        // Rapid back-and-forth expansion with no compression accumulation
        if (snapshot.ExpansionCountM15 >= ChoppyExpansionCount && snapshot.CompressionCountM15 <= 1)
            return true;

        return false;
    }

    private static bool IsTrendingBull(MarketSnapshotContract snapshot, decimal close)
    {
        if (snapshot.Ema50H1 <= 0m || snapshot.Ema200H1 <= 0m)
            return false;

        // Require a meaningful EMA spread to avoid acting on a flat/ambiguous cross
        var spreadRatio = (snapshot.Ema50H1 - snapshot.Ema200H1) / snapshot.Ema200H1;
        if (spreadRatio < EmaSpreadMinRatio)
            return false;

        var goldenCross = snapshot.Ema50H1 > snapshot.Ema200H1;
        var priceAboveEma50 = close > snapshot.Ema50H1;
        var rsiNotBearish = snapshot.RsiH1 == 0m || snapshot.RsiH1 >= 50m;

        return goldenCross && priceAboveEma50 && rsiNotBearish;
    }

    private static bool IsTrendingBear(MarketSnapshotContract snapshot, decimal close)
    {
        if (snapshot.Ema50H1 <= 0m || snapshot.Ema200H1 <= 0m)
            return false;

        // Require a meaningful EMA spread (death cross must be decisive)
        var spreadRatio = (snapshot.Ema200H1 - snapshot.Ema50H1) / snapshot.Ema200H1;
        if (spreadRatio < EmaSpreadMinRatio)
            return false;

        var deathCross = snapshot.Ema50H1 < snapshot.Ema200H1;
        var priceBelowEma50 = close < snapshot.Ema50H1;
        var rsiBearish = snapshot.RsiH1 > 0m && snapshot.RsiH1 < 50m;

        return deathCross && priceBelowEma50 && rsiBearish;
    }

    private static decimal ResolveClose(MarketSnapshotContract snapshot)
    {
        if (snapshot.AuthoritativeRate > 0m)
            return snapshot.AuthoritativeRate;

        var h1Candle = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "H1", StringComparison.OrdinalIgnoreCase));

        return h1Candle?.Close
            ?? snapshot.TimeframeData.FirstOrDefault()?.Close
            ?? snapshot.Bid;
    }
}
