# Azure Production Setup (single App Service: Brain + AI Worker)

This runbook deploys `brain` (.NET) and `aiworker` (FastAPI) to **one Azure App Service** using a **single custom container**.

## Is one App Service possible?

Yes, but only with a custom container image.

- Standard App Service runtime stacks are single-runtime (for example, only .NET or only Python).
- This repo now includes a root `Dockerfile` and startup script that run both services in one Linux container:
  - `brain` listens on App Service port (`PORT`, default `8080`)
  - `aiworker` listens internally on `127.0.0.1:8001`
  - `brain` calls `aiworker` via `External__AIWorkerBaseUrl=http://127.0.0.1:8001`

## 1) Azure resources you create

1. **One Linux App Service** configured for **Container** deployment.
2. (Recommended) **Azure SQL** Server + Database for `brain` persistence.

Optional but recommended:
- Application Insights
- Key Vault (for secrets)

## 2) GitHub Actions workflow included

Workflow file:
- `.github/workflows/deploy-single-appservice.yml`

What it does:
1. Builds one image from root `Dockerfile`.
2. Pushes image to GHCR as:
   - `ghcr.io/<owner>/trade-auto-brain-ai:<commit-sha>`
   - `ghcr.io/<owner>/trade-auto-brain-ai:latest`
3. Deploys that image to your App Service using publish profile secret.

## 3) After you get the App Service publish profile

Download publish profile from Azure Portal:
- App Service -> **Overview** -> **Get publish profile**

Then configure GitHub repository secrets:

1. `AZURE_WEBAPP_NAME`
   - Value: your App Service name (example: `trade-auto`)
2. `AZURE_WEBAPP_PUBLISH_PROFILE`
   - Value: full XML content of downloaded publish profile file

## 4) GHCR image pull requirement

App Service must be able to pull your GHCR image.

### Option A (simplest): make package public

- In GitHub -> Packages -> `trade-auto-brain-ai` -> set visibility to **Public**.
- No registry credentials needed in App Service.

### Option B: keep package private

Set these App Service Application Settings:
- `DOCKER_REGISTRY_SERVER_URL=https://ghcr.io`
- `DOCKER_REGISTRY_SERVER_USERNAME=<github-username>`
- `DOCKER_REGISTRY_SERVER_PASSWORD=<github-personal-access-token-with-read:packages>`

## 5) App Service application settings

Set these on the **same** App Service (Configuration -> Application settings):

### Core runtime
- `WEBSITES_PORT=8080`
- `PORT=8080`
- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://0.0.0.0:8080`
- `External__AIWorkerBaseUrl=http://127.0.0.1:8001`
- `PYTHONUNBUFFERED=1`

### Security
- `Security__Enabled=true`
- `Security__ApiKeyHeaderName=X-API-Key`
- `Security__ApiKey=<strong-random-key>`

### Data
- `ConnectionStrings__DefaultConnection=<azure-sql-connection-string>`

Azure SQL connection string template:

`Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;Persist Security Info=False;User ID=<user>;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`

### Trading safety
- `Trading__ExecutionMode=ManualOnly`

### AI committee (worker env in same container)
- `AI_STRATEGY=committee`
- `CONSENSUS_MIN_AGREEMENT=2`
- `CONSENSUS_ENTRY_TOLERANCE_PCT=0.003`

### Provider keys/models (set at least one provider)
- `OPENROUTER_API_KEY=<key>`
- `GROK_FORCE_OPENROUTER=true`
- `GROK_RUNTIME_TRANSPORT=openrouter`
- `GROK_OPENROUTER_MODEL=x-ai/grok-4-fast`
- Optional: `OPENAI_API_KEY`, `OPENAI_MODEL`, `GEMINI_API_KEY`, `GEMINI_MODEL`, `PERPLEXITY_API_KEY`, `PERPLEXITY_MODEL`

### News layers (optional)
- `TELEGRAM_READ_MODE=bot`
- `TELEGRAM_BOT_TOKEN=<token>`
- `TELEGRAM_LISTEN_CHANNELS=<csv>`
- `TELEGRAM_NOTIFY_CHANNELS=<csv>`
- `EXTERNAL_NEWS_ENABLED=true`
- `EXTERNAL_NEWS_FEEDS=<csv-of-rss-urls>`
- `EXTERNAL_NEWS_LOOKBACK_MINUTES=360`
- `EXTERNAL_NEWS_MAX_ITEMS=30`

## 6) Deploy procedure

1. Push changes to `main` (or run workflow manually from Actions tab).
2. Wait for `Deploy Brain + AI Worker to single App Service` workflow to pass.
3. In Azure Portal, restart App Service once after first deployment.
4. Confirm container is running from App Service log stream.

## 7) Verification checklist

Run against your one App Service URL `https://trade-auto.azurewebsites.net`:

1. `GET /health`
2. `GET /api/monitoring/ai-health`
3. `GET /api/monitoring/runtime-status`
4. `POST /api/monitoring/simulator/start`
5. `GET /api/monitoring/approvals`
6. `GET /api/strategies` (expect `Standard` and `WarPremium`)

## 8) Notes and limitations

- This single-App-Service design is cost-efficient and simple to manage.
- It is less isolated than separate services; a container restart affects both `brain` and `aiworker`.
- If you need independent scaling, uptime isolation, or separate network controls, split back to two App Services.
