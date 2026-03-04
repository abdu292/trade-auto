# PRD3 End-to-End Test Guide (THE REAL ONE)

This is the only guide you need for end-to-end testing.

## 0) Fast answers to common questions

### 0.1 Where to set `useMockAI`?

You can set it in either place:

1) **Replay API request body** (`POST /api/replay/start`)
```json
{
  "symbol": "XAUUSD",
  "speedMultiplier": 200,
  "useAI": true,
  "useMockAI": true
}
```

2) **UI Replay screen toggle**: “Use mock AI (explicit)”
   - Implementation references:
     - `ui/lib/features/replay/presentation/replay_screen.dart`
     - `ui/lib/data/brain_api.dart`

Defaults in code are real AI (`UseAI=true`) and explicit mock off (`UseMockAI=false`).

### 0.2 How to export from MT5?

Use the built‑in **Bars export** feature in current MT5. This produces exactly the candle data our replay engine consumes (you do not need tick exports).

1. In MT5, go to **Symbols** (right‑click chart or `View → Symbols`).
2. Select `XAUUSD` then click the **Bars** tab.
3. Choose the desired **timeframe** (H1, then M15, then M5) and set the **date range**.
4. Click **Request** to load the bars, then click **Export Bars** and save the CSV.
   (Alternatively you can use **File → Save As** directly from the chart and select “Bars”.)
5. Repeat for the three timeframes.

Exported files usually include columns such as:

```
date,time,open,high,low,close,tick volume,volume,spread
```

Only the timestamp plus OHLC are required; extra columns are ignored.

For older MT5 versions the History Center route still works; ensure you export bars, not ticks.
### 0.3 What are `tmp_replay` and `tmp_replay2`?

- They are temporary local test folders created during replay verification.
- They contain sample CSV files (`h1.csv`, `m15.csv`, `m5.csv`) used to test import/replay quickly.
- They are **not runtime dependencies** and can be safely deleted.

---

## 1) Data source truth (important)

- **Live mode**: candles come from real MT5 EA snapshots (`/mt5/market-snapshot`), using `H1/M15/M5`.
  - `mt5ea/ExpertAdvisor.mq5`
  - `mt5ea/Http/ApiClient.mqh`
- **Replay mode**: candles come only from imported CSV files.
  - `brain/src/Infrastructure/Services/External/HistoricalReplayService.cs`

---

## 2) Start services (recommended scripts first)

### Windows (recommended)

From repo root:

```powershell
./start-local.ps1
```

This starts Brain (`:5000`) and AI worker (`:8001`) and performs health checks.

### Linux/container helper

Use script reference:

```bash
scripts/start-brain-ai.sh
```

### Manual fallback

```powershell
cd aiworker
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001

# second terminal
dotnet run --project brain/src/Web/Web.csproj
```

Health checks:
- `GET http://127.0.0.1:8001/health`
- `GET http://127.0.0.1:5000/health`

---

## 3) Live mode test (real MT5)

1. Attach `mt5ea/ExpertAdvisor.ex5` to `XAUUSD` chart.
2. Set EA inputs:
   - `BrainBaseUrl = http://127.0.0.1:5000`
   - `BrainApiKey = dev-local-change-me`
3. Confirm runtime ingestion:
   - `GET /api/monitoring/runtime`
4. Verify timeline order (`GET /api/monitoring/timeline?take=300`):
   - `CYCLE_STARTED`
   - `RULE_ENGINE_SETUP_CANDIDATE` or `RULE_ENGINE_ABORT`
   - `NEWS_CHECK` (if setup valid)
   - `AI_ANALYZE_REQUEST -> AI_ANALYZE_RESPONSE` (if news allows)
   - `DECISION_EVALUATED`
   - `TRADE_ROUTED` (if tradable)

---

## 4) Replay mode test (historical)

### 4.1 Import CSV files

Now that the API accepts a multipart/form-data upload, you can use Swagger's file picker directly:

1. Open `http://localhost:5000/swagger`.
2. Find the `POST /api/replay/import` operation.
3. Click **Try it out**.
4. Choose your CSV file using the **file** field, then fill the `symbol` and `timeframe` query parameters.
5. Execute the request; you should see the imported count in the response.

Repeat for each timeframe (`H1`, `M15`, `M5`).

If you prefer command-line or another client, you can still send the file as form-data:

```bash
curl -X POST "http://localhost:5000/api/replay/import?symbol=XAUUSD&timeframe=H1" \
  -F "file=@h1.csv"
```

Swagger now provides the upload control, so manual tools are optional.
### 4.2 Start replay (cheap test first)

Mock (cheap dry-run):

```json
{
  "symbol": "XAUUSD",
  "speedMultiplier": 200,
  "useMockAI": true,
  "initialCashAed": 50000  // change to larger amount if you need more capacity
}
```

### 4.3 Start replay (real AI paid validation)

```json
{
  "symbol": "XAUUSD",
  "speedMultiplier": 50,
  "useAI": true,
  "useMockAI": false,
  "initialCashAed": 75000  // e.g. bump from default 50k to 75k
}
```

### 4.4 Validate replay

- Status: `GET /api/replay/status?symbol=XAUUSD`
- Timeline: `GET /api/monitoring/timeline?take=500`

Expected order per replay cycle:
1. `REPLAY_CYCLE_STARTED`
2. `RULE_ENGINE_SETUP_CANDIDATE` or `RULE_ENGINE_ABORT`
3. `REPLAY_NEWS_CHECK` (if setup valid)
4. `REPLAY_AI_RESPONSE` (if not blocked)
5. `REPLAY_TRADE_ARMED` or `REPLAY_CYCLE_NO_TRADE`

Replay safety: no real trade execution (`replayMode=true`, `executionBlocked=true`).

---

## 5) Cost-safe testing sequence (recommended)

1. Run full replay in mock mode (`useMockAI=true`) to verify pipeline behavior.
2. Run a **short** replay window in real AI mode (`useMockAI=false`, lower speed).
3. Stop after validating stage order and 1-3 cycles.

---

## 6) Quick troubleshooting

- Stops at `NEWS_CHECK`: news gate blocked.
- `RULE_ENGINE_ABORT` before AI: setup invalid.
- No live cycles: verify EA URL/API key/symbol.
- Real AI expensive: use mock for broad runs, real AI only for short spot checks.

---

## 7) Cleanup (optional)

To remove temporary sample replay folders created during testing:

```powershell
Remove-Item -Recurse -Force .\tmp_replay, .\tmp_replay2
```

---

## 8) Final acceptance checklist

System is good end-to-end when all are true:
- Live MT5 snapshots are arriving.
- Rule engine runs before AI.
- News gate appears before AI in timeline.
- Replay imports H1/M15/M5 and follows expected stage order.
- Real AI is default unless mock is explicitly enabled.
- Replay never executes real trades.
