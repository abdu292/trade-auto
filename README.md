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

- **Essentials**: [docs/ESSENTIALS.md](docs/ESSENTIALS.md)
- **AI Provider Decision**: [docs/AI_PROVIDER_DECISION.md](docs/AI_PROVIDER_DECISION.md)
- **Spec (authoritative)**: [spec/spec_v3.md](spec/spec_v3.md)
- **Implemented SOP**: [spec/SOP_SPEC_V3_IMPLEMENTATION.md](spec/SOP_SPEC_V3_IMPLEMENTATION.md)

