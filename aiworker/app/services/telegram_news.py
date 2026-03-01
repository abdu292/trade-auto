from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
import logging
import re
from typing import Any

import httpx

from app.ai.config import (
    TELEGRAM_BOT_BASE_URL,
    TELEGRAM_BOT_TOKEN,
    TELEGRAM_READ_MODE,
    TELEGRAM_API_ID,
    TELEGRAM_API_HASH,
    TELEGRAM_SESSION_STRING,
    TELEGRAM_SESSION_NAME,
    TELEGRAM_CHANNELS,
    TELEGRAM_LOOKBACK_MINUTES,
    TELEGRAM_MAX_UPDATES,
    TELEGRAM_BLOCK_KEYWORDS,
    TELEGRAM_CAUTION_KEYWORDS,
    TELEGRAM_BULLISH_KEYWORDS,
    TELEGRAM_BEARISH_KEYWORDS,
    TELEGRAM_CHANNEL_WEIGHTS,
    TELEGRAM_TRUSTED_CORE_CHANNELS,
)

logger = logging.getLogger(__name__)


@dataclass
class TelegramNewsContext:
    enabled: bool
    impact_tag: str
    risk_tag: str
    direction_bias: str
    telegram_state: str
    panic_suspected: bool
    buy_score: float
    sell_score: float
    dominance: float
    tags: list[str]
    summary: str
    headlines: list[str]
    items: list[dict[str, str]]


