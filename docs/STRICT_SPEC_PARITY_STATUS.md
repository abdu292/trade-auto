# Strict Spec Parity Status (v3)

This document maps current implementation to the strict requirements in `spec/spec_v3.md` for production go-live decisions.

## Current status summary

- **Implemented and working**
  - Deterministic precedence-style gating in backend decision engine.
  - Waterfall high veto + pending cancel behavior.
  - Hazard-window expiry intersection veto.
  - 100g minimum sizing + spread-aware capacity guard.
  - TradingView webhook ingestion + persisted TV alert logs.
  - Telegram state influence and panic-suspected handling in decision flow.
  - Runtime monitoring endpoints for macro cache/hazards/telegram channels.
  - MT5 payload/response fields expanded for rail/cause/waterfall/session/size class.

- **Implemented now for strict live AI parity**
  - AI worker live runtime is now fixed to **Grok-only analyzer semantics**.
  - Live strategy is fixed to `single` (no committee in live path).
  - Grok transport supports OpenRouter now, with optional direct xAI later.
  - Health endpoint exposes parity blockers for readiness checks.

- **Not yet full strict parity (must-do before claiming 100%)**
  - Macro/institutional cache source is currently heuristic, not full Perplexity-backed external macro feed.
  - Full 60+ Telegram listener/registry learning lifecycle still requires wider channel onboarding and production tuning.
  - Cross-asset feed completeness (DXY/US10Y/real yields/XAG) is still partial.
  - Historical pattern overlay (`pattern_stats`) is not fully integrated into live scoring/rotation-cap logic as strict spec intends.
  - Backtest/replay harness proving strict success criteria is not fully delivered.

## Production runtime profile (spec-aligned)
- `AI_PROVIDER_MODE=grok-only` (internal fixed runtime value)
- `GROK_RUNTIME_TRANSPORT=openrouter`
- `OPENROUTER_API_KEY=<key>`
- `GROK_OPENROUTER_MODEL=x-ai/grok-2-latest`

Optional later:
- `GROK_RUNTIME_TRANSPORT=direct`
- `GROK_API_KEY=<key>`
- `GROK_MODEL=grok-2-latest`

## Go-live gate checklist for strict parity claim

Before declaring "full strict spec parity":
1. AI health confirms Grok-only live path with no parity blockers.
2. TradingView webhook alerts verified end-to-end in runtime and decision logs.
3. Hazard-window veto verified under active blocked window.
4. Macro cache switched from heuristic to external async macro feed (Perplexity-style enrichment).
5. Telegram channel universe expanded and weighted-learning loop validated.
6. Backtest/replay report shows required success criteria from section 17.

## Key purchase guidance

- For strict parity, **Grok API key is mandatory**.
- Perplexity key is recommended when upgrading macro cache from heuristic to external source quality.
