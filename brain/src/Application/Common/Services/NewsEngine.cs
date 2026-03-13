using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 8: NEWS ENGINE
/// Purpose: Environment and permission interpreter.
/// NEWS must output: VerificationDepth, LiquidityQuality, hazardWindowActive,
/// NextTier1UltraWithin90m/45m, GlobalRegime, TradingRegime, SpikeState, WaterfallRisk,
/// OverallMODE, MacroBiasForGold, Rail-A permission, Rail-B permission, MinutesToNextCleanWindow.
/// 
/// The raw feed tells us when news exists.
/// NEWS tells us what that environment means for gold.
/// </summary>
public static class NewsEngine
{
    public static NewsEngineResult Process(
        MarketSnapshotContract snapshot,
        WaterfallCrisisEngineResult waterfall,
        VolatilityRegimeEngineResult volatility,
        SessionEngineResult session)
    {
        // Verification depth (how much verification is needed)
        var verificationDepth = DetermineVerificationDepth(snapshot, waterfall);
        
        // Liquidity quality
        var liquidityQuality = DetermineLiquidityQuality(snapshot, volatility);
        
        // Hazard window active
        var hazardWindowActive = snapshot.IsUsRiskWindow || snapshot.NewsEventFlag;
        
        // Next Tier 1 Ultra event timing
        var nextTier1Ultra = DetermineNextTier1Ultra(snapshot, session);
        
        // Global regime
        var globalRegime = DetermineGlobalRegime(snapshot, volatility);
        
        // Trading regime
        var tradingRegime = DetermineTradingRegime(volatility, waterfall);
        
        // Spike state
        var spikeState = DetermineSpikeState(snapshot, volatility);
        
        // Waterfall risk (from waterfall engine, but NEWS interprets it)
        var waterfallRisk = waterfall.WaterfallRisk;
        
        // Overall mode
        var overallMode = DetermineOverallMode(snapshot, volatility, waterfall, session);
        
        // Macro bias for gold
        var macroBiasForGold = DetermineMacroBias(snapshot);
        
        // Rail permissions
        var (railAPermission, railBPermission) = DetermineRailPermissions(
            waterfall, 
            volatility, 
            hazardWindowActive,
            session);
        
        // Minutes to next clean window
        var minutesToNextCleanWindow = CalculateMinutesToNextCleanWindow(
            snapshot, 
            session, 
            hazardWindowActive);
        
        return new NewsEngineResult(
            VerificationDepth: verificationDepth,
            LiquidityQuality: liquidityQuality,
            HazardWindowActive: hazardWindowActive,
            NextTier1UltraWithin90m: nextTier1Ultra.Within90m,
            NextTier1UltraWithin45m: nextTier1Ultra.Within45m,
            GlobalRegime: globalRegime,
            TradingRegime: tradingRegime,
            SpikeState: spikeState,
            WaterfallRisk: waterfallRisk,
            OverallMODE: overallMode,
            MacroBiasForGold: macroBiasForGold,
            RailAPermission: railAPermission,
            RailBPermission: railBPermission,
            MinutesToNextCleanWindow: minutesToNextCleanWindow);
    }

    private static string DetermineVerificationDepth(
        MarketSnapshotContract snapshot,
        WaterfallCrisisEngineResult waterfall)
    {
        if (waterfall.WaterfallRisk == "HIGH" || waterfall.CrisisVeto)
        {
            return "DEEP";
        }
        
        if (snapshot.NewsEventFlag && snapshot.IsUsRiskWindow)
        {
            return "MEDIUM";
        }
        
        if (snapshot.NewsEventFlag || snapshot.IsUsRiskWindow)
        {
            return "LIGHT";
        }
        
        return "MINIMAL";
    }

    private static string DetermineLiquidityQuality(
        MarketSnapshotContract snapshot,
        VolatilityRegimeEngineResult volatility)
    {
        // Check spread quality
        var spreadQuality = snapshot.Spread < 0.5m ? "HIGH"
            : snapshot.Spread < 0.7m ? "MEDIUM"
            : "LOW";
        
        // Check volatility quality
        var volatilityQuality = volatility.VolatilityClass == "NORMAL" ? "HIGH"
            : volatility.VolatilityClass == "EXPANDED" ? "MEDIUM"
            : "LOW";
        
        // Aggregate
        if (spreadQuality == "HIGH" && volatilityQuality == "HIGH")
        {
            return "HIGH";
        }
        
        if (spreadQuality == "LOW" || volatilityQuality == "LOW")
        {
            return "LOW";
        }
        
        return "MEDIUM";
    }

