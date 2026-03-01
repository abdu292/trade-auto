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


def _read_int_env(name: str, default: int = 0) -> int:
    raw = os.getenv(name, str(default)).strip()
    try:
        return int(raw)
    except ValueError:
        return default


def _read_weight_map_env(name: str, default: str = "") -> dict[str, float]:
    mapping: dict[str, float] = {}
    for item in _read_csv_env(name, default):
        if "=" not in item:
            continue
        key, value = item.split("=", 1)
        channel_key = key.strip().lower()
        if not channel_key:
            continue
        if not channel_key.startswith("@") and not channel_key.startswith("-100"):
            channel_key = f"@{channel_key}"
        try:
            weight = float(value.strip())
        except ValueError:
            continue
        mapping[channel_key] = max(0.3, min(3.0, weight))
    return mapping


GROK_API_KEY = os.getenv("GROK_API_KEY")
GROK_MODEL = os.getenv("GROK_MODEL", "grok-4")
OPENROUTER_API_KEY = os.getenv("OPENROUTER_API_KEY")
GROK_OPENROUTER_MODEL = os.getenv("GROK_OPENROUTER_MODEL", "x-ai/grok-4-fast")
GROK_RUNTIME_TRANSPORT = os.getenv("GROK_RUNTIME_TRANSPORT", "openrouter").strip().lower()
GROK_FORCE_OPENROUTER = os.getenv("GROK_FORCE_OPENROUTER", "true").strip().lower() in {"1", "true", "yes", "on"}
OPENROUTER_MULTI_MODEL_COMMITTEE = os.getenv("OPENROUTER_MULTI_MODEL_COMMITTEE", "true").strip().lower() in {"1", "true", "yes", "on"}
OPENROUTER_MODEL_OPENAI = os.getenv("OPENROUTER_MODEL_OPENAI", "openai/gpt-5").strip()
OPENROUTER_MODEL_GEMINI = os.getenv("OPENROUTER_MODEL_GEMINI", "google/gemini-2.5-pro").strip()
OPENROUTER_MODEL_GROK = os.getenv("OPENROUTER_MODEL_GROK", GROK_OPENROUTER_MODEL).strip()
OPENROUTER_MODEL_PERPLEXITY = os.getenv("OPENROUTER_MODEL_PERPLEXITY", "perplexity/sonar-pro").strip()
if GROK_FORCE_OPENROUTER:
    GROK_RUNTIME_TRANSPORT = "openrouter"
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY", "").strip()
OPENAI_MODEL = os.getenv("OPENAI_MODEL", "gpt-5").strip()
PERPLEXITY_API_KEY = os.getenv("PERPLEXITY_API_KEY", "").strip()
PERPLEXITY_MODEL = os.getenv("PERPLEXITY_MODEL", "sonar-pro").strip()
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY", "").strip()
GEMINI_MODEL = os.getenv("GEMINI_MODEL", "gemini-2.5-pro").strip()

AI_PROVIDER_MODE = os.getenv("AI_PROVIDER_MODE", "committee-live").strip().lower()
AI_STRATEGY = os.getenv("AI_STRATEGY", "committee").strip().lower()
CONSENSUS_MIN_AGREEMENT = _read_int_env("CONSENSUS_MIN_AGREEMENT", 2)
CONSENSUS_ENTRY_TOLERANCE_PCT = float(os.getenv("CONSENSUS_ENTRY_TOLERANCE_PCT", "0.003"))

