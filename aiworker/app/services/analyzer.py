import logging
import asyncio
import json
from datetime import datetime, timedelta, timezone
from pathlib import Path

from app.ai.config import (
    AI_ANALYZERS,
    CONSENSUS_ENTRY_TOLERANCE_PCT,
    AI_NEWS_TIMEOUT_SECONDS,
    AI_COMMITTEE_TIMEOUT_SECONDS,
    AI_MACRO_TIMEOUT_SECONDS,
    AI_SINGLE_LEAD_MODEL,
)
from app.ai.provider_manager import AIProviderManager
from app.models.contracts import MarketSnapshot, TradeSignal
from app.services.ai_gate import AiGate
from app.services.external_news import ExternalNewsContext
from app.services.macro_intel import MacroIntelContext, MacroIntelService
from app.services.telegram_news import TelegramNewsContext, TelegramNewsService

logger = logging.getLogger(__name__)

_PROMPT_TEXT_CACHE: dict[str, str] = {}
_MAX_PROVIDER_TRACES = 120
_MAX_TRACE_FIELD_CHARS = 3500


def _is_supported_symbol(symbol: str) -> bool:
    normalized = (symbol or "").strip().upper()
    return normalized.startswith("XAUUSD")


def _json_default(value: object) -> object:
    if isinstance(value, (datetime,)):
        return value.isoformat()
    if hasattr(value, "model_dump"):
        try:
            return value.model_dump()
        except Exception:
            return str(value)
    return str(value)


def _safe_json_dumps(payload: object) -> str:
    return json.dumps(payload, ensure_ascii=False, default=_json_default)


def _load_short_prompt(filename: str) -> str:
    cached = _PROMPT_TEXT_CACHE.get(filename)
    if cached is not None:
        return cached

    here = Path(__file__).resolve()
    repo_root = here.parents[3]
    prompt_path = repo_root / "prompts" / filename
    try:
        text = prompt_path.read_text(encoding="utf-8").strip()
    except Exception:
        text = ""

    _PROMPT_TEXT_CACHE[filename] = text
    return text


