# AI Integration Summary

Quick reference for the multi-AI provider committee consensus system.

---

## System Overview

```
MT5 EA (OnTick throttled: 5s snapshot push, 2s trade poll)
    ↓
Brain API (Singleton stores: snapshot + pending queue)
    ↓
AI Worker (**Committee Mode**: parallel voting across all providers)
    ↓
Brain Risk Engine → MT5 EA executes
```

---

## Architecture Highlights

### Committee Consensus (Production Default)

- **Parallel Voting**: All configured providers analyze simultaneously via `asyncio.gather`
- **Rail Grouping**: Group votes by BULL/BEAR direction
- **Entry Tolerance**: Filter signals within `CONSENSUS_ENTRY_TOLERANCE_PCT` (0.3%)
- **Agreement Threshold**: Require `CONSENSUS_MIN_AGREEMENT` models (minimum 2)
- **Signal Averaging**: Average entry/TP/SL from agreeing models

### Provider Registry (Extensible)

```python
_provider_registry = {
    "openai": OpenAIProvider,
    "grok": GrokProvider,
    "perplexity": PerplexityProvider,
    "gemini": GeminiProvider,
}
```

**Add new provider:** Implement `AIProvider` interface → Register in dict → Add config → Zero architecture changes

---

## File Structure

```
aiworker/
├── .env                          # API keys + committee config
├── requirements.txt              # FastAPI, OpenAI SDK, httpx
├── main.py                       # Uvicorn entry point
└── app/
    ├── main.py                   # FastAPI app + /analyze endpoint
    ├── routers/
    │   ├── analyze.py            # POST /analyze (market → signal)
    │   └── health.py             # GET /health
    ├── services/
    │   ├── analyzer.py           # AnalyzerService (orchestrates committee)
    │   └── providers.py          # Provider factory
    ├── ai/
    │   ├── config.py             # Load env vars, build_analyzers()
    │   ├── provider_manager.py   # Committee voting logic
    │   └── providers/
    │       ├── base_provider.py  # AIProvider interface
    │       ├── openai_provider.py    # OpenAI SDK client
    │       ├── grok_provider.py      # xAI (OpenAI-compatible)
    │       ├── perplexity_provider.py # Perplexity (OpenAI-compatible)
    │       └── gemini_provider.py    # Google Generative Language API
    ├── models/
    │   └── contracts.py          # MarketSnapshot, TradeSignal, AnalysisResult
    └── parsers/
        └── signal_parser.py      # JSON extraction + validation
```

---

## Configuration

### Environment Variables (.env)

```bash
# === Provider API Keys ===
OPENAI_API_KEY=sk-proj-...
GROK_API_KEY=xai-...
PERPLEXITY_API_KEY=...
GEMINI_API_KEY=...

# === Committee Strategy ===
AI_STRATEGY=committee           # or "single" for min_agreement=1
CONSENSUS_MIN_AGREEMENT=2       # Require N models to agree
CONSENSUS_ENTRY_TOLERANCE_PCT=0.003  # 0.3% entry price tolerance

# === Model Selection (comma-separated) ===
OPENAI_MODELS=gpt-4o-mini,gpt-4.1-mini
GROK_MODELS=grok-2-latest
PERPLEXITY_MODELS=sonar-pro
GEMINI_MODELS=gemini-2.0-flash

# === Logging ===
LOG_LEVEL=INFO
```

### Strategy Modes

| Mode | min_agreement | Behavior | Use Case |
|------|---------------|----------|----------|
| **single** | 1 | Accept signal from any model | Dev/testing |
| **committee** | 2+ | Require N models to agree | Production |

---

## API Endpoints

### Health Check

```bash
GET http://localhost:8001/health
```

**Response:**
```json
{
  "status": "healthy",
  "configured_providers": 4,
  "providers": [
    "openai:gpt-4o-mini",
    "openai:gpt-4.1-mini",
    "grok:grok-2-latest",
    "gemini:gemini-2.0-flash"
  ]
}
```

### Analyze Market Snapshot

