from fastapi import APIRouter
from app.ai.config import (
    AI_ANALYZERS,
    AI_PROVIDER_MODE,
    AI_STRATEGY,
    CONSENSUS_MIN_AGREEMENT,
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
    if len(analyzers) < 2:
        parity_blockers.append("committee live mode requires at least two analyzers")
    if AI_STRATEGY != "committee":
        parity_blockers.append("committee live mode requires AI_STRATEGY=committee")
    if CONSENSUS_MIN_AGREEMENT < 2:
        parity_blockers.append("committee live mode requires CONSENSUS_MIN_AGREEMENT>=2")

    return {
        "status": "ok",
        "ai": {
            "providerMode": AI_PROVIDER_MODE,
            "transport": GROK_RUNTIME_TRANSPORT,
            "strategy": AI_STRATEGY,
            "minAgreement": CONSENSUS_MIN_AGREEMENT,
            "liveDecisionEngine": "committee",
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
