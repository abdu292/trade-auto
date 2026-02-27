import json
import logging
from typing import Optional
import httpx
from app.ai.providers.base_provider import AIProvider, TradeSignal, AIProviderConfig

logger = logging.getLogger(__name__)


class GeminiProvider(AIProvider):
    """Google Gemini provider via Generative Language API."""

    async def analyze(self, market_context: dict) -> Optional[TradeSignal]:
        try:
            prompt = (
                f"{self._build_system_prompt()}\n\n"
                f"Analyze this forex market and respond JSON only:\n{json.dumps(market_context)}"
            )

            url = (
                f"https://generativelanguage.googleapis.com/v1beta/models/"
                f"{self.config.model}:generateContent"
            )

            payload = {
                "contents": [
                    {
                        "parts": [
                            {
                                "text": prompt,
                            }
                        ]
                    }
                ],
                "generationConfig": {
                    "temperature": self.config.temperature,
                    "maxOutputTokens": self.config.max_tokens,
                },
            }

            async with httpx.AsyncClient(timeout=self.config.timeout) as client:
                response = await client.post(url, params={"key": self.config.api_key}, json=payload)
                response.raise_for_status()
                data = response.json()

            text = (
                data.get("candidates", [{}])[0]
                .get("content", {})
                .get("parts", [{}])[0]
                .get("text", "")
            )

            if not text:
                logger.warning("Gemini returned empty response")
                return None

            return await self.validate_response(text)
        except Exception as ex:
            logger.error("Gemini provider error: %s", str(ex))
            return None

    async def validate_response(self, response: str) -> Optional[TradeSignal]:
        try:
            clean = response.strip()
            if clean.startswith("```"):
                clean = clean.strip("`")
                clean = clean.replace("json\n", "", 1)

            json_start = clean.find("{")
            json_end = clean.rfind("}") + 1
            if json_start < 0 or json_end <= 0:
                logger.warning("No JSON found in Gemini response")
                return None

            data = json.loads(clean[json_start:json_end])
            if "signal" in data and data.get("signal") is None:
                return None

            return TradeSignal(
                rail=data.get("rail", "BUY_LIMIT"),
                entry=float(data["entry"]),
                tp=float(data["tp"]),
                sl=float(data["sl"]),
                pe=data.get("pe", "00:30"),
                ml=str(data.get("ml", "01:00")),
                confidence=float(data.get("confidence", 0.5)),
                reasoning=data.get("reasoning", ""),
            )
        except Exception as ex:
            logger.error("Failed to parse Gemini response: %s", str(ex))
            return None
