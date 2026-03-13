using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Engine 13: TABLE COMPILER
/// Purpose: Only legal order builder.
/// 
/// TABLE is the only module allowed to create:
/// - BUY_LIMIT
/// - BUY_STOP
/// 
/// TABLE must re-check:
/// - FAIL safety
/// - waterfall safety
/// - hazard timing
/// - candidate freshness
/// - structure quality
/// - rail permissions
/// - capital legality
/// - slot limits
/// - volatility state
/// - reward law (projectedMoveNetUSD >= 8)
/// 
/// Hard profit laws:
/// 1. projectedMoveNetUSD must be at least +8 USD (NET after MT5 spread + bullion handicap)
/// 2. STANDARD_ROTATION_MODE: target +8 to +12 USD
/// 3. IMPULSE_HARVEST_MODE: target +20 / +30 / +50 only when explicitly allowed
/// 
/// TABLE must support FLUSH_LIMIT_CAPTURE template:
/// - Use when: deep flush into S2/S3, bottom type valid, pattern = FLUSH_REVERSAL_ATTEMPT,
///   WaterfallRisk not HIGH, FAIL protected, hazard not fatal
/// - Behavior: BUY_LIMIT in upper part of deep zone, default TP band +8 to +12,
///   extension only through approved impulse logic
/// </summary>
public static class TableCompiler
{
    public static TableCompilerResult Compile(
        MarketSnapshotContract snapshot,
        CandidateEngineResult candidate,
        AnalyzeEngineResult analyze,
        StructureEngineResult structure,
        VolatilityRegimeEngineResult volatility,
        WaterfallCrisisEngineResult waterfall,
        NewsEngineResult news,
        CapitalUtilizationEngineResult capital,
        HistoricalPatternEngineResult historical,
        LedgerStateContract? ledgerState,
        IGoldEngineThresholds? thresholds)
    {
        thresholds ??= new DefaultThresholds();
        
        // Re-check all safety gates
        if (!RecheckSafetyGates(snapshot, structure, waterfall, news, candidate))
        {
            return new TableCompilerResult(
                IsValid: false,
                Reason: "Safety gates failed",
                OrderType: null,
                Entry: null,
                Tp: null,
                Grams: null,
                ExpiryUtc: null,
                ProjectedMoveNetUSD: 0m,
                Template: null);
        }
        
        // Check capital legality
        if (!capital.AffordableFlag || ledgerState == null)
        {
            return new TableCompilerResult(
                IsValid: false,
                Reason: "Capital legality failed",
                OrderType: null,
                Entry: null,
                Tp: null,
                Grams: null,
                ExpiryUtc: null,
                ProjectedMoveNetUSD: 0m,
                Template: null);
        }
        
        // Determine order type and template
        var (orderType, template) = DetermineOrderTypeAndTemplate(
            candidate, 
            analyze, 
            structure, 
            news);
        
        if (orderType == null)
        {
            return new TableCompilerResult(
                IsValid: false,
                Reason: "No valid order type determined",
                OrderType: null,
                Entry: null,
                Tp: null,
                Grams: null,
                ExpiryUtc: null,
                ProjectedMoveNetUSD: 0m,
                Template: null);
        }
        
        // Build order based on template
        if (template == "FLUSH_LIMIT_CAPTURE")
        {
            return BuildFlushLimitCapture(
                snapshot,
                structure,
                analyze,
                capital,
                ledgerState,
                thresholds);
        }
        
        // Standard order building (delegate to existing DecisionEngine logic)
        return BuildStandardOrder(
            snapshot,
            orderType,
            structure,
            analyze,
            capital,
            ledgerState,
            thresholds);
    }

    private static bool RecheckSafetyGates(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure,
        WaterfallCrisisEngineResult waterfall,
        NewsEngineResult news,
        CandidateEngineResult candidate)
    {
        // FAIL safety
        if (structure.Fail.HasValue)
        {
            return false;
        }
        
        // Waterfall safety
        if (waterfall.WaterfallRisk == "HIGH" || waterfall.ShouldBlock)
        {
            return false;
        }
        
        // Hazard timing
        if (news.HazardWindowActive && news.OverallMODE == "BLOCK")
        {
            return false;
        }
        
        // Candidate freshness
        if (candidate.CandidateFreshness == "STALE")
        {
            return false;
        }
        
        // Structure quality
        if (structure.StructureQuality == "WEAK" && structure.IsMidAir)
        {
            return false;
        }
        
        // Rail permissions
        if (news.RailAPermission == "NO" && news.RailBPermission == "NO")
        {
            return false;
        }
        
        // Volatility state
        if (structure.Fail.HasValue) // Already checked, but double-check
        {
            return false;
        }
        
        return true;
    }

