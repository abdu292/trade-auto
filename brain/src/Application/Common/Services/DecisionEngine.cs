using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

public static class DecisionEngine
{
    private const decimal OunceToGram = 31.1035m;
    private const decimal UsdToAed = 3.674m;
    private const decimal ShopSpreadUsdPerOz = 0.80m;
    private const decimal SafetyBufferGrams = 10m;

    private static readonly Lock WarModeGate = new();
    private static WarModeState _warModeState = new("UNKNOWN", 0.5m, DateTimeOffset.MinValue, []);
    private static bool _firstLegBanActive;
    private static bool _deescalationRiskActive;

    /// <summary>
    /// Resets runtime guard state to make replay runs deterministic and isolated.
    /// </summary>
    public static void ResetRuntimeGuards()
    {
        lock (WarModeGate)
        {
            _warModeState = new WarModeState("UNKNOWN", 0.5m, DateTimeOffset.MinValue, []);
            _firstLegBanActive = false;
            _deescalationRiskActive = false;
        }
    }

    public static DecisionResultContract Evaluate(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        TradeSignalContract aiSignal,
        LedgerStateContract ledgerState,
        string? strategyProfileName = null,
        decimal minTradeGrams = 100m,
        decimal pretableSizeModifier = 1.0m)
    {
        if (!IsSupportedGoldSymbol(snapshot.Symbol))
        {
            return NoTrade("Only XAUUSD-family symbols are permitted.", aiSignal.AlignmentScore, snapshot);
        }

        // FIRST_LEG_BAN gate: blocks all trades until H1 reclaim + M15 base + M5 compression + news calm are confirmed
        if (_firstLegBanActive)
        {
            if (!HasBaseFormedAfterFlush(snapshot))
            {
                return NoTrade(
                    "TABLE ABORTED — FIRST_LEG_BAN (late in first move, no reclaim/base yet).",
                    aiSignal.AlignmentScore,
                    snapshot,
                    cause: "FIRST_LEG_BAN",
                    waterfallRisk: "HIGH",
                    railPermissionA: "BLOCKED",
                    railPermissionB: "BLOCKED");
            }

            lock (WarModeGate)
            {
                _firstLegBanActive = false;
            }
        }

        // DEESCALATION_RISK gate: blocks all trades until structural flush + BottomPermission + sentiment normalized + news safe
        if (_deescalationRiskActive)
        {
            if (!IsDeescalationRiskUnlocked(snapshot))
            {
                return NoTrade(
                    "TABLE ABORTED — DEESCALATION_RISK (flush/base/sentiment not yet confirmed).",
                    aiSignal.AlignmentScore,
                    snapshot,
                    engineState: "CAPITAL_PROTECTED",
                    cause: "DEESC_KILL_SWITCH",
                    waterfallRisk: "HIGH",
                    railPermissionA: "BLOCKED",
                    railPermissionB: "BLOCKED");
            }

            lock (WarModeGate)
            {
                _deescalationRiskActive = false;
            }
        }

        var regimeTag = ResolveRegimeTag(aiSignal, regime);
        var riskState = ResolveRiskState(aiSignal, regime);
        return regimeTag switch
        {
            "WAR_PREMIUM" or "DEESCALATION_RISK" => EvaluateWarPremium(snapshot, regime, aiSignal, ledgerState, regimeTag, riskState, minTradeGrams),
            _ => EvaluateStandard(snapshot, regime, aiSignal, ledgerState, regimeTag, riskState, minTradeGrams, pretableSizeModifier),
        };
    }

    private static DecisionResultContract EvaluateStandard(
        MarketSnapshotContract snapshot,
        RegimeClassificationContract regime,
        TradeSignalContract aiSignal,
        LedgerStateContract ledgerState,
        string regimeTag,
        string riskState,
        decimal minTradeGrams,
        decimal pretableSizeModifier = 1.0m)
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

        // FIRST_LEG_BAN activation: crowded/late first-leg entry — block and wait for reclaim/base
        if (IsFirstLegBanConditionsMet(snapshot, aiSignal))
        {
            lock (WarModeGate)
            {
                _firstLegBanActive = true;
            }

            return NoTrade(
                "TABLE ABORTED — FIRST_LEG_BAN (late in first move, no reclaim/base yet).",
                score,
                snapshot,
                cause: "FIRST_LEG_BAN",
                waterfallRisk: "HIGH",
                railPermissionA: "BLOCKED",
                railPermissionB: "BLOCKED");
        }

