# 🤖 AI Integration Setup Guide

## Overview

This guide covers the multi-AI provider committee consensus system for Trade-Auto. The AI worker queries multiple models in parallel and requires agreement before emitting trading signals.

### Supported Providers

- ✅ **OpenAI** (GPT-4, GPT-4o-mini, GPT-4.1-mini)
- ✅ **Grok** (xAI Grok-2-latest)
- ✅ **Perplexity** (Sonar Pro)
- ✅ **Gemini** (Google Gemini 2.0 Flash)
- ✅ **Extensible Registry** — Add providers with config only, zero architecture changes

---

## Architecture

```
MT5 Expert Advisor (OnTick every 5s)
    ↓ POST /mt5/market-snapshot
    ↓ (OHLC M5/M15/H1 + ATR/ADR/MA20)
    ↓
Brain API (ASP.NET Core)
    ├─ InMemoryLatestMarketSnapshotStore (singleton)
    └─ SignalPollingBackgroundService (every 30s)
        ↓ GET latest snapshot
        ↓ POST http://localhost:8001/analyze
        ↓
Python AI Worker (FastAPI) — Committee Mode
    ├─ OpenAI: gpt-4o-mini ──┐
    ├─ OpenAI: gpt-4.1-mini ─┤
    ├─ Grok: grok-2-latest ──┼─→ asyncio.gather (parallel)
    ├─ Perplexity: sonar-pro ─┤
    ├─ Gemini: gemini-2.0-flash ─┘
    └─ Vote + Group by Rail + Entry Tolerance Check
        ↓ Average agreeing signals
        ↓ Return consensus signal
        ↓
Brain API Risk Engine
    ├─ Validate rail, entry, TP/SL
    └─ InMemoryPendingTradeStore.Enqueue()
        ↓
MT5 EA polls GET /mt5/pending-trades (every 2s)
    ↓ Dequeue trade (204 if empty)
    ↓ Risk Guards validate
    ↓ Execute order
```

---

## Committee Consensus Strategy

The AI worker **always runs in committee mode**. All configured providers vote in parallel.

### How It Works

1. **Parallel Voting**: All providers analyze the same market snapshot simultaneously using `asyncio.gather`
2. **Rail Grouping**: Votes are grouped by signal direction (BULL/BEAR/NEUTRAL)
3. **Entry Tolerance**: Signals must have entry prices within `CONSENSUS_ENTRY_TOLERANCE_PCT` (default 0.3%)
4. **Agreement Threshold**: Require at least `CONSENSUS_MIN_AGREEMENT` models to agree
5. **Signal Averaging**: Average the entry/TP/SL/ML/expiry from agreeing models

### Example: 5-Model Committee Vote

```
Market: EURUSD @ 1.08500

Votes Received:
- OpenAI gpt-4o-mini:      BULL @ 1.08490 TP:1.08700 SL:1.08350
- OpenAI gpt-4.1-mini:     BULL @ 1.08495 TP:1.08710 SL:1.08345  
- Grok grok-2-latest:      BULL @ 1.08520 TP:1.08720 SL:1.08330
- Perplexity sonar-pro:    BEAR @ 1.08510 TP:1.08200 SL:1.08650
- Gemini gemini-2.0-flash: BULL @ 1.08505 TP:1.08715 SL:1.08340

Analysis:
- BULL rail: 4 votes (entries within 0.3% tolerance: 1.08490-1.08520)
- BEAR rail: 1 vote
- Agreement: 4 ≥ min_agreement (2) ✅
- Consensus Signal: BULL @ 1.08502 (avg), TP: 1.08711, SL: 1.08341
```

### Why Committee?

- **Production Safety**: Multiple models must agree before executing
- **Reduce False Signals**: Filters noise, requires consensus
- **Model Diversity**: Combines OpenAI, xAI, Google, and Perplexity strengths
- **Graceful Degradation**: If 1-2 models fail (API errors), committee still functions

---

## Configuration Modes

### 1. "Single" Mode (min_agreement = 1)

