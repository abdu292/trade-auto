# AI Worker

FastAPI service providing deterministic XAUUSD signal orchestration for the Brain backend.

## Features

- ✅ **Grok-only live decision engine** (spec-aligned)
- ✅ **Grok transport selectable**: `openrouter` now, `direct` later
- ✅ **Single-analyzer deterministic runtime path**
- ✅ **Telegram context enrichment + consensus tags**
- ✅ **Production Use**: Called by Brain API for market snapshot analysis

## Run

```bash
python -m venv .venv
.venv/Scripts/activate
pip install -r requirements.txt

# Create .env with API keys (see AI_INTEGRATION_GUIDE.md)
# GROK_RUNTIME_TRANSPORT=openrouter
# OPENROUTER_API_KEY=...
# GROK_OPENROUTER_MODEL=x-ai/grok-4.1-fast
# (later optional) GROK_RUNTIME_TRANSPORT=direct
# (later optional) GROK_API_KEY=...
# (later optional) GROK_MODEL=grok-2-latest
# TELEGRAM_BOT_TOKEN=...
# TELEGRAM_READ_MODE=client
# TELEGRAM_API_ID=123456
# TELEGRAM_API_HASH=your_api_hash
# TELEGRAM_SESSION_NAME=trade_auto_reader
# (optional) TELEGRAM_SESSION_STRING=...
# TELEGRAM_LISTEN_CHANNELS=@analysis_channel_one,@analysis_channel_two,-1001234567890
# TELEGRAM_NOTIFY_CHANNELS=@client_updates_channel

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

## Configuration

**Key environment variables:**
- `GROK_RUNTIME_TRANSPORT`: `openrouter` (recommended now) or `direct`
- `OPENROUTER_API_KEY`: required when transport is `openrouter`
- `GROK_OPENROUTER_MODEL`: must be Grok model id (default `x-ai/grok-4.1-fast`)
- `GROK_API_KEY`: required when transport is `direct`
- `GROK_MODEL`: direct Grok model name (default `grok-2-latest`)
- `CONSENSUS_ENTRY_TOLERANCE_PCT`: Entry price tolerance (default: 0.003 = 0.3%)
- `TELEGRAM_READ_MODE`: `bot` (default, requires bot access) or `client` (MTProto user session)
- `TELEGRAM_API_ID`, `TELEGRAM_API_HASH`: Required for `TELEGRAM_READ_MODE=client`
- `TELEGRAM_SESSION_NAME` / `TELEGRAM_SESSION_STRING`: Client session storage for MTProto login
- `TELEGRAM_LISTEN_CHANNELS`: Comma-separated channels/IDs used for news ingestion and consensus
- `TELEGRAM_NOTIFY_CHANNELS`: Optional list for outbound notifications (if reused by other services)
- `TELEGRAM_BOT_TOKEN`, `TELEGRAM_LOOKBACK_MINUTES`, `TELEGRAM_*_KEYWORDS`: Telegram news ingestion and risk tagging

### Telegram read modes

- `bot` mode: uses `getUpdates`; can only read channels where the bot receives `channel_post` updates.
- `client` mode: uses Telethon MTProto user session; supports reading public channels by username without bot admin rights.

### Initialize MTProto session (one-time)

```bash
set TELEGRAM_API_ID=123456
set TELEGRAM_API_HASH=your_api_hash
python scripts/init_telegram_session.py
```

Copy printed `TELEGRAM_SESSION_STRING` into `.env`, then run with:

```bash
TELEGRAM_READ_MODE=client
```

## Spec parity health check

Check:

```powershell
Invoke-RestMethod http://localhost:8001/health | ConvertTo-Json -Depth 8
```

Expected:
- `ai.analyzerCount = 1`
- `ai.liveDecisionEngine = grok`
- `ai.transport = openrouter` or `direct`
- `ai.analyzers[0]` contains `grok`
- `ai.parityBlockers = []`