class TelegramNewsService:
    def __init__(self) -> None:
        self._enabled = self._is_reader_enabled()

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
                telegram_state="QUIET",
                panic_suspected=False,
                buy_score=0.0,
                sell_score=0.0,
                dominance=0.0,
                tags=["telegram_not_configured", f"telegram_mode_{TELEGRAM_READ_MODE}"],
                summary=f"Telegram reader is not configured for mode={TELEGRAM_READ_MODE}.",
                headlines=[],
                items=[],
            )

        items = await self._fetch_recent_items()
        headlines = [f"{item['channel']}: {item['text']}" for item in items]
        return self._analyze_headlines(symbol, headlines, items)

    @staticmethod
    def _is_reader_enabled() -> bool:
        if TELEGRAM_READ_MODE == "client":
            return TELEGRAM_API_ID > 0 and bool(TELEGRAM_API_HASH)
        return bool(TELEGRAM_BOT_TOKEN)

    async def _fetch_recent_items(self) -> list[dict[str, str]]:
        if TELEGRAM_READ_MODE == "client":
            items = await self._fetch_recent_items_client()
            if items:
                return items
            if TELEGRAM_BOT_TOKEN:
                logger.warning("Telegram client mode returned no items; falling back to bot getUpdates path.")

        updates = await self._fetch_updates_bot()
        return self._extract_recent_items_from_updates(updates)

    async def _fetch_updates_bot(self) -> list[dict[str, Any]]:
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

    async def _fetch_recent_items_client(self) -> list[dict[str, str]]:
        try:
            from telethon import TelegramClient
            from telethon.sessions import StringSession
        except Exception:
            logger.warning("Telethon is not installed. Install dependencies from requirements.txt to use TELEGRAM_READ_MODE=client.")
            return []

        if TELEGRAM_API_ID <= 0 or not TELEGRAM_API_HASH:
            return []

        session = StringSession(TELEGRAM_SESSION_STRING) if TELEGRAM_SESSION_STRING else TELEGRAM_SESSION_NAME
        since = datetime.now(timezone.utc) - timedelta(minutes=max(1, TELEGRAM_LOOKBACK_MINUTES))
        allowed_channels = {self._normalize_channel(value) for value in TELEGRAM_CHANNELS}
        per_channel_limit = max(5, min(TELEGRAM_MAX_UPDATES, 50))
        collected: list[dict[str, str]] = []

        async with TelegramClient(session, TELEGRAM_API_ID, TELEGRAM_API_HASH) as client:
            if not await client.is_user_authorized():
                logger.warning(
                    "Telegram client session is not authorized. Complete first login for TELEGRAM_READ_MODE=client using TELEGRAM_SESSION_STRING or session file."
                )
                return []

            for channel in allowed_channels:
                channel_ref = channel[1:] if channel.startswith("@") else channel
                try:
                    entity = await client.get_entity(channel_ref)
                    messages = await client.get_messages(entity, limit=per_channel_limit)
                except Exception as ex:
                    logger.warning("Failed reading channel %s via Telegram client: %s", channel, ex)
                    continue

                for message in messages:
                    if not message:
                        continue
                    if not message.date:
                        continue

                    message_dt = message.date.astimezone(timezone.utc)
                    if message_dt < since:
                        continue

                    text = (message.message or "").strip()
                    if not text:
                        continue

                    compact = self._compact_whitespace(text)
                    category = self._categorize(compact)
                    collected.append({
                        "channel": channel,
                        "timestamp": message_dt.isoformat(),
                        "category": category,
                        "text": compact[:220],
                    })

        collected.sort(key=lambda item: item["timestamp"])
        return collected[-25:]

    def _extract_recent_items_from_updates(self, updates: list[dict[str, Any]]) -> list[dict[str, str]]:
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
            category = self._categorize(compact)

            items.append({
                "channel": current_channel,
                "timestamp": post_dt.isoformat(),
                "category": category,
                "text": compact[:220],
            })

        return items[-25:]

    @staticmethod
    def _categorize(text: str) -> str:
        lowered = text.lower()
        if any(keyword.strip().lower() in lowered for keyword in TELEGRAM_BLOCK_KEYWORDS if keyword.strip()):
            return "HIGH"
        if any(keyword.strip().lower() in lowered for keyword in TELEGRAM_CAUTION_KEYWORDS if keyword.strip()):
            return "MODERATE"
        return "LOW"

    def _analyze_headlines(self, symbol: str, headlines: list[str], items: list[dict[str, str]]) -> TelegramNewsContext:
        if not headlines:
            return TelegramNewsContext(
                enabled=True,
                impact_tag="LOW",
                risk_tag="CAUTION",
                direction_bias="NEUTRAL",
                telegram_state="QUIET",
                panic_suspected=False,
                buy_score=0.0,
                sell_score=0.0,
                dominance=0.0,
                tags=["telegram_no_recent_posts"],
                summary="No recent Telegram channel posts in configured lookback window.",
                headlines=[],
                items=[],
            )

        block_hits = self._count_keyword_hits(headlines, TELEGRAM_BLOCK_KEYWORDS)
        caution_hits = self._count_keyword_hits(headlines, TELEGRAM_CAUTION_KEYWORDS)
        bullish_hits = self._count_keyword_hits(headlines, TELEGRAM_BULLISH_KEYWORDS)
        bearish_hits = self._count_keyword_hits(headlines, TELEGRAM_BEARISH_KEYWORDS)

        buy_score, sell_score = self._compute_weighted_scores(items)
        dominance = self._compute_dominance(buy_score, sell_score)
        telegram_state = self._resolve_consensus_state(buy_score, sell_score, dominance, len(items))
        panic_suspected = self._detect_panic_sell(items, sell_score, buy_score)

        impact_tag = "HIGH" if block_hits > 0 else ("MODERATE" if caution_hits > 0 else "LOW")
        risk_tag = "BLOCK" if impact_tag == "HIGH" else ("CAUTION" if impact_tag == "MODERATE" else "SAFE")
        if panic_suspected:
            risk_tag = "BLOCK"
            impact_tag = "HIGH"

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
        tags.append(f"telegram_state_{telegram_state.lower()}")
        if panic_suspected:
            tags.append("telegram_panic_suspected")

        summary = (
            f"Telegram ({symbol}) posts={len(headlines)}, "
            f"state={telegram_state}, impact={impact_tag}, risk={risk_tag}, "
            f"buy={buy_score:.2f}, sell={sell_score:.2f}, dominance={dominance:.2f}, "
            f"panic={panic_suspected}, block={block_hits}, caution={caution_hits}"
        )

        return TelegramNewsContext(
            enabled=True,
            impact_tag=impact_tag,
            risk_tag=risk_tag,
            direction_bias=direction_bias,
            telegram_state=telegram_state,
            panic_suspected=panic_suspected,
            buy_score=buy_score,
            sell_score=sell_score,
            dominance=dominance,
            tags=tags,
            summary=summary,
            headlines=headlines,
            items=items,
        )

    @staticmethod
    def _compute_weighted_scores(items: list[dict[str, str]]) -> tuple[float, float]:
        buy_score = 0.0
        sell_score = 0.0
        trusted_core = {
            TelegramNewsService._normalize_channel(channel)
            for channel in TELEGRAM_TRUSTED_CORE_CHANNELS
        }
        for item in items:
            text = (item.get("text") or "").lower()
            weight = 1.0
            if item.get("category") == "HIGH":
                weight = 1.25
            elif item.get("category") == "MODERATE":
                weight = 1.10

            channel = TelegramNewsService._normalize_channel(item.get("channel") or "")
            channel_weight = TELEGRAM_CHANNEL_WEIGHTS.get(channel, 1.0)
            if channel in trusted_core:
                channel_weight = max(channel_weight, 1.5)
            weight *= channel_weight

            buy_tokens = ("buy", "long", "bullish", "accumulate")
            sell_tokens = ("sell", "short", "bearish", "dump", "panic")
            has_buy = any(token in text for token in buy_tokens)
            has_sell = any(token in text for token in sell_tokens)

            if has_buy and not has_sell:
                buy_score += weight
            elif has_sell and not has_buy:
                sell_score += weight

        return buy_score, sell_score

    @staticmethod
    def _compute_dominance(buy_score: float, sell_score: float) -> float:
        total = buy_score + sell_score
        if total <= 0.0:
            return 0.0
        return max(buy_score, sell_score) / (total + 1e-6)

    @staticmethod
    def _resolve_consensus_state(buy_score: float, sell_score: float, dominance: float, activity: int) -> str:
        if activity < 2 or (buy_score + sell_score) < 1.5:
            return "QUIET"

        if buy_score > sell_score:
            if dominance >= 0.85:
                return "STRONG_BUY"
            if dominance >= 0.70:
                return "BUY"

        if sell_score > buy_score:
            if dominance >= 0.85:
                return "STRONG_SELL"
            if dominance >= 0.70:
                return "SELL"

        return "MIXED"

    @staticmethod
    def _detect_panic_sell(items: list[dict[str, str]], sell_score: float, buy_score: float) -> bool:
        if sell_score < 3.0 or sell_score <= buy_score * 1.8:
            return False

        panic_tokens = ("panic", "crash", "dump", "waterfall", "liquidation")
        panic_hits = 0
        for item in items[-15:]:
            text = (item.get("text") or "").lower()
            if any(token in text for token in panic_tokens):
                panic_hits += 1

        return panic_hits >= 2

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
