from fastapi import APIRouter
from app.ai.config import AI_ANALYZERS, AI_PROVIDER_MODE, TELEGRAM_BOT_TOKEN, TELEGRAM_CHANNELS


router = APIRouter(tags=["Health"])


@router.get("/health")
async def health() -> dict[str, object]:
    return {
        "status": "ok",
        "ai": {
            "providerMode": AI_PROVIDER_MODE,
            "analyzerCount": len(AI_ANALYZERS),
            "analyzers": [analyzer.name for analyzer in AI_ANALYZERS],
        },
        "telegram": {
            "enabled": bool(TELEGRAM_BOT_TOKEN),
            "channels": TELEGRAM_CHANNELS,
            "channelCount": len(TELEGRAM_CHANNELS),
        },
    }
