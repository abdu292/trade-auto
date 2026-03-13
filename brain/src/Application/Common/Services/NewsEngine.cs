using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// NEWS Engine per spec/00_instructions
/// Position: after VERIFY
/// Role: environment and permission interpreter
/// Must output: VerificationDepth, LiquidityQuality, hazardWindowActive, Rail-A/B permissions, etc.
/// </summary>
public static class NewsEngine
{
    public record NewsEngineResult(
        string VerificationDepth,  // DEEP, STANDARD, SHALLOW
        string LiquidityQuality,  // EXCELLENT, GOOD, FAIR, POOR, CRITICAL
        bool HazardWindowActive,
        bool NextTier1UltraWithin90m,
        bool NextTier1UltraWithin45m,
        string GlobalRegime,  // BULLISH, NEUTRAL, BEARISH, SHOCK
        string TradingRegime,  // CLEAN, VOLATILE, HAZARD, CRISIS
        string SpikeState,  // NONE, RISING, PEAK, FADING
        string WaterfallRisk,  // LOW, MEDIUM, HIGH
        string OverallMODE,  // STANDARD, WAR_PREMIUM, DEESCALATION_RISK, UNKNOWN
        string MacroBiasForGold,  // STRONG_BUY, BUY, NEUTRAL, SELL, STRONG_SELL
        string RailAPermission,  // YES, ONLY_AFTER_STRUCTURE, NO
        string RailBPermission,  // YES, STRICT, NO
        int MinutesToNextCleanWindow
    );

