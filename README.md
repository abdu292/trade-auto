# Trade Auto Platform

Monorepo for a multi-project automation platform:

- `brain` - .NET 10 Clean Architecture backend
- `aiworker` - FastAPI AI analysis service
- `ui` - Flutter app (Riverpod + Dio)
- `mt5ea` - MQL5 Expert Advisor skeleton

See each project README for run instructions.

## Structure

- `brain` - ASP.NET Core .NET 10 Clean Architecture service
- `aiworker` - FastAPI AI signal worker
- `ui` - Flutter (Riverpod + Dio)
- `mt5ea` - MQL5 Expert Advisor skeleton

## Local Quick Start

1. Run `brain` backend.
2. Run `aiworker` on port `8000`.
3. Run Flutter UI with `--dart-define=BRAIN_API_BASE_URL=http://localhost:5000/api`.
4. Point MT5 EA `BrainBaseUrl` to backend host.

