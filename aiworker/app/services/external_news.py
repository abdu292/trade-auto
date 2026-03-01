from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from email.utils import parsedate_to_datetime
import logging
import re
from typing import Any
import xml.etree.ElementTree as ET

import httpx

from app.ai.config import (
    EXTERNAL_NEWS_BEARISH_KEYWORDS,
    EXTERNAL_NEWS_BLOCK_KEYWORDS,
    EXTERNAL_NEWS_BULLISH_KEYWORDS,
    EXTERNAL_NEWS_CAUTION_KEYWORDS,
    EXTERNAL_NEWS_ENABLED,
    EXTERNAL_NEWS_FEEDS,
    EXTERNAL_NEWS_LOOKBACK_MINUTES,
    EXTERNAL_NEWS_MAX_ITEMS,
)

logger = logging.getLogger(__name__)


@dataclass
class ExternalNewsContext:
    enabled: bool
    feed_count: int
    impact_tag: str
    risk_tag: str
    direction_bias: str
    news_state: str
    buy_score: float
    sell_score: float
    dominance: float
    panic_suspected: bool
    tags: list[str]
    summary: str
    headlines: list[str]
    items: list[dict[str, str]]


class ExternalNewsService:
    def __init__(self) -> None:
        self._enabled = EXTERNAL_NEWS_ENABLED and len(EXTERNAL_NEWS_FEEDS) > 0

    @property
    def enabled(self) -> bool:
        return self._enabled

    async def collect_news_context(self, symbol: str) -> ExternalNewsContext:
        if not self._enabled:
            return ExternalNewsContext(
                enabled=False,
                feed_count=0,
                impact_tag="LOW",
                risk_tag="CAUTION",
                direction_bias="NEUTRAL",
                news_state="QUIET",
                buy_score=0.0,
                sell_score=0.0,
                dominance=0.0,
                panic_suspected=False,
                tags=["external_news_disabled"],
                summary="External RSS news layer is disabled.",
                headlines=[],
                items=[],
            )

        items = await self._fetch_recent_items()
        headlines = [f"{item['source']}: {item['title']}" for item in items]
        return self._analyze_items(symbol, headlines, items)

    async def _fetch_recent_items(self) -> list[dict[str, str]]:
        since = datetime.now(timezone.utc) - timedelta(minutes=max(15, EXTERNAL_NEWS_LOOKBACK_MINUTES))
        max_items = max(5, min(EXTERNAL_NEWS_MAX_ITEMS, 80))
        all_items: list[dict[str, str]] = []

        async with httpx.AsyncClient(timeout=10.0, follow_redirects=True) as client:
            for feed_url in EXTERNAL_NEWS_FEEDS:
                try:
                    response = await client.get(feed_url)
                    response.raise_for_status()
                    parsed = self._parse_feed(feed_url, response.text, since)
                    all_items.extend(parsed)
                except Exception as ex:
                    logger.warning("External news feed read failed for %s: %s", feed_url, ex)

        all_items.sort(key=lambda item: item.get("published", ""), reverse=True)
        deduped: list[dict[str, str]] = []
        seen: set[str] = set()
        for item in all_items:
            key = f"{item.get('source','')}|{item.get('title','')}|{item.get('link','')}".lower()
            if key in seen:
                continue
            seen.add(key)
            deduped.append(item)
            if len(deduped) >= max_items:
                break
        return deduped

    def _parse_feed(self, feed_url: str, xml_text: str, since: datetime) -> list[dict[str, str]]:
        source = self._source_from_url(feed_url)
        try:
            root = ET.fromstring(xml_text)
        except ET.ParseError:
            return []

        items: list[dict[str, str]] = []

        rss_items = root.findall(".//item")
        for node in rss_items:
            title = self._extract_text(node, ["title"])
            if not title:
                continue
            published = self._extract_text(node, ["pubDate", "published", "updated"]) or ""
            published_dt = self._parse_datetime(published)
            if published_dt is not None and published_dt < since:
                continue
            summary = self._extract_text(node, ["description", "summary"]) or ""
            link = self._extract_text(node, ["link"]) or ""
            items.append(
                {
                    "source": source,
                    "title": self._compact_whitespace(title)[:220],
                    "summary": self._compact_whitespace(summary)[:320],
                    "link": link.strip(),
                    "published": (published_dt or datetime.now(timezone.utc)).isoformat(),
                }
            )

        atom_entries = root.findall(".//{http://www.w3.org/2005/Atom}entry")
        for node in atom_entries:
            title = self._extract_text(node, ["{http://www.w3.org/2005/Atom}title", "title"])
            if not title:
                continue
            published = self._extract_text(
                node,
                [
                    "{http://www.w3.org/2005/Atom}updated",
                    "{http://www.w3.org/2005/Atom}published",
                    "updated",
                    "published",
                ],
            ) or ""
            published_dt = self._parse_datetime(published)
            if published_dt is not None and published_dt < since:
                continue
            summary = self._extract_text(
                node,
                ["{http://www.w3.org/2005/Atom}summary", "{http://www.w3.org/2005/Atom}content", "summary"],
            ) or ""
            link = ""
            for link_node in node.findall("{http://www.w3.org/2005/Atom}link"):
                href = (link_node.attrib.get("href") or "").strip()
                rel = (link_node.attrib.get("rel") or "alternate").strip().lower()
                if href and rel in {"alternate", ""}:
                    link = href
                    break
            items.append(
                {
                    "source": source,
                    "title": self._compact_whitespace(title)[:220],
                    "summary": self._compact_whitespace(summary)[:320],
                    "link": link,
                    "published": (published_dt or datetime.now(timezone.utc)).isoformat(),
                }
            )

        return items

    def _analyze_items(
        self,
        symbol: str,
        headlines: list[str],
        items: list[dict[str, str]],
    ) -> ExternalNewsContext:
        if not items:
            return ExternalNewsContext(
                enabled=True,
                feed_count=len(EXTERNAL_NEWS_FEEDS),
                impact_tag="LOW",
                risk_tag="CAUTION",
                direction_bias="NEUTRAL",
                news_state="QUIET",
                buy_score=0.0,
                sell_score=0.0,
                dominance=0.0,
                panic_suspected=False,
                tags=["external_news_no_recent_items"],
                summary="No recent items found in configured external news feeds.",
                headlines=[],
                items=[],
            )

        block_hits = self._count_keyword_hits(items, EXTERNAL_NEWS_BLOCK_KEYWORDS)
        caution_hits = self._count_keyword_hits(items, EXTERNAL_NEWS_CAUTION_KEYWORDS)
        bullish_hits = self._count_keyword_hits(items, EXTERNAL_NEWS_BULLISH_KEYWORDS)
        bearish_hits = self._count_keyword_hits(items, EXTERNAL_NEWS_BEARISH_KEYWORDS)

        buy_score = float(bullish_hits)
        sell_score = float(bearish_hits)
        dominance = self._compute_dominance(buy_score, sell_score)

        impact_tag = "HIGH" if block_hits > 0 else ("MODERATE" if caution_hits > 0 else "LOW")
        risk_tag = "BLOCK" if impact_tag == "HIGH" else ("CAUTION" if impact_tag == "MODERATE" else "SAFE")

        panic_suspected = block_hits > 0 and bearish_hits >= bullish_hits
        if panic_suspected:
            risk_tag = "BLOCK"
            impact_tag = "HIGH"

        direction_bias = "NEUTRAL"
        if bullish_hits > bearish_hits:
            direction_bias = "BULLISH"
        elif bearish_hits > bullish_hits:
            direction_bias = "BEARISH"

        news_state = "QUIET"
        if dominance >= 0.65 and buy_score > 0:
            news_state = "RISK_ON"
        elif dominance <= -0.65 and sell_score > 0:
            news_state = "RISK_OFF"
        elif buy_score + sell_score >= 3:
            news_state = "MIXED"

        tags = [f"external_news_items_{len(items)}", f"external_news_impact_{impact_tag.lower()}"]
        if bullish_hits > 0:
            tags.append("external_news_bullish_gold")
        if bearish_hits > 0:
            tags.append("external_news_bearish_gold")
        if panic_suspected:
            tags.append("external_news_panic_suspected")

        summary = (
            f"ExternalNews ({symbol}) items={len(items)}, state={news_state}, impact={impact_tag}, "
            f"risk={risk_tag}, buy={buy_score:.2f}, sell={sell_score:.2f}, dominance={dominance:.2f}, "
            f"panic={panic_suspected}, block={block_hits}, caution={caution_hits}"
        )

        return ExternalNewsContext(
            enabled=True,
            feed_count=len(EXTERNAL_NEWS_FEEDS),
            impact_tag=impact_tag,
            risk_tag=risk_tag,
            direction_bias=direction_bias,
            news_state=news_state,
            buy_score=buy_score,
            sell_score=sell_score,
            dominance=dominance,
            panic_suspected=panic_suspected,
            tags=tags,
            summary=summary,
            headlines=headlines,
            items=items,
        )

    @staticmethod
    def _extract_text(node: ET.Element, tag_names: list[str]) -> str:
        for name in tag_names:
            child = node.find(name)
            if child is not None and child.text:
                return child.text.strip()
        return ""

    @staticmethod
    def _source_from_url(url: str) -> str:
        compact = url.lower()
        compact = re.sub(r"^https?://", "", compact)
        compact = compact.split("/", 1)[0]
        return compact

    @staticmethod
    def _parse_datetime(raw: str) -> datetime | None:
        value = (raw or "").strip()
        if not value:
            return None

        try:
            parsed = parsedate_to_datetime(value)
            if parsed.tzinfo is None:
                parsed = parsed.replace(tzinfo=timezone.utc)
            return parsed.astimezone(timezone.utc)
        except Exception:
            pass

        try:
            normalized = value.replace("Z", "+00:00")
            parsed = datetime.fromisoformat(normalized)
            if parsed.tzinfo is None:
                parsed = parsed.replace(tzinfo=timezone.utc)
            return parsed.astimezone(timezone.utc)
        except Exception:
            return None

    @staticmethod
    def _count_keyword_hits(items: list[dict[str, str]], keywords: list[str]) -> int:
        total = 0
        prepared = [keyword.strip().lower() for keyword in keywords if keyword.strip()]
        if not prepared:
            return 0
        for item in items:
            text = f"{item.get('title', '')} {item.get('summary', '')}".lower()
            if any(keyword in text for keyword in prepared):
                total += 1
        return total

    @staticmethod
    def _compute_dominance(buy_score: float, sell_score: float) -> float:
        total = buy_score + sell_score
        if total <= 0:
            return 0.0
        return max(-1.0, min(1.0, (buy_score - sell_score) / total))

    @staticmethod
    def _compact_whitespace(value: str) -> str:
        return re.sub(r"\s+", " ", value).strip()