```bash
AI_STRATEGY=single
CONSENSUS_MIN_AGREEMENT=1  # Accept signal from any model
```

- **Use Case**: Development, testing, fast iteration
- **Behavior**: Still queries all models in parallel, accepts first agreeing signal

### 2. "Committee" Mode (min_agreement ≥ 2)

```bash
AI_STRATEGY=committee
CONSENSUS_MIN_AGREEMENT=2  # Require at least 2 models to agree
CONSENSUS_ENTRY_TOLERANCE_PCT=0.003  # 0.3% entry price tolerance
```

- **Use Case**: Production, real money trading
- **Behavior**: Requires N models to agree before emitting signal

---

## Setup: Get API Keys

### OpenAI

1. Go to https://platform.openai.com/api-keys
2. Create new secret key
3. Copy to `.env`

### Grok (xAI)

1. Go to https://console.x.ai
2. Sign up for xAI developer access
3. Generate API key
4. Copy to `.env`

### Perplexity

1. Go to https://www.perplexity.ai/settings/api
2. Create API key
3. Copy to `.env`

### Gemini (Google)

1. Go to https://aistudio.google.com/app/apikey
2. Create API key
3. Copy to `.env`

---

## Setup: Create .env File

Create `.env` in the `aiworker/` directory:

```bash
# === Provider API Keys ===
# At least one provider required for committee to function

OPENAI_API_KEY=sk-proj-PASTE_YOUR_OPENAI_KEY_HERE
# GROK_API_KEY=
# PERPLEXITY_API_KEY=
# GEMINI_API_KEY=

# === Strategy Configuration ===
AI_STRATEGY=committee
CONSENSUS_MIN_AGREEMENT=2
CONSENSUS_ENTRY_TOLERANCE_PCT=0.003

# === Model Selection (comma-separated) ===
# Configure which models to use per provider
OPENAI_MODELS=gpt-4o-mini,gpt-4.1-mini
GROK_MODELS=grok-2-latest
PERPLEXITY_MODELS=sonar-pro
GEMINI_MODELS=gemini-2.0-flash

# === Logging ===
LOG_LEVEL=INFO

# === Telegram News (optional but recommended) ===
# Bot must be added to channels (or receive forwarded channel posts).
TELEGRAM_BOT_TOKEN=123456789:YOUR_BOT_TOKEN
TELEGRAM_CHANNELS=@goldnews,@macroalerts,-1001234567890
TELEGRAM_LOOKBACK_MINUTES=180
TELEGRAM_MAX_UPDATES=100

# Optional keyword overrides
TELEGRAM_BLOCK_KEYWORDS=cpi,nonfarm,nfp,fomc,emergency,war,waterfall
TELEGRAM_CAUTION_KEYWORDS=powell,minutes,ppi,jobless,volatility,selloff
TELEGRAM_BULLISH_KEYWORDS=gold bid,safe haven,buy gold,long xau
TELEGRAM_BEARISH_KEYWORDS=gold selloff,strong dollar,long dxy,bearish gold
```

**Notes:**
- You can enable/disable providers by commenting out API keys
- Multiple models per provider creates multiple analyzers (e.g., 2 OpenAI models = 2 committee votes)
- If only 1 provider has a key, set `CONSENSUS_MIN_AGREEMENT=1` for testing
- `TELEGRAM_CHANNELS` is fully flexible: add/remove channels anytime by comma-separating usernames or chat IDs

### Telegram News Integration

- AI worker fetches recent channel posts from Telegram Bot API `getUpdates`
- Messages are filtered by configured channels and lookback window
- Keyword hits are normalized into `SAFE | CAUTION | BLOCK` tags
- Telegram risk and bias are merged into final signal fields: `safetyTag`, `directionBias`, `newsTags`, `alignmentScore`

---

## Setup: Install Dependencies

```powershell
cd aiworker
pip install -r requirements.txt
```

