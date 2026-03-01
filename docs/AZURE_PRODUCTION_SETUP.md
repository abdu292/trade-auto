# Azure Production Setup (App Service + Azure SQL)

This runbook is for deploying the backend to Azure App Service, database to Azure SQL, and mobile app with Production as default.

## 1) Azure SQL

1. Create Azure SQL Server + Database.
2. Add firewall rules for App Service outbound access.
3. Prepare connection string in Azure format:

`Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;Persist Security Info=False;User ID=<user>;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`

4. Set in App Service configuration as:
- `ConnectionStrings__DefaultConnection`

## 2) App Service (Brain backend)

Set application settings:
- `ASPNETCORE_ENVIRONMENT=Production`
- `Security__Enabled=true`
- `Security__ApiKeyHeaderName=X-API-Key`
- `Security__ApiKey=<strong-random-key>`
- `ConnectionStrings__DefaultConnection=<azure-sql-connection-string>`

Deploy backend from CI/CD or zip deploy.

## 3) AI Worker deployment

Deploy AI worker separately (App Service/container/VM).

Key settings:
- `AI_STRATEGY=committee`
- `CONSENSUS_MIN_AGREEMENT=2`
- `GROK_FORCE_OPENROUTER=true`
- `GROK_RUNTIME_TRANSPORT=openrouter`
- `OPENROUTER_API_KEY=<key>`
- `GROK_OPENROUTER_MODEL=x-ai/grok-4.1-fast`
- Optional analyzers: OpenAI, Gemini, Perplexity keys/models

## 4) Mobile app environment defaults

Build normally:

`flutter build apk`

In app:
- Production remains default.
- User can switch to Local using API environment switcher.
- URLs are configured in app code (Production + Local) and switched by environment selection.

## 5) Post-deploy verification

1. `GET /health` for Brain.
2. `GET /api/strategies` confirms `Standard` and `WarPremium` profiles.
3. `POST /mode` on AI worker returns structured mode payload.
4. `POST /api/monitoring/simulator/start` with `strategyProfile: "WarPremium"` works.

## 6) Security recommendations

- Rotate API keys regularly.
- Restrict App Service inbound access with WAF/IP restrictions if possible.
- Store secrets in Key Vault and reference them from App Service settings.
- Do not ship local/dev keys in app binaries.
