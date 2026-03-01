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

1. Run `brain` backend.
2. Run `aiworker` on port `8001`.
3. Run Flutter UI with `--dart-define=BRAIN_API_BASE_URL=http://localhost:5000/api`.
4. Point MT5 EA `BrainBaseUrl` to backend host.

## Documentation

- **Live Demo Test (XAUUSD)**: [docs/LIVE_XAUUSD_DEMO_TEST.md](docs/LIVE_XAUUSD_DEMO_TEST.md)
- **AI Provider Decision**: [docs/AI_PROVIDER_DECISION.md](docs/AI_PROVIDER_DECISION.md)
- **Spec (authoritative)**: [spec/spec_v4_war_premium.md](spec/spec_v4_war_premium.md)
- **Implemented SOP**: [spec/SOP_SPEC_V3_IMPLEMENTATION.md](spec/SOP_SPEC_V3_IMPLEMENTATION.md)
- **Strict Parity Status**: [docs/STRICT_SPEC_PARITY_STATUS.md](docs/STRICT_SPEC_PARITY_STATUS.md)
- **Trader Mode Guide**: [docs/TRADER_MODE_GUIDE.md](docs/TRADER_MODE_GUIDE.md)
- **Go-Live Checklist**: [docs/GO_LIVE_CHECKLIST.md](docs/GO_LIVE_CHECKLIST.md)

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