class AnalyzerService:
    def __init__(self) -> None:
        self._has_live_analyzers = len(AI_ANALYZERS) > 0
        self._manager = AIProviderManager(AI_ANALYZERS) if self._has_live_analyzers else None
        self._telegram_news = TelegramNewsService()
        self._macro_intel = MacroIntelService()
        self._ai_gate = AiGate()

        # CR9: build a single-lead-model manager for the live decision path.
        # Other configured analyzers are reserved for STUDY / SELF CROSSCHECK only.
        # When there is only one analyzer or AI_SINGLE_LEAD_MODEL is off, use the
        # full manager.  This is set to a non-None value whenever _has_live_analyzers
        # is True (the only case where this code path is reached).
        if self._has_live_analyzers and AI_SINGLE_LEAD_MODEL and len(AI_ANALYZERS) > 1:
            self._lead_manager: AIProviderManager = AIProviderManager(AI_ANALYZERS[:1])
            logger.info(
                "CR9 single-lead-model active: live path uses %s; %d other model(s) reserved for STUDY.",
                AI_ANALYZERS[0].name,
                len(AI_ANALYZERS) - 1,
            )
        elif self._has_live_analyzers:
            self._lead_manager = self._manager  # type: ignore[assignment]
        else:
            self._lead_manager = None  # type: ignore[assignment]

        if not self._has_live_analyzers:
            logger.warning(
                "No live AI analyzers configured; using deterministic simulation fallback analyzer."
            )

    async def analyze(self, snapshot: MarketSnapshot) -> TradeSignal:
        if not _is_supported_symbol(snapshot.symbol):
            raise ValueError("Only XAUUSD-family symbols are supported")

        allowed_tfs = {"H1", "M15", "M5"}
        scoped_timeframes = [
            item for item in snapshot.timeframeData
            if (item.timeframe or "").upper() in allowed_tfs
        ]
        if not scoped_timeframes:
            scoped_timeframes = snapshot.timeframeData

        primary_tf = scoped_timeframes[0]
        volatility_expansion = snapshot.volatilityExpansion
        if volatility_expansion is None:
            volatility_expansion = (snapshot.atr / snapshot.adr) if snapshot.adr > 0 else 0.0

        authoritative_rate = snapshot.authoritativeRate if snapshot.authoritativeRate > 0 else primary_tf.close

        market_context = {
            "symbol": snapshot.symbol,
            "current_price": authoritative_rate,
            "open": primary_tf.open,
            "high": primary_tf.high,
            "low": primary_tf.low,
            "close": primary_tf.close,
            "timeframes": [item.model_dump() for item in scoped_timeframes],
            "ma20": snapshot.ma20,
            "ma20_h4": snapshot.ma20H4,
            "ma20_h1": snapshot.ma20H1,
            "ma20_m30": snapshot.ma20M30,
            "rsi_h1": snapshot.rsiH1,
            "rsi_m15": snapshot.rsiM15,
            "atr": snapshot.atr,
            "adr": snapshot.adr,
            "atr_h1": snapshot.atrH1,
            "atr_m15": snapshot.atrM15,
            "previous_day_high": snapshot.previousDayHigh,
            "previous_day_low": snapshot.previousDayLow,
            "weekly_high": snapshot.weeklyHigh,
            "weekly_low": snapshot.weeklyLow,
            "day_open": snapshot.dayOpen,
            "week_open": snapshot.weekOpen,
            "session_high": snapshot.sessionHigh,
            "session_low": snapshot.sessionLow,
            "session_high_japan": snapshot.sessionHighJapan,
            "session_low_japan": snapshot.sessionLowJapan,
            "session_high_india": snapshot.sessionHighIndia,
            "session_low_india": snapshot.sessionLowIndia,
            "session_high_london": snapshot.sessionHighLondon,
            "session_low_london": snapshot.sessionLowLondon,
            "session_high_ny": snapshot.sessionHighNy,
            "session_low_ny": snapshot.sessionLowNy,
            "previous_session_high": snapshot.previousSessionHigh,
            "previous_session_low": snapshot.previousSessionLow,
            "ema50_h1": snapshot.ema50H1,
            "ema200_h1": snapshot.ema200H1,
            "adr_used_pct": snapshot.adrUsedPct,
            "session": snapshot.session,
            "timestamp": snapshot.timestamp.isoformat(),
            "volatility_expansion": volatility_expansion,
            "day_of_week": snapshot.dayOfWeek,
            "telegram_impact_tag": snapshot.telegramImpactTag,
            "tradingview_confirmation": snapshot.tradingViewConfirmation,
            "is_compression": snapshot.isCompression,
            "is_expansion": snapshot.isExpansion,
            "is_atr_expanding": snapshot.isAtrExpanding,
            "has_overlap_candles": snapshot.hasOverlapCandles,
            "has_impulse_candles": snapshot.hasImpulseCandles,
            "has_liquidity_sweep": snapshot.hasLiquiditySweep,
            "has_panic_drop_sequence": snapshot.hasPanicDropSequence,
            "is_post_spike_pullback": snapshot.isPostSpikePullback,
            "is_london_ny_overlap": snapshot.isLondonNyOverlap,
            "is_breakout_confirmed": snapshot.isBreakoutConfirmed,
            "is_us_risk_window": snapshot.isUsRiskWindow,
            "is_friday": snapshot.isFriday,
            "spread": snapshot.spread,
            "spread_median_60m": snapshot.spreadMedian60m,
            "spread_max_60m": snapshot.spreadMax60m,
            "spread_min_1m": snapshot.spreadMin1m,
            "spread_avg_1m": snapshot.spreadAvg1m,
            "spread_max_1m": snapshot.spreadMax1m,
            "spread_min_5m": snapshot.spreadMin5m,
            "spread_avg_5m": snapshot.spreadAvg5m,
            "spread_max_5m": snapshot.spreadMax5m,
            "tick_rate_per_30s": snapshot.tickRatePer30s,
            "freeze_gap_detected": snapshot.freezeGapDetected,
            "slippage_estimate_points": snapshot.slippageEstimatePoints,
            "session_vwap": snapshot.sessionVwap,
            "compression_ranges_m15": snapshot.compressionRangesM15,
            "pending_orders": snapshot.pendingOrders,
            "open_positions": snapshot.openPositions,
            "order_execution_events": snapshot.orderExecutionEvents,
            "free_margin": snapshot.freeMargin,
            "equity": snapshot.equity,
            "balance": snapshot.balance,
            "compression_count_m15": snapshot.compressionCountM15,
            "expansion_count_m15": snapshot.expansionCountM15,
            "impulse_strength_score": snapshot.impulseStrengthScore,
            "telegram_state": snapshot.telegramState,
            "panic_suspected": snapshot.panicSuspected,
            "tv_alert_type": snapshot.tvAlertType,
            "session_phase": snapshot.sessionPhase,
            "mt5_server_time": snapshot.mt5ServerTime.isoformat() if snapshot.mt5ServerTime else None,
            "ksa_time": snapshot.ksaTime.isoformat() if snapshot.ksaTime else None,
            "uae_time": snapshot.uaeTime.isoformat() if snapshot.uaeTime else None,
            "india_time": snapshot.indiaTime.isoformat() if snapshot.indiaTime else None,
            "internal_clock_utc": snapshot.internalClockUtc.isoformat() if snapshot.internalClockUtc else None,
            "utc_reference_time": snapshot.utcReferenceTime.isoformat() if snapshot.utcReferenceTime else None,
            "time_skew_ms": snapshot.timeSkewMs,
            "system_fetched_gold_rate": snapshot.systemFetchedGoldRate,
            "rate_delta_usd": snapshot.rateDeltaUsd,
            "rate_authority": snapshot.rateAuthority,
            "authoritative_rate": authoritative_rate,
        }
        market_context["_ai_provider_traces"] = []

        def _compact_provider_traces() -> list[dict[str, object]]:
            raw = market_context.get("_ai_provider_traces", [])
            if not isinstance(raw, list):
                return []

            compact: list[dict[str, object]] = []
            for item in raw[:_MAX_PROVIDER_TRACES]:
                if not isinstance(item, dict):
                    continue

                normalized: dict[str, object] = {}
                for key, value in item.items():
                    if isinstance(value, str):
                        if len(value) > _MAX_TRACE_FIELD_CHARS:
                            normalized[key] = value[:_MAX_TRACE_FIELD_CHARS] + "...<truncated>"
                        else:
                            normalized[key] = value
                    else:
                        normalized[key] = value
                compact.append(normalized)

            return compact

        try:
            telegram_news = await asyncio.wait_for(
                self._telegram_news.collect_news_context(snapshot.symbol),
                timeout=max(1.0, AI_NEWS_TIMEOUT_SECONDS),
            )
        except asyncio.TimeoutError:
            logger.warning("Telegram context timed out after %.1fs; using safe fallback context.", AI_NEWS_TIMEOUT_SECONDS)
            telegram_news = TelegramNewsContext(
                enabled=False,
                impact_tag="LOW",
                risk_tag="CAUTION",
                direction_bias="NEUTRAL",
                telegram_state="QUIET",
                panic_suspected=False,
                buy_score=0.0,
                sell_score=0.0,
                dominance=0.0,
                tags=["telegram_timeout"],
                summary="Telegram context timeout; fallback context used.",
                headlines=[],
                items=[],
            )

        external_news = ExternalNewsContext(
            enabled=False,
            feed_count=0,
            impact_tag="LOW",
            risk_tag="CAUTION",
            direction_bias="NEUTRAL",
            news_state="QUIET",
            buy_score=0.0,
            sell_score=0.0,
            dominance=0.0,
            panic_suspected=False,
            tags=["external_news_disabled_prd"],
            summary="External RSS news disabled per PRD; AI news relies on prompt-driven context.",
            headlines=[],
            items=[],
        )
        market_context["telegram_news"] = {
            "impact_tag": telegram_news.impact_tag,
            "risk_tag": telegram_news.risk_tag,
            "direction_bias": telegram_news.direction_bias,
            "telegram_state": telegram_news.telegram_state,
            "panic_suspected": telegram_news.panic_suspected,
            "buy_score": telegram_news.buy_score,
            "sell_score": telegram_news.sell_score,
            "dominance": telegram_news.dominance,
            "tags": telegram_news.tags,
            "summary": telegram_news.summary,
            "headlines": telegram_news.headlines,
            "items": telegram_news.items,
        }
        market_context["external_news"] = {
            "enabled": external_news.enabled,
            "feed_count": external_news.feed_count,
            "impact_tag": external_news.impact_tag,
            "risk_tag": external_news.risk_tag,
            "direction_bias": external_news.direction_bias,
            "news_state": external_news.news_state,
            "panic_suspected": external_news.panic_suspected,
            "buy_score": external_news.buy_score,
            "sell_score": external_news.sell_score,
            "dominance": external_news.dominance,
            "tags": external_news.tags,
            "summary": external_news.summary,
            "headlines": external_news.headlines,
            "items": external_news.items,
        }

        try:
            macro_intel = await asyncio.wait_for(
                self._macro_intel.collect_context(snapshot.symbol),
                timeout=max(1.0, AI_MACRO_TIMEOUT_SECONDS),
            )
        except asyncio.TimeoutError:
            logger.warning("Macro intelligence timed out after %.1fs; using safe fallback context.", AI_MACRO_TIMEOUT_SECONDS)
            macro_intel = MacroIntelContext(summary="Macro intelligence timeout; fallback context used.")

        market_context["macro_intel"] = {
            "geo_headline": macro_intel.geo_headline,
            "dxy_bias": macro_intel.dxy_bias,
            "yields_bias": macro_intel.yields_bias,
            "cross_metals_bias": macro_intel.cross_metals_bias,
            "cb_flow": macro_intel.cb_flow,
            "inst_positioning": macro_intel.inst_positioning,
            "event_risk": macro_intel.event_risk,
            "summary": macro_intel.summary,
        }

        indicators_regime = _classify_indicators_regime(snapshot, macro_intel)
        risk_state = _classify_risk_state(snapshot, macro_intel, telegram_news, external_news)
        regime_tag = _resolve_regime_tag(snapshot, macro_intel, indicators_regime, risk_state)
        prompt_refs = [
            "prompts/master_prompt.md",
            "prompts/short_prompt_news.md",
            "prompts/short_prompt_analyze.md",
            "prompts/short_prompt_validate.md",
            "prompts/short_prompt_study.md",
            "prompts/short_prompt_self_crosscheck.md",
            "prompts/short_prompt_captial_utilization.md",
        ]
        provider_models = [item.name for item in AI_ANALYZERS]
        ai_request = {
            "cycle_id": snapshot.cycleId,
            "symbol": snapshot.symbol,
            "market_context": {k: v for k, v in market_context.items() if not k.startswith("_")},
            "prompt_refs": prompt_refs,
            "provider_models": provider_models,
        }

        def _build_request_with_prompt_dispatch() -> dict[str, object]:
            provider_traces = _compact_provider_traces()
            dispatch: list[dict[str, object]] = []
            ai_used: list[dict[str, str]] = []
            seen_dispatch: set[str] = set()
            seen_ai: set[str] = set()

            for trace in provider_traces:
                if not isinstance(trace, dict):
                    continue
                if trace.get("eventType") != "AI_PROVIDER_REQUEST":
                    continue
                analyzer = str(trace.get("analyzer", ""))
                provider = str(trace.get("provider", ""))
                model = str(trace.get("model", ""))
                key = f"{analyzer}|{provider}|{model}"

                if key and key not in seen_dispatch:
                    seen_dispatch.add(key)
                    dispatch.append(
                        {
                            "analyzer": analyzer,
                            "provider": provider,
                            "model": model,
                            "system_prompt": trace.get("system_prompt"),
                            "user_prompt": trace.get("user_prompt"),
                        }
                    )

                if key and key not in seen_ai:
                    seen_ai.add(key)
                    ai_used.append(
                        {
                            "analyzer": analyzer,
                            "provider": provider,
                            "model": model,
                        }
                    )

            return {
                **ai_request,
                "ai_used": ai_used,
                "prompt_dispatch": dispatch,
            }
        stage_events: list[dict[str, object]] = [
            {
                "eventType": "TELEGRAM_INTERPRETED",
                "stage": "telegram",
                "source": "aiworker",
                "payload": {
                    "telegram_state": telegram_news.telegram_state,
                    "impact_tag": telegram_news.impact_tag,
                    "risk_tag": telegram_news.risk_tag,
                    "direction_bias": telegram_news.direction_bias,
                    "panic_suspected": telegram_news.panic_suspected,
                    "summary": telegram_news.summary,
                },
            },
            {
                "eventType": "MACRO_CONTEXT_BUILT",
                "stage": "macro",
                "source": "aiworker",
                "payload": {
                    "geo_headline": macro_intel.geo_headline,
                    "dxy_bias": macro_intel.dxy_bias,
                    "yields_bias": macro_intel.yields_bias,
                    "cross_metals_bias": macro_intel.cross_metals_bias,
                    "cb_flow": macro_intel.cb_flow,
                    "inst_positioning": macro_intel.inst_positioning,
                    "event_risk": macro_intel.event_risk,
                    "summary": macro_intel.summary,
                },
            },
        ]

        if risk_state == "BLOCK":
            return TradeSignal(
                rail="NO_TRADE",
                entry=0.0,
                tp=0.0,
                pe=snapshot.timestamp + timedelta(minutes=5),
                ml=300,
                confidence=0.0,
                safetyTag="BLOCK",
                directionBias="NEUTRAL",
                alignmentScore=0.0,
                newsImpactTag=_resolve_news_impact_tag(snapshot, telegram_news, external_news),
                tvConfirmationTag=_resolve_tv_confirmation(snapshot),
                newsTags=_resolve_news_tags(snapshot, volatility_expansion, telegram_news, external_news),
                summary=_build_pretable_summary(snapshot, regime_tag, risk_state, macro_intel),
                consensusPassed=True,
                agreementCount=1,
                requiredAgreement=1,
                disagreementReason="RISK_BLOCKED_PRETABLE",
                providerVotes=["pretable_classifier:block"],
                modeHint=_resolve_mode_hint(snapshot, telegram_news, external_news),
                modeConfidence=_resolve_mode_confidence(snapshot, telegram_news, external_news),
                modeTtlSeconds=_resolve_mode_ttl(snapshot, telegram_news, external_news),
                modeKeywords=_resolve_mode_keywords(telegram_news, external_news),
                regimeTag=regime_tag,
                riskState=risk_state,
                geoHeadline=macro_intel.geo_headline,
                dxyBias=macro_intel.dxy_bias,
                yieldsBias=macro_intel.yields_bias,
                crossMetalsBias=macro_intel.cross_metals_bias,
                cbFlow=macro_intel.cb_flow,
                instPositioning=macro_intel.inst_positioning,
                eventRisk=macro_intel.event_risk,
                promptRefs=prompt_refs,
                providerModels=provider_models,
                aiTraceJson=_safe_json_dumps({
                    "ai_request": _build_request_with_prompt_dispatch(),
                    "provider_traces": _compact_provider_traces(),
                    "stage": "pretable",
                    "result": "blocked",
                    "reason": "RISK_BLOCKED_PRETABLE",
                    "telegram_state": telegram_news.telegram_state,
                    "telegram_summary": telegram_news.summary,
                    "macro_summary": macro_intel.summary,
                    "events": stage_events + [{
                        "eventType": "RISK_BLOCKED_PRETABLE",
                        "stage": "pretable",
                        "source": "aiworker",
                        "payload": {
                            "regime_tag": regime_tag,
                            "risk_state": risk_state,
                            "summary": _build_pretable_summary(snapshot, regime_tag, risk_state, macro_intel),
                        },
                    }],
                }),
                cycleId=snapshot.cycleId,
            )

        if not self._has_live_analyzers or self._manager is None or self._lead_manager is None:
            return _build_fallback_signal(
                snapshot,
                volatility_expansion,
                telegram_news,
                external_news,
                prompt_refs,
                provider_models,
                stage_events,
                _compact_provider_traces(),
            )

        # ── CR9 AI Gate: check data freshness and session budget before calling LLM ──
        gate_blocked, gate_reason = await self._ai_gate.check_async(
            snapshot_timestamp=snapshot.timestamp,
            session=snapshot.session,
            risk_state=risk_state,
        )
        if gate_blocked:
            stage_events.append({
                "eventType": "AI_GATE_BLOCKED",
                "stage": "ai_gate",
                "source": "aiworker",
                "payload": {"reason": gate_reason},
            })
            return TradeSignal(
                rail="NO_TRADE",
                entry=0.0,
                tp=0.0,
                pe=snapshot.timestamp + timedelta(minutes=5),
                ml=300,
                confidence=0.0,
                safetyTag="BLOCK",
                directionBias="NEUTRAL",
                alignmentScore=0.0,
                newsImpactTag=_resolve_news_impact_tag(snapshot, telegram_news, external_news),
                tvConfirmationTag=_resolve_tv_confirmation(snapshot),
                newsTags=_resolve_news_tags(snapshot, volatility_expansion, telegram_news, external_news),
                summary=f"AI gate blocked: {gate_reason}",
                consensusPassed=True,
                agreementCount=1,
                requiredAgreement=1,
                disagreementReason=gate_reason,
                providerVotes=[f"ai_gate:blocked:{gate_reason}"],
                modeHint=_resolve_mode_hint(snapshot, telegram_news, external_news),
                modeConfidence=_resolve_mode_confidence(snapshot, telegram_news, external_news),
                modeTtlSeconds=_resolve_mode_ttl(snapshot, telegram_news, external_news),
                modeKeywords=_resolve_mode_keywords(telegram_news, external_news),
                regimeTag=regime_tag,
                riskState=risk_state,
                geoHeadline=macro_intel.geo_headline,
                dxyBias=macro_intel.dxy_bias,
                yieldsBias=macro_intel.yields_bias,
                crossMetalsBias=macro_intel.cross_metals_bias,
                cbFlow=macro_intel.cb_flow,
                instPositioning=macro_intel.inst_positioning,
                eventRisk=macro_intel.event_risk,
                promptRefs=prompt_refs,
                providerModels=provider_models,
                aiTraceJson=_safe_json_dumps({
                    "ai_request": _build_request_with_prompt_dispatch(),
                    "provider_traces": _compact_provider_traces(),
                    "stage": "ai_gate",
                    "result": "blocked",
                    "reason": gate_reason,
                    "events": stage_events,
                }),
                cycleId=snapshot.cycleId,
            )

        # ── CR9: use single lead model for the live decision path ────────────
        # pre-stage (news / analyze) committee calls have been removed per CR9:
        # the market context already carries telegram / macro / news data so
        # there is no need for a separate AI round-trip to interpret them.
        lead_manager = self._lead_manager
        min_agreement = 1  # single-lead path always requires exactly 1 agreement
        pre_stage_votes: list[str] = []

        try:
            committee = await asyncio.wait_for(
                lead_manager.analyze_with_committee(
                    market_context,
                    min_agreement=min_agreement,
                    entry_tolerance_pct=CONSENSUS_ENTRY_TOLERANCE_PCT,
                ),
                timeout=max(5.0, AI_COMMITTEE_TIMEOUT_SECONDS),
            )
        except asyncio.TimeoutError:
            logger.warning("AI committee timed out after %.1fs", AI_COMMITTEE_TIMEOUT_SECONDS)
            committee = lead_manager.timeout_decision(required_agreement=min_agreement)

        # CR9: record this call against the session budget
        await self._ai_gate.record_call_async(snapshot.session)

        stage_events.append({
            "eventType": "AI_COMMITTEE_EVALUATED",
            "stage": "committee",
            "source": "aiworker",
            "payload": {
                "agreement_count": committee.agreement_count,
                "required_agreement": committee.required_agreement,
                "consensus_passed": committee.consensus_passed,
                "disagreement_reason": committee.disagreement_reason,
                "provider_votes": committee.provider_votes,
            },
        })

        if not committee.consensus_passed or committee.signal is None:
            if committee.agreement_count == 0:
                fallback = _build_fallback_signal(
                    snapshot,
                    volatility_expansion,
                    telegram_news,
                    external_news,
                    prompt_refs,
                    provider_models,
                    stage_events,
                    _compact_provider_traces(),
                )
                fallback.providerVotes = list(dict.fromkeys((pre_stage_votes or []) + (fallback.providerVotes or [])))
                fallback.summary = (
                    "Fallback simulation analyzer used after committee produced no usable signal. "
                    f"reason={committee.disagreement_reason or 'no_consensus'}"
                )
                fallback.aiTraceJson = _safe_json_dumps({
                    "ai_request": _build_request_with_prompt_dispatch(),
                    "provider_traces": _compact_provider_traces(),
                    "stage": "committee",
                    "result": "fallback",
                    "reason": committee.disagreement_reason or "no_consensus",
                    "pre_stage_votes": pre_stage_votes,
                    "committee_votes": committee.provider_votes,
                    "agreement_count": committee.agreement_count,
                    "required_agreement": committee.required_agreement,
                    "events": stage_events + [{
                        "eventType": "AI_FALLBACK_TRIGGERED",
                        "stage": "fallback",
                        "source": "aiworker",
                        "payload": {
                            "reason": committee.disagreement_reason or "no_consensus",
                        },
                    }],
                })
                return fallback

            logger.warning(
                "Consensus failed: required=%s agreed=%s reason=%s",
                committee.required_agreement,
                committee.agreement_count,
                committee.disagreement_reason,
            )
            return TradeSignal(
                rail="NO_TRADE",
                entry=0.0,
                tp=0.0,
                pe=snapshot.timestamp + timedelta(minutes=5),
                ml=300,
                confidence=0.0,
                safetyTag="BLOCK",
                directionBias="NEUTRAL",
                alignmentScore=0.0,
                newsImpactTag=_resolve_news_impact_tag(snapshot, telegram_news, external_news),
                tvConfirmationTag=_resolve_tv_confirmation(snapshot),
                newsTags=_resolve_news_tags(snapshot, volatility_expansion, telegram_news, external_news),
                summary=(
                    f"AI consensus failed: {committee.disagreement_reason or 'insufficient agreement'}"
                ),
                consensusPassed=False,
                agreementCount=committee.agreement_count,
                requiredAgreement=committee.required_agreement,
                disagreementReason=committee.disagreement_reason,
                providerVotes=list(dict.fromkeys((pre_stage_votes or []) + (committee.provider_votes or []))),
                modeHint=_resolve_mode_hint(snapshot, telegram_news, external_news),
                modeConfidence=_resolve_mode_confidence(snapshot, telegram_news, external_news),
                modeTtlSeconds=_resolve_mode_ttl(snapshot, telegram_news, external_news),
                modeKeywords=_resolve_mode_keywords(telegram_news, external_news),
                regimeTag=regime_tag,
                riskState=risk_state,
                geoHeadline=macro_intel.geo_headline,
                dxyBias=macro_intel.dxy_bias,
                yieldsBias=macro_intel.yields_bias,
                crossMetalsBias=macro_intel.cross_metals_bias,
                cbFlow=macro_intel.cb_flow,
                instPositioning=macro_intel.inst_positioning,
                eventRisk=macro_intel.event_risk,
                promptRefs=prompt_refs,
                providerModels=provider_models,
                aiTraceJson=_safe_json_dumps({
                    "ai_request": _build_request_with_prompt_dispatch(),
                    "provider_traces": _compact_provider_traces(),
                    "stage": "committee",
                    "result": "failed",
                    "pre_stage_votes": pre_stage_votes,
                    "committee_votes": committee.provider_votes,
                    "agreement_count": committee.agreement_count,
                    "required_agreement": committee.required_agreement,
                    "reason": committee.disagreement_reason,
                    "events": stage_events,
                }),
                cycleId=snapshot.cycleId,
            )

        signal = committee.signal

        # ── CR9: validation AI stages (validate/study/self_crosscheck/capital_utilization)
        # have been moved OUT of the live path.  Running 4 more full-committee AI
        # calls per cycle was the single largest source of wasted tokens.
        # These stages are preserved for the async STUDY / SELF CROSSCHECK module.
        validation_votes: list[str] = []

        pending_expiry = _parse_pe(snapshot.timestamp, signal.pe)
        max_life = _parse_ml(signal.ml)

        return TradeSignal(
            rail=signal.rail,
            entry=signal.entry,
            tp=signal.tp,
            pe=pending_expiry,
            ml=max_life,
            confidence=signal.confidence,
            safetyTag=_resolve_safety_tag(snapshot, signal.confidence, volatility_expansion, telegram_news, external_news),
            directionBias=_resolve_direction_bias(signal.rail, telegram_news, external_news),
            alignmentScore=_resolve_alignment_score(snapshot, signal.confidence, volatility_expansion, telegram_news, external_news),
            newsImpactTag=_resolve_news_impact_tag(snapshot, telegram_news, external_news),
            tvConfirmationTag=_resolve_tv_confirmation(snapshot),
            newsTags=_resolve_news_tags(snapshot, volatility_expansion, telegram_news, external_news),
            summary=_build_decision_table(snapshot, signal.rail, regime_tag, risk_state, macro_intel),
            consensusPassed=committee.consensus_passed,
            agreementCount=committee.agreement_count,
            requiredAgreement=committee.required_agreement,
            disagreementReason=committee.disagreement_reason,
            providerVotes=list(dict.fromkeys((pre_stage_votes or []) + (committee.provider_votes or []) + validation_votes)),
            modeHint=_resolve_mode_hint(snapshot, telegram_news, external_news),
            modeConfidence=_resolve_mode_confidence(snapshot, telegram_news, external_news),
            modeTtlSeconds=_resolve_mode_ttl(snapshot, telegram_news, external_news),
            modeKeywords=_resolve_mode_keywords(telegram_news, external_news),
            regimeTag=regime_tag,
            riskState=risk_state,
            geoHeadline=macro_intel.geo_headline,
            dxyBias=macro_intel.dxy_bias,
            yieldsBias=macro_intel.yields_bias,
            crossMetalsBias=macro_intel.cross_metals_bias,
            cbFlow=macro_intel.cb_flow,
            instPositioning=macro_intel.inst_positioning,
            eventRisk=macro_intel.event_risk,
            promptRefs=prompt_refs,
            providerModels=provider_models,
            aiTraceJson=_safe_json_dumps({
                "ai_request": _build_request_with_prompt_dispatch(),
                "provider_traces": _compact_provider_traces(),
                "stage": "final",
                "result": "trade_signal",
                "committee_votes": committee.provider_votes,
                "agreement_count": committee.agreement_count,
                "required_agreement": committee.required_agreement,
                "telegram_summary": telegram_news.summary,
                "macro_summary": macro_intel.summary,
                "events": stage_events + [{
                    "eventType": "AI_FINAL_SIGNAL_READY",
                    "stage": "final",
                    "source": "aiworker",
                    "payload": {
                        "rail": signal.rail,
                        "entry": signal.entry,
                        "tp": signal.tp,
                        "confidence": signal.confidence,
                    },
                }],
            }),
            cycleId=snapshot.cycleId,
        )


