from abc import ABC, abstractmethod
from typing import Optional
from dataclasses import dataclass
from pathlib import Path
import os
import json
from datetime import date, datetime

@dataclass
class AIProviderConfig:
    """Configuration for each AI provider"""
    name: str
    provider: str
    api_key: str
    model: str
    temperature: float = 0.7
    max_tokens: int = 500
    timeout: int = 10


@dataclass
class TradeSignal:
    """Structured trade signal output"""
    rail: str  # BUY_LIMIT / BUY_STOP
    entry: float
    tp: float  # Take Profit
    sl: float  # Stop Loss
    pe: str    # Pending expiry (HH:MM)
    ml: str    # Max life (HH:MM)
    confidence: float  # 0.0 - 1.0
    reasoning: str
    
    def to_dict(self):
        return {
            "rail": self.rail,
            "entry": self.entry,
            "tp": self.tp,
            "sl": self.sl,
            "pe": self.pe,
            "ml": self.ml,
            "confidence": self.confidence,
            "reasoning": self.reasoning
        }


def _json_default(value):
    if isinstance(value, (datetime, date)):
        return value.isoformat()
    return str(value)


def dump_market_context(market_context: dict) -> str:
    return json.dumps(market_context, ensure_ascii=False, default=_json_default)


_PROMPT_CACHE: dict[str, str] = {}


def _find_repo_root(start: Path) -> Path:
    """Walk up from start to find repo root (containing .git or prompts/ folder)."""
    current = start
    for _ in range(8):  # max 8 levels up
        if (current / ".git").exists() or (current / "prompts").is_dir():
            return current
        parent = current.parent
        if parent == current:
            break
        current = parent
    # Fallback: use 4 levels up from this file (original behaviour)
    return start.parents[3]


def _load_prompt_for_role(role: str = "standard") -> str:
    """Load provider-specific master prompt from the prompts/ folder.
    
    Per PRD IMPORTANT NOTE 2: ChatGPT, Grok & Perplexity use their respective
    master prompts; other AI models use the standard master_prompt.
    """
    cache_key = role or "standard"
    if cache_key in _PROMPT_CACHE:
        return _PROMPT_CACHE[cache_key]

    here = Path(__file__).resolve()
    repo_root = _find_repo_root(here.parent)

    configured = os.getenv("MASTER_PROMPT_PATH", "").strip()
    candidates: list[Path] = []

    if configured:
        candidates.append(Path(configured))

    # Role-specific prompt first (e.g. prompts/master_prompt_grok.md)
    if role and role not in ("standard", ""):
        candidates.append(repo_root / "prompts" / f"master_prompt_{role}.md")

    # Generic fallback locations (prompts/ folder is primary)
    candidates.extend([
        repo_root / "prompts" / "master_prompt",
        repo_root / "spec" / "master_prompt",
        repo_root / "aiworker" / "master_prompt",
    ])

    for candidate in candidates:
        try:
            if candidate.exists() and candidate.is_file():
                text = candidate.read_text(encoding="utf-8").strip()
                if text:
                    _PROMPT_CACHE[cache_key] = text
                    return text
        except Exception:
            continue

    _PROMPT_CACHE[cache_key] = ""
    return ""


class AIProvider(ABC):
    """Base class for all AI providers"""

    # Subclasses override this to load their provider-specific master prompt.
    # Valid roles: "grok", "chat_gpt", "perplexity", "standard"
    PROMPT_ROLE: str = "standard"

    def __init__(self, config: AIProviderConfig):
        self.config = config
    
    @abstractmethod
    async def analyze(self, market_context: dict) -> Optional[TradeSignal]:
        """
        Analyze market data and return a trade signal
        Args:
            market_context: Dict with OHLC, technicals, session, events
        Returns:
            TradeSignal or None if no opportunity
        """
        pass
    
    @abstractmethod
    async def validate_response(self, response: str) -> Optional[TradeSignal]:
        """
        Parse and validate AI response into structured format
        """
        pass

    def _build_system_prompt(self) -> str:
        """Build system prompt, using provider-specific master prompt when available."""
        master_prompt = _load_prompt_for_role(self.PROMPT_ROLE)
        response_contract = """You are a gold (XAUUSD) trading AI. Analyze the market data and provide a buy-first pending recommendation.

Execution context:
- Inputs are structured MT5 snapshots plus Telegram/news context.
- Do not request screenshots.
- Return only machine-readable JSON.

IMPORTANT: Always respond with ONLY valid JSON in this exact format:
{
    "rail": "BUY_LIMIT" or "BUY_STOP",
  "entry": <price>,
  "tp": <price>,
    "sl": 0,
  "pe": "HH:MM",
  "ml": "HH:MM",
  "confidence": <0.0-1.0>,
  "reasoning": "<brief explanation>"
}

Rules:
- Symbol is XAUUSD only.
- Buy-first only. Never output sell-first logic.
- No hedging and no market execution.
- No stop-loss logic, keep sl=0.
- pe (pending expiry) = when to cancel if not triggered
- ml (max life) = max hold time for the trade
- If no good setup, respond with: {"signal": null}

DO NOT add any explanation outside the JSON."""

        if not master_prompt:
            return response_contract

        return f"{master_prompt}\n\n{response_contract}"

