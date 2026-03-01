# Brain (.NET 10)

Clean Architecture backend with layers:

- `src/Domain`
- `src/Application`
- `src/Infrastructure`
- `src/Web`

## Highlights

- Minimal APIs grouped by feature endpoint modules.
- MediatR vertical slices (`Trades`, `Strategies`, `Sessions`, `Signals`, `Risk`).
- EF Core SQL Server with entity configurations and seed data.
- MT5-first runtime endpoints for snapshots, pending trades, and trade status callbacks.
- Hosted background services for session scheduling and signal polling.
- Serilog + ProblemDetails + health checks + OpenAPI.

## Run

```bash
dotnet restore
dotnet run --project src/Web/Web.csproj
```

## Endpoints

- `/api/trades/*`
- `/api/strategies/*`
- `/api/risk/*`
- `/api/sessions/*`
- `/api/signals/*`
- `/api/tradingview/webhook`
- `/api/tradingview/latest`
- `/api/monitoring/*`
- `/mt5/*` (protected by API key + optional IP allowlist)
- `/health`

## Strategy Profiles

- `Standard` is the default active profile from seed data.
- `WarPremium` enables war-expansion rails, stricter kill-switch behavior, and first-leg ban logic.
- Activate profile:
	- `GET /api/strategies`
	- `PUT /api/strategies/{id}/activate`

## War Mode Feed

- Backend consumes AI mode feed (`WAR_PREMIUM`, `DEESCALATION_RISK`, `UNKNOWN`) with confidence/TTL/keywords.
- If mode feed is unavailable, backend continues with local safety fallbacks (non-blocking).
- Strategy profile (`Standard`/`WarPremium`) does not change AI transport by itself; AI transport remains controlled by AI worker environment variables.
- AI worker defaults to OpenRouter transport (`GROK_FORCE_OPENROUTER=true`), so both profiles use the same Grok transport path unless explicitly disabled.

## Simulator

- Start simulator: `POST /api/monitoring/simulator/start`
- Request supports `strategyProfile`:
	- `Standard`
	- `WarPremium`
- Status endpoint `GET /api/monitoring/simulator/status` returns active simulator profile.

Example payload:

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

## Minimal Security (single-user)

`src/Web/appsettings.Development.json` includes local defaults:

- `Security:Enabled=true`
- `Security:ApiKeyHeaderName=X-API-Key`
- `Security:ApiKey=dev-local-change-me`
- `Security:AllowedIps=[127.0.0.1, ::1]`

For production, set `Security:ApiKey` via environment/secret store and avoid committing real keys.

## TradingView Webhook

- Configure `TradingView:WebhookSecret` in `src/Web/appsettings.Development.json` (or env/secret store in production).
- Send alerts from TradingView webhook with flexible payload fields: `symbol`, `timeframe`, `signal`, `bias`, `riskTag`, `score`, `volatility`, `timestamp`, `notes`.
- Latest TradingView signal is merged into decision alignment in polling flow and can strengthen or block trade eligibility.

## Telegram Notifications

- `INotificationService` now supports real Telegram delivery when configured.
- Configure either `External:Telegram:*` (preferred) or environment variables:
	- `TELEGRAM_BOT_TOKEN`
	- `TELEGRAM_NOTIFY_CHANNELS` (comma-separated `@channel` or `-100...` chat IDs)
- Outbound notifications use `TELEGRAM_NOTIFY_CHANNELS` only (no fallback to listener channels).
- Same Telegram bot token can be reused by listener and notifier services, as long as bot permissions are granted in each channel.
- If Telegram keys are missing, backend automatically falls back to mock notification feed.
