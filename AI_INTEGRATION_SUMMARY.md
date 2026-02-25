# 🤖 Multi-AI Integration — Quick Reference

## What Was Implemented

### 1. **Provider Abstraction Layer**
- `AIProvider` base class with async interface
- OpenAI, Grok, Perplexity implementations
- Each provider normalizes to trade signal JSON

### 2. **Provider Manager**
- Fallback strategy: tries providers in order
- Consensus strategy: requires agreement from multiple providers
- Handles errors gracefully, switches to next provider

### 3. **Structured Response Format**
All AI responses normalized to:
```json
{
  "rail": "A|B|C",           // Confidence level
  "entry": 1.09550,          // Entry price
  "tp": 1.09700,             // Take profit
  "sl": 1.09400,             // Stop loss
  "pe": "01:30",             // Pending expiry (HH:MM)
  "ml": "02:00",             // Max life (HH:MM)
  "confidence": 0.75,        // 0.0-1.0
  "reasoning": "..."         // Why this signal
}
```

### 4. **Configuration-Driven**
Via `.env` file:
- Enable/disable providers with API keys
- Choose fallback OR consensus strategy
- Set minimum agreement for consensus

---

## File Structure

```
aiworker/
├── app/
│   ├── ai/                          # NEW: AI module
│   │   ├── __init__.py
│   │   ├── config.py                # Load API keys & config
│   │   ├── provider_manager.py      # Fallback/consensus logic
│   │   └── providers/
│   │       ├── __init__.py
│   │       ├── base_provider.py     # Abstract base class
│   │       ├── openai_provider.py   # ChatGPT implementation
│   │       ├── grok_provider.py     # Grok implementation
│   │       └── perplexity_provider.py # Perplexity implementation
│   ├── services/
│   │   └── analyzer.py              # UPDATED: Uses provider manager
│   └── routers/
│       └── analyze.py               # UPDATED: Real AI, no mock
├── AI_INTEGRATION_GUIDE.md          # Complete setup guide
└── requirements.txt                 # UPDATED: +openai

brain/
├── src/
│   └── Domain/
│       └── Contracts/
│           └── MarketContextContract.cs  # NEW: Structured market data
```

---

## Quick Setup (5 minutes)

### 1. Get API Keys
```
OpenAI: https://platform.openai.com/api-keys
Grok: https://api.x.ai
Perplexity: https://www.perplexity.ai/api
```

### 2. Create .env
```bash
cd aiworker
echo 'OPENAI_API_KEY=sk-proj-...' > .env
echo 'GROK_API_KEY=...' >> .env
echo 'PERPLEXITY_API_KEY=...' >> .env
echo 'AI_STRATEGY=fallback' >> .env
```

### 3. Install & Test
```powershell
pip install -r requirements.txt
.\start-local.ps1

# Test endpoint
$data = @{ symbol="EURUSD"; close=1.095; ... } | ConvertTo-Json
curl -X POST http://127.0.0.1:8001/analyze -d $data
```

---

## Strategy Comparison

| Aspect | Fallback | Consensus |
|--------|----------|-----------|
| Speed | ⚡ 1-2 sec | 🐢 3-5 sec |
| Cost | 💰 Cheapest | 💸 2-3x |
| Confidence | 😐 Medium | 😎 High |
| Use Case | Dev/Testing | Live Trading |
| Failure Rate | Medium | Low |

**Recommendation:**
- Development: Use **Fallback** + Grok (fast + cheap)
- Live Trade: Use **Consensus** with OpenAI + Grok (high confidence)

---

## How to Use

### Option 1: Fallback (Try providers in order)
```env
AI_STRATEGY=fallback
OPENAI_API_KEY=sk-...
GROK_API_KEY=xai-...
PERPLEXITY_API_KEY=pplx-...
```

→ Will try OpenAI first, fall back to Grok if it fails, then Perplexity

### Option 2: Consensus (Require agreement)
```env
AI_STRATEGY=consensus
CONSENSUS_MIN_AGREEMENT=2
OPENAI_API_KEY=sk-...
GROK_API_KEY=xai-...
PERPLEXITY_API_KEY=pplx-...
```

→ Will query all 3, only accept if ≥2 agree on same rail & entry

---

## Example Flow

```
1. Brain API detects London session opening
2. Fetches OHLC candles from MT5
3. Calculates RSI, MACD, moving averages, ATR
4. Sends to Python worker:
   {
     "symbol": "EURUSD",
     "close": 1.0950,
     "rsi14": 45.5,
     "macd": 0.0012,
     "ma20": 1.0920,
     "session": "London",
     "upcoming_event": "BOE Decision"
   }

5. Python worker:
   - Tries OpenAI → Success! Gets: {"rail": "B", "entry": 1.0955, ...}
   - Returns signal immediately

6. Brain API:
   - Validates against risk engine
   - Creates trade order
   - Sends to MT5 EA

7. MT5 places BUY_LIMIT @ 1.0955 with TP/SL
```

---

## Customization

### Change Provider Priority
Edit `aiworker/app/ai/config.py`:
```python
AI_PROVIDERS: Dict[str, AIProviderConfig] = {}

# First in dict = highest priority for fallback
if GROK_API_KEY:
    AI_PROVIDERS["grok"] = ...     # Try this first

if OPENAI_API_KEY:
    AI_PROVIDERS["openai"] = ...   # Then this

if PERPLEXITY_API_KEY:
    AI_PROVIDERS["perplexity"] = ...  # Finally this
```

### Improve Signal Quality
Edit `aiworker/app/ai/providers/base_provider.py` method `_build_system_prompt()`:
```python
def _build_system_prompt(self) -> str:
    return """
    You are a professional forex trader focusing on [YOUR STRATEGY].
    
    RULES:
    - Only suggest trades during [YOUR SESSIONS]
    - Avoid trading within 1 hour of [YOUR EVENTS]
    - RSI > 70 = Strong signal (Rail A)
    - [Your custom rules here...]
    
    Always respond with JSON.
    """
```

---

## Next: Market Data Integration

Currently using mock data. To connect real MT5 data:

1. **Update Brain API** to calculate technicals from MT5
2. **Create MT5 EA endpoint** to expose OHLC data
3. **Map MarketContextContract** from real calculations
4. **Test with live prices**

See `AI_INTEGRATION_GUIDE.md` for details.

---

## Troubleshooting

**"No AI providers configured"**
→ Set API keys in `.env`

**"All providers failed"**
→ Check API key validity, rate limits, network

**"Response is inconsistent"**
→ Enable consensus mode to require agreement

**"Too slow"**
→ Use fallback + Grok instead of consensus

---

## Cost Estimate (per 100 signals)

| Provider | Fallback | Consensus |
|----------|----------|-----------|
| OpenAI | $0.30 | $0.90 |
| Grok | $0.20 | $0.60 |
| Perplexity | $0.10 | $0.30 |

**Monthly (1000 signals/day):**
- Fallback Grok: ~$60
- Consensus (GPT-4): ~$2,700
