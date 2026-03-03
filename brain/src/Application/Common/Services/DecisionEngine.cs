using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

public static class DecisionEngine
{
    private const decimal OunceToGram = 31.1035m;
    private const decimal UsdToAed = 3.674m;
    private const decimal ShopSpreadUsdPerOz = 0.80m;
    private const decimal MinTradeGrams = 100m;
    private const decimal SafetyBufferGrams = 10m;

    private static readonly Lock WarModeGate = new();
    private static WarModeState _warModeState = new("UNKNOWN", 0.5m, DateTimeOffset.MinValue, []);
    private static bool _firstLegBanActive;

    public static DecisionResultContract Evaluate(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        TradeSignalContract aiSignal,
        LedgerStateContract ledgerState,
        string? strategyProfileName = null)
    {
        if (!IsSupportedGoldSymbol(snapshot.Symbol))
        {
            return NoTrade("Only XAUUSD-family symbols are permitted.", aiSignal.AlignmentScore, snapshot);
        }

        var regimeTag = ResolveRegimeTag(aiSignal, regime);
        var riskState = ResolveRiskState(aiSignal, regime);
        return regimeTag switch
        {
            "WAR_PREMIUM" or "DEESCALATION_RISK" => EvaluateWarPremium(snapshot, regime, aiSignal, ledgerState, regimeTag, riskState),
            _ => EvaluateStandard(snapshot, regime, aiSignal, ledgerState, regimeTag, riskState),
        };
    }