def _parse_pe(base_time: datetime, pe_value: str) -> datetime:
    try:
        hours_str, minutes_str = pe_value.split(":", 1)
        return base_time + timedelta(hours=int(hours_str), minutes=int(minutes_str))
    except Exception:
        return datetime.now(timezone.utc) + timedelta(minutes=30)


def _parse_ml(ml_value: str) -> int:
    try:
        hours_str, minutes_str = ml_value.split(":", 1)
        return (int(hours_str) * 3600) + (int(minutes_str) * 60)
    except Exception:
        return 3600


def _resolve_news_impact_tag(
    snapshot: MarketSnapshot,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
) -> str:
    snapshot_tag = (snapshot.telegramImpactTag or "").upper()
    rank = {"LOW": 1, "MODERATE": 2, "HIGH": 3}
    candidates = [
        snapshot_tag if snapshot_tag in rank else "LOW",
        telegram_news.impact_tag if telegram_news.impact_tag in rank else "LOW",
        external_news.impact_tag if external_news.impact_tag in rank else "LOW",
    ]
    return max(candidates, key=lambda value: rank.get(value, 1))


def _resolve_tv_confirmation(snapshot: MarketSnapshot) -> str:
    tag = (snapshot.tradingViewConfirmation or "").upper()
    if tag in {"CONFIRM", "NEUTRAL", "CONTRADICT"}:
        return tag
    return "NEUTRAL"


