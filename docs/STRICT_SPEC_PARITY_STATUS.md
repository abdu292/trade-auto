# Strict Spec Parity Status (v4)

This document maps current implementation to the strict requirements in `spec/spec_v4_war_premium.md` for production go-live decisions.

## Current status summary

- **Implemented and working**
  - Switchable strategy profiles with active routing in decision engine (`Standard`, `WarPremium`).
  - Dedicated mode feed endpoint in AI worker (`POST /mode`) returning mode/confidence/keywords/ttl.
  - WarPremium decision rail support with stateful mode handling and kill-switch pathing.
  - Simulator profile mode (`strategyProfile`) with WarPremium-biased synthetic market behavior.
  - MT5 broker pending-order cancel control endpoint + EA consume/delete path.
  - Reserved pending cash guard to prevent over-allocation when queueing new orders.
  - Deterministic precedence-style gating in backend decision engine.
  - Waterfall high veto + pending cancel behavior.
  - Hazard-window expiry intersection veto.
  - 100g minimum sizing + spread-aware capacity guard.
  - TradingView webhook ingestion + persisted TV alert logs.
  - Telegram state influence and panic-suspected handling in decision flow.
  - Runtime monitoring endpoints for macro cache/hazards/telegram channels.
  - MT5 payload/response fields expanded for rail/cause/waterfall/session/size class.

- **Implemented now for committee live AI parity (Phase 1 start)**
  - AI worker now supports live **committee analyzer semantics** with configurable providers.
  - Live strategy target is `committee` with quorum threshold (`CONSENSUS_MIN_AGREEMENT`).
  - Brain enforces hard `NO_TRADE` when AI quorum fails (`AI_QUORUM_FAILED`).
  - Health endpoint exposes committee parity blockers for readiness checks.

- **Not yet full strict parity (must-do before claiming 100%)**
  - Full lid/shelf micro-structure proof from raw M1/M5 bars is still simplified via current signal flags.
  - Telegram de-esc/escalation burst logic exists but can be further hardened for strict threshold governance.
  - Cross-asset feed completeness (DXY/US10Y/real yields/XAG) remains partial.
  - Historical pattern overlay (`pattern_stats`) is not yet fully integrated into live scoring/rotation-cap logic.
  - Full backtest/replay report proving strict v4 success criteria is still pending.

## Production runtime profile (committee target)
- `AI_PROVIDER_MODE=committee-live`
- `AI_STRATEGY=committee`
- `CONSENSUS_MIN_AGREEMENT=2`
- `GROK_RUNTIME_TRANSPORT=openrouter`
- `OPENROUTER_API_KEY=<key>`
- `GROK_OPENROUTER_MODEL=x-ai/grok-4.1-fast`
- `OPENAI_API_KEY=<key>`
- `OPENAI_MODEL=gpt-4.1-mini`
- `GEMINI_API_KEY=<key>`
- `GEMINI_MODEL=gemini-2.0-flash`
- optional: `PERPLEXITY_API_KEY=<key>`
- optional: `PERPLEXITY_MODEL=sonar`

Optional Grok direct transport:
- `GROK_RUNTIME_TRANSPORT=direct`
- `GROK_API_KEY=<key>`
- `GROK_MODEL=grok-2-latest`

## Go-live gate checklist for strict parity claim

Before declaring "full strict spec parity":
1. AI health confirms committee live path (`AI_STRATEGY=committee`) with no parity blockers.
2. TradingView webhook alerts verified end-to-end in runtime and decision logs.
3. Hazard-window veto verified under active blocked window.
4. Macro cache switched from heuristic to external async macro feed (Perplexity-style enrichment).
5. Telegram channel universe expanded and weighted-learning loop validated.
6. Backtest/replay report shows required success criteria from section 17.

## Key purchase guidance

- For committee parity, **Grok API key + at least one additional model key are required**.
- Perplexity key remains recommended when upgrading macro cache from heuristic to external source quality.
