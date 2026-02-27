import logging
from typing import Optional, Dict, List, Tuple
import asyncio
from app.ai.providers.base_provider import AIProvider, TradeSignal, AIProviderConfig
from app.ai.providers.openai_provider import OpenAIProvider
from app.ai.providers.openrouter_provider import OpenRouterProvider
from app.ai.providers.grok_provider import GrokProvider
from app.ai.providers.perplexity_provider import PerplexityProvider
from app.ai.providers.gemini_provider import GeminiProvider

logger = logging.getLogger(__name__)


class AIProviderManager:
    _provider_registry = {
        "openai": OpenAIProvider,
        "openrouter": OpenRouterProvider,
        "grok": GrokProvider,
        "perplexity": PerplexityProvider,
        "gemini": GeminiProvider,
    }

    def __init__(self, configs: List[AIProviderConfig]):
        self.providers: Dict[str, AIProvider] = {}
        self.configs = configs
        self.provider_names = [config.name for config in configs]
        self._initialize_providers()

    def _initialize_providers(self):
        for config in self.configs:
            provider_key = config.provider.lower()
            provider_class = self._provider_registry.get(provider_key)
            if provider_class is None:
                logger.warning("Unknown provider '%s' for analyzer '%s'", config.provider, config.name)
                continue
            try:
                self.providers[config.name] = provider_class(config)
                logger.info("Initialized analyzer %s (provider=%s, model=%s)", config.name, config.provider, config.model)
            except Exception as ex:
                logger.error("Failed to initialize analyzer %s: %s", config.name, str(ex))

    async def _analyze_one(self, analyzer_name: str, market_context: dict) -> Optional[Tuple[str, TradeSignal]]:
        provider = self.providers.get(analyzer_name)
        if provider is None:
            return None
        try:
            signal = await provider.analyze(market_context)
            if signal is None:
                return None
            return analyzer_name, signal
        except Exception as ex:
            logger.error("Analyzer %s failed: %s", analyzer_name, str(ex))
            return None

    async def analyze_with_committee(
        self,
        market_context: dict,
        min_agreement: int,
        entry_tolerance_pct: float,
    ) -> Optional[TradeSignal]:
        if not self.provider_names:
            logger.warning("No analyzers configured")
            return None

        tasks = [self._analyze_one(name, market_context) for name in self.provider_names]
        results = await asyncio.gather(*tasks)
        votes = [result for result in results if result is not None]

        if not votes:
            logger.warning("No analyzer returned a usable signal")
            return None

        if len(votes) == 1:
            single = votes[0][1]
            logger.info("Single analyzer available; using %s @ %s", single.rail, single.entry)
            return single

        grouped: Dict[str, List[TradeSignal]] = {}
        for analyzer_name, signal in votes:
            grouped.setdefault(signal.rail, []).append(signal)
            logger.info("Vote %s -> rail=%s entry=%s tp=%s", analyzer_name, signal.rail, signal.entry, signal.tp)

        best_rail = max(grouped.items(), key=lambda kv: len(kv[1]))[0]
        rail_group = grouped[best_rail]

        anchor = rail_group[0]
        agreeing = []
        for signal in rail_group:
            if anchor.entry == 0:
                continue
            diff = abs(signal.entry - anchor.entry) / abs(anchor.entry)
            if diff <= entry_tolerance_pct:
                agreeing.append(signal)

        if len(agreeing) < min_agreement:
            logger.warning("Committee disagreement: rail=%s votes=%s agreeing=%s required=%s", best_rail, len(rail_group), len(agreeing), min_agreement)
            return None

        avg_entry = sum(item.entry for item in agreeing) / len(agreeing)
        avg_tp = sum(item.tp for item in agreeing) / len(agreeing)
        avg_sl = sum(item.sl for item in agreeing) / len(agreeing)
        avg_conf = sum(item.confidence for item in agreeing) / len(agreeing)

        return TradeSignal(
            rail=best_rail,
            entry=avg_entry,
            tp=avg_tp,
            sl=avg_sl,
            pe=anchor.pe,
            ml=anchor.ml,
            confidence=avg_conf,
            reasoning=f"committee:{len(agreeing)}/{len(votes)}",
        )
