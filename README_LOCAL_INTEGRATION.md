# 🚀 LOCAL INTEGRATION GUIDE

Complete multi-service local development setup: **brain** (ASP.NET Core) ↔ **aiworker** (FastAPI) ↔ **mt5ea** (MQL5)

---

## 📋 Quick Start (5 minutes)

### 1️⃣ Terminal 1: Start AI Worker
```powershell
cd .\aiworker\
.\venv\Scripts\Activate.ps1
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001
```

### 2️⃣ Terminal 2: Start Brain Backend
```powershell
cd .\brain\src\Web\
dotnet run
```

### 3️⃣ Terminal 3: Test
```powershell
curl http://localhost:5000/health
```

**✅ You're done.** Every 30 seconds, brain automatically sends market snapshots to aiworker for analysis.

---

## 📚 Documentation

### Getting Started
- **[QUICK_REFERENCE.md](QUICK_REFERENCE.md)** ⭐ START HERE
  - Service URLs, endpoints, common testing commands
  - 5-minute read

- **[LOCAL_INTEGRATION_SETUP.md](LOCAL_INTEGRATION_SETUP.md)**
  - Complete step-by-step execution guide
  - 7 manual test cases with curl examples
  - Troubleshooting checklist

### Understanding the System
- **[IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md)**
  - Overview of all changes made
  - Requirements checklist
  - Architecture summary

- **[SHARED_MODELS.md](SHARED_MODELS.md)**
  - JSON model definitions for all services
  - C# ↔ Python contract mappings
  - Actual HTTP request/response payloads

- **[ARCHITECTURE_DIAGRAMS.md](ARCHITECTURE_DIAGRAMS.md)**
  - 10 detailed ASCII diagrams
  - System topology, request flow, DI container, security filter
  - Data transformation details

### Building & Extending
- **[MINIMAL_API_PATTERN.md](MINIMAL_API_PATTERN.md)**
  - How to add new endpoints (5 minutes per endpoint)
  - Dependency injection patterns
  - Structured logging best practices

- **[END_TO_END_TESTING.md](END_TO_END_TESTING.md)**
  - 10 complete test scenarios
  - What to expect at each step
  - Performance & stability verification

---

