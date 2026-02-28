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