    /// <summary>
    /// Processes news/calendar feeds and returns environment interpretation.
    /// </summary>
    public static NewsEngineResult ProcessNews(
        MarketSnapshotContract snapshot,
        IReadOnlyCollection<EconomicEventContract>? upcomingEvents = null,
        IReadOnlyCollection<EconomicEventContract>? recentEvents = null,
        MacroIntelligenceContract? macroIntel = null,
        string? currentMode = null)
    {
        var verificationDepth = "STANDARD";
        var liquidityQuality = "GOOD";
        var hazardWindowActive = false;
        var nextTier1UltraWithin90m = false;
        var nextTier1UltraWithin45m = false;
        var globalRegime = "NEUTRAL";
        var tradingRegime = "CLEAN";
        var spikeState = "NONE";
        var waterfallRisk = "LOW";
        var overallMode = currentMode ?? "STANDARD";
        var macroBiasForGold = "NEUTRAL";
        var railAPermission = "YES";
        var railBPermission = "YES";
        var minutesToNextCleanWindow = 999;

        // Process upcoming events
        if (upcomingEvents != null && upcomingEvents.Any())
        {
            var now = DateTimeOffset.UtcNow;
            var tier1Events = upcomingEvents
                .Where(e => e.Tier == "Tier1" || e.Tier == "Ultra")
                .OrderBy(e => e.EventTime)
                .ToList();

            if (tier1Events.Any())
            {
                var nextEvent = tier1Events.First();
                var minutesUntil = (nextEvent.EventTime - now).TotalMinutes;

                if (minutesUntil <= 45)
                {
                    nextTier1UltraWithin45m = true;
                    nextTier1UltraWithin90m = true;
                    hazardWindowActive = true;
                    tradingRegime = "HAZARD";
                    liquidityQuality = "POOR";
                    railAPermission = "ONLY_AFTER_STRUCTURE";
                    railBPermission = "NO";
                }
                else if (minutesUntil <= 90)
                {
                    nextTier1UltraWithin90m = true;
                    hazardWindowActive = true;
                    tradingRegime = "VOLATILE";
                    liquidityQuality = "FAIR";
                    railBPermission = "STRICT";
                }

                minutesToNextCleanWindow = Math.Max(0, (int)minutesUntil + 30);  // Add 30min post-event buffer
            }
        }

        // Process recent events for spike/waterfall detection
        if (recentEvents != null && recentEvents.Any())
        {
            var recentTier1 = recentEvents
                .Where(e => (DateTimeOffset.UtcNow - e.EventTime).TotalMinutes <= 120)
                .Where(e => e.Tier == "Tier1" || e.Tier == "Ultra")
                .ToList();

            if (recentTier1.Any())
            {
                var latestEvent = recentTier1.OrderByDescending(e => e.EventTime).First();
                var minutesSince = (DateTimeOffset.UtcNow - latestEvent.EventTime).TotalMinutes;

                if (minutesSince <= 15)
                {
                    spikeState = "PEAK";
                    tradingRegime = "HAZARD";
                    liquidityQuality = "CRITICAL";
                    railAPermission = "NO";
                    railBPermission = "NO";
                }
                else if (minutesSince <= 60)
                {
                    spikeState = "FADING";
                    tradingRegime = "VOLATILE";
                    liquidityQuality = "FAIR";
                }
            }
        }

        // Process macro intelligence
        if (macroIntel != null)
        {
            // Determine global regime from macro factors
            var bullishFactors = 0;
            var bearishFactors = 0;

            if (macroIntel.DxyBias == "WEAK")
            {
                bullishFactors += 2;  // Weak DXY supports gold
            }
            else if (macroIntel.DxyBias == "STRONG")
            {
                bearishFactors += 2;
            }

            if (macroIntel.YieldPressure == "FALLING")
            {
                bullishFactors += 1;
            }
            else if (macroIntel.YieldPressure == "RISING")
            {
                bearishFactors += 1;
            }

            if (macroIntel.GeoRiskState == "HIGH")
            {
                bullishFactors += 2;  // High geo risk supports gold
            }

            if (macroIntel.OilState == "RISING")
            {
                bullishFactors += 1;
            }

            if (bullishFactors > bearishFactors + 1)
            {
                globalRegime = "BULLISH";
                macroBiasForGold = "BUY";
            }
            else if (bearishFactors > bullishFactors + 1)
            {
                globalRegime = "BEARISH";
                macroBiasForGold = "SELL";
            }

            // Check for shock conditions
            if (macroIntel.GeoRiskState == "CRITICAL" || macroIntel.Headlines?.Any(h => h.Contains("shock", StringComparison.OrdinalIgnoreCase)) == true)
            {
                globalRegime = "SHOCK";
                tradingRegime = "CRISIS";
                waterfallRisk = "HIGH";
                railAPermission = "ONLY_AFTER_STRUCTURE";
                railBPermission = "NO";
            }
        }

        // Determine waterfall risk from market state
        if (snapshot.HasPanicDropSequence || snapshot.PanicSuspected)
        {
            waterfallRisk = "HIGH";
            tradingRegime = "CRISIS";
            railAPermission = "ONLY_AFTER_STRUCTURE";
            railBPermission = "NO";
        }
        else if (snapshot.VolatilityExpansion > 0.5m || snapshot.IsAtrExpanding)
        {
            waterfallRisk = "MEDIUM";
            tradingRegime = "VOLATILE";
        }

        // Determine verification depth based on regime and events
        if (tradingRegime == "CLEAN" && !hazardWindowActive)
        {
            verificationDepth = "DEEP";
        }
        else if (tradingRegime == "HAZARD" || tradingRegime == "CRISIS")
        {
            verificationDepth = "SHALLOW";
        }

        // Override mode if crisis detected
        if (waterfallRisk == "HIGH" && tradingRegime == "CRISIS")
        {
            overallMode = "DEESCALATION_RISK";
        }

        return new NewsEngineResult(
            verificationDepth,
            liquidityQuality,
            hazardWindowActive,
            nextTier1UltraWithin90m,
            nextTier1UltraWithin45m,
            globalRegime,
            tradingRegime,
            spikeState,
            waterfallRisk,
            overallMode,
            macroBiasForGold,
            railAPermission,
            railBPermission,
            minutesToNextCleanWindow
        );
    }
}

// Supporting contracts
public record EconomicEventContract(
    DateTimeOffset EventTime,
    string Tier,  // Tier1, Tier2, Tier3, Ultra
    string Currency,
    string EventName,
    string? Actual,
    string? Forecast,
    string? Previous,
    string? Impact);  // High, Medium, Low

public record MacroIntelligenceContract(
    string? DxyBias,  // STRONG, NEUTRAL, WEAK
    string? YieldPressure,  // RISING, NEUTRAL, FALLING
    string? GeoRiskState,  // LOW, MEDIUM, HIGH, CRITICAL
    string? OilState,  // RISING, NEUTRAL, FALLING
    string? CBDemandState,
    string? InstitutionalDemandState,
    IReadOnlyList<string>? Headlines);