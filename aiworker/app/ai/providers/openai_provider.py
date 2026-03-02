import json
import logging
from typing import Optional
from openai import AsyncOpenAI, APIError
from app.ai.providers.base_provider import AIProvider, TradeSignal, AIProviderConfig

logger = logging.getLogger(__name__)


class OpenAIProvider(AIProvider):
    """ChatGPT 4 / 3.5 Turbo provider"""

    PROMPT_ROLE = "chat_gpt"

    def __init__(self, config: AIProviderConfig):
        super().__init__(config)
        self.client = AsyncOpenAI(api_key=config.api_key)
    
    async def analyze(self, market_context: dict) -> Optional[TradeSignal]:
        """Call OpenAI API and parse response"""
        try:
            user_prompt = f"Analyze this forex market: {json.dumps(market_context)}"
            
            response = await self.client.chat.completions.create(
                model=self.config.model,  # "gpt-4" or "gpt-3.5-turbo"
                temperature=self.config.temperature,
                max_tokens=self.config.max_tokens,
                timeout=self.config.timeout,
                messages=[
                    {
                        "role": "system",
                        "content": self._build_system_prompt()
                    },
                    {
                        "role": "user",
                        "content": user_prompt
                    }
                ]
            )
            
            response_text = response.choices[0].message.content
            logger.info(f"OpenAI response: {response_text}")
            
            return await self.validate_response(response_text)
        
        except APIError as e:
            logger.error(f"OpenAI API error: {str(e)}")
            return None
        except Exception as e:
            logger.error(f"OpenAI provider error: {str(e)}")
            return None
    
    async def validate_response(self, response: str) -> Optional[TradeSignal]:
        """Parse JSON response from OpenAI"""
        try:
            # Extract JSON from response (in case of extra text)
            json_start = response.find('{')
            json_end = response.rfind('}') + 1
            
            if json_start == -1 or json_end == 0:
                logger.warning("No JSON found in OpenAI response")
                return None
            
            json_str = response[json_start:json_end]
            data = json.loads(json_str)
            
            # Check for null signal
            if "signal" in data and data.get("signal") is None:
                logger.info("OpenAI returned no signal")
                return None
            
            return TradeSignal(
                rail=data.get("rail", "BUY_LIMIT"),
                entry=float(data["entry"]),
                tp=float(data["tp"]),
                sl=float(data["sl"]),
                pe=data.get("pe", "00:30"),
                ml=str(data.get("ml", "02:00")),
                confidence=float(data.get("confidence", 0.5)),
                reasoning=data.get("reasoning", "")
            )
        
        except (json.JSONDecodeError, KeyError, ValueError) as e:
            logger.error(f"Failed to parse OpenAI response: {str(e)}")
            return None