```bash
POST http://localhost:8001/analyze
Content-Type: application/json

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

**Response (Consensus Success):**
```json
{
  "has_signal": true,
  "rail": "BULL",
  "entry": 1.08530,
  "tp": 1.08730,
  "sl": 1.08380,
  "pe": "2025-01-15T14:30:00Z",
  "ml": 120,
  "reasoning": "Committee consensus: 4/5 models agree on BULL breakout above MA20 (H1)",
  "confidence": 0.8,
  "provider_votes": {
    "openai:gpt-4o-mini": "BULL @ 1.08528",
    "openai:gpt-4.1-mini": "BULL @ 1.08532",
    "grok:grok-2-latest": "BULL @ 1.08535",
    "perplexity:sonar-pro": "BEAR @ 1.08510",
    "gemini:gemini-2.0-flash": "BULL @ 1.08529"
  }
}
```

**Response (Disagreement):**
```json
{
  "has_signal": false,
  "reasoning": "Committee disagreement: rail=BULL votes=5 agreeing=1 required=2"
}
```

---

## Integration Flow

### 1. MT5 → Brain: Market Snapshot Push

**Endpoint:** `POST /mt5/market-snapshot`  
**Frequency:** Every 5 seconds (EA throttled via `SnapshotPushSeconds` timer)  
**Storage:** `InMemoryLatestMarketSnapshotStore` (singleton, thread-safe)

```mql5
// ExpertAdvisor.mq5
static datetime lastSnapshotPush = 0;
const int SnapshotPushSeconds = 5;

void OnTick() {
    datetime now = TimeCurrent();
    if ((now - lastSnapshotPush) >= SnapshotPushSeconds) {
        if (g_api.PostMarketSnapshot(_Symbol)) {
            lastSnapshotPush = now;
        }
    }
}
```

### 2. Brain → AI Worker: Signal Polling

**Service:** `SignalPollingBackgroundService` (runs every 30s)  
**Endpoint:** `POST http://localhost:8001/analyze`

```csharp
// SignalPollingBackgroundService.cs
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        var snapshot = await marketData.GetSnapshotAsync("EURUSD");
        var result = await aiWorker.AnalyzeAsync(snapshot);
        
        if (result.HasSignal)
        {
            pendingTrades.Enqueue(result.ToPendingTradeContract());
            _logger.LogInformation("Queued {Rail} signal @ {Entry}", 
                result.Rail, result.Entry);
        }
        
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    }
}
```

### 3. AI Worker: Committee Voting

**Method:** `AIProviderManager.analyze_with_committee()`

```python
async def analyze_with_committee(
    self, market_context: dict, min_agreement: int, entry_tolerance_pct: float
) -> AnalysisResult:
    # Parallel voting
    tasks = [self._analyze_one(cfg, market_context) for cfg in self.configs]
    results = await asyncio.gather(*tasks, return_exceptions=True)
    
    # Group by rail
    votes_by_rail = {}
    for cfg, result in zip(self.configs, results):
        if isinstance(result, TradeSignal):
            rail = result.rail
            if rail not in votes_by_rail:
                votes_by_rail[rail] = []
            votes_by_rail[rail].append((cfg.name, result))
    
    # Find consensus
    for rail, votes in votes_by_rail.items():
        if len(votes) < min_agreement:
            continue
        
        # Entry tolerance check
        entries = [v[1].entry for v in votes]
        avg_entry = sum(entries) / len(entries)
        
        agreeing = [v for v in votes 
                   if abs(v[1].entry - avg_entry) / avg_entry <= entry_tolerance_pct]
        
        if len(agreeing) >= min_agreement:
            # Average agreeing signals
            return self._average_signals([v[1] for v in agreeing], rail, agreeing)
    
    return AnalysisResult(has_signal=False, 
                         reasoning="Committee disagreement")
```

### 4. Brain → MT5: Pending Trade Delivery

**Endpoint:** `GET /mt5/pending-trades`  
**Frequency:** Every 2 seconds (EA throttled via `PollTradeSeconds` timer)  
**Storage:** `InMemoryPendingTradeStore` (singleton, `ConcurrentQueue`)

```csharp
// Mt5Endpoints.cs
app.MapGet("/mt5/pending-trades", (IPendingTradeStore store) =>
{
    if (store.TryDequeue(out var trade))
    {
        return TypedResults.Ok(trade);
    }
    return TypedResults.NoContent();  // 204 when queue empty
});
```

```mql5
// ExpertAdvisor.mq5
static datetime lastTradePoll = 0;
const int PollTradeSeconds = 2;

void OnTick() {
    datetime now = TimeCurrent();
    if ((now - lastTradePoll) >= PollTradeSeconds) {
        PendingTradeContract trade;
        if (g_api.GetPendingTrade(trade)) {
            if (g_riskGuards.Validate(trade)) {
                g_executor.Execute(trade);
            }
        }
        lastTradePoll = now;
    }
}
```

---

## Provider Details

### OpenAI Provider