def _resolve_safety_tag(
    snapshot: MarketSnapshot,
    confidence: float,
    volatility_expansion: float,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
) -> str:
    impact = _resolve_news_impact_tag(snapshot, telegram_news, external_news)
    if impact == "HIGH":
        return "BLOCK"
    if snapshot.isFriday and snapshot.isLondonNyOverlap:
        return "BLOCK"
    if snapshot.hasPanicDropSequence:
        return "BLOCK"
    if volatility_expansion >= 1.20 and snapshot.hasImpulseCandles:
        return "BLOCK"
    if impact == "MODERATE":
        return "CAUTION"
    if confidence < 0.55 or volatility_expansion >= 0.90:
        return "CAUTION"
    return "SAFE"


def _resolve_direction_bias(rail: str, telegram_news: TelegramNewsContext, external_news: ExternalNewsContext) -> str:
    combined_score = telegram_news.dominance + external_news.dominance
    if combined_score >= 0.25:
        return "BULLISH"
    if combined_score <= -0.25:
        return "BEARISH"
    if telegram_news.direction_bias in {"BULLISH", "BEARISH"}:
        return telegram_news.direction_bias
    if external_news.direction_bias in {"BULLISH", "BEARISH"}:
        return external_news.direction_bias
    return "BULLISH" if rail in {"BUY_LIMIT", "BUY_STOP"} else "NEUTRAL"


