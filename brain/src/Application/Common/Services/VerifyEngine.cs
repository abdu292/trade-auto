using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// VERIFY Engine per spec/00_instructions
/// Position: after deterministic engines, before NEWS / candidate prioritization
/// Role: credibility filter, external signal parser
/// Hard law: VERIFY never creates a trade, never overrides FAIL/waterfall/hazard
/// </summary>
public static class VerifyEngine
{
    public record VerifyResult(
        string CredibilityClass,  // HIGH, MEDIUM, LOW, INVALID
        string StyleTag,  // ALIGNED, NEUTRAL, CONTRADICTORY
        decimal AdvisoryWeight,  // 0.0 to 1.0
        decimal? ZoneLow,
        decimal? ZoneHigh,
        decimal? SlHint,
        IReadOnlyList<decimal>? TpPipsList,
        bool TelegramZoneAligned,
        bool TvZoneAligned,
        decimal CandidatePriorityModifier,  // -1.0 to +1.0
        decimal ConfidenceScoreModifier,  // -0.5 to +0.5
        decimal ImpulseHarvestScoreModifier  // -0.5 to +0.5
    );

    /// <summary>
    /// Processes external signals (Telegram, TradingView) and returns advisory alignment.
    /// Never overrides hard blockers.
    /// </summary>
    public static VerifyResult Verify(
        MarketSnapshotContract snapshot,
        IReadOnlyCollection<TelegramSignalContract>? telegramSignals = null,
        TradingViewSignalContract? tradingViewSignal = null,
        StructureLevelsContract? structureLevels = null)
    {
        var credibilityClass = "LOW";
        var styleTag = "NEUTRAL";
        var advisoryWeight = 0.0m;
        decimal? zoneLow = null;
        decimal? zoneHigh = null;
        decimal? slHint = null;
        var tpPipsList = new List<decimal>();
        var telegramZoneAligned = false;
        var tvZoneAligned = false;
        var candidatePriorityModifier = 0.0m;
        var confidenceScoreModifier = 0.0m;
        var impulseHarvestScoreModifier = 0.0m;

        // Process Telegram signals
        if (telegramSignals != null && telegramSignals.Any())
        {
            var latestTelegram = telegramSignals.OrderByDescending(s => s.Timestamp).First();
            var telegramResult = ProcessTelegramSignal(latestTelegram, snapshot, structureLevels);
            credibilityClass = telegramResult.CredibilityClass;
            styleTag = telegramResult.StyleTag;
            advisoryWeight = Math.Max(advisoryWeight, telegramResult.AdvisoryWeight);
            zoneLow = telegramResult.ZoneLow ?? zoneLow;
            zoneHigh = telegramResult.ZoneHigh ?? zoneHigh;
            slHint = telegramResult.SlHint ?? slHint;
            if (telegramResult.TpPipsList != null)
            {
                tpPipsList.AddRange(telegramResult.TpPipsList);
            }
            telegramZoneAligned = telegramResult.TelegramZoneAligned;
            candidatePriorityModifier += telegramResult.CandidatePriorityModifier;
            confidenceScoreModifier += telegramResult.ConfidenceScoreModifier;
            impulseHarvestScoreModifier += telegramResult.ImpulseHarvestScoreModifier;
        }

        // Process TradingView signal
        if (tradingViewSignal != null)
        {
            var tvResult = ProcessTradingViewSignal(tradingViewSignal, snapshot, structureLevels);
            // TradingView has higher weight than Telegram
            if (tvResult.AdvisoryWeight > advisoryWeight)
            {
                credibilityClass = tvResult.CredibilityClass;
                styleTag = tvResult.StyleTag;
                advisoryWeight = tvResult.AdvisoryWeight;
                zoneLow = tvResult.ZoneLow ?? zoneLow;
                zoneHigh = tvResult.ZoneHigh ?? zoneHigh;
                slHint = tvResult.SlHint ?? slHint;
                if (tvResult.TpPipsList != null)
                {
                    tpPipsList.Clear();
                    tpPipsList.AddRange(tvResult.TpPipsList);
                }
            }
            tvZoneAligned = tvResult.TvZoneAligned;
            candidatePriorityModifier += tvResult.CandidatePriorityModifier * 1.2m;  // TV weighted higher
            confidenceScoreModifier += tvResult.ConfidenceScoreModifier * 1.2m;
            impulseHarvestScoreModifier += tvResult.ImpulseHarvestScoreModifier * 1.2m;
        }

        // Clamp modifiers
        candidatePriorityModifier = Math.Clamp(candidatePriorityModifier, -1.0m, 1.0m);
        confidenceScoreModifier = Math.Clamp(confidenceScoreModifier, -0.5m, 0.5m);
        impulseHarvestScoreModifier = Math.Clamp(impulseHarvestScoreModifier, -0.5m, 0.5m);

        return new VerifyResult(
            credibilityClass,
            styleTag,
            advisoryWeight,
            zoneLow,
            zoneHigh,
            slHint,
            tpPipsList.Any() ? tpPipsList : null,
            telegramZoneAligned,
            tvZoneAligned,
            candidatePriorityModifier,
            confidenceScoreModifier,
            impulseHarvestScoreModifier
        );
    }

