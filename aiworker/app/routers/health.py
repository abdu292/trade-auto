from fastapi import APIRouter
from app.ai.config import TELEGRAM_BOT_TOKEN, TELEGRAM_CHANNELS


router = APIRouter(tags=["Health"])


@router.get("/health")
async def health() -> dict[str, object]:
    return {
        "status": "ok",
        "telegram": {
            "enabled": bool(TELEGRAM_BOT_TOKEN),
            "channels": TELEGRAM_CHANNELS,
            "channelCount": len(TELEGRAM_CHANNELS),
        },
    }
