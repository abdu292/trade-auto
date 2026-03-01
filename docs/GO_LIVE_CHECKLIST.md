# Go-Live Checklist (Standard + WarPremium)

Use this checklist to ensure the full stack is ready and predictable before live operation.

## 1) Environment & Keys

### Brain (.NET)
- `ASPNETCORE_ENVIRONMENT=Development` for local testing.
- `ConnectionStrings__DefaultConnection` (or default LocalDB) is valid.
- `Security:ApiKey` is set and matches MT5 EA `BrainApiKey`.

### AI Worker (FastAPI)
- Required baseline:
  - `AI_STRATEGY=committee`
  - `CONSENSUS_MIN_AGREEMENT=2`
- Grok transport (default path):
  - `GROK_RUNTIME_TRANSPORT=openrouter`
  - `OPENROUTER_API_KEY=<your_key>`
  - `GROK_OPENROUTER_MODEL=x-ai/grok-4.1-fast`
- Additional analyzers (recommended):
  - `OPENAI_API_KEY`, `OPENAI_MODEL`
  - `GEMINI_API_KEY`, `GEMINI_MODEL`
  - Optional `PERPLEXITY_API_KEY`, `PERPLEXITY_MODEL`

### MT5 EA
- `BrainBaseUrl` points to backend host (example `http://127.0.0.1:5000`).
- `BrainApiKey` matches backend API key.
- MT5 terminal allows WebRequest for backend URL.

## 2) Start Order

1. Start backend (`brain`).
2. Start AI worker (`aiworker`) on port `8001`.
3. Start Flutter UI (`ui`).
4. Attach EA to `XAUUSD` chart and enable AutoTrading.

## 3) Profile & Mode Verification

1. Open UI `StrategyControlScreen`.
2. Confirm active profile (`Standard` by default unless explicitly changed).
3. Use Quick Mode Switch to test both profiles:
   - Activate `Standard`
   - Activate `WarPremium`
4. Validate backend state via `GET /api/strategies`.

## 4) Mode Feed Verification

- Call AI worker `POST /mode` and confirm payload includes:
  - `mode`
  - `confidence`
  - `keywords`
  - `ttlSeconds`
- Confirm backend polling loop is consuming mode hints (runtime logs show mode transitions / mode feed records).

## 5) Simulator Verification (Both Profiles)

Run twice using `/api/monitoring/simulator/start`:
- `strategyProfile: "Standard"`
- `strategyProfile: "WarPremium"`

Then verify with `GET /api/monitoring/simulator/status`:
- `strategyProfile` reflects selected profile.
- WarPremium shows more aggressive volatility/shock behavior than Standard.

## 6) Safety Controls Verification

- Trigger a high-risk scenario and confirm:
  - Pending queue is cleared in backend.
  - Cancel-pending control is published for EA.
  - EA consumes `/mt5/control/cancel-pending/consume` and deletes pending buy orders.

## 7) Operational Rules

- Use `Standard` as baseline default.
- Use `WarPremium` only during true war-expansion conditions.
- If mode flips to de-escalation or waterfall risk rises, do not force manual re-arming until structure re-forms.

## 8) Recommended Daily Pre-Session Checks

- `GET /health` for backend and AI worker.
- `GET /api/monitoring/runtime` for strategy/mode/session visibility.
- Confirm Telegram and TradingView feeds are updating.
- Confirm no stale pending orders remain from previous session.

## 9) If Something Fails

- Port conflict: free ports `5000` and `8001`, then restart services.
- AI endpoint unavailable: backend continues with local fallbacks, but do not trust war-mode transitions until AI worker is healthy.
- Strategy mismatch: re-activate desired profile from UI and re-check `/api/strategies`.

## 10) Final Go/No-Go Gate

Go live only if all are true:
- Backend + AI worker healthy
- Profile switch works from UI
- Mode feed returns valid structured payload
- Simulator profile behavior verified
- EA control-plane cancel path verified
- No parity blockers you consider critical for your session risk tolerance
