from abc import ABC, abstractmethod
from typing import Optional
from dataclasses import dataclass
import json

@dataclass
class AIProviderConfig:
    """Configuration for each AI provider"""
    name: str
    api_key: str
    model: str
    temperature: float = 0.7
    max_tokens: int = 500
    timeout: int = 10


@dataclass
class TradeSignal:
    """Structured trade signal output"""
    rail: str  # A, B, C
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
        return """You are a professional forex trading AI. Analyze the market data and provide trade recommendations.

IMPORTANT: Always respond with ONLY valid JSON in this exact format:
{
  "rail": "A" or "B" or "C",
  "entry": <price>,
  "tp": <price>,
  "sl": <price>,
  "pe": "HH:MM",
  "ml": "HH:MM",
  "confidence": <0.0-1.0>,
  "reasoning": "<brief explanation>"
}

Rules:
- rail "A" = High confidence (> 0.8)
- rail "B" = Medium confidence (0.5-0.8)
- rail "C" = Low confidence (< 0.5)
- pe (pending expiry) = when to cancel if not triggered
- ml (max life) = max hold time for the trade
- TP should be 1.5x to 2x the risk
- SL should protect capital (usually 2-3% of entry)
- If no good setup, respond with: {"signal": null}

DO NOT add any explanation outside the JSON."""
