# 🤖 AI Integration Setup Guide

## Overview

This guide walks you through setting up multi-AI provider support for Trade-Auto. The system supports:

- ✅ **OpenAI** (ChatGPT, GPT-4)
- ✅ **Grok** (xAI)
- ✅ **Perplexity** (AI search)
- ✅ **Claude** (Anthropic) — can be added
- ✅ **Fallback strategy** — try providers in order until one succeeds
- ✅ **Consensus strategy** — require agreement from multiple providers

---

## Architecture

```
Brain API (ASP.NET Core)
    ↓ Market Data (OHLC, technicals, session)
    ↓
Python AI Worker (FastAPI)
    ↓
    ├─ OpenAI Provider
    ├─ Grok Provider  
    ├─ Perplexity Provider
    └─ Provider Manager (fallback/consensus)
    ↓ Structured Signal (JSON)
    ↓
Brain API Risk Engine
    ↓
MT5 Expert Advisor
```

---

## Step 1: Get API Keys

### OpenAI (ChatGPT/GPT-4)
1. Go to https://platform.openai.com/api-keys
2. Create new secret key
3. Copy to `.env`

### Grok (xAI)
1. Go to https://api.x.ai
2. Sign up for xAI API access
3. Get API key
4. Copy to `.env`

### Perplexity
1. Go to https://www.perplexity.ai/api
2. Create account
3. Get API key
4. Copy to `.env`

---

## Step 2: Create .env File

Create `.env` in the `aiworker/` directory:

```bash
# Required: At least one provider
OPENAI_API_KEY=sk-proj-your-key-here
GROK_API_KEY=your-grok-key
PERPLEXITY_API_KEY=your-perplexity-key

# Strategy: fallback or consensus
AI_STRATEGY=fallback

# For consensus mode: minimum providers that must agree
CONSENSUS_MIN_AGREEMENT=2

# Logging
LOG_LEVEL=INFO
```

---

## Step 3: Install Dependencies

```powershell
cd aiworker
pip install -r requirements.txt
```

---

## Step 4: Update Market Data in Brain API

The Brain API needs to send structured market data instead of snapshots.

### Create MarketSnapshotPollingService update to calculate technicals:

```csharp
// brain/src/Infrastructure/Services/External/MarketSnapshotPollingService.cs

private MarketContextContract BuildMarketContext(string symbol)
{
    // Fetch OHLC data from MT5 via your bridge
    var ohlc = FetchOHLC(symbol);
    
    // Calculate technical indicators
    var rsi = CalculateRSI(ohlc, 14);
    var macd = CalculateMACD(ohlc);
    var ma20 = CalculateMA(ohlc, 20);
    var ma50 = CalculateMA(ohlc, 50);
    var atr = CalculateATR(ohlc, 14);
    
    // Get session info
    var session = GetCurrentSession(symbol);
    
    // Get economic calendar events
    var events = GetUpcomingEconomicEvents();
    
    return new MarketContextContract(
        Symbol: symbol,
        CurrentPrice: ohlc.Close,
        Open: ohlc.Open,
        High: ohlc.High,
        Low: ohlc.Low,
        Close: ohlc.Close,
        Volume: ohlc.Volume,
        CandleTime: ohlc.Time,
        RSI14: rsi,
        MACD: macd.Main,
        MACDSignal: macd.Signal,
        MA20: ma20,
        MA50: ma50,
        ATR14: atr,
        SessionName: session.Name,
        IsSessionOpen: session.IsOpen,
        MinutesUntilSessionEnd: session.MinutesUntilEnd,
        UpcomingEvent: events.FirstOrDefault()?.Name,
        EventImpact: events.FirstOrDefault()?.Impact,
        LastSignalId: null,
        LastSignalTime: null
    );
}
```

---

## Step 5: Test AI Integration

```powershell
# Start services
cd trade-auto
.\start-local.ps1

# Test with fallback (will try OpenAI first, then Grok, then Perplexity)
$market = @{
    symbol = "EURUSD"
    close = 1.09500
    open = 1.09400
    high = 1.09600
    low = 1.09300
    volume = 1000000
    rsi14 = 65.5
    macd = 0.0015
    ma20 = 1.09200
    ma50 = 1.08900
    atr14 = 0.00150
    session = "London"
    is_session_open = $true
    minutes_until_session_end = 120
}

$response = Invoke-RestMethod -Uri "http://127.0.0.1:8001/analyze" `
    -Method POST `
    -Body ($market | ConvertTo-Json) `
    -ContentType "application/json"

