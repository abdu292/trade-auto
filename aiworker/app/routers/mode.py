import json
from datetime import datetime, timezone
from pydantic import BaseModel, Field
from fastapi import APIRouter
from openai import AsyncOpenAI

from app.ai.config import (
    OPENROUTER_API_KEY,
    GROK_OPENROUTER_MODEL,
    GROK_RUNTIME_TRANSPORT,
    GROK_API_KEY,
    GROK_MODEL,
)
from app.services.telegram_news import TelegramNewsService

router = APIRouter(tags=["Mode"])


class ModeRequest(BaseModel):
    symbol: str = Field(default="XAUUSD")
    session: str | None = None
    timestamp: datetime | None = None
    telegramState: str | None = None
    telegramImpactTag: str | None = None
    isExpansion: bool = False
    hasImpulseCandles: bool = False
    hasPanicDropSequence: bool = False
    tvAlertType: str | None = None


class ModeResponse(BaseModel):
    mode: str
    confidence: float
    keywords: list[str]
    ttlSeconds: int
    capturedAtUtc: datetime


@router.post("/mode", response_model=ModeResponse)
async def resolve_mode(request: ModeRequest) -> ModeResponse:
    telegram = TelegramNewsService()
    context = await telegram.collect_news_context(request.symbol)

    keywords = _extract_mode_keywords(context.headlines)
    grok_mode = await _try_grok_mode(request, context, keywords)
    if grok_mode is not None:
        return grok_mode

    if request.hasPanicDropSequence or context.panic_suspected:
        return ModeResponse(
            mode="DEESCALATION_RISK",
            confidence=0.95,
            keywords=_merge_keywords(keywords, ["panic", "waterfall"]),
            ttlSeconds=1800,
            capturedAtUtc=datetime.now(timezone.utc),
        )

    state = (request.telegramState or context.telegram_state or "QUIET").upper()
    impact = (request.telegramImpactTag or context.impact_tag or "LOW").upper()
    tv = (request.tvAlertType or "NONE").upper()

    deesc_tokens = {"ceasefire", "talks", "mediation", "contained", "deal", "truce", "backchannel"}
    esc_tokens = {"strikes", "missiles", "drones", "hormuz", "shipping", "oil", "retaliation", "escalation"}

    has_deesc = any(token in keywords for token in deesc_tokens)
    has_esc = any(token in keywords for token in esc_tokens)

    if has_deesc or (state in {"SELL", "STRONG_SELL"} and impact in {"MODERATE", "HIGH"}):
        return ModeResponse(
            mode="DEESCALATION_RISK",
            confidence=0.82 if has_deesc else 0.70,
            keywords=keywords,
            ttlSeconds=1800,
            capturedAtUtc=datetime.now(timezone.utc),
        )

    if has_esc or ((request.isExpansion and request.hasImpulseCandles) and state in {"BUY", "STRONG_BUY"}):
        return ModeResponse(
            mode="WAR_PREMIUM",
            confidence=0.80 if has_esc else 0.68,
            keywords=keywords,
            ttlSeconds=1200,
            capturedAtUtc=datetime.now(timezone.utc),
        )

    if tv in {"LID_BREAK", "BREAKOUT", "SESSION_BREAK"} and state in {"BUY", "STRONG_BUY"}:
        return ModeResponse(
            mode="WAR_PREMIUM",
            confidence=0.62,
            keywords=keywords,
            ttlSeconds=900,
            capturedAtUtc=datetime.now(timezone.utc),
        )

    return ModeResponse(
        mode="UNKNOWN",
        confidence=0.45,
        keywords=keywords,
        ttlSeconds=900,
        capturedAtUtc=datetime.now(timezone.utc),
    )


def _extract_mode_keywords(headlines: list[str]) -> list[str]:
    tokens = (
        "ceasefire",
        "talks",
        "mediation",
        "contained",
        "deal",
        "truce",
        "backchannel",
        "strikes",
        "missiles",
        "drones",
        "hormuz",
        "shipping",
        "oil",
        "retaliation",
        "escalation",
    )
    lowered = " ".join(headlines).lower()
    found: list[str] = []
    for token in tokens:
        if token in lowered and token not in found:
            found.append(token)
    return found


def _merge_keywords(existing: list[str], extra: list[str]) -> list[str]:
    merged = list(existing)
    for token in extra:
        if token not in merged:
            merged.append(token)
    return merged


async def _try_grok_mode(
    request: ModeRequest,
    context,
    keywords: list[str],
) -> ModeResponse | None:
    try:
        client = _build_grok_client()
        if client is None:
            return None

        prompt = {
            "symbol": request.symbol,
            "session": request.session,
            "timestamp": (request.timestamp or datetime.now(timezone.utc)).isoformat(),
            "telegram_state": request.telegramState or context.telegram_state,
            "telegram_impact": request.telegramImpactTag or context.impact_tag,
            "is_expansion": request.isExpansion,
            "has_impulse_candles": request.hasImpulseCandles,
            "has_panic_drop_sequence": request.hasPanicDropSequence,
            "tv_alert_type": request.tvAlertType,
            "headlines": context.headlines[-20:],
            "keywords": keywords,
        }

        response = await client.chat.completions.create(
            model=_resolve_grok_model(),
            temperature=0.0,
            max_tokens=220,
            timeout=8,
            messages=[
                {
                    "role": "system",
                    "content": (
                        "You are a mode classifier for XAUUSD war premium strategy. "
                        "Return only JSON with keys: mode, confidence, keywords, ttl_seconds. "
                        "mode must be one of WAR_PREMIUM, DEESCALATION_RISK, UNKNOWN."
                    ),
                },
                {"role": "user", "content": json.dumps(prompt)},
            ],
        )

        content = (response.choices[0].message.content or "").strip()
        start = content.find("{")
        end = content.rfind("}")
        if start < 0 or end <= start:
            return None

        parsed = json.loads(content[start : end + 1])
        mode = (parsed.get("mode") or "UNKNOWN").strip().upper()
        if mode not in {"WAR_PREMIUM", "DEESCALATION_RISK", "UNKNOWN"}:
            mode = "UNKNOWN"

        confidence = float(parsed.get("confidence") or 0.5)
        confidence = max(0.0, min(1.0, confidence))
        ttl = int(parsed.get("ttl_seconds") or 900)
        ttl = max(300, min(3600, ttl))
        model_keywords = parsed.get("keywords") or []
        if not isinstance(model_keywords, list):
            model_keywords = []
        merged = _merge_keywords(keywords, [str(item).strip().lower() for item in model_keywords if str(item).strip()])

        return ModeResponse(
            mode=mode,
            confidence=confidence,
            keywords=merged,
            ttlSeconds=ttl,
            capturedAtUtc=datetime.now(timezone.utc),
        )
    except Exception:
        return None


def _build_grok_client() -> AsyncOpenAI | None:
    transport = (GROK_RUNTIME_TRANSPORT or "openrouter").strip().lower()
    if transport == "openrouter" and OPENROUTER_API_KEY:
        return AsyncOpenAI(api_key=OPENROUTER_API_KEY, base_url="https://openrouter.ai/api/v1")
    if transport == "direct" and GROK_API_KEY:
        return AsyncOpenAI(api_key=GROK_API_KEY, base_url="https://api.x.ai/openai/")
    return None


def _resolve_grok_model() -> str:
    transport = (GROK_RUNTIME_TRANSPORT or "openrouter").strip().lower()
    if transport == "direct":
        return GROK_MODEL
    return GROK_OPENROUTER_MODEL