- **SDK:** `openai==2.24.0` with `AsyncOpenAI` client
- **Base URL:** `https://api.openai.com/v1`
- **Models:** `gpt-4o-mini`, `gpt-4.1-mini`, `gpt-4-turbo`
- **Key:** `OPENAI_API_KEY=sk-proj-...`

### Grok Provider (xAI)

- **SDK:** OpenAI SDK (compatible API)
- **Base URL:** `https://api.x.ai/v1`
- **Models:** `grok-2-latest`, `grok-beta`
- **Key:** `GROK_API_KEY=xai-...`

### Perplexity Provider

- **SDK:** OpenAI SDK (compatible API)
- **Base URL:** `https://api.perplexity.ai`
- **Models:** `sonar-pro`, `sonar`
- **Key:** `PERPLEXITY_API_KEY=pplx-...`

### Gemini Provider (Google)

- **Client:** `httpx.AsyncClient`
- **API:** Google Generative Language API
- **Base URL:** `https://generativelanguage.googleapis.com/v1beta`
- **Models:** `gemini-2.0-flash-exp`, `gemini-1.5-pro`
- **Key:** `GEMINI_API_KEY=...` (query param in URL)

**Unique handling:**
- Uses `generateContent` endpoint with `generationConfig`
- Response structure: `candidates[0].content.parts[0].text`
- Strips markdown code fences from response (`````json`)

---

## Adding New Providers

### Step 1: Implement AIProvider

```python
# aiworker/app/ai/providers/custom_provider.py

from app.ai.providers.base_provider import AIProvider
from app.models.contracts import TradeSignal
import httpx

class CustomProvider(AIProvider):
    async def analyze(self, market_context: dict) -> TradeSignal:
        prompt = self._build_user_prompt(market_context)
        
        async with httpx.AsyncClient(timeout=self.timeout) as client:
            response = await client.post(
                f"https://api.custom.com/analyze",
                headers={"Authorization": f"Bearer {self.api_key}"},
                json={"prompt": prompt}
            )
            response.raise_for_status()
        
        return self._parse_response(response.json()["output"])
```

### Step 2: Register in Provider Manager

```python
# aiworker/app/ai/provider_manager.py

from app.ai.providers.custom_provider import CustomProvider

_provider_registry = {
    "openai": OpenAIProvider,
    "grok": GrokProvider,
    "perplexity": PerplexityProvider,
    "gemini": GeminiProvider,
    "custom": CustomProvider,  # ← Add here
}
```

### Step 3: Add Configuration

```python
# aiworker/app/ai/config.py

CUSTOM_API_KEY = os.getenv("CUSTOM_API_KEY")

def build_analyzers():
    analyzers = []
    
    # ... existing providers ...
    
    if CUSTOM_API_KEY:
        models = os.getenv("CUSTOM_MODELS", "default").split(",")
        for model in models:
            analyzers.append(AIProviderConfig(
                name=f"custom:{model.strip()}",
                provider="custom",
                model=model.strip(),
                api_key=CUSTOM_API_KEY,
                temperature=0.2,
                max_tokens=450,
                timeout=20
            ))
    
    return analyzers
```

### Step 4: Update Environment

```bash
# .env
CUSTOM_API_KEY=your-key
CUSTOM_MODELS=model-v1,model-v2
```

**Done.** Provider automatically participates in committee voting.

---

## Testing

### Start Services

```powershell
# Terminal 1: AI Worker
cd aiworker
uvicorn app.main:app --reload --port 8001

# Terminal 2: Brain API
cd brain/src/Web
dotnet run

# Terminal 3: MT5 EA (attach to chart in MT5)
```

### Send Test Snapshot

```powershell
$snapshot = @{
    symbol = "EURUSD"
    timeframeData = @(
        @{ timeframe="M5"; open=1.08500; high=1.08550; low=1.08480; 
           close=1.08530; atr=0.00045; adr=0.00120; ma20=1.08490 },
        @{ timeframe="H1"; open=1.08400; high=1.08600; low=1.08350; 
           close=1.08530; atr=0.00150; adr=0.00120; ma20=1.08450 }
    )
}

Invoke-RestMethod -Uri "http://localhost:5209/mt5/market-snapshot" `
    -Method POST `
    -Body ($snapshot | ConvertTo-Json -Depth 5) `
    -ContentType "application/json"
