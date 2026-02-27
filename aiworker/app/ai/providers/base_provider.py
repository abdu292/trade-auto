from abc import ABC, abstractmethod
from typing import Optional
from dataclasses import dataclass

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


class AIProvider(ABC):
    """Base class for all AI providers"""
    
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
        """Common system prompt for all providers"""
        return """You are a gold (XAUUSD) trading AI. Analyze the market data and provide a buy-first pending recommendation.

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