    private static (bool Within90m, bool Within45m) DetermineNextTier1Ultra(
        MarketSnapshotContract snapshot,
        SessionEngineResult session)
    {
        // Simplified: check if we're in a high-risk news window
        // Actual implementation would parse calendar feed
        var within90m = snapshot.IsUsRiskWindow && snapshot.NewsEventFlag;
        var within45m = within90m && session.Phase == "END";
        
        return (within90m, within45m);
    }

    private static string DetermineGlobalRegime(
        MarketSnapshotContract snapshot,
        VolatilityRegimeEngineResult volatility)
    {
        if (snapshot.GeoRiskFlag)
        {
            return "GEOPOLITICAL_SHOCK";
        }
        
        if (volatility.Regime == "SHOCK" || volatility.Regime == "LIQUIDATION")
        {
            return "CRISIS";
        }
        
        if (volatility.Regime == "NEWS_SPIKE")
        {
            return "NEWS_DRIVEN";
        }
        
        return "NORMAL";
    }

    private static string DetermineTradingRegime(
        VolatilityRegimeEngineResult volatility,
        WaterfallCrisisEngineResult waterfall)
    {
        if (waterfall.PatternType == "WATERFALL_CONTINUATION")
        {
            return "WATERFALL";
        }
        
        return volatility.Regime;
    }

    private static string DetermineSpikeState(
        MarketSnapshotContract snapshot,
        VolatilityRegimeEngineResult volatility)
    {
        if (snapshot.HasPanicDropSequence)
        {
            return "PANIC_SPIKE";
        }
        
        if (volatility.Regime == "NEWS_SPIKE" && snapshot.NewsEventFlag)
        {
            return "NEWS_SPIKE";
        }
        
        if (volatility.Regime == "EXPANSION" && snapshot.HasImpulseCandles)
        {
            return "IMPULSE_SPIKE";
        }
        
        return "NONE";
    }

    private static string DetermineOverallMode(
        MarketSnapshotContract snapshot,
        VolatilityRegimeEngineResult volatility,
        WaterfallCrisisEngineResult waterfall,
        SessionEngineResult session)
    {
        if (waterfall.CrisisVeto || waterfall.WaterfallRisk == "HIGH")
        {
            return "BLOCK";
        }
        
        if (volatility.ShouldBlockNewTrades)
        {
            return "CAUTION";
        }
        
        if (snapshot.NewsEventFlag && session.Phase == "START")
        {
            return "WAIT";
        }
        
        return "NORMAL";
    }

    private static string DetermineMacroBias(MarketSnapshotContract snapshot)
    {
        // Simplified: would use DXY, yields, etc. from snapshot
        // DXYState, YieldPressureState would be in snapshot
        return "NEUTRAL"; // Default, actual implementation would analyze macro factors
    }

    private static (string RailAPermission, string RailBPermission) DetermineRailPermissions(
        WaterfallCrisisEngineResult waterfall,
        VolatilityRegimeEngineResult volatility,
        bool hazardWindowActive,
        SessionEngineResult session)
    {
        var railA = "YES";
        var railB = "YES";
        
        // Block Rail-B if high waterfall risk
        if (waterfall.WaterfallRisk == "HIGH")
        {
            railB = "NO";
            railA = "ONLY_AFTER_STRUCTURE";
        }
        
        // Block Rail-B if volatility extreme
        if (volatility.VolatilityClass == "EXTREME")
        {
            railB = "NO";
            railA = "ONLY_AFTER_STRUCTURE";
        }
        
        // Block both if hazard window active
        if (hazardWindowActive && session.Phase == "START")
        {
            railA = "NO";
            railB = "NO";
        }
        
        // Block Rail-B if crisis veto
        if (waterfall.CrisisVeto)
        {
            railB = "NO";
            railA = "NO";
        }
        
        return (railA, railB);
    }

    private static int CalculateMinutesToNextCleanWindow(
        MarketSnapshotContract snapshot,
        SessionEngineResult session,
        bool hazardWindowActive)
    {
        if (!hazardWindowActive)
        {
            return 0; // Already in clean window
        }
        
        // Simplified: estimate based on session phase
        // Actual implementation would parse calendar feed
        if (session.Phase == "START")
        {
            return 30; // Rough estimate
        }
        
        if (session.Phase == "MID")
        {
            return 60; // Rough estimate
        }
        
        return 90; // Default estimate
    }
}

/// <summary>
/// News Engine output contract
/// </summary>
public sealed record NewsEngineResult(
    string VerificationDepth,
    string LiquidityQuality,
    bool HazardWindowActive,
    bool NextTier1UltraWithin90m,
    bool NextTier1UltraWithin45m,
    string GlobalRegime,
    string TradingRegime,
    string SpikeState,
    string WaterfallRisk,
    string OverallMODE,
    string MacroBiasForGold,
    string RailAPermission,
    string RailBPermission,
    int MinutesToNextCleanWindow);