TELEGRAM_BOT_TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "")
TELEGRAM_BOT_BASE_URL = os.getenv("TELEGRAM_BOT_BASE_URL", "https://api.telegram.org")
TELEGRAM_READ_MODE = os.getenv("TELEGRAM_READ_MODE", "bot").strip().lower()
TELEGRAM_API_ID = _read_int_env("TELEGRAM_API_ID", 0)
TELEGRAM_API_HASH = os.getenv("TELEGRAM_API_HASH", "").strip()
TELEGRAM_SESSION_STRING = os.getenv("TELEGRAM_SESSION_STRING", "").strip()
TELEGRAM_SESSION_NAME = os.getenv("TELEGRAM_SESSION_NAME", "trade_auto_reader").strip() or "trade_auto_reader"
TELEGRAM_LISTEN_CHANNELS = _read_csv_env(
    "TELEGRAM_LISTEN_CHANNELS",
    os.getenv(
        "TELEGRAM_CHANNELS",
        "@GRAB_PROFIT01,@Goldvvipsignals_TM,@Apexgoldtraders,@Gentle1122,@M3_HASSSNAIN,@richard1000pips,@Sureshotfx_Signals_Freefx,@goldexsignals,@PRO_TRADERS_1,@Traders_Gold_Xauusd,@GHP_Trading_Education,@Bengoldtrader_signalsfx,@GOLD_TRADING225,@ForexExpertz99,@elitegoldanalysis,@Silv_FX,@Eliz_fxac_ademy1,@XAUUSDTradingZoneOffical4x,@goldtradingsetup007,@Ethan_Fx00,@forexbignarplace,@Masterfx786,@Daily_TheFxGold1",
    ),
)
TELEGRAM_CHANNELS = TELEGRAM_LISTEN_CHANNELS
TELEGRAM_NOTIFY_CHANNELS = _read_csv_env(
    "TELEGRAM_NOTIFY_CHANNELS",
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
TELEGRAM_CHANNEL_WEIGHTS = _read_weight_map_env("TELEGRAM_CHANNEL_WEIGHTS", "")
TELEGRAM_TRUSTED_CORE_CHANNELS = _read_csv_env("TELEGRAM_TRUSTED_CORE_CHANNELS", "")


def build_analyzers() -> List[AIProviderConfig]:
    analyzers: List[AIProviderConfig] = []

    if OPENROUTER_MULTI_MODEL_COMMITTEE and OPENROUTER_API_KEY:
        multi_models = [
            ("openrouter-openai", OPENROUTER_MODEL_OPENAI),
            ("openrouter-gemini", OPENROUTER_MODEL_GEMINI),
            ("openrouter-grok", OPENROUTER_MODEL_GROK),
            ("openrouter-perplexity", OPENROUTER_MODEL_PERPLEXITY),
        ]
        for analyzer_name, model_name in multi_models:
            if not model_name:
                continue
            analyzers.append(
                AIProviderConfig(
                    name=f"{analyzer_name}:{model_name}",
                    provider="openrouter",
                    api_key=OPENROUTER_API_KEY,
                    model=model_name,
                    temperature=0.2,
                    max_tokens=450,
                    timeout=20,
                )
            )

    elif GROK_RUNTIME_TRANSPORT == "openrouter" and OPENROUTER_API_KEY and "grok" in GROK_OPENROUTER_MODEL.lower():
        analyzers.append(
            AIProviderConfig(
                name=f"openrouter-grok:{GROK_OPENROUTER_MODEL}",
                provider="openrouter",
                api_key=OPENROUTER_API_KEY,
                model=GROK_OPENROUTER_MODEL,
                temperature=0.2,
                max_tokens=450,
                timeout=20,
            )
        )

    elif GROK_RUNTIME_TRANSPORT == "direct" and GROK_API_KEY:
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

    if OPENAI_API_KEY:
        analyzers.append(
            AIProviderConfig(
                name=f"openai:{OPENAI_MODEL}",
                provider="openai",
                api_key=OPENAI_API_KEY,
                model=OPENAI_MODEL,
                temperature=0.2,
                max_tokens=450,
                timeout=20,
            )
        )

    if PERPLEXITY_API_KEY:
        analyzers.append(
            AIProviderConfig(
                name=f"perplexity:{PERPLEXITY_MODEL}",
                provider="perplexity",
                api_key=PERPLEXITY_API_KEY,
                model=PERPLEXITY_MODEL,
                temperature=0.2,
                max_tokens=450,
                timeout=20,
            )
        )

    if GEMINI_API_KEY:
        analyzers.append(
            AIProviderConfig(
                name=f"gemini:{GEMINI_MODEL}",
                provider="gemini",
                api_key=GEMINI_API_KEY,
                model=GEMINI_MODEL,
                temperature=0.2,
                max_tokens=450,
                timeout=20,
            )
        )

    preferred_order = _read_csv_env("AI_ANALYZER_ORDER", "")
    if preferred_order:
        ordered: List[AIProviderConfig] = []
        leftovers = analyzers.copy()
        for provider_name in preferred_order:
            for analyzer in list(leftovers):
                if analyzer.provider.lower() == provider_name.lower() or analyzer.name.lower().startswith(provider_name.lower() + ":"):
                    ordered.append(analyzer)
                    leftovers.remove(analyzer)
        ordered.extend(leftovers)
        analyzers = ordered

    return analyzers


AI_ANALYZERS = build_analyzers()
