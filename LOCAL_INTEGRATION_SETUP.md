# LOCAL INTEGRATION SETUP GUIDE

This guide walks you through running the complete local integration: **brain** ↔ **aiworker** ↔ **mt5ea**.

---

## Overview

```
┌─────────────────────────────────────────────────────────────────┐
│ Flow: Market Data → AI Analysis → Trade Signals → MT5 EA        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│ 1. brain (ASP.NET Core)                                       │
│    ├─ Hosts: /api/signals/*, /mt5/*, /health                │
│    ├─ HttpAIWorkerClient → http://localhost:8001             │
│    └─ BackgroundService (30s) → generates & sends snapshots  │
│                                                                 │
│ 2. aiworker (FastAPI Python)                                 │
│    ├─ Listens: http://localhost:8001                         │
│    ├─ POST /analyze → processes MarketSnapshot              │
│    └─ Returns: TradeSignal (confidence, entry, tp, etc)     │
│                                                                 │
│ 3. mt5ea (MQL5 Expert Advisor)                               │
│    ├─ Polls: GET http://localhost:5000/mt5/pending-trades  │
│    ├─ Executes trades (simulated or real)                   │
│    └─ Callback: POST /mt5/trade-status (execution summary)  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Prerequisites

- **.NET 10 SDK** (for brain)
- **Python 3.10+** (for aiworker)
- **Visual Studio Code** or **Visual Studio**
- **PowerShell** or **Command Prompt**

---

## Step 1: Start aiworker (FastAPI Service)

### Terminal 1: Python Environment

```powershell
cd .\aiworker\
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001
```

**Expected output:**
```
INFO:     Uvicorn running on http://127.0.0.1:8001
```

✅ Head to http://localhost:8001/docs to see interactive API docs

---

## Step 2: Start brain (ASP.NET Core Backend)

### Terminal 2: ASP.NET Core Service

```powershell
cd .\brain\src\Web\
dotnet restore
dotnet run
```

**Expected output:**
```
[INF] Application starting...
[INF] Hosting environment: Development
[INF] wwwroot root: C:\...\brain\src\Web\wwwroot
[INF] Application started. Press Ctrl+C to shut down.
[INF] Listening on: https://localhost:7096
[INF] Listening on: http://localhost:5000
```

✅ Open http://localhost:5000/swagger to see Swagger UI

---

## Step 3: Check Logs - Watch Integration Flow

### Terminal 3: Monitor Logs

Keep both Terminal 1 and 2 visible in split view. You'll see:

**Every 30 seconds from brain:** (MarketSnapshotPollingService)
```
[INF] 📊 [Poll] Sending EURUSD snapshot to AI Worker (Session=EUROPE, Ma20=1.1025, ATR=0.00095)
```

**Immediately in aiworker:**
```
INFO:     127.0.0.1:12345 - "POST /analyze HTTP/1.1" 200 OK
```

**Then back in brain:**
```
[INF] ✅ [Poll] Analysis received: BUY_LIMIT @ 1.10250 (TP=1.10401, Confidence=0.74)
```

---

## Step 4: Test Endpoints Manually

### Test 1: Health Check

```powershell
curl -X GET http://localhost:5000/health
```

**Response (200 OK):**
```json
{
  "status": "Healthy"
}
```

---

### Test 2: Get All Signals

```powershell
curl -X GET http://localhost:5000/api/signals
```

**Response (200 OK):**
```json
[
  {
    "id": "uuid",
    "symbol": "EURUSD",
    "signal": "BUY_LIMIT",
    "createdAt": "2025-02-25T14:30:00Z"
  }
]
```

---

### Test 3: Analyze Single Symbol (Manual Snapshot)

```powershell
$body = @{
    symbol = "EURUSD"
    timeframeData = @(
        @{
            timeframe = "H1"
            open = 1.10200
            high = 1.10300
            low = 1.10150
            close = 1.10250
        }
    )
    atr = 0.00095
    adr = 0.0012
    ma20 = 1.10250
    session = "EUROPE"
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

curl -X POST http://localhost:5000/api/signals/analyze/EURUSD `
  -ContentType "application/json" `
  -Body $body
```

**Response (200 OK):**
```json
{
  "symbol": "EURUSD",
  "signal": "BUY_LIMIT",
  "entry": 1.10250,
  "tp": 1.10401,
  "ml": 3600,
  "confidence": 0.74
}
```

---

### Test 4: Get MT5 Pending Trades

```powershell
$headers = @{
    "X-API-Key" = "dev-local-change-me"
}

curl -X GET http://localhost:5000/mt5/pending-trades `
  -Headers $headers
```

**Response (200 OK):**
```json
{
  "id": "uuid",
  "type": "BUY_LIMIT",
  "symbol": "EURUSD",
  "price": 1.10250,
  "tp": 1.10400,
  "expiry": "2025-02-25T15:00:00Z",
  "ml": 3600
}
```

---

### Test 5: MT5 Send Trade Status Callback

```powershell
$headers = @{
    "X-API-Key" = "dev-local-change-me"
}

$body = @{
    tradeId = "550e8400-e29b-41d4-a716-446655440000"
    status = "EXECUTED"
} | ConvertTo-Json

curl -X POST http://localhost:5000/mt5/trade-status `
  -Headers $headers `
  -ContentType "application/json" `
  -Body $body
```

**Response (200 OK):**
```json
{
  "received": true
}
```

---

## Key Environment Variables

### For brain (appsettings.Development.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TradeAutoDb;Trusted_Connection=True;"
  },
  "Security": {
    "Enabled": true,
    "ApiKeyHeaderName": "X-API-Key",
    "ApiKey": "dev-local-change-me",
    "AllowedIps": ["127.0.0.1", "::1"]
  }
}
```

### For aiworker (no env needed)

FastAPI automatically serves on `http://127.0.0.1:8001`

---

## Troubleshooting

### ❌ "Connection refused" when brain tries to call aiworker

**Cause:** aiworker is not running or listening on wrong port

**Fix:**
```powershell
# In aiworker Terminal
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001
```

Check logs in brain:
```
✗ [AIWorker] HTTP request failed. Is aiworker running on http://localhost:8001?
```

---

### ❌ "401 Unauthorized" on MT5 endpoints

**Cause:** Missing or wrong API key

**Fix:** Always include header:
```powershell
$headers = @{ "X-API-Key" = "dev-local-change-me" }
```

---

### ❌ Database errors in brain

**Cause:** LocalDB not initialized

**Fix:**
```powershell
cd brain/src/Web
dotnet ef database update
```

---

### ❌ "No module named 'app'" in aiworker

**Cause:** Virtual environment not activated

**Fix:**
```powershell
cd aiworker
.\venv\Scripts\Activate.ps1
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001
```

---

## Structured Logging - What to Look For

With **Serilog** + structured logs, you'll see:

```
[INFO] → [AIWorker] POST /analyze for EURUSD, session=EUROPE
[INFO] ← [AIWorker] Analysis complete: BUY_LIMIT (confidence=0.74)
[INFO] → GET /api/signals
[INFO] ← GET /api/signals returned 42 signals
[INFO] → POST /mt5/trade-status: TradeId=..., Status=EXECUTED
[INFO] ← /mt5/trade-status: Status callback processed
```

**Arrow symbols:**
- `→` = Outbound request
- `←` = Response received
- `✓` / `✅` = Success
- `✗` = Error

---

## Architecture Highlights

### Clean Separation of Concerns

1. **brain** = orchestration + API gateway
   - Receives requests from MT5
   - Calls aiworker for analysis
   - Stores signals in DB
   - Scheduled snapshot polling

2. **aiworker** = ML/analysis engine
   - Pure FastAPI service
   - No dependencies on brain
   - Can be replaced with real LLM/model later

3. **mt5ea** = execution layer
   - Polls brain for pending trades
   - Sends execution status back
   - Can run on separate machine

### Extension Points (Easy to Replace)

To switch from mock to real implementations:

**HttpAIWorkerClient** - Already wired to call real aiworker

**MarketSnapshotPollingService** - Replace `GenerateMockSnapshot()` with:
- Real market data from broker API
- Websocket stream instead of polling

**IAIWorkerClient** - Already decoupled from implementation

---

## Next Steps

1. **Add real market data** → Replace mock snapshots with actual OHLC data
2. **Add real ML model** → Replace mock analysis in aiworker
3. **Add MT5 polling** → Write MQL5 script that calls `/mt5/pending-trades`
4. **Add database persistence** → Trade signals, status history
5. **Add monitoring** → Application Insights or Grafana

---

## Files Modified

- ✅ `brain/src/Infrastructure/Services/External/HttpAIWorkerClient.cs` (NEW)
- ✅ `brain/src/Infrastructure/Services/Background/MarketSnapshotPollingService.cs` (NEW)
- ✅ `brain/src/Infrastructure/DependencyInjection/DependencyInjection.cs` (UPDATED)
- ✅ `brain/src/Web/Endpoints/SignalsEndpoints.cs` (UPDATED)
- ✅ `brain/src/Web/Endpoints/Mt5Endpoints.cs` (UPDATED)

All changes preserve backward compatibility. Existing endpoints still work!
