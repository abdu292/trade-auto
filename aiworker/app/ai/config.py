import os
from typing import List
from dotenv import load_dotenv
from app.ai.providers.base_provider import AIProviderConfig

load_dotenv()


def _read_openai_models() -> List[str]:
    raw = os.getenv("OPENAI_MODELS", "gpt-4.1-mini")
    return [item.strip() for item in raw.split(",") if item.strip()]


def _read_csv_env(name: str, default: str = "") -> List[str]:
    raw = os.getenv(name, default)
    items = [item.strip() for item in raw.split(",") if item.strip()]
    deduped: List[str] = []
    for item in items:
        if item not in deduped:
            deduped.append(item)
    return deduped


OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
GROK_API_KEY = os.getenv("GROK_API_KEY")
PERPLEXITY_API_KEY = os.getenv("PERPLEXITY_API_KEY")
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")

# committee|single
AI_STRATEGY = os.getenv("AI_STRATEGY", "committee").lower()
CONSENSUS_MIN_AGREEMENT = int(os.getenv("CONSENSUS_MIN_AGREEMENT", "1"))
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

    if OPENAI_API_KEY:
        for model in _read_openai_models():
            analyzers.append(
                AIProviderConfig(
                    name=f"openai:{model}",
                    provider="openai",
                    api_key=OPENAI_API_KEY,
                    model=model,
                    temperature=0.2,
                    max_tokens=450,
                    timeout=20,
                )
            )

    if GROK_API_KEY:
        analyzers.append(
            AIProviderConfig(
                name="grok:grok-2-latest",
                provider="grok",
                api_key=GROK_API_KEY,
                model="grok-2-latest",
                temperature=0.2,
                max_tokens=450,
                timeout=20,
            )
        )

    if PERPLEXITY_API_KEY:
        analyzers.append(
            AIProviderConfig(
                name="perplexity:sonar-pro",
                provider="perplexity",
                api_key=PERPLEXITY_API_KEY,
                model="sonar-pro",
                temperature=0.2,
                max_tokens=450,
                timeout=20,
            )
        )

    if GEMINI_API_KEY:
        analyzers.append(
            AIProviderConfig(
                name="gemini:gemini-2.0-flash",
                provider="gemini",
                api_key=GEMINI_API_KEY,
                model="gemini-2.0-flash",
                temperature=0.2,
                max_tokens=450,
                timeout=20,
            )
        )

    return analyzers


AI_ANALYZERS = build_analyzers()
