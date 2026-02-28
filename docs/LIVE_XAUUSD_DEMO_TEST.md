# Live XAUUSD Demo Test (Production-Style)

This is the only test guide to run the system exactly as real trading, but on your MT5 demo account.

## 1) What this validates
- Real MT5 ticks from demo account.
- Real EA posting snapshots to backend.
- Real backend/aiworker decisions.
- Real pending orders placed on MT5 demo account.
- Real BUY trigger and TP callbacks.
- Real ledger + slips update from callbacks.

## 2) Required setup
- MT5 desktop logged into demo account.
- XAUUSD chart open.
- EA compiled and attached from `mt5ea/ExpertAdvisor.mq5`.
- Backend + aiworker running.
- Flutter app running.
- TradingView webhook enabled (optional but recommended for spec behavior).

AI runtime note (current):
- Live decision engine is Grok-only semantics.
- Current transport can be OpenRouter (`GROK_RUNTIME_TRANSPORT=openrouter`) with Grok model id (example: `x-ai/grok-2-latest`).

## 3) Start services
From repo root:

```powershell
.\start-local.ps1
```

Health checks:

```powershell
Invoke-RestMethod http://127.0.0.1:5000/health
Invoke-RestMethod http://127.0.0.1:8001/health
```

## 4) MT5 EA settings (must match)
Attach EA to XAUUSD and set:
- `BrainBaseUrl = http://127.0.0.1:5000`
- `BrainApiKey = dev-local-change-me`
- `SnapshotPushSeconds = 5`
- `PollTradeSeconds = 2`

MT5 options:
- Enable AutoTrading.
- Allow WebRequest for:
  - `http://127.0.0.1:5000`
  - `http://localhost:5000`

## 5) TradingView webhook (recommended)

### 5.1 Enable webhook in backend
1. Open `brain/src/Web/appsettings.Development.json`.
2. Set `TradingView:WebhookSecret` to a strong value (example: `tv-local-2026`).
3. Restart backend after saving.

If `TradingView:WebhookSecret` is empty, webhook works without secret (not recommended).

### 5.2 Create TradingView alert
In TradingView for your XAUUSD chart:
1. Click **Alert**.
2. Choose your condition (indicator/signal).
3. Set trigger frequency (recommended: once per bar close).
4. Enable **Webhook URL** and set:
   - `http://127.0.0.1:5000/api/tradingview/webhook`
5. Set alert message JSON to:

```json
{
  "secret": "tv-local-2026",
  "symbol": "XAUUSD",
  "timeframe": "M15",
  "signal": "BUY",
  "confirmationTag": "CONFIRM",
  "bias": "BULLISH",
  "riskTag": "SAFE",
  "score": 0.72,
  "volatility": 0.35,
  "source": "TRADINGVIEW",
  "notes": "rsi+ema"
}
```

### 5.3 Verify webhook is received
- Open `http://127.0.0.1:5000/swagger`.
- Run `GET /api/tradingview/latest`.
- Confirm latest signal reflects your alert.

## 6) Real-time test flow
1. Let EA run for 2-5 minutes on XAUUSD.
2. Watch backend logs for snapshot ingestion and decision cycle.
3. Watch MT5 logs for pending trade pulls and order placement.
4. Confirm demo account pending order appears in MT5.
5. On trigger/TP, verify callbacks update ledger and slips.

## 7) Verify from UI
- Dashboard:
  - Health = healthy
  - Runtime card updates (Bid/Ask/Spread/Session/Queue)
  - Ledger updates after fills
- Trades:
  - Active trades
  - Recent signals
  - Runtime telemetry

## 7.1 Verify backend runtime modules (Swagger manual test)

Open Swagger:
- `http://127.0.0.1:5000/swagger`

Run these endpoints using **Try it out**:

1. `GET /api/monitoring/runtime`
  - Verify runtime object returns `session`, `bid`, `ask`, `spread`, `pendingQueueDepth`, and `activeBlockedHazardWindows`.