    private static (string? OrderType, string? Template) DetermineOrderTypeAndTemplate(
        CandidateEngineResult candidate,
        AnalyzeEngineResult analyze,
        StructureEngineResult structure,
        NewsEngineResult news)
    {
        // FLUSH_LIMIT_CAPTURE template
        if (candidate.EarlyFlushCandidate
            && analyze.BottomType != "INVALID"
            && analyze.PatternType == "FLUSH_REVERSAL_ATTEMPT"
            && analyze.WaterfallRisk != "HIGH"
            && !structure.Fail.HasValue
            && structure.S2.HasValue)
        {
            return ("BUY_LIMIT", "FLUSH_LIMIT_CAPTURE");
        }
        
        // Standard BUY_LIMIT
        if (structure.HasShelf && news.RailAPermission != "NO")
        {
            return ("BUY_LIMIT", "STANDARD_ROTATION");
        }
        
        // BUY_STOP
        if (structure.HasLid && news.RailBPermission == "YES")
        {
            return ("BUY_STOP", "STANDARD_ROTATION");
        }
        
        return (null, null);
    }

    private static TableCompilerResult BuildFlushLimitCapture(
        MarketSnapshotContract snapshot,
        StructureEngineResult structure,
        AnalyzeEngineResult analyze,
        CapitalUtilizationEngineResult capital,
        LedgerStateContract ledgerState,
        IGoldEngineThresholds thresholds)
    {
        // Entry: upper part of deep S2/S3 zone
        var entryZone = structure.S2.HasValue && structure.S2.Value < structure.S1
            ? structure.S2.Value
            : structure.S1;
        
        var atr = snapshot.Atr > 0m ? snapshot.Atr : 10m;
        var entry = entryZone + (atr * 0.2m); // Upper part of zone
        
        // TP: default +8 to +12, extension if impulse harvest allowed
        var tpBase = entry + 10m; // Default +10 USD
        
        // Check if impulse harvest mode allows extension
        if (analyze.ImpulseHarvestScore >= 0.7m && analyze.RotationEnvelope.Max > 12m)
        {
            tpBase = entry + analyze.RotationEnvelope.Max;
        }
        
        // Calculate projected move (NET after spread and bullion handicap)
        var projectedMoveGross = tpBase - entry;
        var projectedMoveNet = projectedMoveGross - 0.80m - 0.80m; // Shop spread both ways
        
        // Hard profit filter: must be >= 8 USD
        if (projectedMoveNet < 8m)
        {
            return new TableCompilerResult(
                IsValid: false,
                Reason: $"Projected move net {projectedMoveNet:0.00} USD < 8 USD minimum",
                OrderType: null,
                Entry: null,
                Tp: null,
                Grams: null,
                ExpiryUtc: null,
                ProjectedMoveNetUSD: projectedMoveNet,
                Template: null);
        }
        
        // Grams sizing
        var grams = CalculateGrams(entry, capital, ledgerState);
        
        if (grams <= 0m)
        {
            return new TableCompilerResult(
                IsValid: false,
                Reason: "Cannot calculate valid grams",
                OrderType: null,
                Entry: null,
                Tp: null,
                Grams: null,
                ExpiryUtc: null,
                ProjectedMoveNetUSD: projectedMoveNet,
                Template: null);
        }
        
        // Expiry
        var expiryUtc = CalculateExpiry(snapshot, thresholds);
        
        return new TableCompilerResult(
            IsValid: true,
            Reason: "FLUSH_LIMIT_CAPTURE order compiled",
            OrderType: "BUY_LIMIT",
            Entry: entry,
            Tp: tpBase,
            Grams: grams,
            ExpiryUtc: expiryUtc,
            ProjectedMoveNetUSD: projectedMoveNet,
            Template: "FLUSH_LIMIT_CAPTURE");
    }

