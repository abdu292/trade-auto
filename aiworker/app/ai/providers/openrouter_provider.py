import json
import logging
import re
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

    def _build_system_prompt(self, is_table_review: bool = False) -> str:
        """Build system prompt using the resolved model-specific role."""
        if is_table_review:
            return """TABLE REVIEW mode. You must respond with ONLY a single JSON object. No markdown, no code blocks, no text before or after.
Use exactly these two keys: "action" (one of: "APPROVE", "CAUTION", "SKIP") and "reasoning" (short string).
Example: {"action": "CAUTION", "reasoning": "Brief reason here."}
Nothing else."""

        master_prompt = _load_prompt_for_role(self._prompt_role)
        response_contract = """You are a gold (XAUUSD) trading AI. Analyze the market data and provide a buy-first pending recommendation.

Execution context:
- Inputs are structured MT5 snapshots plus Telegram/news context.
- Do not request screenshots.
- You must respond with ONLY a single JSON object. No markdown, no code blocks, no "Action:" or other text.

Use exactly these keys only: rail, entry, tp, sl, pe, ml, confidence, reasoning.
- "rail": "BUY_LIMIT" or "BUY_STOP"
- "entry": number (price)
- "tp": number (price)
- "sl": 0
- "pe": "HH:MM", "ml": "HH:MM"
- "confidence": number 0.0-1.0
- "reasoning": "brief explanation"
Do NOT use keys like "action" or "tradeId". Output only this JSON.

If no good setup: {"signal": null}
Otherwise one line of JSON, e.g.: {"rail":"BUY_LIMIT","entry":2650.5,"tp":2660,"sl":0,"pe":"00:30","ml":"02:00","confidence":0.7,"reasoning":"..."}"""

        if not master_prompt:
            return response_contract

        return f"{master_prompt}\n\n{response_contract}"

    async def analyze(self, market_context: dict) -> Optional[TradeSignal]:
        try:
            is_table_review = "table_review_context" in market_context
            user_prompt = (
                dump_market_context(market_context)
                if not is_table_review
                else f"Review this TABLE: {market_context.get('table_review_context', {})}"
            )
            if is_table_review:
                user_prompt = f"TABLE REVIEW. {market_context.get('table_review_context', {}).get('task', '')} Context: {user_prompt}"
            else:
                user_prompt = f"Analyze this forex market: {user_prompt}"
            system_prompt = self._build_system_prompt(is_table_review=is_table_review)
            extra_system_prompt = market_context.get("_extra_system_prompt")
            if isinstance(extra_system_prompt, str) and extra_system_prompt.strip():
                system_prompt = f"{system_prompt}\n\n{extra_system_prompt.strip()}"
            self._trace_request(market_context, system_prompt, user_prompt)

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

            response_text = self._extract_response_text(response)
            if not (response_text and response_text.strip()):
                logger.warning(
                    "OpenRouter returned empty or non-text content for model=%s",
                    getattr(self.config, "model", "?"),
                )
                return None
            logger.info("OpenRouter response: %s", response_text[:500])
            signal = await self.validate_response(response_text, market_context=market_context)
            self._trace_response(market_context, response_text or "", signal)
            return signal
        except APIError as ex:
            logger.error("OpenRouter API error: %s", str(ex))
            self._trace_error(market_context, str(ex))
            return None
        except Exception as ex:
            logger.error("OpenRouter provider error: %s", str(ex))
            self._trace_error(market_context, str(ex))
            return None

    def _extract_response_text(self, response) -> str:
        choices = getattr(response, "choices", None)
        if not choices:
            return ""

        first_choice = choices[0]
        message = getattr(first_choice, "message", None)
        if message is None:
            return ""

        content = getattr(message, "content", None)
        if content is None:
            return ""

        if isinstance(content, str):
            return content

        if isinstance(content, list):
            text_parts: list[str] = []
            for part in content:
                if isinstance(part, dict):
                    text = (
                        part.get("text")
                        or part.get("content")
                        or part.get("output")
                        or part.get("input")
                    )
                else:
                    text = (
                        getattr(part, "text", None)
                        or getattr(part, "content", None)
                        or getattr(part, "output", None)
                    )
                if isinstance(text, str) and text.strip():
                    text_parts.append(text)
            if text_parts:
                return "\n".join(text_parts)
            # Fallback: stringify and try to pull out any JSON-like substring later
            try:
                raw = json.dumps(content, default=str)
                if "rail" in raw and "entry" in raw:
                    return raw
            except Exception:
                pass

        return ""

    @staticmethod
    def _strip_markdown_json(text: str) -> str:
        """Strip markdown code fences (e.g. ```json ... ```) so we can parse JSON."""
        s = text.strip()
        # Remove opening fence: optional "json" or "JSON" after backticks
        if s.startswith("```"):
            s = re.sub(r"^```(?:json|JSON)?\s*\n?", "", s, count=1)
        if s.endswith("```"):
            s = re.sub(r"\n?```\s*$", "", s, count=1)
        return s.strip()

    @staticmethod
    def _extract_json_from_text(cleaned: str) -> Optional[dict]:
        """Extract first JSON object from text; if truncated (no closing }), try to complete or use regex."""
        json_start = cleaned.find("{")
        if json_start == -1:
            return None
        json_end = cleaned.rfind("}") + 1
        if json_end > json_start:
            try:
                return json.loads(cleaned[json_start:json_end])
            except json.JSONDecodeError:
                pass
        # Truncated response: try to parse with a trailing } (model may have hit max_tokens)
        fragment = cleaned[json_start:].strip()
        if '"rail"' in fragment and '"entry"' in fragment:
            # Try adding closing brace(s) to balance
            open_braces = fragment.count("{") - fragment.count("}")
            if open_braces > 0:
                try:
                    return json.loads(fragment + "}" * open_braces)
                except json.JSONDecodeError:
                    pass
            # Last resort: regex for numeric entry/tp
            entry_m = re.search(r'"entry"\s*:\s*([0-9.]+)', fragment)
            tp_m = re.search(r'"tp"\s*:\s*([0-9.]+)', fragment)
            rail_m = re.search(r'"rail"\s*:\s*"(\w+)"', fragment)
            if entry_m and tp_m:
                return {
                    "rail": (rail_m.group(1) if rail_m else "BUY_LIMIT"),
                    "entry": float(entry_m.group(1)),
                    "tp": float(tp_m.group(1)),
                    "sl": 0,
                    "confidence": 0.7,
                    "reasoning": "Parsed from truncated response",
                }
        return None

    @staticmethod
    def _action_to_confidence(action: str) -> float:
        a = (action or "").strip().upper()
        if a == "APPROVE":
            return 0.8
        if a == "SKIP":
            return 0.3
        return 0.5  # CAUTION or unknown

    async def validate_response(
        self, response: Optional[str], market_context: Optional[dict] = None
    ) -> Optional[TradeSignal]:
        try:
            if not isinstance(response, str) or not response.strip():
                logger.warning("OpenRouter returned empty or non-text content")
                return None

            cleaned = self._strip_markdown_json(response)
            data = self._extract_json_from_text(cleaned)

            if data is None:
                # Plain text like "**Action: SKIP**" or prose: no JSON, no error spam
                if re.search(r"action:\s*skip|action:\s*approve|action:\s*caution", cleaned, re.I):
                    logger.info("OpenRouter returned plain-text action (no JSON); treating as no signal")
                else:
                    logger.warning("No JSON object in OpenRouter response (first 300 chars): %s", (response or "")[:300])
                return None

            if "signal" in data and data.get("signal") is None:
                logger.info("OpenRouter returned no signal")
                return None

            # Table-review schema: {"action": "APPROVE"|"CAUTION"|"SKIP", "reasoning": "..."}
            if "action" in data and "reasoning" in data and data.get("entry") is None and data.get("tp") is None:
                ctx = (market_context or {}).get("table_review_context") or {}
                entry = ctx.get("entry") or 0
                tp = ctx.get("tp") or 0
                if entry == 0 or tp == 0:
                    entry = 1.0
                    tp = 1.0
                return TradeSignal(
                    rail=ctx.get("rail") or "BUY_LIMIT",
                    entry=float(entry),
                    tp=float(tp),
                    sl=0.0,
                    pe=ctx.get("pe") or "00:30",
                    ml=ctx.get("ml") or "02:00",
                    confidence=self._action_to_confidence(data.get("action")),
                    reasoning=(data.get("reasoning") or "").strip(),
                )

            def _num(v: object) -> float:
                if isinstance(v, (int, float)):
                    return float(v)
                if isinstance(v, str):
                    return float(v.strip())
                raise ValueError(f"Not a number: {type(v)}")

            entry_val = data.get("entry")
            tp_val = data.get("tp")
            if entry_val is None or tp_val is None:
                logger.info("OpenRouter JSON missing entry/tp (keys: %s); treating as no signal", list(data.keys()))
                return None
            return TradeSignal(
                rail=data.get("rail", "BUY_LIMIT"),
                entry=_num(entry_val),
                tp=_num(tp_val),
                sl=_num(data.get("sl", 0)),
                pe=data.get("pe", "00:30") or "00:30",
                ml=str(data.get("ml", "02:00") or "02:00"),
                confidence=float(data.get("confidence", 0.5)),
                reasoning=data.get("reasoning", "") or "",
            )
        except (json.JSONDecodeError, KeyError, ValueError) as ex:
            logger.error(
                "Failed to parse OpenRouter response: %s. Response snippet: %s",
                str(ex),
                (response or "")[:400],
            )
            return None