2. `GET /api/monitoring/macro-cache`
  - Verify macro cache fields are present (`macroBias`, `institutionalBias`, `cacheAgeMinutes`).

3. `GET /api/monitoring/hazard-windows`
  - Verify list is returned (empty or with items).

4. `POST /api/monitoring/hazard-windows`
  - Body example:

```json
{
  "title": "NFP Test Window",
  "category": "MACRO",
  "startUtc": "2026-02-28T10:00:00Z",
  "endUtc": "2026-02-28T10:30:00Z",
  "isBlocked": true
}
```

  - Then run `GET /api/monitoring/runtime` again.
  - Expected: `activeBlockedHazardWindows` increases when current time is inside that window.
  - Expected behavior: new trades should be vetoed if their expiry intersects an active blocked hazard window.

5. `GET /api/monitoring/telegram-channels`
  - Verify channel registry and weights are returned.

6. Optional TradingView manual test from Swagger
  - Run `POST /api/tradingview/webhook` with the same JSON payload used in TradingView.
  - Then run `GET /api/tradingview/latest` and confirm the payload is stored.

## 7.2 Weekend / market-closed realistic simulation (Swagger)

When MT5 market is closed (Saturday/Sunday), use simulator endpoints that feed the same snapshot path used by the live signal loop.

Open Swagger:
- `http://127.0.0.1:5000/swagger`

Run:

1. `POST /api/monitoring/simulator/start`
  - Body example:

```json
{
  "startPrice": 2890.0,
  "volatilityUsd": 0.45,
  "baseSpread": 0.18,
  "intervalSeconds": 5,
  "sessionOverride": "LONDON",
  "enableShockEvents": true
}
```

2. Wait 15-30 seconds, then run:
  - `GET /api/monitoring/runtime`
  - `GET /api/monitoring/simulator/status`

3. Verify runtime fields change over time (`bid`, `ask`, `spread`, `session`) and decision loop keeps running.

4. Optional one-tick generation:
  - `POST /api/monitoring/simulator/step`

5. Stop simulator when done:
  - `POST /api/monitoring/simulator/stop`

Important:
- This is realistic for control-flow testing (same backend ingestion + gating path as MT5 snapshot store).
- It is not a broker-execution substitute for final slippage/liquidity behavior; do final acceptance again with live MT5 market open.

## 8) Hard pass criteria
- Only XAUUSD trades created.
- Only BUY_LIMIT/BUY_STOP pending orders.
- No order under 100g.
- High waterfall context produces `NO_TRADE` (or capital protected state).
- Active blocked hazard windows veto intersecting expiries.
- BUY/TP callbacks update ledger consistently.
- Macro cache refreshes every 30 minutes asynchronously.

## 9) If orders do not place
- Recheck MT5 WebRequest allowlist.
- Recheck EA `BrainBaseUrl` and API key.
- Confirm backend reachable at `127.0.0.1:5000`.
- Ensure EA is attached to XAUUSD chart (not other symbol).

## 10) Keys and provider guidance
Current best practical setup:
- Keep Grok-only semantics.
- If using OpenRouter transport now:
  - `GROK_RUNTIME_TRANSPORT=openrouter`
  - `OPENROUTER_API_KEY=<key>`
  - `GROK_OPENROUTER_MODEL=x-ai/grok-2-latest`
- If using direct xAI transport later:
  - `GROK_RUNTIME_TRANSPORT=direct`
  - `GROK_API_KEY=<key>`
  - `GROK_MODEL=grok-2-latest`

When to buy direct Grok credits:
- Buy direct Grok credits when you want transport independence from OpenRouter and tighter vendor-level control.

Perplexity/Gemini direct keys:
- Not mandatory for your immediate live demo test.
- Add later when implementing full macro cache + multi-provider fallback in production.