    private static DecisionResultContract EvaluateStandard(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        TradeSignalContract aiSignal,
        LedgerStateContract ledgerState,
        string regimeTag,
        string riskState)
    {
        var session = NormalizeSession(snapshot.Session);
        var waterfallRisk = ResolveWaterfallRiskStandard(snapshot, regime, aiSignal);
        if (waterfallRisk == "HIGH")
        {
            return NoTrade("Waterfall/panic veto triggered.", aiSignal.AlignmentScore, snapshot, engineState: "CAPITAL_PROTECTED", waterfallRisk: waterfallRisk);
        }

        if (string.Equals(aiSignal.SafetyTag, "BLOCK", StringComparison.OrdinalIgnoreCase) || regime.IsBlocked)
        {
            return NoTrade("P1/P2 safety block.", aiSignal.AlignmentScore, snapshot, engineState: "CAPITAL_PROTECTED", waterfallRisk: waterfallRisk);
        }

        var cause = ResolveCauseStandard(snapshot, regime, aiSignal);
        var mode = ResolveModeStandard(snapshot, aiSignal);
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

        var primaryClose = snapshot.AuthoritativeRate > 0m
            ? snapshot.AuthoritativeRate
            : snapshot.TimeframeData
            .FirstOrDefault(tf => string.Equals(tf.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.Close
            ?? snapshot.TimeframeData.First().Close;

        var spikeCatchAllowed = IsSpikeCatchAllowedStandard(snapshot, cause, mode, waterfallRisk, telegramState);
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
        var sizeClass = ResolveSizeClassStandard(telegramState, waterfallRisk, railPermissionA);

        var bucketCash = ledgerState.DeployableCashAed * 0.80m;
        var maxGrams = ToMaxAffordableGrams(bucketCash, entry) - SafetyBufferGrams;
        var sizePct = ParseSizePercent(sizeClass);
        var gramsFromSizeClass = ToMaxAffordableGrams(bucketCash * sizePct, entry);
        var grams = Math.Floor(Math.Min(maxGrams, gramsFromSizeClass));

        if (grams < MinTradeGrams)
        {
            return NoTrade("Capacity below 100g minimum after spread/buffer.", score, snapshot, waterfallRisk: waterfallRisk, cause: cause, mode: mode, railPermissionA: railPermissionA, railPermissionB: railPermissionB);
        }

        var expiryBand = GetSessionExpiryBandStandard(session);
        var expiry = snapshot.Timestamp.UtcDateTime.Add(expiryBand.Min);

        return new DecisionResultContract(
            IsTradeAllowed: true,
            Status: "ARMED",
            EngineState: "ARMED",
            Mode: mode,
            Cause: cause,
            WaterfallRisk: waterfallRisk,
            Reason: $"Standard gates passed ({session}, {rail}, {sizeClass}).",
            Bucket: bucket,
            Rail: rail,
            Session: session,
            SessionPhase: NormalizeSessionPhase(snapshot.SessionPhase),
            RegimeTag: regimeTag,
            RiskState: riskState,
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

    private static bool IsSupportedGoldSymbol(string? symbol)
    {
        var normalized = (symbol ?? string.Empty).Trim().ToUpperInvariant();
        return normalized.StartsWith("XAUUSD", StringComparison.Ordinal);
    }

    private static DecisionResultContract EvaluateWarPremium(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        TradeSignalContract aiSignal,
        LedgerStateContract ledgerState,
        string regimeTag,
        string riskState)
    {
        var session = NormalizeSession(snapshot.Session);
        var now = DateTimeOffset.UtcNow;
        var mode = ResolveWarMode(snapshot, aiSignal, now);
        var waterfallRisk = ResolveWaterfallRiskWar(snapshot, mode, aiSignal);
        var telegramState = NormalizeTelegramState(snapshot.TelegramState);

        if (_firstLegBanActive)
        {
            if (!HasBaseFormedAfterFlush(snapshot))
            {
                return NoTrade(
                    "First-leg ban active until base + reclaim/retest proof.",
                    aiSignal.AlignmentScore,
                    snapshot,
                    cause: "FIRST_LEG_BAN",
                    mode: mode,
                    waterfallRisk: "HIGH",
                    railPermissionA: "BLOCKED",
                    railPermissionB: "BLOCKED");
            }

            lock (WarModeGate)
            {
                _firstLegBanActive = false;
            }
        }

        var deEscTrigger = mode == "DEESCALATION_RISK" || waterfallRisk == "HIGH";
        if (deEscTrigger)
        {
            lock (WarModeGate)
            {
                _firstLegBanActive = true;
            }

            return NoTrade(
                "WarPremium kill-switch active (de-escalation/high waterfall).",
                aiSignal.AlignmentScore,
                snapshot,
                engineState: "CAPITAL_PROTECTED",
                waterfallRisk: "HIGH",
                cause: "DEESC_KILL_SWITCH",
                mode: mode,
                railPermissionA: "BLOCKED",
                railPermissionB: "BLOCKED");
        }

        var primaryClose = snapshot.AuthoritativeRate > 0m
            ? snapshot.AuthoritativeRate
            : snapshot.TimeframeData
            .FirstOrDefault(tf => string.Equals(tf.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.Close
            ?? snapshot.TimeframeData.First().Close;

        var gmax = Math.Floor(ToMaxAffordableGrams(ledgerState.DeployableCashAed, primaryClose) - SafetyBufferGrams);
        if (gmax < MinTradeGrams)
        {
            return NoTrade(
                "WarPremium capacity below 100g after spread/buffer.",
                aiSignal.AlignmentScore,
                snapshot,
                cause: "CAPACITY",
                mode: mode,
                waterfallRisk: waterfallRisk,
                railPermissionA: "BLOCKED",
                railPermissionB: "BLOCKED");
        }

        if (mode == "WAR_PREMIUM" && waterfallRisk == "LOW")
        {
            if (NormalizeSessionPhase(snapshot.SessionPhase) == "START")
            {
                return NoTrade(
                    "Rail-B blocked in session start phase to avoid first spike chase.",
                    aiSignal.AlignmentScore,
                    snapshot,
                    cause: "FIRST_SPIKE_BAN",
                    mode: mode,
                    waterfallRisk: waterfallRisk,
                    railPermissionA: "ALLOWED",
                    railPermissionB: "BLOCKED");
            }

            if (!IsLidBreakConfirmed(snapshot))
            {
                return NoTrade(
                    "Rail-B requires lid break confirmation.",
                    aiSignal.AlignmentScore,
                    snapshot,
                    cause: "NO_LID_BREAK",
                    mode: mode,
                    waterfallRisk: waterfallRisk,
                    railPermissionA: "ALLOWED",
                    railPermissionB: "AFTER_LID_BREAK");
            }

            var stageFractions = new[] { 0.25m, 0.25m, 0.20m, 0.15m, 0.10m };
            var tpDistances = new[] { 160m, 150m, 120m, 90m, 70m };
            var stage = Math.Clamp(ledgerState.OpenBuyCount, 0, stageFractions.Length - 1);

            if (stage == 4 && snapshot.RsiH1 >= 72m)
            {
                return NoTrade(
                    "Stage B5 blocked: RSI_H1 >= 72.",
                    aiSignal.AlignmentScore,
                    snapshot,
                    cause: "B5_RSI_BLOCK",
                    mode: mode,
                    waterfallRisk: waterfallRisk,
                    railPermissionA: "ALLOWED",
                    railPermissionB: "BLOCKED");
            }

            var stageGrams = Math.Floor(gmax * stageFractions[stage]);
            if (stageGrams < MinTradeGrams)
            {
                return NoTrade(
                    "Stage grams below 100g minimum.",
                    aiSignal.AlignmentScore,
                    snapshot,
                    cause: "STAGE_CAPACITY",
                    mode: mode,
                    waterfallRisk: waterfallRisk,
                    railPermissionA: "ALLOWED",
                    railPermissionB: "BLOCKED");
            }

            var entryBuffer = ResolveWarEntryBuffer(snapshot);
            var entry = primaryClose + entryBuffer;
            var tp = entry + tpDistances[stage];
            var expiry = snapshot.Timestamp.UtcDateTime.Add(GetWarRailBExpiry(session));

            return new DecisionResultContract(
                IsTradeAllowed: true,
                Status: "ARMED",
                EngineState: "ARMED",
                Mode: mode,
                Cause: "WAR_EXPANSION_PYRAMID",
                WaterfallRisk: waterfallRisk,
                Reason: $"WarPremium Rail-B stage B{stage + 1} armed.",
                Bucket: "WAR_B",
                Rail: "BUY_STOP",
                Session: session,
                SessionPhase: NormalizeSessionPhase(snapshot.SessionPhase),
                RegimeTag: regimeTag,
                RiskState: riskState,
                SizeClass: $"B{stage + 1}:{(int)(stageFractions[stage] * 100m)}%",
                Entry: decimal.Round(entry, 2),
                Tp: decimal.Round(tp, 2),
                Grams: decimal.Round(stageGrams, 2),
                ExpiryUtc: new DateTimeOffset(expiry, TimeSpan.Zero),
                MaxLifeSeconds: (int)GetWarRailBExpiry(session).TotalSeconds,
                AlignmentScore: Math.Clamp(aiSignal.AlignmentScore, 0m, 1m),
                TelegramState: telegramState,
                RailPermissionA: "ALLOWED",
                RailPermissionB: "ALLOWED",
                RotationCapThisSession: 5);
        }

        if (!IsShelfProof(snapshot))
        {
            return NoTrade(
                "Rail-A requires shelf proof (sweep/reclaim/retest/compression).",
                aiSignal.AlignmentScore,
                snapshot,
                cause: "MID_AIR_BAN",
                mode: mode,
                waterfallRisk: waterfallRisk,
                railPermissionA: "AFTER_STRUCTURE",
                railPermissionB: "BLOCKED");
        }

        var reloadFraction = mode == "UNKNOWN" ? 0.20m : 0.35m;
        var reloadGrams = Math.Floor(gmax * reloadFraction);
        if (reloadGrams < MinTradeGrams)
        {
            return NoTrade(
                "Reload grams below 100g minimum.",
                aiSignal.AlignmentScore,
                snapshot,
                cause: "RELOAD_CAPACITY",
                mode: mode,
                waterfallRisk: waterfallRisk,
                railPermissionA: "BLOCKED",
                railPermissionB: "BLOCKED");
        }

        var reloadEntry = primaryClose - Clamp(snapshot.AtrM15 > 0m ? snapshot.AtrM15 * 0.25m : 8m, 8m, 12m);
        var reloadTpDistance = Clamp(snapshot.AtrM15 > 0m ? snapshot.AtrM15 * 0.9m : 18m, 12m, 25m);
        var reloadTp = reloadEntry + reloadTpDistance;
        var reloadExpiry = snapshot.Timestamp.UtcDateTime.Add(GetWarRailAExpiry(session));

        return new DecisionResultContract(
            IsTradeAllowed: true,
            Status: "ARMED",
            EngineState: "ARMED",
            Mode: mode,
            Cause: "WAR_STRUCTURE_RELOAD",
            WaterfallRisk: waterfallRisk,
            Reason: "WarPremium Rail-A shelf reload armed.",
            Bucket: "WAR_A",
            Rail: "BUY_LIMIT",
            Session: session,
            SessionPhase: NormalizeSessionPhase(snapshot.SessionPhase),
            RegimeTag: regimeTag,
            RiskState: riskState,
            SizeClass: $"RELOAD:{(int)(reloadFraction * 100m)}%",
            Entry: decimal.Round(reloadEntry, 2),
            Tp: decimal.Round(reloadTp, 2),
            Grams: decimal.Round(reloadGrams, 2),
            ExpiryUtc: new DateTimeOffset(reloadExpiry, TimeSpan.Zero),
            MaxLifeSeconds: (int)GetWarRailAExpiry(session).TotalSeconds,
            AlignmentScore: Math.Clamp(aiSignal.AlignmentScore, 0m, 1m),
            TelegramState: telegramState,
            RailPermissionA: "ALLOWED",
            RailPermissionB: "BLOCKED",
            RotationCapThisSession: 2);
    }

    private static string NormalizeSessionPhase(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "START" => "START",
            "MID" => "MID",
            "END" => "END",
            _ => "UNKNOWN",
        };
    }

    private static string ResolveRegimeTag(TradeSignalContract aiSignal, RegimeClassificationContract regime)
    {
        var tag = (aiSignal.RegimeTag ?? string.Empty).Trim().ToUpperInvariant();
        if (tag is "WAR_PREMIUM" or "DEESCALATION_RISK" or "STANDARD")
        {
            return tag;
        }

        return string.Equals(regime.Regime, "NEWS_SPIKE", StringComparison.OrdinalIgnoreCase)
            ? "WAR_PREMIUM"
            : "STANDARD";
    }

    private static string ResolveRiskState(TradeSignalContract aiSignal, RegimeClassificationContract regime)
    {
        var state = (aiSignal.RiskState ?? string.Empty).Trim().ToUpperInvariant();
        if (state is "SAFE" or "CAUTION" or "BLOCK")
        {
            return state;
        }

        return regime.RiskTag.ToUpperInvariant() switch
        {
            "SAFE" => "SAFE",
            "BLOCK" => "BLOCK",
            _ => "CAUTION",
        };
    }

    private static string ResolveWarMode(MarketSnapshotContract snapshot, TradeSignalContract aiSignal, DateTimeOffset now)
    {
        lock (WarModeGate)
        {
            var hintedMode = NormalizeWarMode(aiSignal.ModeHint);
            var ttlSeconds = Math.Clamp(aiSignal.ModeTtlSeconds, 300, 3600);
            var confidence = Math.Clamp(aiSignal.ModeConfidence, 0m, 1m);
            var keywords = aiSignal.ModeKeywords ?? [];

            if (hintedMode is "WAR_PREMIUM" or "DEESCALATION_RISK")
            {
                _warModeState = new WarModeState(
                    hintedMode,
                    confidence,
                    now.AddSeconds(ttlSeconds),
                    keywords);
            }
            else if (_warModeState.ExpiresAtUtc <= now)
            {
                _warModeState = new WarModeState("UNKNOWN", 0.5m, now.AddMinutes(10), []);
            }

            if (snapshot.HasPanicDropSequence || snapshot.PanicSuspected)
            {
                _warModeState = new WarModeState("DEESCALATION_RISK", 0.95m, now.AddMinutes(45), _warModeState.Keywords);
            }

            return _warModeState.Mode;
        }
    }

    private static string NormalizeWarMode(string? mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "WAR_PREMIUM" => "WAR_PREMIUM",
            "DEESCALATION_RISK" => "DEESCALATION_RISK",
            _ => "UNKNOWN",
        };
    }

    private static string ResolveWaterfallRiskWar(MarketSnapshotContract snapshot, string mode, TradeSignalContract aiSignal)
    {
        var adrUsed = ResolveAdrUsedRatio(snapshot);
        var spreadInstability = snapshot.SpreadMedian60m > 0m
            && snapshot.Spread >= snapshot.SpreadMedian60m * 1.8m;

        var mediumSignals = 0;
        if (adrUsed >= 1.20m) mediumSignals++;
        if (snapshot.RsiH1 >= 70m && snapshot.HasOverlapCandles) mediumSignals++;
        if (snapshot.CompressionCountM15 >= 3 && !snapshot.IsBreakoutConfirmed && snapshot.IsAtrExpanding) mediumSignals++;
        if (mode == "DEESCALATION_RISK" && NormalizeTelegramState(snapshot.TelegramState) is "SELL" or "STRONG_SELL") mediumSignals++;

        var highRisk = (mode == "DEESCALATION_RISK" && (adrUsed >= 1.00m || snapshot.HasPanicDropSequence))
            || snapshot.HasPanicDropSequence
            || spreadInstability
            || string.Equals(aiSignal.SafetyTag, "BLOCK", StringComparison.OrdinalIgnoreCase);

        if (highRisk)
        {
            return "HIGH";
        }

        return mediumSignals >= 2 ? "MEDIUM" : "LOW";
    }

    private static decimal ResolveAdrUsedRatio(MarketSnapshotContract snapshot)
    {
        if (snapshot.AdrUsedPct > 0m)
        {
            return snapshot.AdrUsedPct / 100m;
        }

        if (snapshot.Adr > 0m)
        {
            return snapshot.Atr / snapshot.Adr;
        }

        return 0m;
    }

    private static bool HasBaseFormedAfterFlush(MarketSnapshotContract snapshot)
    {
        var compressionReady = snapshot.CompressionCountM15 >= 3 && snapshot.HasOverlapCandles;
        var reclaim = snapshot.TvAlertType is "SHELF_RECLAIM" or "RETEST_HOLD";
        return compressionReady && reclaim && !snapshot.HasPanicDropSequence;
    }

    private static bool IsLidBreakConfirmed(MarketSnapshotContract snapshot)
    {
        if (!snapshot.IsBreakoutConfirmed)
        {
            return false;
        }

        var tvBreak = snapshot.TvAlertType is "LID_BREAK" or "BREAKOUT" or "SESSION_BREAK";
        return tvBreak || snapshot.CompressionCountM15 >= 3;
    }

    private static bool IsShelfProof(MarketSnapshotContract snapshot)
    {
        var reclaim = snapshot.TvAlertType is "SHELF_RECLAIM" or "RETEST_HOLD";
        var structure = snapshot.HasLiquiditySweep && snapshot.IsCompression && snapshot.CompressionCountM15 >= 2;
        return reclaim && structure;
    }

    private static decimal ResolveWarEntryBuffer(MarketSnapshotContract snapshot)
    {
        var atrBuffer = snapshot.AtrM15 > 0m ? snapshot.AtrM15 * 0.10m : 1.0m;
        return Clamp(atrBuffer, 0.5m, 2.0m);
    }

    private static TimeSpan GetWarRailBExpiry(string session) => session switch
    {
        "JAPAN" => TimeSpan.FromMinutes(20),
        "LONDON" => TimeSpan.FromMinutes(15),
        "NY" => TimeSpan.FromMinutes(10),
        _ => TimeSpan.FromMinutes(20)
    };

    private static TimeSpan GetWarRailAExpiry(string session) => session switch
    {
        "NY" => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromMinutes(45)
    };

    private static (TimeSpan Min, TimeSpan Max) GetSessionExpiryBandStandard(string session) => session.ToUpperInvariant() switch
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

    private static string ResolveWaterfallRiskStandard(MarketSnapshotContract snapshot, RegimeClassificationContract regime, TradeSignalContract aiSignal)
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

    private static string ResolveModeStandard(MarketSnapshotContract snapshot, TradeSignalContract aiSignal)
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

    private static string ResolveCauseStandard(MarketSnapshotContract snapshot, RegimeClassificationContract regime, TradeSignalContract aiSignal)
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

    private static bool IsSpikeCatchAllowedStandard(
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

    private static string ResolveSizeClassStandard(string telegramState, string waterfallRisk, string railPermissionA)
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
            SessionPhase: NormalizeSessionPhase(snapshot.SessionPhase),
            RegimeTag: "STANDARD",
            RiskState: "BLOCK",
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

    private sealed record WarModeState(
        string Mode,
        decimal Confidence,
        DateTimeOffset ExpiresAtUtc,
        IReadOnlyCollection<string> Keywords);
}
