using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 7: VERIFY ENGINE
/// Purpose: Credibility filter and external signal parser.
/// Checks Zone/SL/TP tags and performs alignment checks.
/// 
/// Hard law:
/// - VERIFY never creates a trade
/// - VERIFY never overrides FAIL / waterfall / hazard
/// - VERIFY may only help already-legal candidates get attention first
/// </summary>
public static class VerifyEngine
{
    public static VerifyEngineResult Process(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure,
        SessionEngineResult session)
    {
        // Parse external signals (Telegram, TradingView)
        var telegramSignal = ParseTelegramSignal(snapshot);
        var tradingViewSignal = ParseTradingViewSignal(snapshot);
        
        // Credibility assessment
        var credibility = AssessCredibility(telegramSignal, tradingViewSignal);
        
        // Zone alignment checks
        var zoneAlignment = CheckZoneAlignment(
            telegramSignal, 
            tradingViewSignal, 
            structure);
        
        // SL/TP hints from external signals
        var slHint = ExtractSlHint(telegramSignal, tradingViewSignal, structure);
        var tpPipsList = ExtractTpPipsList(telegramSignal, tradingViewSignal);
        
        // Candidate priority modifier
        var candidatePriorityModifier = CalculateCandidatePriorityModifier(
            credibility, 
            zoneAlignment);
        
        // Confidence score modifier
        var confidenceScoreModifier = CalculateConfidenceModifier(
            credibility, 
            zoneAlignment);
        
        // Impulse harvest score modifier
        var impulseHarvestScoreModifier = CalculateImpulseHarvestModifier(
            credibility, 
            zoneAlignment);
        
        return new VerifyEngineResult(
            CredibilityClass: credibility.Class,
            CredibilityReason: credibility.Reason,
            StyleTag: credibility.StyleTag,
            AdvisoryWeight: credibility.Weight,
            ZoneLow: zoneAlignment.ZoneLow,
            ZoneHigh: zoneAlignment.ZoneHigh,
            SlHint: slHint,
            TpPipsList: tpPipsList,
            TelegramZoneAligned: zoneAlignment.TelegramAligned,
            TvZoneAligned: zoneAlignment.TradingViewAligned,
            CandidatePriorityModifier: candidatePriorityModifier,
            ConfidenceScoreModifier: confidenceScoreModifier,
            ImpulseHarvestScoreModifier: impulseHarvestScoreModifier);
    }

    private static (string Class, string Reason, string StyleTag, decimal Weight) AssessCredibility(
        TelegramSignal? telegram,
        TradingViewSignal? tradingView)
    {
        var signals = new List<(string Source, decimal Weight)>();
        
        if (telegram != null && telegram.IsValid)
        {
            signals.Add(("TELEGRAM", telegram.Weight));
        }
        
        if (tradingView != null && tradingView.IsValid)
        {
            signals.Add(("TRADINGVIEW", tradingView.Weight));
        }
        
        if (signals.Count == 0)
        {
            return ("NONE", "No external signals", "UNKNOWN", 0m);
        }
        
        var totalWeight = signals.Sum(s => s.Weight);
        var avgWeight = totalWeight / signals.Count;
        
        var credibilityClass = avgWeight >= 0.8m ? "HIGH"
            : avgWeight >= 0.6m ? "MEDIUM"
            : "LOW";
        
        var styleTag = signals.Count > 1 ? "MULTI_SOURCE" 
            : signals[0].Source == "TELEGRAM" ? "TELEGRAM_ONLY"
            : "TRADINGVIEW_ONLY";
        
        return (credibilityClass, 
            $"Credibility: {credibilityClass} from {signals.Count} source(s), avg weight {avgWeight:0.##}",
            styleTag,
            avgWeight);
    }

    private static (decimal? ZoneLow, decimal? ZoneHigh, bool TelegramAligned, bool TradingViewAligned) CheckZoneAlignment(
        TelegramSignal? telegram,
        TradingViewSignal? tradingView,
        StructureEngineResult structure)
    {
        decimal? zoneLow = null;
        decimal? zoneHigh = null;
        bool telegramAligned = false;
        bool tradingViewAligned = false;
        
        // Check Telegram zone alignment with structure
        if (telegram != null && telegram.IsValid && telegram.ZoneLow.HasValue && telegram.ZoneHigh.HasValue)
        {
            zoneLow = telegram.ZoneLow;
            zoneHigh = telegram.ZoneHigh;
            
            // Check if Telegram zone overlaps with S1/S2/S3
            telegramAligned = OverlapsWithStructure(
                telegram.ZoneLow.Value, 
                telegram.ZoneHigh.Value, 
                structure);
        }
        
        // Check TradingView zone alignment with structure
        if (tradingView != null && tradingView.IsValid && tradingView.ZoneLow.HasValue && tradingView.ZoneHigh.HasValue)
        {
            if (!zoneLow.HasValue) zoneLow = tradingView.ZoneLow;
            if (!zoneHigh.HasValue) zoneHigh = tradingView.ZoneHigh;
            
            tradingViewAligned = OverlapsWithStructure(
                tradingView.ZoneLow.Value, 
                tradingView.ZoneHigh.Value, 
                structure);
        }
        
        return (zoneLow, zoneHigh, telegramAligned, tradingViewAligned);
    }

    private static bool OverlapsWithStructure(
        decimal zoneLow,
        decimal zoneHigh,
        StructureEngineResult structure)
    {
        // Check if zone overlaps with S1, S2, S3
        var structureLevels = new List<decimal> { structure.S1 };
        if (structure.S2.HasValue) structureLevels.Add(structure.S2.Value);
        if (structure.S3.HasValue) structureLevels.Add(structure.S3.Value);
        
        return structureLevels.Any(level => level >= zoneLow && level <= zoneHigh);
    }

