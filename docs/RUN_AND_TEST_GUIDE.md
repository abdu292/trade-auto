# Run and Test Guide (Simple, Swagger-First)

This is the fastest way to run the full stack locally and test everything without PowerShell API scripts.

## 1) Start everything together

From repo root:

`./start-local.ps1`

What this script does:
- Starts Brain backend (`brain/src/Web/Web.csproj`) on port `5000`
- Starts AI Worker (`aiworker`) on port `8001`
- Prints health check results for both services

If ports are already busy, stop existing processes and run again.

## 2) Open Swagger UIs

- Brain Swagger: `http://127.0.0.1:5000/swagger`
- AI Worker Swagger: `http://127.0.0.1:8001/docs`

Use Swagger UI for all API testing below.

## 3) Minimal backend checks (Brain Swagger)

In `brain` Swagger:

1. `GET /health` → expect `200`
2. `GET /api/strategies` → expect `Standard` and `WarPremium`
3. Activate `WarPremium`:
   - Copy `id` for `WarPremium` from previous call
   - Run `PUT /api/strategies/{id}/activate`
4. `GET /api/strategies` again → `WarPremium` should be active

## 4) AI mode check (AI Worker Swagger)

In `aiworker` Swagger:

1. Open `POST /mode`
2. Use sample payload:

```json
{
  "symbol": "XAUUSD",
  "session": "LONDON",
  "telegramState": "BUY",
  "telegramImpactTag": "MODERATE",
  "isExpansion": true,
  "hasImpulseCandles": true,
  "hasPanicDropSequence": false,
  "tvAlertType": "LID_BREAK"
}
```

3. Expect JSON with:
- `mode` (`WAR_PREMIUM` or other)
- `confidence`
- `keywords`
- `ttlSeconds`

## 5) Simulator check (Brain Swagger)

In `brain` Swagger:

1. `POST /api/monitoring/simulator/start`
2. Body:

```json
{
  "startPrice": 2890,
  "volatilityUsd": 0.45,
  "baseSpread": 0.18,
  "intervalSeconds": 5,
  "enableShockEvents": true,
  "strategyProfile": "WarPremium"
}
```

3. `GET /api/monitoring/simulator/status`
- verify `isRunning = true`
- verify `strategyProfile = WARPREMIUM`

4. `POST /api/monitoring/simulator/stop`

## 6) UI check (mobile)

Run UI from `ui`:

`flutter run`

Then in app:
- Ensure Environment = `Production` (default)
- Switch to `Local` when testing against local backend
- Open Strategy screen and switch between `Standard` and `WarPremium`
- Verify active profile updates

## 7) Telegram ingestion check

AI Worker reads channels from `TELEGRAM_LISTEN_CHANNELS` (or `TELEGRAM_CHANNELS`) in environment.

- Ensure Telegram is configured (`TELEGRAM_READ_MODE`, session/token vars)
- Call AI Worker `GET /health` and inspect Telegram-related fields
- Call `POST /mode` and `POST /analyze` while channels are active

## 8) Pre-deployment quick gate

Before deploy:
- Brain and AI Worker both healthy
- Strategy activation works in Swagger
- `/mode` returns structured data
- Simulator works with `WarPremium`
- UI can switch environments and strategies

For Azure deployment details, use:
- `docs/AZURE_PRODUCTION_SETUP.md`