## 🏗️ System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  BRAIN (ASP.NET Core, localhost:5000)                           │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Endpoints:                                                │  │
│  │  • POST /api/signals/analyze/{symbol}                   │  │
│  │  • GET  /api/signals                                    │  │
│  │  • GET  /mt5/pending-trades          (requires API key) │  │
│  │  • POST /mt5/trade-status            (requires API key) │  │
│  │                                                           │  │
│  │ Services:                                                │  │
│  │  • HttpAIWorkerClient (calls http://localhost:8001)     │  │
│  │  • MarketSnapshotPollingService (30s timer)             │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                             ↕ HTTP
                        POST /analyze
┌─────────────────────────────────────────────────────────────────┐
│  AIWORKER (FastAPI, localhost:8001)                             │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Endpoints:                                                │  │
│  │  • POST /analyze                                         │  │
│  │    Request:  { symbol, timeframeData[], atr, ma20... }  │  │
│  │    Response: { rail, entry, tp, confidence... }         │  │
│  │                                                           │  │
│  │  • GET  /docs (Swagger UI for testing)                 │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                             ↕ HTTP
                      /mt5/pending-trades
                        /mt5/trade-status
┌─────────────────────────────────────────────────────────────────┐
│  MT5 EXPERT ADVISOR                                             │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Polls brain for pending trade orders                      │  │
│  │ Executes trades in MetaTrader 5 terminal                 │  │
│  │ Sends execution status callbacks back to brain           │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔄 Signal Generation Flow

```
Every 30 seconds:

1. MarketSnapshotPollingService wakes up
   ↓
2. Generates mock EURUSD market snapshot
   (timeframe data, ATR, MA20, session, timestamp)
   ↓
3. Calls HttpAIWorkerClient.AnalyzeAsync()
   ↓
4. POST http://localhost:8001/analyze
   (converts C# → Python JSON format)
   ↓
5. AI Worker analyzes market data
   (Returns: BUY_LIMIT, SELL_STOP, etc.)
   ↓
6. Response deserialized back to C#
   ↓
7. Logs: ✅ [Poll] Analysis received: BUY_LIMIT
   ↓
8. Sleep 30 seconds
   ↓
9. Repeat forever
```

---

## 🧪 Testing

### Health Checks
```powershell
# Brain
curl http://localhost:5000/health

# AI Worker
curl http://localhost:8001/docs  # Shows Swagger UI
```

### Analyze a Symbol
```powershell
curl -X POST http://localhost:5000/api/signals/analyze/EURUSD `
  -Headers @{"Content-Type"="application/json"} `
  -Body @"
{
  "symbol": "EURUSD",
  "timeframeData": [{"timeframe": "H1", "open": 1.1020, "high": 1.1030, "low": 1.1015, "close": 1.1025}],
  "atr": 0.00095,
  "adr": 0.0012,
  "ma20": 1.1025,
  "session": "EUROPE",
  "timestamp": "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')"
}
"@
```

### Get MT5 Pending Trades
```powershell
curl -X GET http://localhost:5000/mt5/pending-trades `
  -Headers @{"X-API-Key" = "dev-local-change-me"}
```

### Send MT5 Trade Status
```powershell
curl -X POST http://localhost:5000/mt5/trade-status `
  -Headers @{"X-API-Key" = "dev-local-change-me"} `
  -Headers @{"Content-Type"="application/json"} `
  -Body '{"tradeId": "550e8400-e29b-41d4-a716-446655440000", "status": "EXECUTED"}'
```

---

## 📊 What's New

**Code Files Added:**
- ✅ `brain/src/Infrastructure/Services/External/HttpAIWorkerClient.cs`
  - Real HTTP client that calls aiworker at localhost:8001
  - Handles C#/Python serialization differences
  - Error handling + structured logging

- ✅ `brain/src/Infrastructure/Services/Background/MarketSnapshotPollingService.cs`
  - BackgroundService that runs every 30 seconds
  - Generates mock market snapshots
  - Automatically sends to aiworker for analysis

**Code Files Updated:**
- ✅ `brain/src/Infrastructure/DependencyInjection/DependencyInjection.cs`
  - Registers HttpAIWorkerClient
  - Registers BackgroundService
  - Removed MockAIWorkerClient (now using real HTTP)

- ✅ `brain/src/Web/Endpoints/SignalsEndpoints.cs`
  - Added structured logging
  - Added OpenAPI documentation

- ✅ `brain/src/Web/Endpoints/Mt5Endpoints.cs`
  - Added structured logging
  - Improved response models
  - Added OpenAPI documentation

---

## 🔐 Security

### API Key for MT5 Endpoints
All `/mt5/*` endpoints require API key header:
```
X-API-Key: dev-local-change-me
```

**Configure in** `appsettings.Development.json`:
```json
{
  "Security": {
    "Enabled": true,
    "ApiKeyHeaderName": "X-API-Key",
    "ApiKey": "dev-local-change-me",
    "AllowedIps": ["127.0.0.1", "::1"]
  }
}
```

**⚠️ For production:** Change API key to secure random value, store in environment variable.

---

## 🐛 Troubleshooting

| Problem | Solution |
|---------|----------|
| **"Connection refused" on aiworker** | Start Terminal 1: `python -m uvicorn app.main:app --host 127.0.0.1 --port 8001` |
| **401 Unauthorized on /mt5/** | Add header: `-Headers @{"X-API-Key"="dev-local-change-me"}` |
| **"No module named app"** | Activate venv: `.\aiworker\venv\Scripts\Activate.ps1` |
| **Database errors in brain** | Run: `cd brain\src\Web\ && dotnet ef database update` |
| **Port 5000 already in use** | Kill process: `netstat -ano \| findstr :5000` then `taskkill /PID <PID>` |
| **No logs visible** | Check `appsettings.Development.json` LogLevel is "Information" |

---

## 📈 Log Format

Structured logging shows request flow clearly:

```
[INF] → [AIWorker] POST /analyze for EURUSD, session=EUROPE
[INF] ← [AIWorker] Analysis complete: BUY_LIMIT (confidence=0.85)
```

Legend:
- `→` = Outbound request
- `←` = Response received
- `✅` = Success
- `✗` = Error
- `[Component]` = Which service (brain, AIWorker, MT5)
- `{Property}` = Structured property (searchable in logs)

---

## 🎓 Learning Path

1. **First 5 min?** → Read [QUICK_REFERENCE.md](QUICK_REFERENCE.md)
2. **Ready to run?** → Follow [LOCAL_INTEGRATION_SETUP.md](LOCAL_INTEGRATION_SETUP.md)
3. **Want to test?** → Use [END_TO_END_TESTING.md](END_TO_END_TESTING.md)
4. **Need details?** → Check [ARCHITECTURE_DIAGRAMS.md](ARCHITECTURE_DIAGRAMS.md)
5. **Adding features?** → Reference [MINIMAL_API_PATTERN.md](MINIMAL_API_PATTERN.md)

---

## 🚀 Next Steps

### Phase 1: Verify Local (Current)
- ✅ Brain and aiworker talking
- ✅ Automatic polling every 30 seconds
- ✅ MT5 endpoints ready for integration

### Phase 2: Real Data (Future)
- Replace `GenerateMockSnapshot()` with broker API
- Support multiple symbols
- Consider WebSocket streaming

### Phase 3: Real ML (Future)
- Replace `MockAIProvider` with LangChain + LLM
- Add model versioning

### Phase 4: MT5 Integration (Future)
- Write MQL5 script for `/mt5/pending-trades`
- Implement actual trade execution
- Add trade history persistence

---

## 📞 Questions?

- **Code comments?** Each file has detailed inline documentation
- **Architecture?** See [ARCHITECTURE_DIAGRAMS.md](ARCHITECTURE_DIAGRAMS.md)
- **How to add endpoints?** See [MINIMAL_API_PATTERN.md](MINIMAL_API_PATTERN.md)
- **Common issues?** See [LOCAL_INTEGRATION_SETUP.md](LOCAL_INTEGRATION_SETUP.md#troubleshooting)
- **Testing?** See [END_TO_END_TESTING.md](END_TO_END_TESTING.md)

---

## ✨ Summary

Complete local integration with:
- ✅ Real HTTP calls (brain → aiworker)
- ✅ Automatic 30-second polling
- ✅ Structured logging for visibility
- ✅ Production-ready code (SOLID, Clean Architecture)
- ✅ Comprehensive documentation
- ✅ Easy to test, extend, and maintain

Ready to go? Start with [QUICK_REFERENCE.md](QUICK_REFERENCE.md) 🎉
