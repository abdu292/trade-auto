# AI Worker

FastAPI service providing multi-AI provider committee consensus for trading signal validation.

## Features

- ✅ **Committee Consensus**: Parallel voting across multiple AI models
- ✅ **4 Providers**: OpenAI, Grok (xAI), Perplexity, Gemini (Google)
- ✅ **Extensible Registry**: Add providers with config only, zero architecture changes
- ✅ **Configurable Thresholds**: Min agreement count, entry tolerance percentage
- ✅ **Production Ready**: Used by Brain API for real MT5 market snapshot analysis

## Run

```bash
python -m venv .venv
.venv/Scripts/activate
pip install -r requirements.txt

# Create .env with API keys (see AI_INTEGRATION_GUIDE.md)
# OPENAI_API_KEY=sk-proj-...
# GROK_API_KEY=...
# PERPLEXITY_API_KEY=...
# GEMINI_API_KEY=...
# TELEGRAM_BOT_TOKEN=...
# TELEGRAM_CHANNELS=@channel_one,@channel_two,-1001234567890

uvicorn app.main:app --reload --port 8001
```

## Endpoints

- `GET /health`
- `POST /analyze`

## Quick Test

```powershell
# Check health
Invoke-RestMethod http://localhost:8001/health

# Analyze market snapshot
$snapshot = @{
	symbol = "XAUUSD"
	timeframeData = @(
		@{ timeframe="M5"; open=1.08500; high=1.08550; low=1.08480; 
		   close=1.08530; atr=0.00045; adr=0.00120; ma20=1.08490 },
		@{ timeframe="H1"; open=1.08400; high=1.08600; low=1.08350; 
		   close=1.08530; atr=0.00150; adr=0.00120; ma20=1.08450 }
	)
}

Invoke-RestMethod -Uri "http://localhost:8001/analyze" `
	-Method POST `
	-Body ($snapshot | ConvertTo-Json -Depth 5) `
	-ContentType "application/json"
```

## Documentation

- [AI Integration Guide](AI_INTEGRATION_GUIDE.md) — Detailed setup and configuration
- [AI Integration Summary](../AI_INTEGRATION_SUMMARY.md) — Quick reference

## Configuration

See [AI_INTEGRATION_GUIDE.md](AI_INTEGRATION_GUIDE.md) for full setup instructions.

**Key environment variables:**
- `AI_STRATEGY`: `committee` (production) or `single` (dev)
- `CONSENSUS_MIN_AGREEMENT`: Minimum models that must agree (default: 2)
- `CONSENSUS_ENTRY_TOLERANCE_PCT`: Entry price tolerance (default: 0.003 = 0.3%)
- `OPENAI_MODELS`, `GROK_MODELS`, `PERPLEXITY_MODELS`, `GEMINI_MODELS`: Comma-separated model names
- `TELEGRAM_CHANNELS`: Comma-separated list of channels/IDs, flexible for adding more later
- `TELEGRAM_BOT_TOKEN`, `TELEGRAM_LOOKBACK_MINUTES`, `TELEGRAM_*_KEYWORDS`: Telegram news ingestion and risk tagging
