using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Evaluates the quality of a trade setup that has already passed rule engine validation.
/// Produces a score from 0–100 across structure, momentum, execution, AI, and sentiment signals.
/// The rule engine remains the authoritative structural gate; this layer ranks valid setups only.
/// </summary>
public static class TradeScoreCalculator
{
    // Score thresholds (configurable intent — kept as constants for determinism)
    public const int NoTradeThreshold = 45;
    public const int WeakThreshold = 60;
    public const int HighConvictionThreshold = 80;

    // Component caps
    private const int MaxStructureScore = 55;
    private const int MaxMomentumScore = 30;
    private const int MaxExecutionScore = 10;
    private const int MaxAiScore = 10;
    private const int SentimentMin = -10;
    private const int SentimentMax = 5;

    /// <summary>
    /// Calculates a trade score for a setup that has already passed rule engine validation.
    /// The setupCandidate must be valid before calling this method.
    /// </summary>
    public static TradeScoreContract Calculate(
        MarketSnapshotContract snapshot,
        SetupCandidateResult setupCandidate,
        TradeSignalContract aiSignal)
    {
        var structureScore = CalculateStructureScore(setupCandidate);
        var momentumScore = CalculateMomentumScore(snapshot);
        var executionScore = CalculateExecutionScore(snapshot);
        var aiScore = CalculateAiScore(aiSignal);
        var sentimentScore = CalculateSentimentScore(snapshot);

        var total = Math.Clamp(structureScore + momentumScore + executionScore + aiScore + sentimentScore, 0, 100);
        var tier = ResolveDecisionTier(total);

        return new TradeScoreContract(
            StructureScore: structureScore,
            MomentumScore: momentumScore,
            ExecutionScore: executionScore,
            AiScore: aiScore,
            SentimentScore: sentimentScore,
            TotalScore: total,
            DecisionTier: tier);
    }

    // ── Structure Score (0–55) ─────────────────────────────────────────────────
    // Derived from the rule engine output: H1 context, M15 setup, M5 entry quality.
    private static int CalculateStructureScore(SetupCandidateResult setupCandidate)
    {
        var score = 0;

        // H1 context quality (up to 20 pts)
        var h1 = setupCandidate.H1Context;
        if (h1 is not null)
        {
            score += h1.Context is "BULLISH" or "BEARISH" ? 10 : 0;
            score += h1.IsTrendAligned ? 5 : 0;
            score += h1.HasReclaim ? 5 : (h1.HasLiquiditySweep ? 3 : 0);
        }

        // M15 setup quality (up to 20 pts)
        var m15 = setupCandidate.M15Setup;
        if (m15?.IsValid == true)
        {
            score += 10; // base for valid M15
            score += m15.IsCompression ? 5 : 0;
            score += m15.HasBase ? 5 : 0;
        }

        // M5 entry quality (up to 15 pts)
        var m5 = setupCandidate.M5Entry;
        if (m5?.IsValid == true)
        {
            score += 5; // base for valid M5
            score += m5.IsBreakout ? 5 : 0;
            score += m5.IsMomentumShift ? 4 : (m5.IsRetest ? 3 : 0);
        }

        return Math.Min(score, MaxStructureScore);
    }

    // ── Momentum Score (0–30) ──────────────────────────────────────────────────
    // Confirms market energy and volatility for scalping entries.
    private static int CalculateMomentumScore(MarketSnapshotContract snapshot)
    {
        var score = 0;

        if (snapshot.IsAtrExpanding) score += 8;
        if (snapshot.IsExpansion) score += 7;
        if (snapshot.HasImpulseCandles)
        {
            score += snapshot.ImpulseStrengthScore >= 1.0m ? 8 : (snapshot.ImpulseStrengthScore >= 0.5m ? 5 : 3);
        }
        if (snapshot.CompressionCountM15 >= 3) score += 5;
        if (snapshot.VolatilityExpansion > 0m) score += 2;

        return Math.Min(score, MaxMomentumScore);
    }

    // ── Execution Score (0–10) ─────────────────────────────────────────────────
    // Market environment quality: spread, freeze, slippage.
    private static int CalculateExecutionScore(MarketSnapshotContract snapshot)
    {
        var score = 0;

        // Good spread: current spread is not more than 1.5× the 60-minute median
        var spreadMedian = snapshot.SpreadMedian60m > 0m ? snapshot.SpreadMedian60m : snapshot.Spread;
        if (snapshot.Spread > 0m && snapshot.Spread <= spreadMedian * 1.5m) score += 4;

        if (!snapshot.FreezeGapDetected) score += 4;

        if (snapshot.SlippageEstimatePoints < 1.0m) score += 2;

        return Math.Min(score, MaxExecutionScore);
    }

    // ── AI Score (0–10) ────────────────────────────────────────────────────────
    // Supporting signal only — never overrides structural rules.
    private static int CalculateAiScore(TradeSignalContract aiSignal)
    {
        var score = 0;

        if (aiSignal.ConsensusPassed)
        {
            score += aiSignal.Confidence >= 0.8m ? 7 : (aiSignal.Confidence >= 0.6m ? 5 : 3);
            score += aiSignal.AgreementCount > aiSignal.RequiredAgreement ? 3 : 0;
        }

        return Math.Min(score, MaxAiScore);
    }

    // ── Sentiment Score (−10 to +5) ────────────────────────────────────────────
    // Telegram-derived crowd context. Negative when panic or high-impact signals.
    // Multiple negative conditions may accumulate; the clamp at the end enforces the floor of -10.
    private static int CalculateSentimentScore(MarketSnapshotContract snapshot)
    {
        var score = 0;

        score += snapshot.TelegramState switch
        {
            "QUIET" => 3,
            "MIXED" => 0,
            "BULLISH" or "BEARISH" => 2,
            "PANIC" => -8,
            _ => 0,
        };

        if (snapshot.PanicSuspected) score -= 5;

        score += snapshot.TelegramImpactTag switch
        {
            "HIGH" => -5,
            "MEDIUM" => -2,
            "LOW" => 0,
            _ => 0,
        };

        return Math.Clamp(score, SentimentMin, SentimentMax);
    }

    private static string ResolveDecisionTier(int total)
    {
        if (total >= HighConvictionThreshold) return "HIGH_CONVICTION";
        if (total >= WeakThreshold) return "VALID_TRADE";
        if (total >= NoTradeThreshold) return "WEAK_SETUP";
        return "NO_TRADE";
    }
}