        // Section 9.3: LIVE_IMPULSE ban — active H1 impulse in motion, no reclaim structure
        if (IsLiveImpulseBan(snapshot))
        {
            return NoTrade(
                "TABLE ABORTED — LIVE_IMPULSE (impulse in motion, no reclaim structure).",
                score,
                snapshot,
                cause: "LIVE_IMPULSE",
                waterfallRisk: "HIGH",
                railPermissionA: "BLOCKED",
                railPermissionB: "BLOCKED");
        }

        // Section 9.1: TOP_LIQUIDATION ban — parabolic rally, overbought, crowded sentiment
        if (IsTopLiquidationBan(snapshot, telegramState))
        {
            return NoTrade(
                "TABLE ABORTED — TOP_LIQUIDATION (parabolic rally/overbought/crowded).",
                score,
                snapshot,
                cause: "TOP_LIQUIDATION",
                waterfallRisk: "HIGH",
                railPermissionA: "BLOCKED",
                railPermissionB: "BLOCKED");
        }

        // Section 9.2: STRUCTURAL_BREAKDOWN ban — H4 support broken, H1 below MA, no base
        if (IsStructuralBreakdownBan(snapshot))
        {
            return NoTrade(
                "TABLE ABORTED — STRUCTURAL_BREAKDOWN (H4 support broken, no recovery base).",
                score,
                snapshot,
                cause: "STRUCTURAL_BREAKDOWN",
                waterfallRisk: "HIGH",
                railPermissionA: "BLOCKED",
                railPermissionB: "BLOCKED");
        }

        // Section 9.4: BottomPermission hard gate — must be TRUE before any TABLE
        var (bottomPermissionGranted, bottomPermissionReason) = EvaluateBottomPermission(snapshot, session);
        if (!bottomPermissionGranted)
        {
            return NoTrade(
            $"TABLE ABORTED — BOTTOMPERMISSION_FALSE ({bottomPermissionReason}).",
                score,
                snapshot,
                cause: "BOTTOMPERMISSION_FALSE",
                waterfallRisk: waterfallRisk,
                railPermissionA: "BLOCKED",
                railPermissionB: "BLOCKED");
        }

        var primaryClose = snapshot.AuthoritativeRate > 0m
            ? snapshot.AuthoritativeRate
            : snapshot.TimeframeData
            .FirstOrDefault(tf => string.Equals(tf.Timeframe, "M5", StringComparison.OrdinalIgnoreCase))?.Close
            ?? snapshot.TimeframeData.First().Close;

        // Section 11.2: BuyStop — H1 above MA20, RSI 55-73, M15 compression ≥6, one at a time
        var buyStopAllowed = IsBuyStopAllowed(snapshot, cause, mode, waterfallRisk, telegramState, ledgerState);
        var rail = buyStopAllowed && railPermissionB == "ALLOWED"
            ? "BUY_STOP"
            : "BUY_LIMIT";

        if (rail == "BUY_STOP" && railPermissionB == "BLOCKED")
        {
            return NoTrade("Rail-B blocked by precedence gates.", score, snapshot, waterfallRisk: waterfallRisk, cause: cause, mode: mode, railPermissionA: railPermissionA, railPermissionB: railPermissionB);
        }

        var atrM15 = snapshot.AtrM15 > 0m ? snapshot.AtrM15 : snapshot.Atr;
        decimal entry;
        decimal entryOffset;
        if (rail == "BUY_STOP")
        {
            // Section 11.2: EntryOffset = max(2 USD, 0.25 × ATR_M15); EntryMT5 = LID + EntryOffset
            entryOffset = Math.Max(2m, 0.25m * atrM15);
            entry = primaryClose + entryOffset;
        }
        else
        {
            // Section 11.1: Buffer = max(1.5 USD, 0.2 × ATR_M15); EntryMT5 = support − Buffer
            entryOffset = Math.Max(1.5m, 0.2m * atrM15);
            entry = primaryClose - entryOffset;
        }

        // §G Pending-Before-Level Law (refinement spec §G):
        // BUY_STOP: entry must be strictly above current Ask (pending order above market).
        // BUY_LIMIT: entry must be strictly below current Bid (pending order below market).
        // Market buys are absolutely forbidden (refinement spec §B). If the computed entry
        // violates these rules → NO_TRADE rather than converting to a market order.
        if (rail == "BUY_STOP" && snapshot.Ask > 0m && entry <= snapshot.Ask)
        {
            return NoTrade(
                "violates_pending_before_level_law: BUY_STOP entry not above current Ask.",
                score,
                snapshot,
                cause: "PENDING_BEFORE_LEVEL_LAW",
                waterfallRisk: waterfallRisk,
                railPermissionA: railPermissionA,
                railPermissionB: railPermissionB);
        }

