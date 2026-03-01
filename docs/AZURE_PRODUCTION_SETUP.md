# Azure Production Setup (two App Services: Brain + AI Worker)

This runbook deploys `brain` (.NET) and `aiworker` (FastAPI) as two separate Azure App Services, with Azure SQL for persistence.

## 1) Architecture

- App Service A: `brain` (public API used by mobile app + EA)
- App Service B: `aiworker` (AI analysis service)
- `brain` calls `aiworker` over HTTPS via configured base URL.
- Azure SQL is used by `brain`.

Recommended endpoint pattern:
- Brain: `https://<brain-app>.azurewebsites.net`
- AI Worker: `https://<aiworker-app>.azurewebsites.net`

## 2) Azure SQL

1. Create Azure SQL Server + Database.
2. Allow Azure services and/or Brain App Service outbound IPs in SQL firewall.
3. Use connection string:

`Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;Persist Security Info=False;User ID=<user>;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`

4. Set this on Brain App Service:
- `ConnectionStrings__DefaultConnection=<azure-sql-connection-string>`

## 3) Brain App Service settings

Set these in Brain App Service Configuration:

### Core backend
- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://0.0.0.0:8080`
- `WEBSITES_PORT=8080`
- `External__AIWorkerBaseUrl=https://<aiworker-app>.azurewebsites.net`
- `Security__Enabled=true`
- `Security__ApiKeyHeaderName=X-API-Key`
- `Security__ApiKey=<strong-random-key>`
- `ConnectionStrings__DefaultConnection=<azure-sql-connection-string>`

### Execution/safety
- `Trading__ExecutionMode=ManualOnly`

## 4) AI Worker App Service settings

Set these in AI Worker App Service Configuration:

### Runtime
- `PYTHONUNBUFFERED=1`
- `PORT=8001` (if your startup command uses this port)

### AI committee
- `AI_STRATEGY=committee`
- `CONSENSUS_MIN_AGREEMENT=2`
- `CONSENSUS_ENTRY_TOLERANCE_PCT=0.003`

### Provider keys/models (set at least one)
- `OPENROUTER_API_KEY=<key>`
- `GROK_FORCE_OPENROUTER=true`
- `GROK_RUNTIME_TRANSPORT=openrouter`
- `GROK_OPENROUTER_MODEL=x-ai/grok-4-fast`
- Optional: `OPENAI_API_KEY`, `OPENAI_MODEL`, `GEMINI_API_KEY`, `GEMINI_MODEL`, `PERPLEXITY_API_KEY`, `PERPLEXITY_MODEL`

### Telegram news context (optional but recommended)
- `TELEGRAM_READ_MODE=bot`
- `TELEGRAM_BOT_TOKEN=<token>`
- `TELEGRAM_LISTEN_CHANNELS=<csv>`
- `TELEGRAM_NOTIFY_CHANNELS=<csv>`

### External RSS news layer (optional but recommended)
- `EXTERNAL_NEWS_ENABLED=true`
- `EXTERNAL_NEWS_FEEDS=<csv-of-rss-urls>`
- `EXTERNAL_NEWS_LOOKBACK_MINUTES=360`
- `EXTERNAL_NEWS_MAX_ITEMS=30`

## 5) Deploy flow

1. Deploy Brain App Service from `brain/src/Web`.
2. Deploy AI Worker App Service from `aiworker`.
3. Set AI Worker settings and verify `GET /health` on AI Worker URL.
4. Set Brain settings (especially `External__AIWorkerBaseUrl`) and restart Brain.
5. Verify Brain can reach AI Worker through `/api/monitoring/ai-health`.

## 6) Post-deploy verification

1. Brain: `GET /health`
2. AI Worker: `GET /health`
3. Brain: `GET /api/monitoring/ai-health` returns healthy payload.
4. Brain: `GET /api/monitoring/runtime-status` shows expected state.
5. Brain: `POST /api/monitoring/simulator/start` then verify pending approvals via `GET /api/monitoring/approvals`.
6. Brain: `GET /api/strategies` shows `Standard` and `WarPremium`.

## 7) Security and ops checklist

- Store secrets in Key Vault and use App Service Key Vault references.
- Restrict AI Worker ingress (IP restrictions/private endpoint) so only Brain can call it.
- Keep `Security__ApiKey` enabled on Brain public API.
- Rotate API keys regularly.
- Enable Application Insights for both App Services.
- Configure alerts for:
	- Brain `GET /health` failures
	- AI Worker `GET /health` failures
	- Brain `GET /api/monitoring/ai-health` failures
	- sustained `AI_QUORUM_FAILED` spikes