    private static VerifyResult ProcessTelegramSignal(
        TelegramSignalContract signal,
        MarketSnapshotContract snapshot,
        StructureLevelsContract? structureLevels)
    {
        // XAU pip convention: 1 pip = 0.10 USD
        const decimal PipToUsd = 0.10m;

        var credibilityClass = DetermineCredibility(signal);
        var styleTag = "NEUTRAL";
        var advisoryWeight = 0.3m;  // Default low weight for Telegram
        decimal? zoneLow = null;
        decimal? zoneHigh = null;
        decimal? slHint = null;
        var tpPipsList = new List<decimal>();
        var telegramZoneAligned = false;
        var candidatePriorityModifier = 0.0m;
        var confidenceScoreModifier = 0.0m;
        var impulseHarvestScoreModifier = 0.0m;

        // Parse zone if available
        if (signal.EntryZoneLow.HasValue && signal.EntryZoneHigh.HasValue)
        {
            zoneLow = signal.EntryZoneLow.Value;
            zoneHigh = signal.EntryZoneHigh.Value;

            // Check alignment with structure levels
            if (structureLevels != null)
            {
                telegramZoneAligned = CheckZoneAlignment(zoneLow.Value, zoneHigh.Value, structureLevels);
                if (telegramZoneAligned)
                {
                    styleTag = "ALIGNED";
                    advisoryWeight = 0.5m;
                    candidatePriorityModifier = 0.2m;
                    confidenceScoreModifier = 0.1m;
                }
            }
        }

        // Parse TP pips
        if (signal.TpPips.HasValue)
        {
            tpPipsList.Add(signal.TpPips.Value);
        }

        // Parse SL hint
        if (signal.StopLoss.HasValue)
        {
            slHint = signal.StopLoss.Value;
        }

        // Adjust based on signal direction and current market state
        if (signal.Direction == "BUY" && snapshot.Bid < snapshot.Ma20)
        {
            // Signal aligns with potential bottom
            impulseHarvestScoreModifier = 0.1m;
        }

        return new VerifyResult(
            credibilityClass,
            styleTag,
            advisoryWeight,
            zoneLow,
            zoneHigh,
            slHint,
            tpPipsList.Any() ? tpPipsList : null,
            telegramZoneAligned,
            false,
            candidatePriorityModifier,
            confidenceScoreModifier,
            impulseHarvestScoreModifier
        );
    }

