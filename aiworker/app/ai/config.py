import os
from typing import List
from dotenv import load_dotenv
from app.ai.providers.base_provider import AIProviderConfig

load_dotenv()

def _read_csv_env(name: str, default: str = "") -> List[str]:
    raw = os.getenv(name, default)
    items = [item.strip() for item in raw.split(",") if item.strip()]
    deduped: List[str] = []
    for item in items:
        if item not in deduped:
            deduped.append(item)
    return deduped


GROK_API_KEY = os.getenv("GROK_API_KEY")
GROK_MODEL = os.getenv("GROK_MODEL", "grok-2-latest")
OPENROUTER_API_KEY = os.getenv("OPENROUTER_API_KEY")
GROK_OPENROUTER_MODEL = os.getenv("GROK_OPENROUTER_MODEL", "x-ai/grok-4.1-fast")
GROK_RUNTIME_TRANSPORT = os.getenv("GROK_RUNTIME_TRANSPORT", "openrouter").strip().lower()

AI_PROVIDER_MODE = "grok-only"
AI_STRATEGY = "single"
CONSENSUS_MIN_AGREEMENT = 1
CONSENSUS_ENTRY_TOLERANCE_PCT = float(os.getenv("CONSENSUS_ENTRY_TOLERANCE_PCT", "0.003"))

TELEGRAM_BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "")
TELEGRAM_BOT_BASE_URL = os.getenv("TELEGRAM_BOT_BASE_URL", "https://api.telegram.org")
TELEGRAM_CHANNELS = _read_csv_env(
    "TELEGRAM_CHANNELS",
    "@SmartMoneySmart837,@richard1000pips,@moonforexchannel,@Apexgoldtraders",
)
TELEGRAM_LOOKBACK_MINUTES = int(os.getenv("TELEGRAM_LOOKBACK_MINUTES", "180"))
TELEGRAM_MAX_UPDATES = int(os.getenv("TELEGRAM_MAX_UPDATES", "100"))
TELEGRAM_BLOCK_KEYWORDS = _read_csv_env(
    "TELEGRAM_BLOCK_KEYWORDS",
    "cpi,nonfarm,nfp,fomc,rate hike,emergency,war,missile,attack,waterfall,flash crash,halt",
)
TELEGRAM_CAUTION_KEYWORDS = _read_csv_env(
    "TELEGRAM_CAUTION_KEYWORDS",
    "powell,minutes,ppi,jobless,hawkish,volatility,liquidation,selloff,risk-off",
)
TELEGRAM_BULLISH_KEYWORDS = _read_csv_env(
    "TELEGRAM_BULLISH_KEYWORDS",
    "gold bid,safe haven,buy gold,long xau,bullish gold",
)
TELEGRAM_BEARISH_KEYWORDS = _read_csv_env(
    "TELEGRAM_BEARISH_KEYWORDS",
    "gold selloff,strong dollar,long dxy,bearish gold",
)


def build_analyzers() -> List[AIProviderConfig]:
    analyzers: List[AIProviderConfig] = []

    if GROK_RUNTIME_TRANSPORT == "openrouter":
        if not OPENROUTER_API_KEY:
            return analyzers
        if "grok" not in GROK_OPENROUTER_MODEL.lower():
            return analyzers

        analyzers.append(
            AIProviderConfig(
                name=f"grok-via-openrouter:{GROK_OPENROUTER_MODEL}",
                provider="openrouter",
                api_key=OPENROUTER_API_KEY,
                model=GROK_OPENROUTER_MODEL,
                temperature=0.2,
                max_tokens=450,
                timeout=20,
            )
        )
        return analyzers

    if GROK_RUNTIME_TRANSPORT == "direct":
        if not GROK_API_KEY:
            return analyzers

        analyzers.append(
            AIProviderConfig(
                name=f"grok:{GROK_MODEL}",
                provider="grok",
                api_key=GROK_API_KEY,
                model=GROK_MODEL,
                temperature=0.2,
                max_tokens=450,
                timeout=20,
            )
        )
        return analyzers

    return analyzers


AI_ANALYZERS = build_analyzers()