        if (rail == "BUY_LIMIT" && snapshot.Bid > 0m && entry >= snapshot.Bid)
        {
            return NoTrade(
                "violates_pending_before_level_law: BUY_LIMIT entry not below current Bid.",
                score,
                snapshot,
                cause: "PENDING_BEFORE_LEVEL_LAW",
                waterfallRisk: waterfallRisk,
                railPermissionA: railPermissionA,
                railPermissionB: railPermissionB);
        }

        // Section 11.1/11.2: TP caps per session (spec_v6 §3)
        var sessionTpCap = GetSessionTpCap(session, snapshot.IsFriday);
        decimal tpDistance;
        if (rail == "BUY_STOP")
        {
            // Section 11.2: TP distance = EntryOffset × 3.5, capped by session cap
            tpDistance = Math.Min(entryOffset * 3.5m, sessionTpCap);
        }
        else
        {
            // Section 11.1: BaseTP = 0.8 × ATR_M15 × 3, capped by session cap
            tpDistance = Math.Min(0.8m * atrM15 * 3m, sessionTpCap);
        }

        var tp = entry + tpDistance;

        // Section 4: C1 bucket for standard rotation (80% of deployable)
        var bucket = "C1";
        // Always recompute from DeployableCashAed to ensure consistency; BucketC1Aed is the canonical value
        var bucketCash = ledgerState.BucketC1Aed > 0m
            ? ledgerState.BucketC1Aed
            : decimal.Round(ledgerState.DeployableCashAed * 0.80m, 2);

        // Section 11.2: BuyStop uses session-specific size bands; BuyLimit uses standard size class
        var sizeClass = rail == "BUY_STOP"
            ? ResolveBuyStopSizeClass(session, snapshot.IsFriday)
            : ResolveSizeClassStandard(telegramState, waterfallRisk, railPermissionA);

        var maxGrams = ToMaxAffordableGrams(bucketCash, entry) - SafetyBufferGrams;
        var sizePct = ParseSizePercent(sizeClass);
        // CR11 PRETABLE: apply sizeModifier from PRETABLE intelligence layer.
        // CAUTION → 0.60 multiplier; SAFE → 1.0; BLOCK → 0.0 (already handled above).
        var effectiveSizePct = sizePct * Math.Clamp(pretableSizeModifier, 0m, 1m);
        var gramsFromSizeClass = ToMaxAffordableGrams(bucketCash * effectiveSizePct, entry);
        var grams = Math.Floor(Math.Min(maxGrams, gramsFromSizeClass));

        if (grams < minTradeGrams)
        {
            return NoTrade($"Capacity below {minTradeGrams:0.##}g minimum after spread/buffer.", score, snapshot, waterfallRisk: waterfallRisk, cause: cause, mode: mode, railPermissionA: railPermissionA, railPermissionB: railPermissionB);
        }

        // CR11 §EXPIRY_RULE: Session expiry bands (Asia 45-60m, London 30-45m, NY 20-30m)
        var expiryDuration = GetSessionExpiryDurationStandard(session, snapshot.IsFriday);
        var expiryUtcOffset = new DateTimeOffset(snapshot.Timestamp.UtcDateTime.Add(expiryDuration), TimeSpan.Zero);

