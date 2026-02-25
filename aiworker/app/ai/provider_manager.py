import logging
from typing import Optional, Dict
from app.ai.providers.base_provider import AIProvider, TradeSignal, AIProviderConfig
from app.ai.providers.openai_provider import OpenAIProvider
from app.ai.providers.grok_provider import GrokProvider
from app.ai.providers.perplexity_provider import PerplexityProvider

logger = logging.getLogger(__name__)


class AIProviderManager:
    """
    Manages multiple AI providers with fallback strategy.
    
    Strategy:
    1. Try primary provider
    2. If fails, try secondary providers in order
    3. If all fail, return None
    4. Optional: Implement consensus (require 2/3 agreement)
    """
    
    def __init__(self, configs: Dict[str, AIProviderConfig]):
        """
        Args:
            configs: Dict of provider_name -> AIProviderConfig
                   First provider in dict is primary
        """
        self.providers: Dict[str, AIProvider] = {}
        self.config = configs
        self.provider_names = list(configs.keys())
        
        self._initialize_providers()
    
    def _initialize_providers(self):
        """Create provider instances based on config"""
        for name, config in self.config.items():
            try:
                if name.lower() == "openai":
                    self.providers[name] = OpenAIProvider(config)
                    logger.info(f"Initialized OpenAI provider: {config.model}")
                
                elif name.lower() == "grok":
                    self.providers[name] = GrokProvider(config)
                    logger.info(f"Initialized Grok provider: {config.model}")
                
                elif name.lower() == "perplexity":
                    self.providers[name] = PerplexityProvider(config)
                    logger.info(f"Initialized Perplexity provider: {config.model}")
                
                else:
                    logger.warning(f"Unknown provider: {name}")
            
            except Exception as e:
                logger.error(f"Failed to initialize {name}: {str(e)}")
    
    async def analyze_with_fallback(self, market_context: dict) -> Optional[TradeSignal]:
        """
        Try providers in order until one succeeds
        Returns first successful signal or None
        """
        logger.info(f"Analyzing market with providers: {self.provider_names}")
        
        for provider_name in self.provider_names:
            if provider_name not in self.providers:
                logger.warning(f"Provider {provider_name} not initialized, skipping")
                continue
            
            try:
                logger.debug(f"Trying provider: {provider_name}")
                signal = await self.providers[provider_name].analyze(market_context)
                
                if signal:
                    logger.info(f"✓ Got signal from {provider_name}: {signal.rail} @ {signal.entry}")
                    return signal
                
                logger.info(f"✗ No signal from {provider_name}, trying next...")
            
            except Exception as e:
                logger.error(f"Error from {provider_name}: {str(e)}, trying next...")
                continue
        
        logger.warning("All providers failed to generate signal")
        return None
    
    async def analyze_with_consensus(self, market_context: dict, min_agreement: int = 2) -> Optional[TradeSignal]:
        """
        Advanced: Get signals from multiple providers and return if consensus
        Useful when you want high confidence
        
        Args:
            market_context: Market data
            min_agreement: Minimum providers that must agree (default 2 out of 3)
        """
        signals = []
        
        for provider_name in self.provider_names[:min_agreement + 1]:  # Query at least min_agreement providers
            if provider_name not in self.providers:
                continue
            
            try:
                signal = await self.providers[provider_name].analyze(market_context)
                if signal:
                    signals.append((provider_name, signal))
                    logger.debug(f"{provider_name}: {signal.rail} @ {signal.entry}")
            
            except Exception as e:
                logger.error(f"Error from {provider_name}: {str(e)}")
        
        if len(signals) < min_agreement:
            logger.warning(f"Only {len(signals)} providers responded, need {min_agreement} for consensus")
            return None
        
        # Check if signals are similar (same rail, entry within 0.5%)
        first_signal = signals[0][1]
        agreeing_signals = []
        
        for provider_name, signal in signals:
            entry_diff = abs(signal.entry - first_signal.entry) / first_signal.entry
            
            if signal.rail == first_signal.rail and entry_diff < 0.005:  # 0.5% tolerance
                agreeing_signals.append(signal)
                logger.debug(f"✓ {provider_name} agrees")
            else:
                logger.debug(f"✗ {provider_name} disagrees")
        
        if len(agreeing_signals) >= min_agreement:
            # Average the agreeing signals
            avg_confidence = sum(s.confidence for s in agreeing_signals) / len(agreeing_signals)
            combined_signal = TradeSignal(
                rail=first_signal.rail,
                entry=first_signal.entry,
                tp=first_signal.tp,
                sl=first_signal.sl,
                pe=first_signal.pe,
                ml=first_signal.ml,
                confidence=avg_confidence,
                reasoning=f"Consensus from {len(agreeing_signals)} providers. " + 
                          first_signal.reasoning
            )
            logger.info(f"✓ Consensus reached: {combined_signal.rail} @ {combined_signal.entry}")
            return combined_signal
        
        logger.warning(f"No consensus (only {len(agreeing_signals)}/{min_agreement} agreed)")
        return None