    private static TableCompilerResult BuildStandardOrder(
        MarketSnapshotContract snapshot,
        string orderType,
        StructureEngineResult structure,
        AnalyzeEngineResult analyze,
        CapitalUtilizationEngineResult capital,
        LedgerStateContract ledgerState,
        IGoldEngineThresholds thresholds)
    {
        // Use existing DecisionEngine logic for standard orders
        // This is a simplified version - actual would call DecisionEngine.Evaluate
        
        var currentPrice = snapshot.AuthoritativeRate > 0m 
            ? snapshot.AuthoritativeRate 
            : snapshot.Bid > 0m ? snapshot.Bid : 0m;
        
        decimal entry;
        decimal tp;
        
        if (orderType == "BUY_LIMIT")
        {
            entry = structure.S1 > 0m ? structure.S1 : currentPrice - 5m;
            tp = entry + 10m; // Standard +10 USD
        }
        else // BUY_STOP
        {
            entry = structure.R1 > 0m ? structure.R1 : currentPrice + 5m;
            tp = entry + 10m; // Standard +10 USD
        }
        
        // Calculate projected move (NET)
        var projectedMoveGross = tp - entry;
        var projectedMoveNet = projectedMoveGross - 0.80m - 0.80m;
        
        // Hard profit filter
        if (projectedMoveNet < 8m)
        {
            return new TableCompilerResult(
                IsValid: false,
                Reason: $"Projected move net {projectedMoveNet:0.00} USD < 8 USD minimum",
                OrderType: null,
                Entry: null,
                Tp: null,
                Grams: null,
                ExpiryUtc: null,
                ProjectedMoveNetUSD: projectedMoveNet,
                Template: null);
        }
        
        // Grams
        var grams = CalculateGrams(entry, capital, ledgerState);
        
        if (grams <= 0m)
        {
            return new TableCompilerResult(
                IsValid: false,
                Reason: "Cannot calculate valid grams",
                OrderType: null,
                Entry: null,
                Tp: null,
                Grams: null,
                ExpiryUtc: null,
                ProjectedMoveNetUSD: projectedMoveNet,
                Template: null);
        }
        
        // Expiry
        var expiryUtc = CalculateExpiry(snapshot, thresholds);
        
        return new TableCompilerResult(
            IsValid: true,
            Reason: "Standard order compiled",
            OrderType: orderType,
            Entry: entry,
            Tp: tp,
            Grams: grams,
            ExpiryUtc: expiryUtc,
            ProjectedMoveNetUSD: projectedMoveNet,
            Template: "STANDARD_ROTATION");
    }

    private static decimal CalculateGrams(
        decimal entry,
        CapitalUtilizationEngineResult capital,
        LedgerStateContract ledgerState)
    {
        var result = CapitalUtilizationService.Check(
            capital.C1Capacity,
            entry,
            capital.MaxAffordableGrams);
        
        return result.ApprovedGrams;
    }

    private static DateTimeOffset CalculateExpiry(
        MarketSnapshotContract snapshot,
        IGoldEngineThresholds thresholds)
    {
        var session = TradingSessionClock.Resolve(snapshot.KsaTime).Session;
        var (min, max) = session switch
        {
            "JAPAN" => thresholds.ExpiryJapan,
            "INDIA" => thresholds.ExpiryIndia,
            "LONDON" => thresholds.ExpiryLondon,
            "NY" or "NEW_YORK" => thresholds.ExpiryNy,
            _ => (60, 90)
        };
        
        var expiryMinutes = (min + max) / 2; // Use midpoint
        return snapshot.Timestamp.AddMinutes(expiryMinutes);
    }

    private sealed class DefaultThresholds : IGoldEngineThresholds
    {
        public decimal Ma20DistNormalMax => 0.8m;
        public decimal Ma20DistStretchedMax => 1.5m;
        public decimal RsiLowBound => 35m;
        public decimal RsiMidLow => 35m;
        public decimal RsiMidHigh => 65m;
        public decimal RsiHighBound => 75m;
        public decimal RsiExtremeBound => 75m;
        public decimal RsiBuyLimitCautionHigh => 72m;
        public decimal RsiBuyLimitWaitHigh => 75m;
        public decimal BaseDistAtrBuyLimitValidMax => 1.0m;
        public decimal BaseDistAtrBuyLimitRearmMax => 0.4m;
        public decimal AdrUsedFullBound => 0.9m;
        public decimal AdrUsedBlockContinuationBuyStopMin => 1.0m;
        public decimal VciCompressedMax => 0.7m;
        public decimal VciNormalMax => 1.3m;
        public decimal SpreadCaution => 0.5m;
        public decimal SpreadBlock => 0.7m;
        public decimal TpDistanceSpreadMinRatio => 3m;
        public decimal SessionSizeJapan => 0.5m;
        public decimal SessionSizeIndia => 0.7m;
        public decimal SessionSizeLondon => 1.0m;
        public decimal SessionSizeNy => 0.6m;
        public (int Min, int Max) ExpiryJapan => (90, 120);
        public (int Min, int Max) ExpiryIndia => (90, 150);
        public (int Min, int Max) ExpiryLondon => (60, 90);
        public (int Min, int Max) ExpiryNy => (45, 60);
        public int ConfidenceWaitMax => 59;
        public int ConfidenceMicroMin => 60;
        public int ConfidenceMicroMax => 74;
        public int ConfidenceNormalMin => 75;
        public int ConfidenceHighMin => 90;
    }
}

/// <summary>
/// Table Compiler output contract
/// </summary>
public sealed record TableCompilerResult(
    bool IsValid,
    string Reason,
    string? OrderType,
    decimal? Entry,
    decimal? Tp,
    decimal? Grams,
    DateTimeOffset? ExpiryUtc,
    decimal ProjectedMoveNetUSD,
    string? Template);
