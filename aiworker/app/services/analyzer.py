import logging
from app.models.contracts import MarketSnapshot, TradeSignal
from app.ai.provider_manager import AIProviderManager
from app.ai.config import AI_PROVIDERS, AI_STRATEGY, CONSENSUS_MIN_AGREEMENT

logger = logging.getLogger(__name__)


class AnalyzerService:
    def __init__(self, use_consensus: bool = False) -> None:
        """
        Initialize analyzer with configured AI providers
        
        Args:
            use_consensus: If True, use consensus strategy. Otherwise use fallback.
        """
        if not AI_PROVIDERS:
            raise RuntimeError("No AI providers configured. Set environment variables: "
                             "OPENAI_API_KEY, GROK_API_KEY, PERPLEXITY_API_KEY")
        
        self._manager = AIProviderManager(AI_PROVIDERS)
        self._use_consensus = use_consensus or AI_STRATEGY == "consensus"

    async def analyze(self, snapshot: MarketSnapshot) -> TradeSignal:
        """
        Analyze market snapshot and return trade signal
        
        Args:
            snapshot: Market data including OHLC, technicals, session, events
        
        Returns:
            TradeSignal with trade recommendation
        
        Raises:
            ValueError: If no signal could be generated
        """
        # Convert snapshot to dict for AI providers
        market_context = {
            "symbol": snapshot.symbol,
            "current_price": snapshot.close,
            "open": snapshot.open,
            "high": snapshot.high,
            "low": snapshot.low,
            "close": snapshot.close,
            "volume": snapshot.volume,
            "rsi14": snapshot.rsi14,
            "macd": snapshot.macd,
            "ma20": snapshot.ma20,
            "ma50": snapshot.ma50,
            "atr14": snapshot.atr14,
            "session": snapshot.session_name,
            "is_session_open": snapshot.is_session_open,
            "minutes_until_session_end": snapshot.minutes_until_session_end,
            "upcoming_event": snapshot.upcoming_event,
            "event_impact": snapshot.event_impact
        }
        
        # Get signal using configured strategy
        if self._use_consensus:
            signal = await self._manager.analyze_with_consensus(
                market_context, 
                min_agreement=CONSENSUS_MIN_AGREEMENT
            )
        else:
            signal = await self._manager.analyze_with_fallback(market_context)
        
        if not signal:
            raise ValueError("No trade signal generated from any AI provider")
        
        logger.info(f"Generated {signal.rail} signal: {signal.entry} (confidence: {signal.confidence})")
        
        # Convert to TradeSignal response format
        return TradeSignal(
            rail=signal.rail,
            entry=signal.entry,
            tp=signal.tp,
            sl=signal.sl,
            pe=signal.pe,
            ml=signal.ml,
            confidence=signal.confidence,
            reasoning=signal.reasoning
        )
