# END-TO-END TESTING GUIDE

## Test Scenario: Complete Request Flow

This guide walks through **every** step of the local integration, from startup to trade signal generation.

---

## 🟢 TEST 1: Startup Verification

### 1.1 Start AI Worker (Terminal 1)

```powershell
# Navigate to aiworker
cd .\aiworker\

# Activate Python virtual environment
.\venv\Scripts\Activate.ps1

# Install dependencies (first time only)
pip install -r requirements.txt

# Start FastAPI on port 8001
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001
```

**Expected Output:**
```
INFO:     Uvicorn running on http://127.0.0.1:8001
INFO:     Application startup complete
```

**✅ Success if:**
- No errors in output
- "Application startup complete" message appears

**Verify in browser:**
- Visit http://localhost:8001/docs
- You should see Swagger UI with `/health` and `/analyze` endpoints

---

### 1.2 Start Brain (Terminal 2)

```powershell
# Navigate to brain Web project
cd .\brain\src\Web\

# Restore NuGet packages (first time only)
dotnet restore

# Run the application
dotnet run
```

**Expected Output:**
```
[INF] Application starting. Path: .\brain\src\Web\
[INF] Hosting environment: Development
[INF] wwwroot root: ...\brain\src\Web\wwwroot
[INF] Application started. Press Ctrl+C to shut down.
[INF] Listening on: http://localhost:5000
[INF] Listening on: https://localhost:7096
[INF] 🔄 MarketSnapshotPolling service starting (poll interval: 30s)
```

**✅ Success if:**
- "Application started" message appears
- MarketSnapshotPolling service starts
- Both HTTP (5000) and HTTPS (7096) ports are listening

**Verify in browser:**
- Visit http://localhost:5000/swagger
- You should see all endpoints (Signals, MT5, etc.)

---

## 🟡 TEST 2: Health Checks (Verify Services Are Ready)

### 2.1 Health Check - Brain

```powershell
# Terminal 3 (or new PowerShell session)
curl http://localhost:5000/health
```

**Expected Response:**
```json
{
  "status": "Healthy"
}
```

**HTTP Status:** `200 OK`

---

### 2.2 Health Check - AI Worker

```powershell
curl http://localhost:8001/health
```

**Note:** FastAPI doesn't have `/health` by default. You'll get 404, which is okay. The service is up.

**Alternative - Check Swagger is loaded:**
```powershell
curl http://localhost:8001/docs | Select-Object -First 5
```

Expected: HTML content (Swagger UI)

---

## 🟡 TEST 3: Automatic Market Snapshot Polling

### 3.1 Watch the Logs

Keep both Terminal 1 and Terminal 2 visible side-by-side.

**Every 30 seconds**, you should see this sequence:

**In Terminal 2 (brain):**
```
[INF] 📊 [Poll] Sending EURUSD snapshot to AI Worker (Session=EUROPE, Ma20=1.1025, ATR=0.00095)
```

**In Terminal 1 (aiworker):**
```
INFO:     127.0.0.1:XXXXX - "POST /analyze HTTP/1.1" 200 OK
```

**Then in Terminal 2 (brain) again:**
```
[INF] ✅ [Poll] Analysis received: BUY_LIMIT @ 1.10250 (TP=1.10401, Confidence=0.85)
```

### 3.2 Timestamp Check

The polling happens every **exactly 30 seconds**. Look at timestamps:

```
14:30:00Z → [Poll] Sending...
14:30:01Z ← [Poll] Analysis complete...
14:30:00Z - 14:30:30Z → [Sleep]
14:30:30Z → [Poll] Sending...  ← Should be ~30s later
```

**✅ Success if:**
- Logs appear every 30 seconds like clockwork
- No errors in the message chain
- Timestamps advance correctly

---

## 🟡 TEST 4: Manual Signal Analysis

### 4.1 Analyze a Symbol (Manual Request)

```powershell
# Build the request body
$body = @{
    symbol = "EURUSD"
    timeframeData = @(
        @{
            timeframe = "H1"
            open = 1.10200
            high = 1.10300
            low = 1.10150
            close = 1.10250
        },
        @{
            timeframe = "H4"
            open = 1.10100
            high = 1.10350
            low = 1.10050
            close = 1.10250
        }
    )
    atr = 0.00095
    adr = 0.0012
    ma20 = 1.10250
    session = "EUROPE"
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json -Depth 10

# Send request
curl -X POST http://localhost:5000/api/signals/analyze/EURUSD `
  -Headers @{"Content-Type"="application/json"} `
  -Body $body
```

**Expected Response (200 OK):**
```json
{
  "symbol": "EURUSD",
  "signal": "BUY_LIMIT",
  "entry": 1.10250,
  "tp": 1.10401,
  "ml": 3600,
  "confidence": 0.85
}
```

**In Logs (Terminal 2):**
```
[INF] → POST /api/signals/analyze/EURUSD
[INF] ← POST /api/signals/analyze/EURUSD complete
```