def _resolve_alignment_score(
    snapshot: MarketSnapshot,
    confidence: float,
    volatility_expansion: float,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
) -> float:
    score = confidence
    if snapshot.session.upper() in {"LONDON", "NEW_YORK", "EUROPE", "INDIA", "JAPAN", "ASIA"}:
        score += 0.04
    if volatility_expansion > 1.0:
        score -= 0.08
    if snapshot.isFriday and snapshot.isLondonNyOverlap:
        score -= 0.25

    impact = _resolve_news_impact_tag(snapshot, telegram_news, external_news)
    if impact == "MODERATE":
        score -= 0.10
    if impact == "HIGH":
        score -= 0.35

    if telegram_news.direction_bias == "BULLISH":
        score += 0.03
    if telegram_news.direction_bias == "BEARISH":
        score -= 0.12

    if external_news.direction_bias == "BULLISH":
        score += 0.03
    if external_news.direction_bias == "BEARISH":
        score -= 0.12

    if external_news.panic_suspected:
        score -= 0.2

    if _resolve_tv_confirmation(snapshot) == "CONFIRM":
        score += 0.06
    elif _resolve_tv_confirmation(snapshot) == "CONTRADICT":
        score -= 0.15

    return max(0.0, min(1.0, score))


def _resolve_news_tags(
    snapshot: MarketSnapshot,
    volatility_expansion: float,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
) -> list[str]:
    tags: list[str] = []
    if snapshot.isUsRiskWindow:
        tags.append("us_macro_window")
    if snapshot.isFriday:
        tags.append("friday_risk")
    if volatility_expansion >= 1.2:
        tags.append("volatility_expansion_spike")
    tags.append(f"news_impact_{_resolve_news_impact_tag(snapshot, telegram_news, external_news).lower()}")
    tags.append(f"tv_confirmation_{_resolve_tv_confirmation(snapshot).lower()}")
    tags.extend(telegram_news.tags)
    tags.extend(external_news.tags)
    if not tags:
        tags.append("no_high_impact_news")
    return list(dict.fromkeys(tags))