```

### Check Logs

**Brain API logs:**
```
[INFO] Stored EURUSD snapshot (2 TFs)
[INFO] Polling market snapshot for EURUSD
[INFO] Calling AI worker: POST http://localhost:8001/analyze
[INFO] Committee consensus: BULL @ 1.08530 (4/5 agreement)
[INFO] Queued pending trade: BUY EURUSD @ 1.08530
```

**AI Worker logs:**
```
[INFO] Analyzing market with 5 providers
[DEBUG] openai:gpt-4o-mini → BULL @ 1.08528
[DEBUG] openai:gpt-4.1-mini → BULL @ 1.08532
[DEBUG] grok:grok-2-latest → BULL @ 1.08535
[DEBUG] perplexity:sonar-pro → BEAR @ 1.08510
[DEBUG] gemini:gemini-2.0-flash → BULL @ 1.08529
[INFO] Committee vote: BULL rail has 4 agreeing votes (≥2 required)
[INFO] Returning consensus signal: BULL @ 1.08531
```

### Poll Pending Trade

```powershell
Invoke-RestMethod -Uri "http://localhost:5209/mt5/pending-trades"
```

**First call (dequeues):**
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

**Subsequent calls:**
```
204 No Content
```

---

## Performance Benchmarks

### Committee Voting Latency

| Providers | Response Time | Notes |
|-----------|--------------|-------|
| 1 (single) | ~500-800ms | Direct call |
| 3 (committee) | ~800-1200ms | Parallel via asyncio.gather |
| 5 (committee) | ~1000-1500ms | Parallel via asyncio.gather |

**Bottleneck:** Slowest provider determines total latency (parallel execution)

### Cost Estimation (1000 signals/day)

| Provider | Model | Input Cost | Output Cost | Daily Est. |
|----------|-------|-----------|-------------|------------|
| OpenAI | gpt-4o-mini | $0.15/1M | $0.60/1M | ~$5-7 |
| Grok | grok-2-latest | Free beta | Free beta | $0 |
| Perplexity | sonar-pro | $0.10/1M | $0.30/1M | ~$3-4 |
| Gemini | gemini-2.0-flash | Free tier | Free tier | $0 |

**Committee (all 4):** ~$8-11/day  
**Single (OpenAI only):** ~$2-3/day

---

## Troubleshooting

### "No consensus signal generated"

**Logs:**
```
[INFO] Committee disagreement: rail=BULL votes=5 agreeing=1 required=2
```

**Solutions:**
- Lower `CONSENSUS_MIN_AGREEMENT=1` for testing
- Increase `CONSENSUS_ENTRY_TOLERANCE_PCT=0.01` (1%)
- Check provider_votes in response to see split

### "No MT5 snapshot available yet"

**Cause:** Brain hasn't received snapshot from EA

**Fix:**
- Verify EA is running and attached to chart in MT5
- Check EA logs: "Posted market snapshot: 200 OK"
- Verify `SnapshotPushSeconds=5` throttling in EA

### OpenAI 401 Unauthorized

**Cause:** Invalid API key

**Fix:**
- Check `.env` has `OPENAI_API_KEY=sk-proj-...` (no quotes)
- Verify key at https://platform.openai.com/api-keys
- Restart AI worker after updating `.env`

### Gemini 400 Bad Request

**Cause:** Model not found or key invalid

**Fix:**
- Use exact model name: `gemini-2.0-flash-exp` or `gemini-1.5-pro`
- Verify key at https://aistudio.google.com/app/apikey
- Check logs for full error response

### Slow Response Times

**Cause:** High latency providers or network issues

**Optimizations:**
- Use `gpt-4o-mini` instead of `gpt-4`
- Lower `max_tokens=300` in config
- Increase `timeout=30` in config
- Remove slow providers from committee

---

## Production Checklist

- [ ] All API keys configured in `.env`
- [ ] `AI_STRATEGY=committee` enabled
- [ ] `CONSENSUS_MIN_AGREEMENT=2` or higher
- [ ] Brain API polls every 30s (not more frequent)
- [ ] MT5 EA throttled to 5s snapshot push, 2s trade poll
- [ ] Logs monitored for committee disagreements
- [ ] First 100 signals reviewed for quality
- [ ] Entry tolerance tuned based on vote patterns
- [ ] Provider costs monitored (check API usage dashboards)
- [ ] Backup provider configured in case primary fails

---

## Related Documentation

- [AI Integration Guide](AI_INTEGRATION_GUIDE.md) — Detailed setup walkthrough
- [Implementation Summary](../IMPLEMENTATION_SUMMARY.md) — Full system architecture
- [Local Integration Setup](../LOCAL_INTEGRATION_SETUP.md) — End-to-end local dev
- [Verification Checklist](../VERIFICATION_CHECKLIST.md) — Testing guide
