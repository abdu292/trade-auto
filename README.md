# Trade Auto Platform

Monorepo for a multi-project automation platform:

	- `aiworker` - FastAPI AI orchestration service

See each project README for run instructions.

## Structure

- `brain` - ASP.NET Core .NET 10 Clean Architecture service
- `aiworker` - FastAPI AI signal worker
- `ui` - Flutter (Riverpod + Dio)
- `mt5ea` - MQL5 Expert Advisor skeleton

## Local Quick Start

1. Run all backend services with `./start-local.ps1`.
2. Open Swagger and follow `docs/RUN_AND_TEST_GUIDE.md`.
3. Run Flutter UI with production/local defines (see `ui/README.md`).
4. Point MT5 EA `BrainBaseUrl` to backend host.

### Database migrations
A handy helper script lives at the repository root:

```powershell
# from workspace root
.\add-migration.ps1 -Name "MyMigrationName"
```

This executes `dotnet ef migrations add` using the Infrastructure project as the
motivation and Web as the startup project; new files appear under
`brain/src/Infrastructure/Data/Migrations`.

## Documentation

- **Start Here (Run + Test)**: [docs/RUN_AND_TEST_GUIDE.md](docs/RUN_AND_TEST_GUIDE.md)
- **Live Demo Test (XAUUSD)**: [docs/LIVE_XAUUSD_DEMO_TEST.md](docs/LIVE_XAUUSD_DEMO_TEST.md)
- **AI Provider Decision**: [docs/AI_PROVIDER_DECISION.md](docs/AI_PROVIDER_DECISION.md)
- **Spec (authoritative)**: [spec/00_instructions](spec/00_instructions) and Physical Gold Final Pack (spec 01–14 under `spec/`). Integration Map: [spec/arch_diagram.jpeg](spec/arch_diagram.jpeg).
- **Architecture (client view)**: [docs/ARCHITECTURE_E2E_CLIENT.md](docs/ARCHITECTURE_E2E_CLIENT.md)
- **Strict Parity Status**: [docs/STRICT_SPEC_PARITY_STATUS.md](docs/STRICT_SPEC_PARITY_STATUS.md)
- **Trader Mode Guide**: [docs/TRADER_MODE_GUIDE.md](docs/TRADER_MODE_GUIDE.md)
- **Go-Live Checklist**: [docs/GO_LIVE_CHECKLIST.md](docs/GO_LIVE_CHECKLIST.md)
- **Azure Production Setup**: [docs/AZURE_PRODUCTION_SETUP.md](docs/AZURE_PRODUCTION_SETUP.md)

## Strategy Profiles

- Active profile is selected in backend strategy profiles and applied by the decision engine.
- Current built-in profiles:
	- Standard (default)
	- WarPremium
- Runtime API:
	- GET [brain/src/Web/Endpoints/StrategyEndpoints.cs](brain/src/Web/Endpoints/StrategyEndpoints.cs)
	- PUT activate profile by id on `/api/strategies/{id}/activate`

## Simulator Profiles

- Simulator can now generate profile-specific market behavior.
- Start simulator with selected profile via `/api/monitoring/simulator/start` body:
	- `strategyProfile: "Standard"` or `strategyProfile: "WarPremium"`
- Status endpoint includes `strategyProfile` so UI/scripts can verify active simulator behavior.

## Ready-Now Steps

1. Follow [docs/GO_LIVE_CHECKLIST.md](docs/GO_LIVE_CHECKLIST.md) end-to-end.
2. Keep `Standard` active by default unless war-expansion conditions justify `WarPremium`.
3. Use UI Strategy page quick switch to move between `Standard` and `WarPremium`.
4. Verify mode feed (`/mode`) and simulator profile behavior before opening live sessions.

## Mobile Environment Switcher

- Mobile app defaults to **Production** environment.
- In app header, use cloud-sync icon to switch between:
	- `Production`
	- `Local`
- URLs are configured in app code and switched by environment selection.