def _build_summary(
    snapshot: MarketSnapshot,
    volatility_expansion: float,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
) -> str:
    return (
        f"Session={snapshot.session}, volExp={volatility_expansion:.2f}, "
        f"usRisk={snapshot.isUsRiskWindow}, friday={snapshot.isFriday}, overlap={snapshot.isLondonNyOverlap}, "
        f"telegram={telegram_news.summary}, externalNews={external_news.summary}"
    )


def _build_pretable_summary(snapshot: MarketSnapshot, regime_tag: str, risk_state: str, macro_intel: MacroIntelContext) -> str:
    return (
        f"PRETABLE_BLOCK | session={snapshot.session}/{snapshot.sessionPhase} | "
        f"regime={regime_tag} | risk={risk_state} | geo={macro_intel.geo_headline} | event={macro_intel.event_risk}"
    )


def _build_decision_table(
    snapshot: MarketSnapshot,
    rail: str,
    regime_tag: str,
    risk_state: str,
    macro_intel: MacroIntelContext,
) -> str:
    rows = [
        "| Field | Value |",
        "|---|---|",
        f"| Session | {snapshot.session}/{snapshot.sessionPhase} |",
        f"| Rail | {rail} |",
        f"| RegimeTag | {regime_tag} |",
        f"| RiskState | {risk_state} |",
        f"| GeoHeadline | {macro_intel.geo_headline} |",
        f"| DXY | {macro_intel.dxy_bias} |",
        f"| Yields | {macro_intel.yields_bias} |",
        f"| XAG/XPT | {macro_intel.cross_metals_bias} |",
        f"| CB Flow | {macro_intel.cb_flow} |",
        f"| Institutional | {macro_intel.inst_positioning} |",
        f"| Event Risk | {macro_intel.event_risk} |",
    ]
    return "\n".join(rows)


