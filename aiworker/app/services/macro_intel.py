import json
import logging
from dataclasses import dataclass
from typing import Any

from openai import AsyncOpenAI

from app.ai.config import (
    PERPLEXITY_API_KEY,
    PERPLEXITY_MODEL,
    OPENROUTER_API_KEY,
    OPENROUTER_MODEL_GEMINI,
)

logger = logging.getLogger(__name__)


def _is_supported_symbol(symbol: str) -> bool:
    normalized = (symbol or "").strip().upper()
    return normalized.startswith("XAUUSD")


@dataclass
class MacroIntelContext:
    geo_headline: str = "NONE"
    dxy_bias: str = "NEUTRAL"
    yields_bias: str = "NEUTRAL"
    cross_metals_bias: str = "NEUTRAL"
    cb_flow: str = "UNKNOWN"
    inst_positioning: str = "UNKNOWN"
    event_risk: str = "LOW"
    summary: str = "No macro intelligence available."


class MacroIntelService:
    async def collect_context(self, symbol: str) -> MacroIntelContext:
        if not _is_supported_symbol(symbol):
            return MacroIntelContext()

        perplexity_payload = await self._fetch_from_perplexity()
        gemini_payload = await self._fetch_from_openrouter_gemini()

        merged = self._merge_payloads(perplexity_payload, gemini_payload)
        return MacroIntelContext(
            geo_headline=merged.get("geo_headline", "NONE"),
            dxy_bias=merged.get("dxy_bias", "NEUTRAL"),
            yields_bias=merged.get("yields_bias", "NEUTRAL"),
            cross_metals_bias=merged.get("cross_metals_bias", "NEUTRAL"),
            cb_flow=merged.get("cb_flow", "UNKNOWN"),
            inst_positioning=merged.get("inst_positioning", "UNKNOWN"),
            event_risk=merged.get("event_risk", "LOW"),
            summary=merged.get("summary", "Macro intelligence blended from available providers."),
        )

    async def _fetch_from_perplexity(self) -> dict[str, Any]:
        if not PERPLEXITY_API_KEY:
            return {}

        prompt = self._build_prompt("Perplexity")
        try:
            client = AsyncOpenAI(api_key=PERPLEXITY_API_KEY, base_url="https://api.perplexity.ai")
            response = await client.chat.completions.create(
                model=PERPLEXITY_MODEL,
                messages=[
                    {"role": "system", "content": "Return only strict JSON."},
                    {"role": "user", "content": prompt},
                ],
                temperature=0,
                max_tokens=350,
            )
            content = response.choices[0].message.content or "{}"
            return self._extract_json(content)
        except Exception as ex:
            logger.warning("Perplexity macro intel unavailable: %s", ex)
            return {}

    async def _fetch_from_openrouter_gemini(self) -> dict[str, Any]:
        if not OPENROUTER_API_KEY or not OPENROUTER_MODEL_GEMINI:
            return {}

        prompt = self._build_prompt("Gemini")
        try:
            client = AsyncOpenAI(api_key=OPENROUTER_API_KEY, base_url="https://openrouter.ai/api/v1")
            response = await client.chat.completions.create(
                model=OPENROUTER_MODEL_GEMINI,
                messages=[
                    {"role": "system", "content": "Return only strict JSON."},
                    {"role": "user", "content": prompt},
                ],
                temperature=0,
                max_tokens=350,
            )
            content = response.choices[0].message.content or "{}"
            return self._extract_json(content)
        except Exception as ex:
            logger.warning("Gemini macro intel unavailable via OpenRouter: %s", ex)
            return {}

    def _build_prompt(self, source: str) -> str:
        return (
            f"Source={source}. Build XAUUSD macro/correlation snapshot for last 12 hours. "
            "Use these keys only: geo_headline, dxy_bias, yields_bias, cross_metals_bias, cb_flow, "
            "inst_positioning, event_risk, summary. "
            "Allowed values: dxy_bias/yields_bias/cross_metals_bias -> BULLISH|BEARISH|NEUTRAL. "
            "event_risk -> LOW|MEDIUM|HIGH. cb_flow/inst_positioning -> FREE TEXT MAX 40 chars. "
            "summary max 120 chars. Output only valid JSON."
        )

    def _extract_json(self, content: str) -> dict[str, Any]:
        content = content.strip()
        if not content:
            return {}

        if content.startswith("```"):
            content = content.replace("```json", "").replace("```", "").strip()

        start = content.find("{")
        end = content.rfind("}")
        if start < 0 or end < 0 or end <= start:
            return {}

        try:
            payload = json.loads(content[start : end + 1])
            if isinstance(payload, dict):
                return payload
        except Exception:
            return {}
        return {}

    def _merge_payloads(self, first: dict[str, Any], second: dict[str, Any]) -> dict[str, Any]:
        if not first and not second:
            return {}

        result = dict(first)
        for key, value in second.items():
            if value in (None, "", "UNKNOWN", "NONE"):
                continue
            result[key] = value

        return result
