# AI Worker

FastAPI service providing deterministic XAUUSD signal orchestration for the Brain backend.

## Features

- ✅ **Committee live decision engine** with strict quorum gating
- ✅ **Grok transport selectable**: `openrouter` now, `direct` later
- ✅ **Hard `NO_TRADE` when committee consensus fails**
- ✅ **Telegram context enrichment + consensus tags**
- ✅ **External RSS news enrichment + risk/bias scoring**
- ✅ **Production Use**: Called by Brain API for market snapshot analysis

## Run

```bash
python -m venv .venv
.venv/Scripts/activate
pip install -r requirements.txt

# Create .env with API keys (see AI_INTEGRATION_GUIDE.md)
# AI_STRATEGY=committee
# CONSENSUS_MIN_AGREEMENT=2
# GROK_RUNTIME_TRANSPORT=openrouter
# GROK_FORCE_OPENROUTER=true
# OPENROUTER_API_KEY=...
# GROK_OPENROUTER_MODEL=x-ai/grok-4.1-fast
# OPENAI_API_KEY=...
# OPENAI_MODEL=gpt-4.1-mini
# GEMINI_API_KEY=...
# GEMINI_MODEL=gemini-2.0-flash
# PERPLEXITY_API_KEY=... (optional)
# PERPLEXITY_MODEL=sonar (optional)
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
# EXTERNAL_NEWS_ENABLED=true
# EXTERNAL_NEWS_FEEDS=https://feeds.reuters.com/reuters/commoditiesNews,https://www.investing.com/rss/news_301.rss

uvicorn app.main:app --reload --port 8001
```

## Endpoints

- `GET /health`
- `POST /analyze`
- `POST /mode`

### Mode endpoint

- `POST /mode` returns structured mode payload for backend state machine:
	- `mode`: `WAR_PREMIUM | DEESCALATION_RISK | UNKNOWN`
	- `confidence`: `0..1`
	- `keywords`: matched escalation/de-escalation terms
	- `ttlSeconds`: mode validity window
- Transport priority:
	- Grok via OpenRouter or direct xAI when configured
	- Safe fallback heuristic when model feed is unavailable

## Quick Test

Use Swagger UI for testing:

- Open `http://127.0.0.1:8001/docs`
- Test endpoints in this order:
  1. `GET /health`
  2. `POST /mode`
  3. `POST /analyze`

For full end-to-end run and test sequence, use:
- [docs/RUN_AND_TEST_GUIDE.md](../docs/RUN_AND_TEST_GUIDE.md)

## Configuration

**Key environment variables:**
- `AI_STRATEGY`: `committee` (recommended) or `single`
- `CONSENSUS_MIN_AGREEMENT`: minimum agreeing analyzers required in committee mode
- `GROK_RUNTIME_TRANSPORT`: `openrouter` (recommended now) or `direct`
- `GROK_FORCE_OPENROUTER`: when `true` (default), OpenRouter is enforced for Grok path across all strategy profiles
- `OPENROUTER_API_KEY`: required when transport is `openrouter`
- `GROK_OPENROUTER_MODEL`: must be Grok model id (default `x-ai/grok-4.1-fast`)
- `GROK_API_KEY`: required when transport is `direct`
- `GROK_MODEL`: direct Grok model name (default `grok-2-latest`)
- `OPENAI_API_KEY`, `OPENAI_MODEL`: OpenAI analyzer settings
- `GEMINI_API_KEY`, `GEMINI_MODEL`: Gemini analyzer settings
- `PERPLEXITY_API_KEY`, `PERPLEXITY_MODEL`: Perplexity analyzer settings
- `CONSENSUS_ENTRY_TOLERANCE_PCT`: Entry price tolerance (default: 0.003 = 0.3%)
- `TELEGRAM_READ_MODE`: `bot` (default, requires bot access) or `client` (MTProto user session)
- `TELEGRAM_API_ID`, `TELEGRAM_API_HASH`: Required for `TELEGRAM_READ_MODE=client`
- `TELEGRAM_SESSION_NAME` / `TELEGRAM_SESSION_STRING`: Client session storage for MTProto login
- `TELEGRAM_LISTEN_CHANNELS`: Comma-separated channels/IDs used for news ingestion and consensus
- `TELEGRAM_NOTIFY_CHANNELS`: Optional list for outbound notifications (if reused by other services)
- `TELEGRAM_BOT_TOKEN`, `TELEGRAM_LOOKBACK_MINUTES`, `TELEGRAM_*_KEYWORDS`: Telegram news ingestion and risk tagging
- `EXTERNAL_NEWS_ENABLED`: Enable/disable external RSS news layer
- `EXTERNAL_NEWS_FEEDS`: Comma-separated RSS feed URLs used for additional news context
- `EXTERNAL_NEWS_LOOKBACK_MINUTES`, `EXTERNAL_NEWS_MAX_ITEMS`, `EXTERNAL_NEWS_*_KEYWORDS`: external news scoring and risk tagging

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
- `ai.analyzerCount >= 2`
- `ai.liveDecisionEngine = committee`
- `ai.transport = openrouter` or `direct`
- `ai.strategy = committee`
- `ai.parityBlockers = []`
