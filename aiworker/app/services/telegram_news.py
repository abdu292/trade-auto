from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
import re
from typing import Any

import httpx

from app.ai.config import (
    TELEGRAM_BOT_BASE_URL,
    TELEGRAM_BOT_TOKEN,
    TELEGRAM_CHANNELS,
    TELEGRAM_LOOKBACK_MINUTES,
    TELEGRAM_MAX_UPDATES,
    TELEGRAM_BLOCK_KEYWORDS,
    TELEGRAM_CAUTION_KEYWORDS,
    TELEGRAM_BULLISH_KEYWORDS,
    TELEGRAM_BEARISH_KEYWORDS,
)


@dataclass
class TelegramNewsContext:
    enabled: bool
    impact_tag: str
    risk_tag: str
    direction_bias: str
    tags: list[str]
    summary: str
    headlines: list[str]
    items: list[dict[str, str]]


class TelegramNewsService:
    def __init__(self) -> None:
        self._enabled = bool(TELEGRAM_BOT_TOKEN)

    @property
    def enabled(self) -> bool:
        return self._enabled

    async def collect_news_context(self, symbol: str) -> TelegramNewsContext:
        if not self._enabled:
            return TelegramNewsContext(
                enabled=False,
                impact_tag="LOW",
                risk_tag="CAUTION",
                direction_bias="NEUTRAL",
                tags=["telegram_not_configured"],
                summary="Telegram bot token is not configured.",
                headlines=[],
                items=[],
            )

        updates = await self._fetch_updates()
        items = self._extract_recent_items(updates)
        headlines = [f"{item['channel']}: {item['text']}" for item in items]
        return self._analyze_headlines(symbol, headlines, items)

    async def _fetch_updates(self) -> list[dict[str, Any]]:
        endpoint = f"{TELEGRAM_BOT_BASE_URL}/bot{TELEGRAM_BOT_TOKEN}/getUpdates"
        params = {
            "limit": max(1, min(TELEGRAM_MAX_UPDATES, 100)),
            "allowed_updates": '["channel_post","edited_channel_post"]',
        }

        async with httpx.AsyncClient(timeout=12.0) as client:
            response = await client.get(endpoint, params=params)
            response.raise_for_status()
            payload = response.json()

        if not payload.get("ok"):
            return []

        result = payload.get("result", [])
        if not isinstance(result, list):
            return []

        return [item for item in result if isinstance(item, dict)]

    def _extract_recent_items(self, updates: list[dict[str, Any]]) -> list[dict[str, str]]:
        since = datetime.now(timezone.utc) - timedelta(minutes=max(1, TELEGRAM_LOOKBACK_MINUTES))
        allowed_channels = {self._normalize_channel(value) for value in TELEGRAM_CHANNELS}
        items: list[dict[str, str]] = []

        for update in updates:
            post = update.get("channel_post") or update.get("edited_channel_post")
            if not isinstance(post, dict):
                continue

            post_ts = post.get("date")
            if not isinstance(post_ts, int):
                continue

            post_dt = datetime.fromtimestamp(post_ts, tz=timezone.utc)
            if post_dt < since:
                continue

            chat = post.get("chat", {}) if isinstance(post.get("chat"), dict) else {}
            username = chat.get("username")
            chat_id = chat.get("id")
            current_channel = self._normalize_channel(username if username else str(chat_id))

            if allowed_channels and current_channel not in allowed_channels:
                continue

            text = post.get("text") or post.get("caption")
            if not isinstance(text, str) or not text.strip():
                continue

            compact = self._compact_whitespace(text)
            lowered = compact.lower()
            category = "LOW"
            if any(keyword.strip().lower() in lowered for keyword in TELEGRAM_BLOCK_KEYWORDS if keyword.strip()):
                category = "HIGH"
            elif any(keyword.strip().lower() in lowered for keyword in TELEGRAM_CAUTION_KEYWORDS if keyword.strip()):
                category = "MODERATE"

            items.append({
                "channel": current_channel,
                "timestamp": post_dt.isoformat(),
                "category": category,
                "text": compact[:220],
            })

        return items[-25:]

    def _analyze_headlines(self, symbol: str, headlines: list[str], items: list[dict[str, str]]) -> TelegramNewsContext:
        if not headlines:
            return TelegramNewsContext(
                enabled=True,
                impact_tag="LOW",
                risk_tag="CAUTION",
                direction_bias="NEUTRAL",
                tags=["telegram_no_recent_posts"],
                summary="No recent Telegram channel posts in configured lookback window.",
                headlines=[],
                items=[],
            )

        block_hits = self._count_keyword_hits(headlines, TELEGRAM_BLOCK_KEYWORDS)
        caution_hits = self._count_keyword_hits(headlines, TELEGRAM_CAUTION_KEYWORDS)
        bullish_hits = self._count_keyword_hits(headlines, TELEGRAM_BULLISH_KEYWORDS)
        bearish_hits = self._count_keyword_hits(headlines, TELEGRAM_BEARISH_KEYWORDS)

        impact_tag = "HIGH" if block_hits > 0 else ("MODERATE" if caution_hits > 0 else "LOW")
        risk_tag = "BLOCK" if impact_tag == "HIGH" else ("CAUTION" if impact_tag == "MODERATE" else "SAFE")

        direction_bias = "NEUTRAL"
        if bullish_hits > bearish_hits:
            direction_bias = "BULLISH"
        elif bearish_hits > bullish_hits:
            direction_bias = "BEARISH"

        tags = [f"telegram_posts_{len(headlines)}"]
        if block_hits > 0:
            tags.append("telegram_high_impact_news")
        if caution_hits > 0:
            tags.append("telegram_macro_caution")
        if bullish_hits > 0:
            tags.append("telegram_bullish_gold")
        if bearish_hits > 0:
            tags.append("telegram_bearish_gold")

        summary = (
            f"Telegram ({symbol}) posts={len(headlines)}, "
            f"impact={impact_tag}, risk={risk_tag}, bullish={bullish_hits}, bearish={bearish_hits}, "
            f"block={block_hits}, caution={caution_hits}"
        )

        return TelegramNewsContext(
            enabled=True,
            impact_tag=impact_tag,
            risk_tag=risk_tag,
            direction_bias=direction_bias,
            tags=tags,
            summary=summary,
            headlines=headlines,
            items=items,
        )

    @staticmethod
    def _normalize_channel(value: str) -> str:
        raw = value.strip().lower()
        if raw.startswith("@"):
            return raw
        if raw.startswith("-100"):
            return raw
        if raw.isdigit() or (raw.startswith("-") and raw[1:].isdigit()):
            return raw
        return f"@{raw}"

    @staticmethod
    def _compact_whitespace(text: str) -> str:
        return re.sub(r"\s+", " ", text).strip()

    @staticmethod
    def _count_keyword_hits(lines: list[str], keywords: list[str]) -> int:
        if not keywords:
            return 0

        hit_count = 0
        lowered = [line.lower() for line in lines]
        for keyword in keywords:
            key = keyword.strip().lower()
            if not key:
                continue
            if any(key in line for line in lowered):
                hit_count += 1

        return hit_count