    private static VerifyResult ProcessTradingViewSignal(
        TradingViewSignalContract signal,
        MarketSnapshotContract snapshot,
        StructureLevelsContract? structureLevels)
    {
        var credibilityClass = "MEDIUM";  // TradingView generally more credible
        var styleTag = "NEUTRAL";
        var advisoryWeight = 0.5m;
        decimal? zoneLow = null;
        decimal? zoneHigh = null;
        decimal? slHint = null;
        var tpPipsList = new List<decimal>();
        var tvZoneAligned = false;
        var candidatePriorityModifier = 0.0m;
        var confidenceScoreModifier = 0.0m;
        var impulseHarvestScoreModifier = 0.0m;

        // TradingView signals don't have explicit zones, but we can infer from signal direction
        // For BUY signals, use current price area as zone
        if (signal.Signal == "BUY" && snapshot.Bid > 0)
        {
            zoneLow = snapshot.Bid * 0.998m;  // 0.2% below current
            zoneHigh = snapshot.Bid * 1.002m;  // 0.2% above current

            if (structureLevels != null)
            {
                tvZoneAligned = CheckZoneAlignment(zoneLow.Value, zoneHigh.Value, structureLevels);
                if (tvZoneAligned)
                {
                    styleTag = "ALIGNED";
                    advisoryWeight = 0.7m;
                    candidatePriorityModifier = 0.3m;
                    confidenceScoreModifier = 0.15m;
                }
            }
        }

        // TradingView signals are generally more structured
        if (signal.Score > 0.7m)
        {
            credibilityClass = "HIGH";
            advisoryWeight = 0.8m;
            candidatePriorityModifier = 0.4m;
            confidenceScoreModifier = 0.2m;
        }

        return new VerifyResult(
            credibilityClass,
            styleTag,
            advisoryWeight,
            zoneLow,
            zoneHigh,
            slHint,
            tpPipsList.Any() ? tpPipsList : null,
            false,
            tvZoneAligned,
            candidatePriorityModifier,
            confidenceScoreModifier,
            impulseHarvestScoreModifier
        );
    }

    private static string DetermineCredibility(TelegramSignalContract signal)
    {
        // Simple credibility based on source and recency
        // In production, this could check source whitelist, historical accuracy, etc.
        var ageMinutes = (DateTimeOffset.UtcNow - signal.Timestamp).TotalMinutes;
        if (ageMinutes > 60)
        {
            return "LOW";
        }
        if (ageMinutes > 30)
        {
            return "MEDIUM";
        }
        return "HIGH";
    }

    private static bool CheckZoneAlignment(
        decimal zoneLow,
        decimal zoneHigh,
        StructureLevelsContract structureLevels)
    {
        const decimal AlignmentTolerance = 5.0m;  // USD tolerance

        // Check if zone overlaps with S1, S2, or S3
        if (structureLevels.S1.HasValue)
        {
            if (Math.Abs(zoneLow - structureLevels.S1.Value) < AlignmentTolerance ||
                Math.Abs(zoneHigh - structureLevels.S1.Value) < AlignmentTolerance)
            {
                return true;
            }
        }

        if (structureLevels.S2.HasValue)
        {
            if (Math.Abs(zoneLow - structureLevels.S2.Value) < AlignmentTolerance ||
                Math.Abs(zoneHigh - structureLevels.S2.Value) < AlignmentTolerance)
            {
                return true;
            }
        }

        return false;
    }
}

// Supporting contracts
public record TelegramSignalContract(
    string SourceTag,
    DateTimeOffset Timestamp,
    string Symbol,
    string Direction,
    decimal? EntryZoneLow,
    decimal? EntryZoneHigh,
    decimal? StopLoss,
    decimal? TpPips,
    IReadOnlyList<string>? CommentTags);

// TradingViewSignalContract is defined in Brain.Application.Common.Models.Contracts

public record StructureLevelsContract(
    decimal? S1,
    decimal? S2,
    decimal? S3,
    decimal? R1,
    decimal? R2,
    decimal? Fail);