**Dependencies:**
- `fastapi==0.116.1` — REST API framework
- `uvicorn==0.35.0` — ASGI server
- `openai==2.24.0` — OpenAI SDK (also used for Grok/Perplexity)
- `httpx==0.28.1` — Async HTTP client (for Gemini)
- `python-dotenv==1.0.1` — Environment variable management
- `pydantic==2.10.6` — Data validation

---

## Market Data Flow

### MT5 → Brain: POST /mt5/market-snapshot

The MT5 Expert Advisor pushes market snapshots every 5 seconds:

```json
{
  "symbol": "EURUSD",
  "timeframeData": [
    {
      "timeframe": "M5",
      "open": 1.08500,
      "high": 1.08550,
      "low": 1.08480,
      "close": 1.08530,
      "atr": 0.00045,
      "adr": 0.00120,
      "ma20": 1.08490
    },
    {
      "timeframe": "M15",
      "open": 1.08450,
      "high": 1.08560,
      "low": 1.08440,
      "close": 1.08530,
      "atr": 0.00075,
      "adr": 0.00120,
      "ma20": 1.08480
    },
    {
      "timeframe": "H1",
      "open": 1.08400,
      "high": 1.08600,
      "low": 1.08350,
      "close": 1.08530,
      "atr": 0.00150,
      "adr": 0.00120,
      "ma20": 1.08450
    }
  ]
}
```

**Stored in:** `InMemoryLatestMarketSnapshotStore` (thread-safe singleton)

### Brain → AI Worker: POST /analyze

Brain's `SignalPollingBackgroundService` polls every 30 seconds:

```csharp
var snapshot = await marketData.GetSnapshotAsync("EURUSD");
var result = await aiWorker.AnalyzeAsync(snapshot);
if (result.HasSignal)
{
    pendingTrades.Enqueue(result.ToPendingTradeContract());
}
```

### AI Worker → Brain: Consensus Signal Response

```json
{
  "rail": "BULL",
  "entry": 1.08530,
  "tp": 1.08730,
  "sl": 1.08380,
  "pe": "2025-01-15T14:30:00Z",
  "ml": 120,
  "reasoning": "4/5 models agree: BULL breakout above MA20 (H1) with strong ATR expansion",
  "confidence": 0.8,
  "provider_votes": {
    "openai:gpt-4o-mini": "BULL",
    "openai:gpt-4.1-mini": "BULL",
    "grok:grok-2-latest": "BULL",
    "perplexity:sonar-pro": "BEAR",
    "gemini:gemini-2.0-flash": "BULL"
  }
}
```

### Brain → MT5: GET /mt5/pending-trades

MT5 EA polls every 2 seconds. Brain dequeues one trade:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "type": "BUY",
  "symbol": "EURUSD",
  "price": 1.08530,
  "stopLoss": 1.08380,
  "takeProfit": 1.08730,
  "maxLifetimeMinutes": 120,
  "priceExpiry": "2025-01-15T14:30:00Z"
}
```

**If queue empty:** Returns `204 No Content`

---

## Testing the Integration

### 1. Start the AI Worker

```powershell
cd aiworker
uvicorn app.main:app --reload --port 8001
```

**Expected output:**
```
INFO:     Started server process [12345]
INFO:     Waiting for application startup.
INFO:     Application startup complete.
INFO:     Uvicorn running on http://127.0.0.1:8001
```

### 2. Start the Brain API

```powershell
cd brain/src/Web
dotnet run
```

**Expected output:**
```
info: Brain.Web[0]
      Now listening on: http://localhost:5209
```

### 3. Send Test Snapshot

```powershell
$snapshot = @{
    symbol = "EURUSD"
    timeframeData = @(
        @{
            timeframe = "M5"
            open = 1.08500
            high = 1.08550
            low = 1.08480
            close = 1.08530
            atr = 0.00045
            adr = 0.00120
            ma20 = 1.08490
        },
        @{
            timeframe = "H1"
            open = 1.08400
            high = 1.08600
            low = 1.08350
            close = 1.08530
            atr = 0.00150
            adr = 0.00120
            ma20 = 1.08450
        }
    )
}

