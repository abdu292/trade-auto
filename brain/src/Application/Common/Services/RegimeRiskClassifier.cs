using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Classifies market regime for trade gates. Per client spec: Friday risk is split into
/// distinct regimes with precise labels (overlap vs late-NY), so UI and logs match actual trigger window.
/// </summary>
public static class RegimeRiskClassifier
{
    private const decimal ExpansionThreshold = 1.15m;
    private const decimal CompressionThreshold = 0.82m;

    // London→NY overlap: spec 01_master_constitution.md — Server 14:55–15:25
    private static readonly TimeSpan FridayOverlapStartServer = new(14, 55, 0);
    private static readonly TimeSpan FridayOverlapEndServer = new(15, 25, 0);

    // Late NY / post-overlap high-risk: block from ~18:00 server (KSA ~18:50) through NY END (20:10 server)
    private static readonly TimeSpan FridayLateNyStartServer = new(18, 0, 0);
    private static readonly TimeSpan FridayLateNyEndServer = new(20, 10, 0);

    public static RegimeClassificationContract Classify(MarketSnapshotContract snapshot)
    {
        var volatilityExpansion = snapshot.VolatilityExpansion > 0m
            ? snapshot.VolatilityExpansion
            : (snapshot.Adr <= 0m ? 0m : snapshot.Atr / snapshot.Adr);

        var isExpansion = snapshot.IsExpansion || snapshot.IsAtrExpanding || volatilityExpansion >= ExpansionThreshold;
        var isCompression = snapshot.IsCompression || (snapshot.HasOverlapCandles && volatilityExpansion <= CompressionThreshold);
        var isNewsHigh = string.Equals(snapshot.TelegramImpactTag, "HIGH", StringComparison.OrdinalIgnoreCase);
        var isPanicPattern = snapshot.HasPanicDropSequence;
        var isWaterfall = (isExpansion && snapshot.HasImpulseCandles && isNewsHigh) || isPanicPattern;
        var isPostSpikePullback = snapshot.IsPostSpikePullback || (volatilityExpansion >= 1.00m && volatilityExpansion < ExpansionThreshold && !snapshot.HasImpulseCandles);

        var isFriday = snapshot.IsFriday;
        var session = (snapshot.Session ?? string.Empty).Trim().ToUpperInvariant();
        var phase = (snapshot.SessionPhase ?? string.Empty).Trim().ToUpperInvariant();
        var isNySession = session is "NY" or "NEW_YORK";
        var t = snapshot.Mt5ServerTime.TimeOfDay;
        var ksaTime = TradingSessionClock.ServerTimeToKsa(snapshot.Mt5ServerTime);
        var ksaTimeOfDay = ksaTime.TimeOfDay;

        // 1) HARD BLOCK only: Friday AND exact late-NY time window (18:00-20:10 server = 18:50-20:60 KSA)
        // Client requirement: "only late New York should be blocked, not generic NEW_YORK END"
        // Use exact clock-based window instead of sessionPhase to avoid blocking too early
        var isInLateNyWindow = IsWithin(t, FridayLateNyStartServer, FridayLateNyEndServer);
        if (isFriday && isNySession && isInLateNyWindow)
        {
            return new RegimeClassificationContract(
                Regime: "FRIDAY_NY_LATE_BLOCK",
                RiskTag: "BLOCK",
                IsBlocked: true,
                IsWaterfall: false,
                Reason: $"Friday late New York (exact window: {FridayLateNyStartServer:hh\\:mm}-{FridayLateNyEndServer:hh\\:mm} server / {ksaTimeOfDay:hh\\:mm} KSA) — hard block per client rule.");
        }

        // 2) Friday London/NY overlap + expansion = CAUTION (tighter rails), not full abort unless another veto.
        if (isFriday && IsWithin(t, FridayOverlapStartServer, FridayOverlapEndServer) && isExpansion)
        {
            return new RegimeClassificationContract(
                Regime: "FRIDAY_OVERLAP_CAUTION",
                RiskTag: "CAUTION",
                IsBlocked: false,
                IsWaterfall: false,
                Reason: "Friday London/NY overlap with expansion — caution / tighter rails.");
        }

        // 3) Friday late NY window but not in LATE/END phase (e.g. expansion only) = CAUTION
        if (isFriday && isNySession && IsWithin(t, FridayLateNyStartServer, FridayLateNyEndServer) && isExpansion && !isNyLateOrEnd)
        {
            return new RegimeClassificationContract(
                Regime: "FRIDAY_EXPANSION_CAUTION",
                RiskTag: "CAUTION",
                IsBlocked: false,
                IsWaterfall: false,
                Reason: "Friday NY expansion — caution; hard block only in LATE/END phase.");
        }

        // 4) Friday spike extension (expansion + impulse + high news) = CAUTION; block only if also hazard
        if (isFriday && isExpansion && snapshot.HasImpulseCandles && isNewsHigh)
        {
            return new RegimeClassificationContract(
                Regime: "FRIDAY_NEWS_HAZARD",
                RiskTag: "CAUTION",
                IsBlocked: false,
                IsWaterfall: true,
                Reason: "Friday expansion with impulse and HIGH news — caution; no impulse chase.");
        }

        // 4) Non-Friday waterfall / news spike
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

    private static bool IsWithin(TimeSpan value, TimeSpan start, TimeSpan end)
    {
        return value >= start && value < end;
    }
}