def _classify_indicators_regime(snapshot: MarketSnapshot, macro_intel: MacroIntelContext) -> str:
    if snapshot.hasPanicDropSequence or snapshot.panicSuspected:
        return "PANIC"

    if snapshot.isExpansion and snapshot.hasImpulseCandles and macro_intel.event_risk in {"MEDIUM", "HIGH"}:
        return "VOLATILE_EXPANSION"

    if snapshot.isCompression and snapshot.hasOverlapCandles:
        return "COMPRESSION"

    return "BALANCED"


def _classify_risk_state(
    snapshot: MarketSnapshot,
    macro_intel: MacroIntelContext,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
) -> str:
    if macro_intel.event_risk == "HIGH":
        return "BLOCK"
    if snapshot.hasPanicDropSequence or snapshot.panicSuspected:
        return "BLOCK"
    if telegram_news.impact_tag == "HIGH" or external_news.impact_tag == "HIGH":
        return "BLOCK"

    caution_count = 0
    if macro_intel.event_risk == "MEDIUM":
        caution_count += 1
    if snapshot.isExpansion:
        caution_count += 1
    if snapshot.spreadMedian60m > 0 and snapshot.spread >= snapshot.spreadMedian60m * 1.8:
        caution_count += 1

    return "CAUTION" if caution_count > 0 else "SAFE"


def _resolve_regime_tag(
    snapshot: MarketSnapshot,
    macro_intel: MacroIntelContext,
    indicators_regime: str,
    risk_state: str,
) -> str:
    if risk_state == "BLOCK":
        return "DEESCALATION_RISK"

    if (
        macro_intel.geo_headline not in {"NONE", ""}
        or macro_intel.event_risk in {"MEDIUM", "HIGH"}
        or indicators_regime == "VOLATILE_EXPANSION"
    ):
        return "WAR_PREMIUM"

    if snapshot.session.upper() in {"LONDON", "NY", "INDIA", "JAPAN"}:
        return "STANDARD"

    return "STANDARD"


def _resolve_mode_hint(
    snapshot: MarketSnapshot,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
) -> str:
    if snapshot.hasPanicDropSequence or telegram_news.panic_suspected or external_news.panic_suspected:
        return "DEESCALATION_RISK"

    state = (telegram_news.telegram_state or "").upper()
    impact = _resolve_news_impact_tag(snapshot, telegram_news, external_news)

    if state in {"SELL", "STRONG_SELL"} and impact in {"MODERATE", "HIGH"}:
        return "DEESCALATION_RISK"

    if snapshot.isExpansion and snapshot.hasImpulseCandles and state in {"BUY", "STRONG_BUY"}:
        return "WAR_PREMIUM"

    if state in {"BUY", "STRONG_BUY"} and impact in {"MODERATE", "HIGH"}:
        return "WAR_PREMIUM"

    if external_news.news_state == "RISK_OFF" and impact in {"MODERATE", "HIGH"}:
        return "DEESCALATION_RISK"

    if external_news.news_state == "RISK_ON" and impact in {"MODERATE", "HIGH"}:
        return "WAR_PREMIUM"

    return "UNKNOWN"


def _resolve_mode_confidence(
    snapshot: MarketSnapshot,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
) -> float:
    mode = _resolve_mode_hint(snapshot, telegram_news, external_news)
    base = 0.55
    if mode == "UNKNOWN":
        return 0.45

    impact = _resolve_news_impact_tag(snapshot, telegram_news, external_news)

    if impact == "HIGH":
        base += 0.20
    elif impact == "MODERATE":
        base += 0.10

    if telegram_news.panic_suspected:
        base += 0.15

    if external_news.panic_suspected:
        base += 0.12

    if snapshot.isExpansion and snapshot.hasImpulseCandles:
        base += 0.08

    return max(0.0, min(1.0, base))