Invoke-RestMethod -Uri "http://localhost:5209/mt5/market-snapshot" `
    -Method POST `
    -Body ($snapshot | ConvertTo-Json -Depth 5) `
    -ContentType "application/json"
```

**Expected response:**
```
OK
```

### 4. Wait 30s for Brain to Poll

Brain logs will show:
```
[INFO] Polling market snapshot for EURUSD
[INFO] Calling AI worker: POST http://localhost:8001/analyze
[INFO] Committee consensus: BULL @ 1.08530 (4/5 agreement)
[INFO] Queued pending trade: BUY EURUSD @ 1.08530
```

### 5. Check Pending Trades

```powershell
Invoke-RestMethod -Uri "http://localhost:5209/mt5/pending-trades"
```

**Expected response:**
```json
{
  "id": "...",
  "type": "BUY",
  "symbol": "EURUSD",
  "price": 1.08530,
  "stopLoss": 1.08380,
  "takeProfit": 1.08730,
  "maxLifetimeMinutes": 120,
  "priceExpiry": "2025-01-15T14:30:00Z"
}
```

**Subsequent calls return:** `204 No Content` (trade dequeued)

---

## Adding New AI Providers

The system uses an extensible provider registry. To add a new provider:

### 1. Create Provider Class

```python
# aiworker/app/ai/providers/my_provider.py

from app.ai.providers.base_provider import AIProvider
from app.models.contracts import TradeSignal
import httpx

class MyProvider(AIProvider):
    BASE_URL = "https://api.myprovider.com/v1"
    
    async def analyze(self, market_context: dict) -> TradeSignal:
        prompt = self._build_user_prompt(market_context)
        
        async with httpx.AsyncClient(timeout=self.timeout) as client:
            response = await client.post(
                f"{self.BASE_URL}/generate",
                headers={"Authorization": f"Bearer {self.api_key}"},
                json={"prompt": prompt, "max_tokens": self.max_tokens}
            )
            response.raise_for_status()
            
        data = response.json()
        return self._parse_response(data["text"])
```

### 2. Register in Provider Manager

```python
# aiworker/app/ai/provider_manager.py

from app.ai.providers.my_provider import MyProvider

_provider_registry = {
    "openai": OpenAIProvider,
    "grok": GrokProvider,
    "perplexity": PerplexityProvider,
    "gemini": GeminiProvider,
    "myprovider": MyProvider,  # ← Add here
}
```

### 3. Add to Config

```python
# aiworker/app/ai/config.py

MY_PROVIDER_API_KEY = os.getenv("MY_PROVIDER_API_KEY")

def build_analyzers():
    analyzers = []
    
    # ... existing providers ...
    
    if MY_PROVIDER_API_KEY:
        models = os.getenv("MY_PROVIDER_MODELS", "default-model").split(",")
        for model in models:
            analyzers.append(AIProviderConfig(
                name=f"myprovider:{model.strip()}",
                provider="myprovider",
                model=model.strip(),
                api_key=MY_PROVIDER_API_KEY,
                temperature=0.2,
                max_tokens=450,
                timeout=20
            ))
    
    return analyzers
```

### 4. Update .env

```bash
MY_PROVIDER_API_KEY=your-key-here
MY_PROVIDER_MODELS=default-model
```

**Zero architecture changes required.** New provider automatically participates in committee voting.

---

## Customizing Prompts

All providers inherit from `AIProvider` base class with shared prompt templates.

### System Prompt

```python
# aiworker/app/ai/providers/base_provider.py

def _build_system_prompt(self) -> str:
    return """You are a professional forex trading AI assistant...
    
    RULES:
    - Analyze multi-timeframe market data (M5, M15, H1)
    - Focus on high-probability setups with clear entry/exit
    - BULL = uptrend bias, BEAR = downtrend bias
    - Always respond with valid JSON only
    
    JSON SCHEMA:
    {
      "rail": "BULL" | "BEAR",
      "entry": 1.08530,
      "tp": 1.08730,
      "sl": 1.08380,
      "pe": "2025-01-15T14:30:00Z",
      "ml": 120,
      "reasoning": "..."
    }
    """
```