    private static decimal? ExtractSlHint(
        TelegramSignal? telegram,
        TradingViewSignal? tradingView,
        StructureEngineResult structure)
    {
        // Prefer Telegram SL hint, then TradingView, then structure S1
        if (telegram != null && telegram.StopLoss.HasValue)
        {
            return telegram.StopLoss;
        }
        
        if (tradingView != null && tradingView.StopLoss.HasValue)
        {
            return tradingView.StopLoss;
        }
        
        return structure.S1 > 0m ? structure.S1 : null;
    }

    private static IReadOnlyCollection<decimal> ExtractTpPipsList(
        TelegramSignal? telegram,
        TradingViewSignal? tradingView)
    {
        var tpList = new List<decimal>();
        
        // XAU pip convention: 1 pip = 0.10 USD
        if (telegram != null && telegram.TakeProfitPips != null)
        {
            tpList.AddRange(telegram.TakeProfitPips.Select(pips => pips * 0.10m));
        }
        
        if (tradingView != null && tradingView.TakeProfitPips != null)
        {
            tpList.AddRange(tradingView.TakeProfitPips.Select(pips => pips * 0.10m));
        }
        
        return tpList;
    }

    private static decimal CalculateCandidatePriorityModifier(
        (string Class, string Reason, string StyleTag, decimal Weight) credibility,
        (decimal? ZoneLow, decimal? ZoneHigh, bool TelegramAligned, bool TradingViewAligned) zoneAlignment)
    {
        var modifier = 1.0m;
        
        // Boost priority if high credibility
        if (credibility.Class == "HIGH")
        {
            modifier += 0.2m;
        }
        else if (credibility.Class == "MEDIUM")
        {
            modifier += 0.1m;
        }
        
        // Boost priority if zone aligned
        if (zoneAlignment.TelegramAligned || zoneAlignment.TradingViewAligned)
        {
            modifier += 0.15m;
        }
        
        return Math.Min(modifier, 1.5m); // Cap at 1.5x
    }

    private static decimal CalculateConfidenceModifier(
        (string Class, string Reason, string StyleTag, decimal Weight) credibility,
        (decimal? ZoneLow, decimal? ZoneHigh, bool TelegramAligned, bool TradingViewAligned) zoneAlignment)
    {
        var modifier = 1.0m;
        
        // Boost confidence if high credibility and aligned
        if (credibility.Class == "HIGH" && (zoneAlignment.TelegramAligned || zoneAlignment.TradingViewAligned))
        {
            modifier += 0.1m;
        }
        
        return Math.Min(modifier, 1.2m); // Cap at 1.2x
    }

    private static decimal CalculateImpulseHarvestModifier(
        (string Class, string Reason, string StyleTag, decimal Weight) credibility,
        (decimal? ZoneLow, decimal? ZoneHigh, bool TelegramAligned, bool TradingViewAligned) zoneAlignment)
    {
        // Only boost impulse harvest if both high credibility and aligned
        if (credibility.Class == "HIGH" && (zoneAlignment.TelegramAligned || zoneAlignment.TradingViewAligned))
        {
            return 1.1m;
        }
        
        return 1.0m;
    }

    private static TelegramSignal? ParseTelegramSignal(MarketSnapshotContract snapshot)
    {
        // Parse Telegram state from snapshot
        var telegramState = snapshot.TelegramState;
        if (string.IsNullOrWhiteSpace(telegramState) || telegramState == "QUIET")
        {
            return null;
        }
        
        // Extract zone/SL/TP from Telegram state (if available in snapshot)
        // This is a simplified parser - actual implementation would parse Telegram messages
        return new TelegramSignal(
            IsValid: true,
            Weight: telegramState.Contains("STRONG") ? 0.9m : 0.7m,
            ZoneLow: null, // Would be parsed from actual Telegram message
            ZoneHigh: null,
            StopLoss: null,
            TakeProfitPips: null);
    }

    private static TradingViewSignal? ParseTradingViewSignal(MarketSnapshotContract snapshot)
    {
        // Parse TradingView signal from snapshot
        var tvAlertType = snapshot.TvAlertType;
        if (string.IsNullOrWhiteSpace(tvAlertType) || tvAlertType == "NONE")
        {
            return null;
        }
        
        // Extract zone/SL/TP from TradingView alert (if available)
        return new TradingViewSignal(
            IsValid: true,
            Weight: 0.8m,
            ZoneLow: null, // Would be parsed from actual TradingView alert
            ZoneHigh: null,
            StopLoss: null,
            TakeProfitPips: null);
    }
}

/// <summary>
/// Verify Engine output contract
/// </summary>
public sealed record VerifyEngineResult(
    string CredibilityClass,
    string CredibilityReason,
    string StyleTag,
    decimal AdvisoryWeight,
    decimal? ZoneLow,
    decimal? ZoneHigh,
    decimal? SlHint,
    IReadOnlyCollection<decimal> TpPipsList,
    bool TelegramZoneAligned,
    bool TvZoneAligned,
    decimal CandidatePriorityModifier,
    decimal ConfidenceScoreModifier,
    decimal ImpulseHarvestScoreModifier);

/// <summary>
/// Telegram signal model
/// </summary>
internal sealed record TelegramSignal(
    bool IsValid,
    decimal Weight,
    decimal? ZoneLow,
    decimal? ZoneHigh,
    decimal? StopLoss,
    IReadOnlyCollection<decimal>? TakeProfitPips);

/// <summary>
/// TradingView signal model
/// </summary>
internal sealed record TradingViewSignal(
    bool IsValid,
    decimal Weight,
    decimal? ZoneLow,
    decimal? ZoneHigh,
    decimal? StopLoss,
    IReadOnlyCollection<decimal>? TakeProfitPips);
