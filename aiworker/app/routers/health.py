from fastapi import APIRouter
from app.ai.config import (
    AI_ANALYZERS,
    AI_PROVIDER_MODE,
    AI_STRATEGY,
    GROK_RUNTIME_TRANSPORT,
    TELEGRAM_READ_MODE,
    TELEGRAM_API_ID,
    TELEGRAM_API_HASH,
    TELEGRAM_BOT_TOKEN,
    TELEGRAM_LISTEN_CHANNELS,
)


router = APIRouter(tags=["Health"])


@router.get("/health")
async def health() -> dict[str, object]:
    analyzers = [analyzer.name for analyzer in AI_ANALYZERS]
    parity_blockers: list[str] = []
    if len(analyzers) != 1:
        parity_blockers.append("live engine requires exactly one analyzer")
    if not analyzers or not ("grok" in analyzers[0].lower()):
        parity_blockers.append("live engine requires Grok as the sole analyzer")
    if AI_STRATEGY != "single":
        parity_blockers.append("live engine requires AI_STRATEGY=single")

    return {
        "status": "ok",
        "ai": {
            "providerMode": AI_PROVIDER_MODE,
            "transport": GROK_RUNTIME_TRANSPORT,
            "strategy": AI_STRATEGY,
            "liveDecisionEngine": "grok",
            "parityBlockers": parity_blockers,
            "analyzerCount": len(AI_ANALYZERS),
            "analyzers": analyzers,
        },
        "telegram": {
            "enabled": bool(TELEGRAM_BOT_TOKEN) or (TELEGRAM_API_ID > 0 and bool(TELEGRAM_API_HASH)),
            "readMode": TELEGRAM_READ_MODE,
            "clientConfigured": TELEGRAM_API_ID > 0 and bool(TELEGRAM_API_HASH),
            "channels": TELEGRAM_LISTEN_CHANNELS,
            "channelCount": len(TELEGRAM_LISTEN_CHANNELS),
        },
    }