        // Section 5.1: ExpiryServer = KSA − 50 min (UTC+2h10m); ExpiryKSA = UTC+3h
        var expiryKsa = expiryUtcOffset.ToOffset(TimeSpan.FromHours(3));
        var expiryServer = expiryUtcOffset.ToOffset(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(10)));

        // Section 3.2: ShopBuy = Entry + 0.80; ShopSell = TP − 0.80
        var shopBuy = decimal.Round(entry + ShopSpreadUsdPerOz, 2);
        var shopSell = decimal.Round(tp - ShopSpreadUsdPerOz, 2);

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
            ExpiryUtc: expiryUtcOffset,
            MaxLifeSeconds: (int)expiryDuration.TotalSeconds,
            AlignmentScore: score,
            TelegramState: telegramState,
            RailPermissionA: railPermissionA,
            RailPermissionB: railPermissionB,
            RotationCapThisSession: 2,
            ShopBuy: shopBuy,
            ShopSell: shopSell,
            ExpiryKSA: expiryKsa,
            ExpiryServer: expiryServer);
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
        string riskState,
        decimal minTradeGrams)
    {
        var session = NormalizeSession(snapshot.Session);
        var now = DateTimeOffset.UtcNow;
        var mode = ResolveWarMode(snapshot, aiSignal, now);
        var waterfallRisk = ResolveWaterfallRiskWar(snapshot, mode, aiSignal);
        var telegramState = NormalizeTelegramState(snapshot.TelegramState);

        // FIRST_LEG_BAN activation: crowded/late first-leg entry — block and wait for reclaim/base
        if (IsFirstLegBanConditionsMet(snapshot, aiSignal))
        {
            lock (WarModeGate)
            {
                _firstLegBanActive = true;
            }

            return NoTrade(
                "TABLE ABORTED — FIRST_LEG_BAN (late in first move, no reclaim/base yet).",
                aiSignal.AlignmentScore,
                snapshot,
                cause: "FIRST_LEG_BAN",
                mode: mode,
                waterfallRisk: "HIGH",
                railPermissionA: "BLOCKED",
                railPermissionB: "BLOCKED");
        }

        // DEESCALATION_RISK activation: de-escalation trap detected — block until flush + BottomPermission confirmed
        var deEscActivate = IsDeescalationRiskConditionsMet(snapshot, mode, aiSignal);
        if (deEscActivate)
        {
            lock (WarModeGate)
            {
                _deescalationRiskActive = true;
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
        if (gmax < minTradeGrams)
        {
            return NoTrade(
                $"WarPremium capacity below {minTradeGrams:0.##}g after spread/buffer.",
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
            if (stageGrams < minTradeGrams)
            {
                return NoTrade(
                    $"Stage grams below {minTradeGrams:0.##}g minimum.",
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
            var expiryWarB = snapshot.Timestamp.UtcDateTime.Add(GetWarRailBExpiry(session));
            var expiryWarBOffset = new DateTimeOffset(expiryWarB, TimeSpan.Zero);

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
                ExpiryUtc: expiryWarBOffset,
                MaxLifeSeconds: (int)GetWarRailBExpiry(session).TotalSeconds,
                AlignmentScore: Math.Clamp(aiSignal.AlignmentScore, 0m, 1m),
                TelegramState: telegramState,
                RailPermissionA: "ALLOWED",
                RailPermissionB: "ALLOWED",
                RotationCapThisSession: 5,
                ShopBuy: decimal.Round(entry + ShopSpreadUsdPerOz, 2),
                ShopSell: decimal.Round(tp - ShopSpreadUsdPerOz, 2),
                ExpiryKSA: expiryWarBOffset.ToOffset(TimeSpan.FromHours(3)),
                ExpiryServer: expiryWarBOffset.ToOffset(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(10))));
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
        if (reloadGrams < minTradeGrams)
        {
            return NoTrade(
                $"Reload grams below {minTradeGrams:0.##}g minimum.",
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
        var reloadExpiryOffset = new DateTimeOffset(snapshot.Timestamp.UtcDateTime.Add(GetWarRailAExpiry(session)), TimeSpan.Zero);

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
            ExpiryUtc: reloadExpiryOffset,
            MaxLifeSeconds: (int)GetWarRailAExpiry(session).TotalSeconds,
            AlignmentScore: Math.Clamp(aiSignal.AlignmentScore, 0m, 1m),
            TelegramState: telegramState,
            RailPermissionA: "ALLOWED",
            RailPermissionB: "BLOCKED",
            RotationCapThisSession: 2,
            ShopBuy: decimal.Round(reloadEntry + ShopSpreadUsdPerOz, 2),
            ShopSell: decimal.Round(reloadTp - ShopSpreadUsdPerOz, 2),
            ExpiryKSA: reloadExpiryOffset.ToOffset(TimeSpan.FromHours(3)),
            ExpiryServer: reloadExpiryOffset.ToOffset(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(10))));
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
        // 1. H1 sweep + reclaim: price swept intraday swing low then closed back above it
        var hasH1SweepReclaim = snapshot.HasLiquiditySweep
            && snapshot.TvAlertType is "SHELF_RECLAIM" or "RETEST_HOLD";

        // 2. M15 base: ≥ 2 overlapping candles off the low (proxies for consecutive strong green candles + higher low)
        var hasM15Base = snapshot.CompressionCountM15 >= 2 && snapshot.HasOverlapCandles;

        // 3. M5 compression: ≥ 6 overlapping M5 candles, contracting range, no new down-impulse (no big red ≥ 1.2×ATR_M5)
        var hasM5Compression = snapshot.CompressionCountM5 >= 6
            && snapshot.IsCompression
            && !snapshot.IsAtrExpanding;

        // 4. News calm: no major US data imminent, no war headline spike, spread at normal levels
        var isNewsCalm = !snapshot.IsUsRiskWindow
            && !snapshot.HasPanicDropSequence
            && (snapshot.SpreadMedian60m == 0m || snapshot.Spread <= snapshot.SpreadMedian60m * 1.5m);

        return hasH1SweepReclaim && hasM15Base && hasM5Compression && isNewsCalm;
    }

    /// <summary>
    /// CR1 §1: Detects a late first-leg entry that should be blocked.
    /// Activates FIRST_LEG_BAN when a strong H1 impulse is underway, the entry is late in the move
    /// (no clean base yet), and Telegram consensus is strongly aligned with the impulse direction.
    /// </summary>
    private static bool IsFirstLegBanConditionsMet(MarketSnapshotContract snapshot, TradeSignalContract aiSignal)
    {
        // Condition 1: Strong H1 impulse already underway — H1 candle body ≥ 1.2×ATR(H1)
        var h1Data = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "H1", StringComparison.OrdinalIgnoreCase));
        var h1Body = h1Data?.CandleBodySize ?? 0m;
        var h1Atr = snapshot.AtrH1 > 0m ? snapshot.AtrH1 : (h1Data?.Atr ?? 0m);
        if (h1Atr == 0m || h1Body < h1Atr * 1.2m) return false;

        // Condition 2: Entry is late in the move — proxied by HasImpulseCandles + IsExpansion +
        // ImpulseStrengthScore ≥ 0.6 (approximates price having moved ≥ 1–1.5×ATR_M15 from start);
        // also requires no clean H1 reclaim or M15 base has formed yet
        var lateInImpulse = snapshot.HasImpulseCandles
            && snapshot.IsExpansion
            && snapshot.ImpulseStrengthScore >= 0.6m;
        var noCleanBase = !snapshot.HasLiquiditySweep
            || snapshot.CompressionCountM15 < 2
            || !snapshot.HasOverlapCandles;
        if (!lateInImpulse || !noCleanBase) return false;

        // Condition 3: Telegram consensus strongly aligned with the impulse
        // STRONG_BUY ≈ BUY_CONSENSUS ≥ 80%; BUY ≈ BUY_CONSENSUS ≥ 70%
        var telegram = NormalizeTelegramState(snapshot.TelegramState);
        return telegram is "STRONG_BUY" or "BUY";
    }

    /// <summary>
    /// CR1 §3: Detects de-escalation risk conditions that should activate the DEESCALATION_RISK kill-switch.
    /// Triggers when WarPremium is fading while price is still elevated and sentiment remains bullish
    /// but peace/de-escalation language is emerging.
    /// </summary>
    private static bool IsDeescalationRiskConditionsMet(MarketSnapshotContract snapshot, string mode, TradeSignalContract aiSignal)
    {
        // Condition 1: WarPremium status fading/liquidating (mode has shifted away from WAR_PREMIUM)
        var warPremiumFading = !string.Equals(mode, "WAR_PREMIUM", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(aiSignal.ModeHint, "DEESCALATION_RISK", StringComparison.OrdinalIgnoreCase)
                || snapshot.HasPanicDropSequence);
        if (!warPremiumFading) return false;

        // Condition 2: Price still relatively high vs recent structure (not at fresh reclaimed base)
        var priceStillHigh = snapshot.AdrUsedPct > 50m
            || (snapshot.AdrUsedPct == 0m && snapshot.RsiH1 >= 60m);
        var notAtFreshBase = !snapshot.HasLiquiditySweep || snapshot.CompressionCountM15 < 2;
        if (!priceStillHigh || !notAtFreshBase) return false;

        // Condition 3: Sentiment still bullish AND war headlines calming / peace language emerging
        // Both must be present: bullish crowd PLUS de-escalation signal (not just one alone)
        var sentimentBullish = NormalizeTelegramState(snapshot.TelegramState) is "STRONG_BUY" or "BUY";
        var geoHeadline = (aiSignal.GeoHeadline ?? string.Empty).Trim().ToUpperInvariant();
        var deescLanguage = geoHeadline is "DEESCALATION" or "CEASEFIRE" or "PEACE" or "TRUCE";
        return sentimentBullish && deescLanguage;
    }

    /// <summary>
    /// CR1 §4: Checks whether DEESCALATION_RISK can be unlocked.
    /// Requires structural flush completion, full BottomPermission, normalized sentiment, and news safety.
    /// </summary>
    private static bool IsDeescalationRiskUnlocked(MarketSnapshotContract snapshot)
    {
        // 1. Structural flush completed: strong H4/H1 down leg, now near prior major support
        var structuralFlush = snapshot.RsiH1 > 0m
            && snapshot.RsiH1 < 45m
            && !snapshot.IsExpansion
            && !snapshot.HasPanicDropSequence;

        // 2. BottomPermission confirmed: H1 sweep+reclaim, M15 base, M5 compression, momentum turning up
        var bottomPermission = snapshot.HasLiquiditySweep
            && snapshot.CompressionCountM15 >= 2
            && snapshot.HasOverlapCandles
            && snapshot.RsiH1 is > 0m and < 60m
            && (snapshot.IsCompression || snapshot.CompressionCountM5 >= 3)
            && !snapshot.IsAtrExpanding;

        // 3. Sentiment normalized: BUY_CONSENSUS back to mixed/moderate (< 70–80%)
        // STRONG_BUY ≈ ≥80%, BUY ≈ ≥70% — both must be absent for sentiment to be considered normalized
        var sentimentNormalized = NormalizeTelegramState(snapshot.TelegramState) is not ("STRONG_BUY" or "BUY");

        // 4. News safe: no imminent major US data or peace headline shock
        var newsSafe = !snapshot.IsUsRiskWindow && !snapshot.HasPanicDropSequence;

        return structuralFlush && bottomPermission && sentimentNormalized && newsSafe;
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

    private static bool IsBuyStopAllowed(
        MarketSnapshotContract snapshot,
        string cause,
        string mode,
        string waterfallRisk,
        string telegramState,
        LedgerStateContract ledgerState)
    {
        // Section 11.2: Only one active BuyStop at a time; no stacking.
        // Block if any buy position (open or pending) exists — BuyStop is for fresh continuation only.
        if (ledgerState.OpenBuyCount > 0) return false;
        if (mode != "IMPULSE") return false;
        if (cause is not "TECH_BREAKOUT" and not "SCHEDULED_MACRO") return false;
        if (waterfallRisk != "LOW") return false;

        // H1 above MA20
        var h1 = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "H1", StringComparison.OrdinalIgnoreCase));
        var h1Close = h1?.Close ?? 0m;
        if (snapshot.Ma20H1 > 0m && h1Close > 0m && h1Close <= snapshot.Ma20H1) return false;

        // RSI(H1) between 55 and 73
        if (snapshot.RsiH1 > 0m && (snapshot.RsiH1 < 55m || snapshot.RsiH1 > 73m)) return false;

        // M15 compression ≥ 6 overlapping candles under lid
        if (snapshot.CompressionCountM15 < 6) return false;

        // No spread instability
        if (snapshot.SpreadMax60m > 0m && snapshot.SpreadMedian60m > 0m && snapshot.SpreadMax60m >= snapshot.SpreadMedian60m * 1.8m) return false;

        // No panic or sell sentiment
        if (telegramState is "SELL" or "STRONG_SELL") return false;
        if (snapshot.PanicSuspected) return false;

        return snapshot.IsBreakoutConfirmed;
    }

    /// <summary>
    /// Section 9.3: LIVE_IMPULSE ban — active H1 impulse in motion with no reclaim structure.
    /// Blocks all new entries until the impulse cools and a base forms.
    /// </summary>
    private static bool IsLiveImpulseBan(MarketSnapshotContract snapshot)
    {
        var h1 = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "H1", StringComparison.OrdinalIgnoreCase));
        var h1Body = h1?.CandleBodySize ?? 0m;
        var h1Atr = snapshot.AtrH1 > 0m ? snapshot.AtrH1 : (h1?.Atr ?? 0m);

        // H1 candle body ≥ 1.2×ATR_H1 indicates active impulse
        var h1Impulse = h1Atr > 0m && h1Body >= h1Atr * 1.2m;

        // ATR is expanding (momentum still in motion)
        var expanding = snapshot.IsAtrExpanding || snapshot.IsExpansion;

        // No reclaim structure formed yet (no sweep+reclaim or compression base)
        var noReclaim = !snapshot.HasLiquiditySweep && snapshot.CompressionCountM15 < 2;

        return h1Impulse && expanding && noReclaim;
    }

    /// <summary>
    /// Section 9.1: TOP_LIQUIDATION ban — parabolic rally, extreme RSI, crowded sentiment, or failure signatures.
    /// </summary>
    private static bool IsTopLiquidationBan(MarketSnapshotContract snapshot, string telegramState)
    {
        // Parabolic: H1 RSI overbought extreme + ATR still expanding
        var isParabolic = snapshot.RsiH1 >= 72m && snapshot.IsAtrExpanding;

        // Late-stage push with crowded sentiment: ADR nearly exhausted + everyone buying
        var isLateStage = snapshot.AdrUsedPct > 85m;
        var isCrowded = telegramState is "STRONG_BUY";
        var lateCrowdedPush = isLateStage && isCrowded;

        // Explicit failure signatures from TradingView alerts
        var hasFailure = snapshot.TvAlertType is "RSI_OVERHEAT" or "ADR_EXHAUSTION";

        return isParabolic || lateCrowdedPush || hasFailure;
    }

    /// <summary>
    /// Section 9.2: STRUCTURAL_BREAKDOWN ban — H4 key support broken, H1 below MA and cannot reclaim, no base.
    /// </summary>
    private static bool IsStructuralBreakdownBan(MarketSnapshotContract snapshot)
    {
        // H4 support broken: price below MA20_H4
        var h4 = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "H4", StringComparison.OrdinalIgnoreCase));
        var h4Close = h4?.Close ?? 0m;
        var h4Broken = snapshot.Ma20H4 > 0m && h4Close > 0m && h4Close < snapshot.Ma20H4;

        // H1 below MA20 and no reclaim (cannot close back above)
        var h1 = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "H1", StringComparison.OrdinalIgnoreCase));
        var h1Close = h1?.Close ?? 0m;
        var h1BelowMa = snapshot.Ma20H1 > 0m && h1Close > 0m && h1Close < snapshot.Ma20H1;
        var noReclaim = !snapshot.HasLiquiditySweep;

        // No M15/M5 base supporting a long
        var noBase = !snapshot.IsCompression && !snapshot.HasOverlapCandles && snapshot.CompressionCountM15 < 2;

        return h4Broken && h1BelowMa && noReclaim && noBase;
    }

    /// <summary>
    /// Section 9.4: BottomPermission hard gate — supports two legal paths (CR8):
    /// Path A (Reversal): H1 sweep + reclaim, M15 base, M5 compression, and momentum.
    /// Path B (Continuation): H1 bullish context intact, M15 compression base, M5 alignment,
    ///   no FAIL threat, no waterfall signature, no hazard conflict.
    ///
    /// Session-adaptive strictness (CR7):
    ///   London: stronger structure confirmation (M15 compression ≥ 4).
    ///   NY: additional spread/liquidity guard.
    /// </summary>
    private static (bool IsGranted, string Reason) EvaluateBottomPermission(
        MarketSnapshotContract snapshot,
        string? session = null)
    {
        var normalizedSession = session ?? NormalizeSession(snapshot.Session);

        // ── Path A: Reversal — H1 sweep+reclaim ───────────────────────────────
        var hasH1SweepReclaim = snapshot.HasLiquiditySweep;

        // Session-adaptive M15 base threshold: London needs ≥ 4 candles (stronger confirmation)
        var m15BaseThreshold = normalizedSession == "LONDON" ? 4 : 2;
        var hasM15Base = snapshot.HasOverlapCandles && snapshot.CompressionCountM15 >= m15BaseThreshold;

        // M5 compression: ≥6 overlapping M5 candles, contracting range, no new down-impulse
        var hasM5Compression = snapshot.IsCompression
            && snapshot.CompressionCountM5 >= 6
            && !snapshot.IsAtrExpanding;

        // Momentum confirmation: RSI(M15) > 35 (not oversold) or RSI not available
        var momentumOk = snapshot.RsiM15 == 0m || snapshot.RsiM15 > 35m;

        var reversalGranted = hasH1SweepReclaim && hasM15Base && hasM5Compression && momentumOk;

        if (reversalGranted)
        {
            return (true, $"PATH_A_REVERSAL: H1SweepReclaim={hasH1SweepReclaim}, M15Base={hasM15Base}(min={m15BaseThreshold}), M5Compression={hasM5Compression}, MomentumOk={momentumOk}");
        }

        // ── Path B: Continuation — trend pullback / reload ────────────────────
        // H1 bullish context: H1 close above MA20
        var h1Data = snapshot.TimeframeData
            .FirstOrDefault(x => string.Equals(x.Timeframe, "H1", StringComparison.OrdinalIgnoreCase));
        var h1Close = h1Data?.Close ?? 0m;
        var h1BullishContext = snapshot.Ma20H1 > 0m && h1Close > 0m && h1Close > snapshot.Ma20H1;

        // M15 compression base: session-adaptive threshold (London: ≥ 4, others: ≥ 3)
        var contM15Threshold = normalizedSession == "LONDON" ? 4 : 3;
        var contM15Base = snapshot.CompressionCountM15 >= contM15Threshold && snapshot.IsCompression;

        // M5 entry alignment
        var contM5Alignment = snapshot.IsCompression && snapshot.CompressionCountM5 >= 3;

        // No FAIL threat: ADR usage not extreme
        var noFailThreat = snapshot.AdrUsedPct <= 85m || snapshot.AdrUsedPct == 0m;

        // No waterfall signature
        var noWaterfallSignature = !snapshot.HasPanicDropSequence
            && (!snapshot.IsExpansion || !snapshot.IsAtrExpanding);

        // No hazard conflict (no high-impact US risk window)
        var noHazardConflict = !snapshot.IsUsRiskWindow;

        // NY-specific: additional spread guard (strictest spike/liquidity check)
        var spreadOk = normalizedSession != "NY"
            || snapshot.SpreadMax1m == 0m
            || snapshot.SpreadAvg1m == 0m
            || snapshot.SpreadMax1m < snapshot.SpreadAvg1m * 1.5m;

        var continuationGranted = h1BullishContext
            && contM15Base
            && contM5Alignment
            && noFailThreat
            && noWaterfallSignature
            && noHazardConflict
            && spreadOk;

        if (continuationGranted)
        {
            return (true, $"PATH_B_CONTINUATION: H1Bullish={h1BullishContext}, M15Base={contM15Base}(min={contM15Threshold}), M5Align={contM5Alignment}, NoFail={noFailThreat}, NoWaterfall={noWaterfallSignature}, NoHazard={noHazardConflict}, SpreadOk={spreadOk}");
        }

        var reason = $"PATH_A: H1SweepReclaim={hasH1SweepReclaim}, M15Base={hasM15Base}(min={m15BaseThreshold}), M5Compression={hasM5Compression}, MomentumOk={momentumOk} | PATH_B: H1Bullish={h1BullishContext}, M15Base={contM15Base}(min={contM15Threshold}), M5Align={contM5Alignment}, NoFail={noFailThreat}, NoWaterfall={noWaterfallSignature}, NoHazard={noHazardConflict}, SpreadOk={spreadOk}";
        return (false, reason);
    }

    /// <summary>
    /// Returns true when the session is in a Friday London or Friday NY window,
    /// which triggers tighter expiry/TP/size caps per spec_v6 §3.
    /// </summary>
    private static bool IsFridayLondonOrNy(string session, bool isFriday)
        => isFriday && session is "LONDON" or "NY";

    /// <summary>
    /// CR11 §TARGET_PROFIT_MODEL: Session TP cap (max USD distance from entry to TP).
    /// Target range: +8 to +15 USD. Maximum adaptive TP: 18 USD (CR11 §ADAPTIVE_TP_ENGINE).
    /// </summary>
    private static decimal GetSessionTpCap(string session, bool isFriday)
    {
        if (IsFridayLondonOrNy(session, isFriday)) return 8m;
        return session switch
        {
            "JAPAN"  => 12m,   // Asia setups: slower moves, mid-range target
            "INDIA"  => 12m,   // India session: moderate volatility
            "LONDON" => 15m,   // London: strongest directional moves (up to 15 USD)
            "NY"     => 15m,   // New York: spike capability but spike risk
            _        => 12m,
        };
    }

    /// <summary>
    /// CR11 §EXPIRY_RULE: Session expiry bands.
    /// Asia/India setups → 45–60 minutes (use midpoint 52 min).
    /// London setups     → 30–45 minutes (use midpoint 37 min).
    /// NY setups         → 20–30 minutes (use midpoint 25 min).
    /// Friday tight windows: 15 min.
    /// </summary>
    private static TimeSpan GetSessionExpiryDurationStandard(string session, bool isFriday)
    {
        if (IsFridayLondonOrNy(session, isFriday)) return TimeSpan.FromMinutes(15);
        return session switch
        {
            "JAPAN"  => TimeSpan.FromMinutes(52),   // CR11: Asia 45–60 min
            "INDIA"  => TimeSpan.FromMinutes(52),   // CR11: Asia 45–60 min
            "LONDON" => TimeSpan.FromMinutes(37),   // CR11: London 30–45 min
            "NY"     => TimeSpan.FromMinutes(25),   // CR11: NY 20–30 min
            _        => TimeSpan.FromMinutes(37),
        };
    }

    /// <summary>
    /// Section 11.2: BuyStop size bands per session (% of C1 bucket).
    /// Japan: 25%, India: 30%, London: 20%, NY: 15%, Friday London/NY: 13%.
    /// </summary>
    private static string ResolveBuyStopSizeClass(string session, bool isFriday)
    {
        if (IsFridayLondonOrNy(session, isFriday)) return "13%";
        return session switch
        {
            "JAPAN" => "25%",
            "INDIA" => "30%",
            "LONDON" => "20%",
            "NY" => "15%",
            _ => "20%",
        };
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
        "13%" => 0.13m,
        "15%" => 0.15m,
        "20%" => 0.20m,
        "25%" => 0.25m,
        "30%" => 0.30m,
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
