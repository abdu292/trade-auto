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
- Mock adapters for AI worker, MT5 bridge, notification, market data.
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
- `/mt5/*` (protected by API key + optional IP allowlist)
- `/health`

## Minimal Security (single-user)

`src/Web/appsettings.Development.json` includes local defaults:

- `Security:Enabled=true`
- `Security:ApiKeyHeaderName=X-API-Key`
- `Security:ApiKey=dev-local-change-me`
- `Security:AllowedIps=[127.0.0.1, ::1]`

For production, set `Security:ApiKey` via environment/secret store and avoid committing real keys.
