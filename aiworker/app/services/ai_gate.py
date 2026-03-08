"""
AI Gate (CR9) — hard pre-flight check before any LLM is invoked.

The gate enforces:
  1. Data freshness  — snapshot must not be older than AI_GATE_MAX_DATA_AGE_SECONDS.
  2. Risk state      — AI is skipped when the deterministic risk classifier says BLOCK.
  3. Session budget  — per-session call counter; resets automatically on session change.

Usage::

    gate = AiGate()
    blocked, reason = gate.check(snapshot, risk_state)
    if blocked:
        return _build_no_trade(reason)
    gate.record_call(snapshot.session)   # call after each AI invocation

"""
from __future__ import annotations

import asyncio
import logging
from dataclasses import dataclass, field
from datetime import datetime, timezone

from app.ai.config import (
    AI_GATE_ENABLED,
    AI_GATE_MAX_DATA_AGE_SECONDS,
    AI_SESSION_MAX_CALLS,
)

logger = logging.getLogger(__name__)


@dataclass
class _SessionBudget:
    session_name: str = ""
    calls_used: int = 0


_budget: _SessionBudget = _SessionBudget()
_budget_lock: asyncio.Lock | None = None


def _get_budget_lock() -> asyncio.Lock:
    """Return (and lazily create) the asyncio.Lock for the budget state.

    The lock must be created inside a running event loop, so we defer
    creation to first use rather than at module import time.
    """
    global _budget_lock
    if _budget_lock is None:
        _budget_lock = asyncio.Lock()
    return _budget_lock


class AiGate:
    """Singleton-friendly gate; one instance per AnalyzerService is fine."""

    # ── public interface ──────────────────────────────────────────────────────

    async def check_async(
        self,
        snapshot_timestamp: datetime,
        session: str,
        risk_state: str,
        cycle_id: str | None = None,
    ) -> tuple[bool, str]:
        """Async version of check() — safe to call from async context.

        Returns (blocked, reason).  When blocked is True the caller must not
        invoke any LLM and should return NO_TRADE without spending tokens.

        cycle_id is used to detect replay mode: when the id starts with
        ``replay_`` the system-time freshness check is skipped because the
        snapshot timestamp is intentionally historical.
        """
        if not AI_GATE_ENABLED:
            return False, ""

        freshness_reason = self._check_freshness(snapshot_timestamp, cycle_id=cycle_id)
        if freshness_reason:
            logger.info("AI gate BLOCKED (stale data): %s", freshness_reason)
            return True, freshness_reason

        if risk_state == "BLOCK":
            return True, "AI_GATE_RISK_BLOCK"

        async with _get_budget_lock():
            budget_reason = self._check_budget_locked(session)
        if budget_reason:
            logger.info("AI gate BLOCKED (session budget): %s", budget_reason)
            return True, budget_reason

        return False, ""

    # Keep a synchronous shim for backward-compatibility / testing.
    def check(
        self,
        snapshot_timestamp: datetime,
        session: str,
        risk_state: str,
        cycle_id: str | None = None,
    ) -> tuple[bool, str]:
        """Synchronous version — use only in tests or non-async contexts.

        cycle_id is used to detect replay mode: when the id starts with
        ``replay_`` the system-time freshness check is skipped.
        """
        if not AI_GATE_ENABLED:
            return False, ""

        freshness_reason = self._check_freshness(snapshot_timestamp, cycle_id=cycle_id)
        if freshness_reason:
            return True, freshness_reason

        if risk_state == "BLOCK":
            return True, "AI_GATE_RISK_BLOCK"

        budget_reason = self._check_budget_locked(session)
        if budget_reason:
            return True, budget_reason

        return False, ""

    async def record_call_async(self, session: str) -> None:
        """Increment the call counter for the current session (async-safe)."""
        if not AI_GATE_ENABLED:
            return
        async with _get_budget_lock():
            _budget.session_name = (session or "").upper()
            _budget.calls_used += 1
        logger.debug(
            "AI gate: session=%s calls=%d/%d",
            _budget.session_name,
            _budget.calls_used,
            AI_SESSION_MAX_CALLS,
        )

    def record_call(self, session: str) -> None:
        """Synchronous shim — use only in tests or non-async contexts."""
        if not AI_GATE_ENABLED:
            return
        _budget.session_name = (session or "").upper()
        _budget.calls_used += 1

    def reset_session(self, session: str) -> None:
        """Force-reset the budget for a new session (synchronous, for tests)."""
        _budget.session_name = (session or "").upper()
        _budget.calls_used = 0

    # ── private helpers ───────────────────────────────────────────────────────

    @staticmethod
    def _check_freshness(snapshot_timestamp: datetime, cycle_id: str | None = None) -> str:
        """Return a non-empty reason string if the snapshot is too old.

        In replay mode (cycle_id starts with ``replay_``) the snapshot
        timestamp is intentionally historical so the system-time comparison
        is skipped.  Use cycle-relative freshness only: the snapshot is
        considered fresh as long as it was produced by this replay cycle.
        """
        # Replay mode: skip system-time comparison — data is deliberately historical.
        if cycle_id and cycle_id.lower().startswith("replay_"):
            return ""

        max_age = max(0.0, AI_GATE_MAX_DATA_AGE_SECONDS)
        if max_age <= 0:
            return ""

        now_utc = datetime.now(timezone.utc)
        ts = snapshot_timestamp
        if ts.tzinfo is None:
            ts = ts.replace(tzinfo=timezone.utc)

        age_seconds = (now_utc - ts).total_seconds()
        if age_seconds > max_age:
            return (
                f"AI_GATE_STALE_DATA: snapshot is {age_seconds:.0f}s old "
                f"(max={max_age:.0f}s)"
            )
        return ""

    @staticmethod
    def _check_budget_locked(session: str) -> str:
        """Check session budget — caller must hold _budget_lock (or be in sync context)."""
        max_calls = max(0, AI_SESSION_MAX_CALLS)
        if max_calls <= 0:
            return ""

        session_upper = (session or "").upper()

        # Session transition → reset counter
        if session_upper and session_upper != _budget.session_name:
            logger.info(
                "AI gate: session changed %s → %s; resetting call budget",
                _budget.session_name,
                session_upper,
            )
            _budget.session_name = session_upper
            _budget.calls_used = 0

        if _budget.calls_used >= max_calls:
            return (
                f"AI_GATE_SESSION_BUDGET_EXHAUSTED: {_budget.calls_used}/{max_calls} "
                f"calls used in session {_budget.session_name}"
            )
        return ""
