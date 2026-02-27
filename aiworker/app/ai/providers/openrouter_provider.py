import json
import logging
from typing import Optional

from openai import APIError, AsyncOpenAI

from app.ai.providers.base_provider import AIProvider, AIProviderConfig, TradeSignal

logger = logging.getLogger(__name__)


class OpenRouterProvider(AIProvider):
    """Universal one-key provider via OpenRouter (OpenAI-compatible API)."""

    def __init__(self, config: AIProviderConfig):
        super().__init__(config)
        self.client = AsyncOpenAI(
            api_key=config.api_key,
            base_url="https://openrouter.ai/api/v1",
        )

    async def analyze(self, market_context: dict) -> Optional[TradeSignal]:
        try:
            user_prompt = f"Analyze this forex market: {json.dumps(market_context)}"

            response = await self.client.chat.completions.create(
                model=self.config.model,
                temperature=self.config.temperature,
                max_tokens=self.config.max_tokens,
                timeout=self.config.timeout,
                messages=[
                    {"role": "system", "content": self._build_system_prompt()},
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
