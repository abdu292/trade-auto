# Trade Auto Platform

Monorepo for a multi-project automation platform:

	- `aiworker` - FastAPI multi-AI committee consensus service (OpenAI, Grok, Perplexity, Gemini)

See each project README for run instructions.

## Structure

- `brain` - ASP.NET Core .NET 10 Clean Architecture service
- `aiworker` - FastAPI AI signal worker
- `ui` - Flutter (Riverpod + Dio)
- `mt5ea` - MQL5 Expert Advisor skeleton

## Local Quick Start

1. Run `brain` backend.
2. Run `aiworker` on port `8001` (see [AI Integration Guide](aiworker/AI_INTEGRATION_GUIDE.md) for API keys setup).
3. Run Flutter UI with `--dart-define=BRAIN_API_BASE_URL=http://localhost:5000/api`.
4. Point MT5 EA `BrainBaseUrl` to backend host.

## Documentation

- **AI Integration**: [AI_INTEGRATION_GUIDE.md](aiworker/AI_INTEGRATION_GUIDE.md) — Committee consensus setup
- **Quick Reference**: [AI_INTEGRATION_SUMMARY.md](AI_INTEGRATION_SUMMARY.md) — Provider details and API reference
- **Local Setup**: [LOCAL_INTEGRATION_SETUP.md](LOCAL_INTEGRATION_SETUP.md) — Full stack setup guide

