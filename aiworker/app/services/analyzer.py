import logging
from datetime import datetime, timedelta, timezone

from app.ai.config import (
    AI_ANALYZERS,
    AI_STRATEGY,
    CONSENSUS_ENTRY_TOLERANCE_PCT,
    CONSENSUS_MIN_AGREEMENT,
)
from app.ai.provider_manager import AIProviderManager
from app.models.contracts import MarketSnapshot, TradeSignal
from app.services.telegram_news import TelegramNewsContext, TelegramNewsService

logger = logging.getLogger(__name__)


class AnalyzerService:
    def __init__(self) -> None:
        if not AI_ANALYZERS:
            raise RuntimeError(
                "No AI analyzers configured. Set OPENAI_API_KEY and OPENAI_MODELS in aiworker/.env"
            )
        self._manager = AIProviderManager(AI_ANALYZERS)
        self._telegram_news = TelegramNewsService()

    async def analyze(self, snapshot: MarketSnapshot) -> TradeSignal:
        if snapshot.symbol.upper() != "XAUUSD":
            raise ValueError("Only XAUUSD is supported")

        primary_tf = snapshot.timeframeData[0]
        volatility_expansion = snapshot.volatilityExpansion
        if volatility_expansion is None:
            volatility_expansion = (snapshot.atr / snapshot.adr) if snapshot.adr > 0 else 0.0

        market_context = {
            "symbol": snapshot.symbol,
            "current_price": primary_tf.close,
            "open": primary_tf.open,
            "high": primary_tf.high,
            "low": primary_tf.low,
            "close": primary_tf.close,
            "timeframes": [item.model_dump() for item in snapshot.timeframeData],
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
            "session_high": snapshot.sessionHigh,
            "session_low": snapshot.sessionLow,
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
        }

        telegram_news = await self._telegram_news.collect_news_context(snapshot.symbol)
        market_context["telegram_news"] = {
            "impact_tag": telegram_news.impact_tag,
            "risk_tag": telegram_news.risk_tag,
            "direction_bias": telegram_news.direction_bias,
            "tags": telegram_news.tags,
            "summary": telegram_news.summary,
            "headlines": telegram_news.headlines,
            "items": telegram_news.items,
        }

        if AI_STRATEGY == "single":
            signal = await self._manager.analyze_with_committee(
                market_context,
                min_agreement=1,
                entry_tolerance_pct=CONSENSUS_ENTRY_TOLERANCE_PCT,
            )
        else:
            signal = await self._manager.analyze_with_committee(
                market_context,
                min_agreement=CONSENSUS_MIN_AGREEMENT,
                entry_tolerance_pct=CONSENSUS_ENTRY_TOLERANCE_PCT,
            )

        if not signal:
            raise ValueError("No consensus signal generated")

        pending_expiry = _parse_pe(snapshot.timestamp, signal.pe)
        max_life = _parse_ml(signal.ml)

        return TradeSignal(
            rail=signal.rail,
            entry=signal.entry,
            tp=signal.tp,
            pe=pending_expiry,
            ml=max_life,
            confidence=signal.confidence,
            safetyTag=_resolve_safety_tag(snapshot, signal.confidence, volatility_expansion, telegram_news),
            directionBias=_resolve_direction_bias(signal, telegram_news),
            alignmentScore=_resolve_alignment_score(snapshot, signal.confidence, volatility_expansion, telegram_news),
            newsImpactTag=_resolve_news_impact_tag(snapshot, telegram_news),
            tvConfirmationTag=_resolve_tv_confirmation(snapshot),
            newsTags=_resolve_news_tags(snapshot, volatility_expansion, telegram_news),
            summary=_build_summary(snapshot, volatility_expansion, telegram_news),
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


def _resolve_news_impact_tag(snapshot: MarketSnapshot, telegram_news: TelegramNewsContext) -> str:
    snapshot_tag = (snapshot.telegramImpactTag or "").upper()
    if snapshot_tag in {"HIGH", "MODERATE", "LOW"}:
        return snapshot_tag
    if telegram_news.impact_tag in {"HIGH", "MODERATE", "LOW"}:
        return telegram_news.impact_tag
    return "LOW"


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
) -> str:
    impact = _resolve_news_impact_tag(snapshot, telegram_news)
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


def _resolve_direction_bias(signal, telegram_news: TelegramNewsContext) -> str:
    if telegram_news.direction_bias in {"BULLISH", "BEARISH"}:
        return telegram_news.direction_bias
    return "BULLISH" if signal.rail in {"BUY_LIMIT", "BUY_STOP"} else "NEUTRAL"


def _resolve_alignment_score(
    snapshot: MarketSnapshot,
    confidence: float,
    volatility_expansion: float,
    telegram_news: TelegramNewsContext,
) -> float:
    score = confidence
    if snapshot.session.upper() in {"LONDON", "NEW_YORK", "EUROPE", "INDIA", "JAPAN", "ASIA"}:
        score += 0.04
    if volatility_expansion > 1.0:
        score -= 0.08
    if snapshot.isFriday and snapshot.isLondonNyOverlap:
        score -= 0.25

    impact = _resolve_news_impact_tag(snapshot, telegram_news)
    if impact == "MODERATE":
        score -= 0.10
    if impact == "HIGH":
        score -= 0.35

    if telegram_news.direction_bias == "BULLISH":
        score += 0.03
    if telegram_news.direction_bias == "BEARISH":
        score -= 0.12

    if _resolve_tv_confirmation(snapshot) == "CONFIRM":
        score += 0.06
    elif _resolve_tv_confirmation(snapshot) == "CONTRADICT":
        score -= 0.15

    return max(0.0, min(1.0, score))


def _resolve_news_tags(
    snapshot: MarketSnapshot,
    volatility_expansion: float,
    telegram_news: TelegramNewsContext,
) -> list[str]:
    tags: list[str] = []
    if snapshot.isUsRiskWindow:
        tags.append("us_macro_window")
    if snapshot.isFriday:
        tags.append("friday_risk")
    if volatility_expansion >= 1.2:
        tags.append("volatility_expansion_spike")
    tags.append(f"news_impact_{_resolve_news_impact_tag(snapshot, telegram_news).lower()}")
    tags.append(f"tv_confirmation_{_resolve_tv_confirmation(snapshot).lower()}")
    tags.extend(telegram_news.tags)
    if not tags:
        tags.append("no_high_impact_news")
    return list(dict.fromkeys(tags))


def _build_summary(snapshot: MarketSnapshot, volatility_expansion: float, telegram_news: TelegramNewsContext) -> str:
    return (
        f"Session={snapshot.session}, volExp={volatility_expansion:.2f}, "
        f"usRisk={snapshot.isUsRiskWindow}, friday={snapshot.isFriday}, overlap={snapshot.isLondonNyOverlap}, "
        f"telegram={telegram_news.summary}"
    )
