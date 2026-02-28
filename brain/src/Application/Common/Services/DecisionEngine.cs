using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

public static class DecisionEngine
{
    private const decimal OunceToGram = 31.1035m;
    private const decimal UsdToAed = 3.674m;
    private const decimal ShopSpreadUsdPerOz = 0.80m;
    private const decimal MinTradeGrams = 100m;
    private const decimal SafetyBufferGrams = 10m;

    public static DecisionResultContract Evaluate(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        TradeSignalContract aiSignal,
        LedgerStateContract ledgerState)
    {
        if (!string.Equals(snapshot.Symbol, "XAUUSD", StringComparison.OrdinalIgnoreCase))
        {
            return NoTrade("Only XAUUSD is permitted.", aiSignal.AlignmentScore, snapshot);
        }

        var session = NormalizeSession(snapshot.Session);
        var waterfallRisk = ResolveWaterfallRisk(snapshot, regime, aiSignal);
        if (waterfallRisk == "HIGH")
        {
            return NoTrade("Waterfall/panic veto triggered.", aiSignal.AlignmentScore, snapshot, engineState: "CAPITAL_PROTECTED", waterfallRisk: waterfallRisk);
        }

        if (string.Equals(aiSignal.SafetyTag, "BLOCK", StringComparison.OrdinalIgnoreCase) || regime.IsBlocked)
        {
            return NoTrade("P1/P2 safety block.", aiSignal.AlignmentScore, snapshot, engineState: "CAPITAL_PROTECTED", waterfallRisk: waterfallRisk);
        }

        var cause = ResolveCause(snapshot, regime, aiSignal);
        var mode = ResolveMode(snapshot, aiSignal);
        var telegramState = NormalizeTelegramState(snapshot.TelegramState);
        var railPermissionA = "ALLOWED";
        var railPermissionB = "ALLOWED";

        if (waterfallRisk == "MEDIUM")
        {
            railPermissionB = "BLOCKED";
            railPermissionA = "AFTER_STRUCTURE";
        }

        if (cause is "UNSCHEDULED_GEO_POLICY" or "LIQUIDITY_SHOCK" or "UNKNOWN")
        {
            railPermissionB = "BLOCKED";
            railPermissionA = "AFTER_STRUCTURE";
        }

        if (telegramState is "SELL" or "STRONG_SELL" or "MIXED")
        {
            railPermissionB = "BLOCKED";
            railPermissionA = "AFTER_STRUCTURE";
        }

        if (snapshot.PanicSuspected)
        {
            railPermissionB = "BLOCKED";
            railPermissionA = "AFTER_STRUCTURE";
        }

        var score = Math.Clamp(aiSignal.AlignmentScore, 0m, 1m);
        if (score < 0.62m)
        {
            return NoTrade("Alignment below threshold.", score, snapshot, waterfallRisk: waterfallRisk, cause: cause, mode: mode, railPermissionA: railPermissionA, railPermissionB: railPermissionB);
        }

        var primaryClose = snapshot.TimeframeData
            .FirstOrDefault(tf => string.Equals(tf.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.Close
            ?? snapshot.TimeframeData.First().Close;

        var spikeCatchAllowed = IsSpikeCatchAllowed(snapshot, cause, mode, waterfallRisk, telegramState);
        var rail = spikeCatchAllowed && railPermissionB == "ALLOWED"
            ? "BUY_STOP"
            : "BUY_LIMIT";

        if (rail == "BUY_STOP" && railPermissionB == "BLOCKED")
        {
            return NoTrade("Rail-B blocked by precedence gates.", score, snapshot, waterfallRisk: waterfallRisk, cause: cause, mode: mode, railPermissionA: railPermissionA, railPermissionB: railPermissionB);
        }

        var entry = rail == "BUY_STOP"
            ? primaryClose + (snapshot.Atr * 0.45m)
            : primaryClose - (snapshot.Atr * (railPermissionA == "AFTER_STRUCTURE" ? 0.95m : 0.55m));

        var tpDistance = session switch
        {
            "JAPAN" => Clamp(snapshot.Atr * 0.95m, 6m, 9m),
            "INDIA" => Clamp(snapshot.Atr * 1.05m, 8m, 12m),
            "LONDON" => Clamp(snapshot.Atr * 1.10m, 8m, 12m),
            "NY" => Clamp(snapshot.Atr * 1.20m, 9m, 15m),
            _ => Clamp(snapshot.Atr, 8m, 12m)
        };

        var tp = entry + tpDistance;
        var bucket = "C1";
        var sizeClass = ResolveSizeClass(telegramState, waterfallRisk, railPermissionA);
        var bucketCash = ledgerState.DeployableCashAed * 0.80m;
        var maxGrams = ToMaxAffordableGrams(bucketCash, entry) - SafetyBufferGrams;
        var sizePct = ParseSizePercent(sizeClass);
        var gramsFromSizeClass = ToMaxAffordableGrams(bucketCash * sizePct, entry);
        var grams = Math.Floor(Math.Min(maxGrams, gramsFromSizeClass));

        if (grams < MinTradeGrams)
        {
            return NoTrade("Capacity below 100g minimum after spread/buffer.", score, snapshot, waterfallRisk: waterfallRisk, cause: cause, mode: mode, railPermissionA: railPermissionA, railPermissionB: railPermissionB);
        }

        var expiryBand = GetSessionExpiryBand(session);
        var expiry = snapshot.Timestamp.UtcDateTime.Add(expiryBand.Min);

        return new DecisionResultContract(
            IsTradeAllowed: true,
            Status: "ARMED",
            EngineState: "ARMED",
            Mode: mode,
            Cause: cause,
            WaterfallRisk: waterfallRisk,
            Reason: $"P1-P5 gates passed ({session}, {rail}, {sizeClass}).",
            Bucket: bucket,
            Rail: rail,
            Session: session,
            SizeClass: sizeClass,
            Entry: decimal.Round(entry, 2),
            Tp: decimal.Round(tp, 2),
            Grams: decimal.Round(grams, 2),
            ExpiryUtc: new DateTimeOffset(expiry, TimeSpan.Zero),
            MaxLifeSeconds: (int)expiryBand.Max.TotalSeconds,
            AlignmentScore: score,
            TelegramState: telegramState,
            RailPermissionA: railPermissionA,
            RailPermissionB: railPermissionB,
            RotationCapThisSession: 2);
    }

    private static (TimeSpan Min, TimeSpan Max) GetSessionExpiryBand(string session) => session.ToUpperInvariant() switch
    {
        "JAPAN" => (TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(60)),
        "INDIA" => (TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(75)),
        "LONDON" => (TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(55)),
        "NY" => (TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(45)),
        _ => (TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60))
    };

    private static string NormalizeSession(string session)
    {
        var normalized = (session ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "ASIA" => "JAPAN",
            "EUROPE" => "LONDON",
            "NEW_YORK" => "NY",
            _ => normalized,
        };
    }

    private static string ResolveWaterfallRisk(MarketSnapshotContract snapshot, RegimeClassificationContract regime, TradeSignalContract aiSignal)
    {
        var score = 0;
        if (snapshot.HasImpulseCandles && snapshot.IsExpansion)
        {
            score++;
        }
        if (snapshot.IsAtrExpanding)
        {
            score++;
        }
        if (snapshot.RsiH1 > 72m || snapshot.RsiM15 > 75m)
        {
            score++;
        }
        if (snapshot.SpreadMax60m > 0m && snapshot.SpreadMedian60m > 0m && snapshot.SpreadMax60m >= snapshot.SpreadMedian60m * 2.2m)
        {
            score++;
        }
        if (snapshot.IsFriday && (NormalizeSession(snapshot.Session) is "LONDON" or "NY") && snapshot.Adr > 0m && snapshot.Atr / snapshot.Adr >= 0.95m)
        {
            score++;
        }
        if (snapshot.PanicSuspected || snapshot.HasPanicDropSequence)
        {
            score++;
        }
        if (regime.IsWaterfall || string.Equals(aiSignal.SafetyTag, "BLOCK", StringComparison.OrdinalIgnoreCase))
        {
            score++;
        }

        if (score >= 2)
        {
            return "HIGH";
        }

        return score == 1 ? "MEDIUM" : "LOW";
    }

    private static string ResolveMode(MarketSnapshotContract snapshot, TradeSignalContract aiSignal)
    {
        if (snapshot.IsExpansion && snapshot.HasImpulseCandles && snapshot.RsiH1 <= 73m)
        {
            return "IMPULSE";
        }

        if (string.Equals(aiSignal.Rail, "BUY_STOP", StringComparison.OrdinalIgnoreCase) && snapshot.RsiH1 <= 73m)
        {
            return "IMPULSE";
        }

        return "EXHAUSTION";
    }

    private static string ResolveCause(MarketSnapshotContract snapshot, RegimeClassificationContract regime, TradeSignalContract aiSignal)
    {
        if (snapshot.PanicSuspected || snapshot.HasPanicDropSequence)
        {
            return "LIQUIDITY_SHOCK";
        }

        if (snapshot.IsUsRiskWindow && string.Equals(snapshot.TelegramImpactTag, "HIGH", StringComparison.OrdinalIgnoreCase))
        {
            return "SCHEDULED_MACRO";
        }

        if (snapshot.IsBreakoutConfirmed && snapshot.HasOverlapCandles)
        {
            return "TECH_BREAKOUT";
        }

        if (string.Equals(regime.Regime, "NEWS_SPIKE", StringComparison.OrdinalIgnoreCase))
        {
            return "UNSCHEDULED_GEO_POLICY";
        }

        if (string.Equals(aiSignal.NewsImpactTag, "HIGH", StringComparison.OrdinalIgnoreCase))
        {
            return "UNSCHEDULED_GEO_POLICY";
        }

        return "UNKNOWN";
    }

    private static bool IsSpikeCatchAllowed(
        MarketSnapshotContract snapshot,
        string cause,
        string mode,
        string waterfallRisk,
        string telegramState)
    {
        if (mode != "IMPULSE") return false;
        if (cause is not "TECH_BREAKOUT" and not "SCHEDULED_MACRO") return false;
        if (waterfallRisk != "LOW") return false;
        if (snapshot.SpreadMax60m > 0m && snapshot.SpreadMedian60m > 0m && snapshot.SpreadMax60m >= snapshot.SpreadMedian60m * 1.8m) return false;
        if (snapshot.RsiH1 > 73m) return false;
        if (telegramState is "SELL" or "STRONG_SELL") return false;
        if (snapshot.PanicSuspected) return false;
        if (snapshot.TvAlertType == "ADR_EXHAUSTION" || snapshot.TvAlertType == "RSI_OVERHEAT") return false;
        if (snapshot.CompressionCountM15 < 3 || !snapshot.IsBreakoutConfirmed) return false;
        return true;
    }

    private static string ResolveSizeClass(string telegramState, string waterfallRisk, string railPermissionA)
    {
        if (waterfallRisk == "MEDIUM" || railPermissionA == "AFTER_STRUCTURE")
        {
            return "25%";
        }

        return telegramState switch
        {
            "STRONG_BUY" => "75%",
            "BUY" => "50%",
            _ => "25%",
        };
    }

    private static decimal ParseSizePercent(string sizeClass) => sizeClass switch
    {
        "25%" => 0.25m,
        "50%" => 0.50m,
        "75%" => 0.75m,
        "100%" => 1.00m,
        _ => 0.25m,
    };

    private static string NormalizeTelegramState(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "STRONG_BUY" => "STRONG_BUY",
            "BUY" => "BUY",
            "MIXED" => "MIXED",
            "SELL" => "SELL",
            "STRONG_SELL" => "STRONG_SELL",
            _ => "QUIET",
        };
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) => Math.Min(max, Math.Max(min, value));

