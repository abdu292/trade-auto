from fastapi import APIRouter
from app.ai.config import (
    AI_ANALYZERS,
    AI_PROVIDER_MODE,
    AI_STRATEGY,
    GROK_RUNTIME_TRANSPORT,
    TELEGRAM_BOT_TOKEN,
    TELEGRAM_CHANNELS,
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
            "enabled": bool(TELEGRAM_BOT_TOKEN),
            "channels": TELEGRAM_CHANNELS,
            "channelCount": len(TELEGRAM_CHANNELS),
        },
    }