**✅ Success if:**
- HTTP 200 response with JSON
- Signal contains all required fields
- Confidence is between 0.0 and 1.0

---

### 4.2 Verify the Data Flow

The request went:
```
curl (localhost:5000)
    ↓
SignalsEndpoints.MapPost()
    ↓
MediatR sends AnalyzeSnapshotCommand
    ↓
Handler calls IAIWorkerClient.AnalyzeAsync()
    ↓
HttpAIWorkerClient calls POST http://localhost:8001/analyze
    ↓
FastAPI aiworker analyzes
    ↓
Response returns TradeSignal (confidence, entry, tp, etc.)
    ↓
JSON serialized back to curl
```

---

## 🟡 TEST 5: Retrieve All Signals

### 5.1 Get Signal History

```powershell
curl http://localhost:5000/api/signals
```

**Expected Response (200 OK):**
```json
[
  {
    "id": "uuid1",
    "symbol": "EURUSD",
    "signal": "BUY_LIMIT",
    "entry": 1.10250,
    "createdAt": "2025-02-25T14:30:00Z"
  },
  {
    "id": "uuid2",
    "symbol": "EURUSD",
    "signal": "SELL_STOP",
    "entry": 1.10200,
    "createdAt": "2025-02-25T14:00:00Z"
  }
]
```

**In Logs (Terminal 2):**
```
[INF] → GET /api/signals
[INF] ← GET /api/signals returned 2 signals
```

**✅ Success if:**
- Returns array of signals
- Each signal has id, symbol, signal type, entry, createdAt
- Signals are ordered by creation date

---

## 🔴 TEST 6: MT5 Integration (API Key Required)

### 6.1 Get Pending Trades (Requires API Key)

```powershell
# Define API key header
$headers = @{
    "X-API-Key" = "dev-local-change-me"
}

# Get pending trades
curl -X GET http://localhost:5000/mt5/pending-trades `
  -Headers $headers
```

**Expected Response (200 OK):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "type": "BUY_LIMIT",
  "symbol": "EURUSD",
  "price": 1.10250,
  "tp": 1.10400,
  "expiry": "2025-02-25T15:00:00Z",
  "ml": 3600
}
```

**In Logs (Terminal 2):**
```
[INF] → GET /mt5/pending-trades
[INF] ← GET /mt5/pending-trades returns trade BUY_LIMIT @ 1.1025
```

**✅ Success if:**
- Returns 200 OK with pending trade
- Trade has all fields: id, type, symbol, price, tp, expiry, ml

---

### 6.2 Test Without API Key (Should Fail)

```powershell
# Without header
curl -X GET http://localhost:5000/mt5/pending-trades
```

**Expected Response (401 Unauthorized):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

**In Logs (Terminal 2):**
```
[WRN] Security: Missing or invalid API key
```

**✅ Success if:**
- Returns 401 Unauthorized
- Endpoint is protected correctly

---

### 6.3 Test With Wrong API Key (Should Fail)

```powershell
$headers = @{
    "X-API-Key" = "wrong-key"
}

curl -X GET http://localhost:5000/mt5/pending-trades `
  -Headers $headers
```

**Expected Response (401 Unauthorized)**

---

## 🔴 TEST 7: MT5 Trade Status Callback

### 7.1 Send Trade Status Update

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
  -Headers @{"Content-Type"="application/json"} `
  -Body $body
```

**Expected Response (200 OK):**
```json
{
  "received": true
}
```

**In Logs (Terminal 2):**
```
[INF] → POST /mt5/trade-status: TradeId=550e8400-e29b-41d4-a716-446655440000, Status=EXECUTED
[INF] ← /mt5/trade-status: Status callback processed
```

**✅ Success if:**
- Returns 200 OK with { received: true }
- Logs show TradeId and Status captured

---

### 7.2 Test Different Status Values

Try these status values:
- `"EXECUTED"` - Trade fully executed
- `"PARTIAL"` - Partial fill
- `"REJECTED"` - Rejected by broker
- `"PENDING"` - Still pending
- `"ERROR"` - Error occurred

All should return 200 OK with same response format.

---

## 🟢 TEST 8: Error Scenarios

### 8.1 Connection Error - AI Worker Offline

**Simulate:** Stop Terminal 1 (aiworker)

**In Terminal 2, wait 30 seconds for next poll:**
```
[WRN] ⚠️ [Poll] Analysis failed for EURUSD. Check if aiworker is running on http://localhost:8001
[ERR] ✗ [AIWorker] HTTP request failed. Is aiworker running on http://localhost:8001?
```

**✅ Success if:**
- Error is logged clearly
- Service continues running (doesn't crash)
- Keeps retrying every 30 seconds

**Recovery:** Restart aiworker, it auto-reconnects on next poll

---

### 8.2 Invalid JSON in Request

```powershell
# Send malformed JSON
curl -X POST http://localhost:5000/api/signals/analyze/EURUSD `
  -Headers @{"Content-Type"="application/json"} `
  -Body "{ invalid json }"
```