def _resolve_mode_ttl(
    snapshot: MarketSnapshot,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
) -> int:
    mode = _resolve_mode_hint(snapshot, telegram_news, external_news)
    if mode == "DEESCALATION_RISK":
        return 1800
    if mode == "WAR_PREMIUM":
        return 1200
    return 900


def _resolve_mode_keywords(telegram_news: TelegramNewsContext, external_news: ExternalNewsContext) -> list[str]:
    keywords: list[str] = []
    for item in telegram_news.items[-5:]:
        text = (item.get("text") or "").lower()
        for token in (
            "ceasefire",
            "talks",
            "mediation",
            "contained",
            "retaliation",
            "escalation",
            "missile",
            "drone",
            "hormuz",
            "shipping",
            "oil shock",
        ):
            if token in text and token not in keywords:
                keywords.append(token)
    for item in external_news.items[-5:]:
        text = f"{item.get('title', '')} {item.get('summary', '')}".lower()
        for token in (
            "ceasefire",
            "talks",
            "retaliation",
            "escalation",
            "missile",
            "drone",
            "hormuz",
            "shipping",
            "fomc",
            "nfp",
            "cpi",
            "safe haven",
        ):
            if token in text and token not in keywords:
                keywords.append(token)
    return keywords


def _is_quiet_replay_cycle(snapshot: MarketSnapshot) -> bool:
    cycle_id = (snapshot.cycleId or "").strip().lower()
    telegram_state = (snapshot.telegramState or "").strip().upper()
    return cycle_id.startswith("replay_") and telegram_state == "QUIET"


def _build_fallback_signal(
    snapshot: MarketSnapshot,
    volatility_expansion: float,
    telegram_news: TelegramNewsContext,
    external_news: ExternalNewsContext,
    prompt_refs: list[str],
    provider_models: list[str],
    stage_events: list[dict[str, object]] | None = None,
    provider_traces: list[dict[str, object]] | None = None,
) -> TradeSignal:
    primary_tf = snapshot.timeframeData[0]
    current_price = float(primary_tf.close)
    atr = max(float(snapshot.atr), 4.0)

    expansion_bias = (
        snapshot.isExpansion
        or snapshot.hasImpulseCandles
        or snapshot.isBreakoutConfirmed
        or snapshot.tvAlertType.upper() in {"LID_BREAK", "BREAKOUT", "SESSION_BREAK"}
    )

    rail = "BUY_STOP" if expansion_bias and not snapshot.isCompression else "BUY_LIMIT"
    entry = current_price + (atr * 0.22) if rail == "BUY_STOP" else current_price - (atr * 0.28)
    tp_distance = max(7.0, min(18.0, atr * 0.95))
    tp = entry + tp_distance

    confidence = 0.66
    if snapshot.session.upper() in {"LONDON", "NEW_YORK", "EUROPE", "INDIA", "JAPAN", "ASIA"}:
        confidence += 0.05
    if snapshot.tradingViewConfirmation.upper() == "CONFIRM":
        confidence += 0.04
    if snapshot.telegramState.upper() in {"BUY", "STRONG_BUY"}:
        confidence += 0.05
    if snapshot.telegramState.upper() in {"SELL", "STRONG_SELL"}:
        confidence -= 0.08
    if snapshot.panicSuspected or snapshot.hasPanicDropSequence:
        confidence -= 0.15
    if volatility_expansion >= 1.10:
        confidence -= 0.08

    confidence = max(0.52, min(0.92, confidence))
    alignment = _resolve_alignment_score(snapshot, confidence, volatility_expansion, telegram_news, external_news)
    direction_bias = _resolve_direction_bias(rail, telegram_news, external_news)

    return TradeSignal(
        rail=rail,
        entry=round(entry, 2),
        tp=round(tp, 2),
        pe=snapshot.timestamp + timedelta(minutes=25),
        ml=3600,
        confidence=confidence,
        safetyTag=_resolve_safety_tag(snapshot, confidence, volatility_expansion, telegram_news, external_news),
        directionBias=direction_bias,
        alignmentScore=alignment,
        newsImpactTag=_resolve_news_impact_tag(snapshot, telegram_news, external_news),
        tvConfirmationTag=_resolve_tv_confirmation(snapshot),
        newsTags=_resolve_news_tags(snapshot, volatility_expansion, telegram_news, external_news),
        summary=(
            "Fallback simulation analyzer used (no external analyzer keys configured). "
            f"session={snapshot.session}, rail={rail}, volExp={volatility_expansion:.2f}"
        ),
        consensusPassed=True,
        agreementCount=1,
        requiredAgreement=1,
        disagreementReason=None,
        providerVotes=[f"fallback-sim:{rail}@{entry:.2f}|c={confidence:.2f}"],
        modeHint=_resolve_mode_hint(snapshot, telegram_news, external_news),
        modeConfidence=_resolve_mode_confidence(snapshot, telegram_news, external_news),
        modeTtlSeconds=_resolve_mode_ttl(snapshot, telegram_news, external_news),
        modeKeywords=_resolve_mode_keywords(telegram_news, external_news),
        regimeTag="STANDARD",
        riskState="CAUTION",
        promptRefs=prompt_refs,
        providerModels=provider_models,
        aiTraceJson=_safe_json_dumps({
            "ai_request": {
                "cycle_id": snapshot.cycleId,
                "symbol": snapshot.symbol,
                "prompt_refs": prompt_refs,
                "provider_models": provider_models,
            },
            "provider_traces": provider_traces or [],
            "stage": "fallback",
            "result": "trade_signal",
            "reason": "no_live_analyzers",
            "telegram_summary": telegram_news.summary,
            "external_news_summary": external_news.summary,
            "events": stage_events or [],
        }),
        cycleId=snapshot.cycleId,
    )
