import json
import logging
from typing import Optional

from openai import APIError, AsyncOpenAI

from app.ai.providers.base_provider import AIProvider, AIProviderConfig, TradeSignal, _load_prompt_for_role, dump_market_context

logger = logging.getLogger(__name__)


def _resolve_openrouter_prompt_role(config_name: str, model: str) -> str:
    """Resolve the prompt role based on the model name for OpenRouter.
    
    Per PRD: ChatGPT, Grok & Perplexity use their respective master prompts.
    Other models use the standard master_prompt.
    """
    name_lower = config_name.lower()
    model_lower = model.lower()
    if "grok" in name_lower or "grok" in model_lower or "x-ai" in model_lower:
        return "grok"
    if "openai" in name_lower or "gpt" in model_lower:
        return "chat_gpt"
    if "perplexity" in name_lower or "sonar" in model_lower:
        return "perplexity"
    return "standard"


class OpenRouterProvider(AIProvider):
    """Universal one-key provider via OpenRouter (OpenAI-compatible API)."""

    def __init__(self, config: AIProviderConfig):
        super().__init__(config)
        self.client = AsyncOpenAI(
            api_key=config.api_key,
            base_url="https://openrouter.ai/api/v1",
        )
        self._prompt_role = _resolve_openrouter_prompt_role(config.name, config.model)

    def _build_system_prompt(self) -> str:
        """Build system prompt using the resolved model-specific role."""
        master_prompt = _load_prompt_for_role(self._prompt_role)
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

    async def analyze(self, market_context: dict) -> Optional[TradeSignal]:
        try:
            user_prompt = f"Analyze this forex market: {dump_market_context(market_context)}"
            system_prompt = self._build_system_prompt()
            extra_system_prompt = market_context.get("_extra_system_prompt")
            if isinstance(extra_system_prompt, str) and extra_system_prompt.strip():
                system_prompt = f"{system_prompt}\n\n{extra_system_prompt.strip()}"

            response = await self.client.chat.completions.create(
                model=self.config.model,
                temperature=self.config.temperature,
                max_tokens=self.config.max_tokens,
                timeout=self.config.timeout,
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_prompt},
                ],
            )

            response_text = response.choices[0].message.content
            logger.info("OpenRouter response: %s", response_text)
            return await self.validate_response(response_text)
        except APIError as ex:
            logger.error("OpenRouter API error: %s", str(ex))
            return None
        except Exception as ex:
            logger.error("OpenRouter provider error: %s", str(ex))
            return None

    async def validate_response(self, response: str) -> Optional[TradeSignal]:
        try:
            json_start = response.find("{")
            json_end = response.rfind("}") + 1

            if json_start == -1 or json_end == 0:
                logger.warning("No JSON found in OpenRouter response")
                return None

            data = json.loads(response[json_start:json_end])
            if "signal" in data and data.get("signal") is None:
                logger.info("OpenRouter returned no signal")
                return None

            return TradeSignal(
                rail=data.get("rail", "BUY_LIMIT"),
                entry=float(data["entry"]),
                tp=float(data["tp"]),
                sl=float(data.get("sl", 0)),
                pe=data.get("pe", "00:30"),
                ml=str(data.get("ml", "02:00")),
                confidence=float(data.get("confidence", 0.5)),
                reasoning=data.get("reasoning", ""),
            )
        except (json.JSONDecodeError, KeyError, ValueError) as ex:
            logger.error("Failed to parse OpenRouter response: %s", str(ex))
            return None