**Expected Response (400 Bad Request):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "JSON parsing error"
}
```

**In Logs:**
```
[ERR] Invalid JSON in request
```

**✅ Success if:**
- Returns 400 Bad Request
- Error message is helpful

---

### 8.3 Missing Required Header on MT5

(Already tested in TEST 6.2 above)

---

## 📊 TEST 9: Performance & Stability

### 9.1 Long-Running Stability Test (30 minutes)

```powershell
# Run this in Terminal 3 to monitor steady-state operation
while ($true) {
    $log = Get-Content "logfile.txt" -Tail 1
    Write-Host "$(Get-Date) - $log"
    Start-Sleep -Seconds 5
}
```

**What to watch for:**
- Polls happen at exactly 30-second intervals
- No memory leaks (check Task Manager)
- No repeated error messages
- Example: Every 30 seconds you see:
  ```
  14:30:00 → [Poll] Sending...
  14:30:01 ✅ [Poll] Analysis received...
  14:31:00 → [Poll] Sending...
  14:31:01 ✅ [Poll] Analysis received...
  ```

**✅ Success if:**
- Runs without errors for 30 minutes
- Consistent timing (±1 second variance is fine)
- No memory growth

---

### 9.2 Concurrent Request Test

```powershell
# Send 5 concurrent requests
1..5 | ForEach-Object -Parallel {
    $headers = @{ "X-API-Key" = "dev-local-change-me" }
    curl -X GET http://localhost:5000/mt5/pending-trades -Headers $headers
} -ThrottleLimit 5
```

**Expected:** All 5 requests return 200 OK

**In Logs:** You'll see 5 request/response pairs

**✅ Success if:**
- No timeouts
- All return 200 OK
- No server errors (500)

---

## 📈 TEST 10: Data Verification

### 10.1 Verify Signal Confidence Range

Run analysis 5 times and check confidence:

```powershell
# Test 1
curl -X POST http://localhost:5000/api/signals/analyze/EURUSD `
  -Headers @{"Content-Type"="application/json"} `
  -Body $body
# Check: confidence should be 0.74 (mock value)

# Test 2, 3, 4, 5: Same query
```

**✅ Success if:**
- Confidence is always between 0.0 and 1.0
- Values are consistent (mock returns same value)

---

### 10.2 Verify Timestamp Format

Check that all timestamps are ISO 8601 UTC:

```powershell
# Should look like:
"timestamp": "2025-02-25T14:30:00Z"  ✅ (ISO 8601 UTC)
# Not like:
"timestamp": "Feb 25 2025 14:30:00"  ❌ (Wrong format)
"timestamp": "2025-02-25T14:30:00+00:00"  ⚠️ (Has timezone offset)
```

**✅ Success if:**
- All timestamps end with `Z` (UTC indicator)
- Format is `YYYY-MM-DDTHH:MM:SSZ`

---

## 🎯 FINAL VERIFICATION CHECKLIST

```
✅ TEST 1:  Startup - Both services start without errors
✅ TEST 2:  Health - Both services respond to health checks
✅ TEST 3:  Auto Polling - Logs show → ← every 30 seconds
✅ TEST 4:  Manual Analysis - POST /api/signals/analyze returns signal
✅ TEST 5:  Get Signals - GET /api/signals returns array
✅ TEST 6:  MT5 Pending - GET /mt5/pending-trades returns trade (with key)
✅ TEST 7:  MT5 Callback - POST /mt5/trade-status accepts status update
✅ TEST 8:  Errors - Connection errors logged, service continues
✅ TEST 9:  Stability - Runs 30 min without issues
✅ TEST 10: Data - Confidence in range, timestamps ISO 8601
```

All 10 tests passing = **Complete Integration Verified** 🎉

---

## 🔧 TROUBLESHOOTING DURING TESTS

| Issue | Check | Fix |
|-------|-------|-----|
| "Connection refused" on /analyze | Is aiworker running on port 8001? | `python -m uvicorn app.main:app --host 127.0.0.1 --port 8001` |
| 401 on /mt5/* endpoints | Did you add X-API-Key header? | Add `-Headers @{"X-API-Key"="dev-local-change-me"}` |
| No logs visible | Is LogLevel set to "Information"? | Check appsettings.Development.json, change to "Information" |
| Memory keeps growing | Is there a leak in polling? | Restart brain, should be stable |
| Requests timeout | Is network slow or service overloaded? | Check both services are responsive, reduce concurrency |
| "Invalid JSON" errors | Did you forget -Body value? | Use `| ConvertTo-Json` on object, not string |

---

## 📝 NOTES FOR TESTING

1. **Don't rush through tests** - Each one tells you something important about the system
2. **Keep both terminal windows visible** - You need to see logs in real-time
3. **Copy-paste commands carefully** - PowerShell is picky about quotes
4. **Save results** - Screenshot or log the passing tests for documentation
5. **Test again after making changes** - New code should pass all tests

---

That's it! 🚀 Run through all tests to verify your complete local integration works perfectly.