    private static decimal ToMaxAffordableGrams(decimal deployableCashAed, decimal entryUsdPerOunce)
    {
        if (deployableCashAed <= 0m || entryUsdPerOunce <= 0m)
        {
            return 0m;
        }

        var shopBuy = entryUsdPerOunce + ShopSpreadUsdPerOz;
        var usdPerGram = shopBuy / OunceToGram;
        if (usdPerGram <= 0m)
        {
            return 0m;
        }

        var aedPerGram = usdPerGram * UsdToAed;
        return deployableCashAed / aedPerGram;
    }

    private static DecisionResultContract NoTrade(
        string reason,
        decimal score,
        MarketSnapshotContract snapshot,
        string engineState = "CAPITAL_PROTECTED",
        string waterfallRisk = "HIGH",
        string cause = "UNKNOWN",
        string mode = "EXHAUSTION",
        string railPermissionA = "BLOCKED",
        string railPermissionB = "BLOCKED") =>
        new(
            IsTradeAllowed: false,
            Status: "NO_TRADE",
            EngineState: engineState,
            Mode: mode,
            Cause: cause,
            WaterfallRisk: waterfallRisk,
            Reason: reason,
            Bucket: "C1",
            Rail: string.Empty,
            Session: NormalizeSession(snapshot.Session),
            SizeClass: "25%",
            Entry: 0m,
            Tp: 0m,
            Grams: 0m,
            ExpiryUtc: DateTimeOffset.UtcNow,
            MaxLifeSeconds: 0,
            AlignmentScore: score,
            TelegramState: NormalizeTelegramState(snapshot.TelegramState),
            RailPermissionA: railPermissionA,
            RailPermissionB: railPermissionB,
            RotationCapThisSession: 0);
}