$response | ConvertTo-Json
```

Expected response:
```json
{
  "rail": "B",
  "entry": 1.09550,
  "tp": 1.09700,
  "sl": 1.09400,
  "pe": "01:30",
  "ml": "02:00",
  "confidence": 0.75,
  "reasoning": "RSI overbought at 65, testing resistance at MA20..."
}
```

---

## Step 6: Choose Strategy

### Fallback Strategy (Default - FAST)
- Try OpenAI → If fails, try Grok → If fails, try Perplexity
- **Pros:** Fast, cheap, always tries alternatives
- **Cons:** May use different AI each time
- **Use case:** Development, quick testing

```env
AI_STRATEGY=fallback
```

### Consensus Strategy (CONFIDENT)
- Get signals from 2+ providers and only accept if they agree on same rail & entry
- **Pros:** High confidence, harder to fool
- **Cons:** Slower (parallel calls), higher cost
- **Use case:** Live trading with real money

```env
AI_STRATEGY=consensus
CONSENSUS_MIN_AGREEMENT=2
```

---

## Step 7: Add Custom Prompt Engineering

Each provider gets the same system prompt for consistency. Customize it in:

```python
# aiworker/app/ai/providers/base_provider.py

def _build_system_prompt(self) -> str:
    return """Your custom trading prompt here..."""
```

For example, add forex-specific rules:

```python
def _build_system_prompt(self) -> str:
    return """You are a professional forex trading AI specializing in swing trades.

ANALYSIS RULES:
- Focus on 4H and Daily timeframes
- Only suggest trades during high-liquidity sessions
- Avoid economic data releases ±1 hour
- Rail A only if RSI > 70 or < 30 (extremes)
- Rail B if price breaks MA20 with ATR confirmation
- Rail C for regular setups

ALWAYS respond with JSON only:
{...}"""
```

---

## Step 8: Error Handling & Logging

All errors are logged with context:

```
[INFO] Analyzing market with providers: ['openai', 'grok', 'perplexity']
[DEBUG] Trying provider: openai
[INFO] ✓ Got signal from openai: B @ 1.09550
[INFO] Generated B signal: 1.09550 (confidence: 0.75)
```

If all fail:
```
[ERROR] OpenAI API error: 429 Rate limit exceeded, trying next...
[ERROR] Grok provider error: 500 Server error, trying next...
[ERROR] Perplexity API error: Connection timeout, trying next...
[WARNING] All providers failed to generate signal
```

---

## Step 9: Add to Docker (Optional)

Create `aiworker/Dockerfile`:

```dockerfile
FROM python:3.12-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install -r requirements.txt

COPY . .

# Load env vars
ENV PYTHONUNBUFFERED=1

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8001"]
```

---

## Pricing Comparison

| Provider | Cost | Model | Speed |
|----------|------|-------|-------|
| OpenAI | $0.03/$0.06 per 1k tokens | GPT-4 Turbo | Fast |
| Grok | $0.02/$0.06 per 1k tokens | Grok 2 | Very Fast |
| Perplexity | $0.01/$0.03 per 1k tokens | 70B Online | Fast |

**Tip:** Use Grok for fast fallback, OpenAI for consensus.

---

## Troubleshooting

### "No AI providers configured"
- Set at least one API key in `.env`
- Restart Python worker: `uvicorn` should pick up env vars

### "All providers failed"
- Check API key format
- Check network connectivity
- Check rate limits (openai.com/account/rate-limits)
- Check provider status pages

### Slow responses
- Use fallback strategy instead of consensus
- Lower max_tokens in config.py
- Use faster model (Grok instead of GPT-4)

### Inconsistent signals
- Enable consensus mode to require agreement
- Customize system prompt to be more specific
- Add more technical indicators to market context

---

## Next Steps

1. ✅ Integrate real market data from MT5 (OHLC, technicals)
2. ✅ Test each provider individually
3. ✅ Deploy to production with consensus mode
4. ✅ Monitor signal quality and adjust prompts
5. ✅ Add fallback to local LLM (Ollama) if needed