### User Prompt

```python
def _build_user_prompt(self, market_context: dict) -> str:
    return f"""Analyze this EURUSD market snapshot:

CURRENT PRICE: {market_context['current_price']}

M5 TIMEFRAME:
- Close: {market_context['m5_close']}
- ATR: {market_context['m5_atr']}
- MA20: {market_context['m5_ma20']}

H1 TIMEFRAME:
- Close: {market_context['h1_close']}
- ATR: {market_context['h1_atr']}
- MA20: {market_context['h1_ma20']}

Provide your trading signal in JSON format."""
```

---

## Troubleshooting

### "No consensus signal generated"

**Cause:** Committee could not agree (votes split or outside entry tolerance)

**Check:**
- `CONSENSUS_MIN_AGREEMENT`: Lower to 1 for testing
- `CONSENSUS_ENTRY_TOLERANCE_PCT`: Increase to 0.01 (1%)
- Logs will show: `Committee disagreement: rail=BULL votes=5 agreeing=1 required=2`

### "No MT5 snapshot available yet"

**Cause:** Brain hasn't received POST /mt5/market-snapshot

**Fix:**
- Start MT5 EA with Expert Advisor compiled and attached to chart
- Check EA logs for "Posted market snapshot: 200 OK"
- Verify EA is calling `g_api.PostMarketSnapshot()` every 5s

### OpenAI 401 Unauthorized

**Cause:** Invalid or missing API key

**Fix:**
- Check `.env` has valid `OPENAI_API_KEY=sk-proj-...`
- Verify key at https://platform.openai.com/api-keys
- Test key with `curl`:
  ```bash
  curl https://api.openai.com/v1/models \
    -H "Authorization: Bearer YOUR_KEY"
  ```

### Gemini 400 Bad Request

**Cause:** Model not found or API key invalid

**Fix:**
- Use model name exactly as shown in Google AI Studio
- Try `gemini-2.0-flash-exp` or `gemini-1.5-pro`
- Verify key at https://aistudio.google.com/app/apikey

### Slow Committee Response

**Cause:** Multiple parallel API calls (expected)

**Benchmark:**
- 1 provider: ~500-800ms
- 3 providers: ~800-1200ms (parallel)
- 5 providers: ~1000-1500ms (parallel)

**Optimization:**
- Use faster models: `gpt-4o-mini` instead of `gpt-4`
- Lower `max_tokens` to 300-400
- Increase timeout for slower providers

---

## Cost Estimation

Based on 1000 signals/day (30s polling, 50% snapshot → signal conversion):

| Provider | Model | Cost per 1K tokens | Daily Cost |
|----------|-------|-------------------|------------|
| OpenAI | gpt-4o-mini | $0.15/$0.60 | ~$5-7 |
| Grok | grok-2-latest | Free beta | $0 |
| Perplexity | sonar-pro | $0.10/$0.30 | ~$3-4 |
| Gemini | gemini-2.0-flash | Free tier | $0 |

**Committee (4 providers):** ~$8-11/day  
**Single (1 provider):** ~$2-3/day

---

## Next Steps

1. ✅ Configure API keys in `.env`
2. ✅ Start AI worker and Brain API
3. ✅ Deploy MT5 EA to demo account
4. ✅ Monitor first 50 committee votes in logs
5. ✅ Tune `CONSENSUS_MIN_AGREEMENT` and `ENTRY_TOLERANCE_PCT`
6. ✅ Review reasoning field for signal quality
7. ✅ Add custom prompt engineering for specific strategies
8. ✅ Deploy to live trading after 100+ validated signals

---

## Related Documentation

- [AI Integration Summary](AI_INTEGRATION_SUMMARY.md) — Quick reference
- [Implementation Summary](../IMPLEMENTATION_SUMMARY.md) — Full architecture
- [Local Integration Setup](../LOCAL_INTEGRATION_SETUP.md) — End-to-end setup
- [Provider Implementation Guide](../brain/MINIMAL_API_PATTERN.md) — Add custom providers
