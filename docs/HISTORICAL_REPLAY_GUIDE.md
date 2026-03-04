# Historical Replay Guide

## Which guide should I use?

- Start with `docs/PRD3_E2E_TEST_GUIDE.md` if your goal is to run tests immediately.
- Use this file as the detailed reference (CSV formats, architecture notes, troubleshooting depth).

## Overview

The Trade Auto system supports **historical replay mode**: import MT5-exported candle data as CSV files and replay them through the exact same decision pipeline used for live trading.

This lets you:
- Test the full pipeline (rule engine → AI → decision engine) against real past market data
- Iterate on rule engine parameters and AI prompts without waiting for live candles
- Inspect every decision in the timeline viewer after replay
- Process months of data in minutes

> **Safety guarantee:** Replay mode never executes real trades. All trade intents produced during replay are logged for analysis only.

---

## System Architecture (PRD3)

The decision pipeline follows a strict hierarchy:

```
Candle Close (MT5 live or replay)
    ↓
Rule Engine  (H1 context → M15 setup → M5 entry)
    ↓  [abort if no setup candidate]
AI Analysis  (confirmation / veto)
    ↓  [abort if veto or low confidence]
Risk Gate Evaluation
    ↓
Trade Intent Generation
    ↓
Execution  (DISABLED in replay mode)
```

**Rule engine always runs first.** AI is only invoked when the deterministic engine has identified a valid setup candidate.

---

## Step 1 — Export Historical Data from MT5

In MetaTrader 5:

1. Open **File → Open Data Folder** → `bases/<broker>/<symbol>/`
2. Or use **File → Save As** from any chart window to export candle data.
3. Recommended: Use the MT5 **History Center** (`F2`) to export.

### Required timeframes

Export three separate CSV files:

| Timeframe | Purpose        |
|-----------|---------------|
| H1        | Context layer  |
| M15       | Setup layer    |
| M5        | Entry layer (driver) |

### Recommended date range

- **Minimum:** 4 weeks
- **Recommended:** 2–3 months

### MT5 CSV format

MT5 exports files in this format (no header required):

```
2024.01.02,00:05,2062.25,2062.82,2062.25,2062.71,328
2024.01.02,00:10,2062.71,2063.14,2062.59,2063.02,412
```

Columns: `Date, Time, Open, High, Low, Close, TickVolume`

The importer also accepts ISO 8601 timestamps in a single column:

```
2024-01-02T00:05:00,2062.25,2062.82,2062.25,2062.71,328
```

---

## Step 2 — Start Services (Brain + AI Worker)

Preferred startup:

- Windows local: run `./start-local.ps1` from repository root.
- Linux/container entrypoint script: `scripts/start-brain-ai.sh`.

Manual fallback:

```bash
cd aiworker
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001

# in a second terminal
dotnet run --project brain/src/Web/Web.csproj
```

The API will be available at `http://localhost:5000`. Open Swagger UI at `http://localhost:5000/swagger`.

---

## Step 3 — Import CSV Files

Import one file per timeframe using `POST /api/replay/import`:

### Using curl

```bash
# Import H1 candles
curl -X POST "http://localhost:5000/api/replay/import?symbol=XAUUSD&timeframe=H1" \
  -H "Content-Type: text/csv" \
  --data-binary @XAUUSD_H1.csv

# Import M15 candles
curl -X POST "http://localhost:5000/api/replay/import?symbol=XAUUSD&timeframe=M15" \
  -H "Content-Type: text/csv" \
  --data-binary @XAUUSD_M15.csv

# Import M5 candles (driver timeframe)
curl -X POST "http://localhost:5000/api/replay/import?symbol=XAUUSD&timeframe=M5" \
  -H "Content-Type: text/csv" \
  --data-binary @XAUUSD_M5.csv
```

### Expected response

```json
{
  "symbol": "XAUUSD",
  "timeframe": "M5",
  "imported": 15840,
  "totalImportedPerTimeframe": {
    "H1": 792,
    "M15": 3168,
    "M5": 15840
  },
  "hint": "Successfully imported 15840 candles. Use POST /api/replay/start to begin replay."
}
```

Only H1 + M15 + M5 are required. You can also import a single timeframe and the replay engine will use what is available.

---

## Step 4 — Start Replay

```bash
curl -X POST "http://localhost:5000/api/replay/start" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "XAUUSD",
    "from": "2024-01-01T00:00:00Z",
    "to": "2024-03-31T23:59:59Z",
    "speedMultiplier": 200,
    "useAI": true,
    "useMockAI": false
  }'
```

### Request parameters

| Parameter        | Type     | Default  | Description |
|-----------------|----------|----------|-------------|
| `symbol`        | string   | XAUUSD   | Symbol to replay |
| `from`          | datetime | null     | Start of replay window (UTC). Null = beginning of imported data. |
| `to`            | datetime | null     | End of replay window (UTC). Null = end of imported data. |
| `speedMultiplier` | int    | 100      | Speed factor. `1` = real-time (1 candle/sec), `200` = 200x faster, `0` = maximum speed |
| `useAI`         | bool     | true     | Call the real AI worker during replay. |
| `useMockAI`     | bool     | false    | Explicitly force mock AI mode for low-cost dry runs. |

### Recommended workflow

- Default mode is real AI (`useAI: true`, `useMockAI: false`) for full end-to-end validation
- Set `useMockAI: true` only when you explicitly want a low-cost dry run

---

## Step 5 — Inspect Decision Timeline

All decision cycles are logged to the database and queryable during and after replay:

```bash
# Get last 200 timeline events
curl "http://localhost:5000/api/monitoring/timeline?take=200"

# Filter by cycle ID
curl "http://localhost:5000/api/monitoring/timeline?cycleId=replay_20240102000500_XAUUSD"
```

### Timeline event types emitted during replay

| Event type                   | Stage        | Meaning |
|-----------------------------|--------------|---------|
| `REPLAY_CYCLE_STARTED`      | replay       | New candle being processed |
| `RULE_ENGINE_SETUP_CANDIDATE` | rule_engine | All three layers passed; AI will be invoked |
| `RULE_ENGINE_ABORT`         | rule_engine  | Cycle aborted — no setup candidate |
| `REPLAY_AI_RESPONSE`        | ai           | AI (or mock) signal received |
| `REPLAY_TRADE_ARMED`        | decision     | Trade intent generated (not executed) |
| `REPLAY_CYCLE_NO_TRADE`     | decision     | Decision engine blocked the trade |

---

## Step 6 — Replay Controls

```bash
# Check status
curl "http://localhost:5000/api/replay/status"

# Pause (to inspect a specific decision cycle)
curl -X POST "http://localhost:5000/api/replay/pause"

# Resume
curl -X POST "http://localhost:5000/api/replay/resume"

# Stop
curl -X POST "http://localhost:5000/api/replay/stop"
```

### Status response

```json
{
  "status": {
    "isRunning": true,
    "isPaused": false,
    "symbol": "XAUUSD",
    "totalCandles": 15840,
    "processedCandles": 4200,
    "cyclesTriggered": 4200,
    "setupCandidatesFound": 312,
    "tradesArmed": 87,
    "replayFrom": "2024-01-01T00:00:00Z",
    "replayTo": "2024-03-31T23:59:59Z",
    "startedUtc": "2024-12-15T10:23:45Z",
    "driverTimeframe": "M5"
  },
  "importedCandles": {
    "H1": 792,
    "M15": 3168,
    "M5": 15840
  }
}
```

---

## Rule Engine Overview

The deterministic rule engine (added in PRD3) has three layers:

### Layer 1 — H1 Context

Determines the broader market bias before considering any setup.

- **BULLISH:** price above MA20 H1, and RSI ≥ 50 (or liquidity sweep present)
- **BEARISH:** price below MA20 H1, and RSI < 50
- **NEUTRAL:** conflicting signals → cycle aborts here

### Layer 2 — M15 Setup

Checks for a structural trade opportunity.

- **Passes** if: compression flag is set, ≥ 2 compression candles detected, overlapping candles form a base, or M15 ATR < 75% of H1 ATR (range contraction)
- **Fails** → cycle aborts

### Layer 3 — M5 Entry

Confirms the setup is actionable right now.

- **Passes** if: breakout confirmed, impulse candle with strength ≥ 0.40, post-spike retest, or compression active with RSI between 35–72
- **Fails** → cycle aborts

Only when all three layers pass does the system produce a **setup candidate** and call the AI for confirmation.

---

## Development Workflow

```
1. Export MT5 data (H1 + M15 + M5 CSV files)
2. Import via POST /api/replay/import × 3
3. Run replay: POST /api/replay/start (useAI: false, high speed)
4. Inspect timeline: GET /api/monitoring/timeline
5. Tune rule engine parameters (RuleEngine.cs) or AI prompts
6. Re-run replay to validate changes
7. Once satisfied, run with useAI: true for full validation
8. Repeat with new market periods
```

This workflow allows full pipeline testing in minutes without live market dependency.

---

## CSV Format Reference

The importer accepts multiple formats automatically:

### MT5 two-column date/time (most common MT5 export)

```
2024.01.02,00:05,2062.25,2062.82,2062.25,2062.71,328
```

### ISO 8601 single timestamp column

```
2024-01-02T00:05:00Z,2062.25,2062.82,2062.25,2062.71,328
```

### With CSV header (header is auto-detected and skipped)

```
Date,Time,Open,High,Low,Close,Volume
2024.01.02,00:05,2062.25,2062.82,2062.25,2062.71,328
```

**Required columns (in order):** timestamp, open, high, low, close, [volume optional]

Additional columns (e.g. Spread, RealVolume) are automatically ignored.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `No candles imported` | Check CSV format. Try adding a header row or removing it. |
| `No candles in requested date range` | The `from`/`to` parameters must match the imported data's timestamps (UTC). |
| `No candles imported for symbol` | Call `POST /api/replay/import` for at least one timeframe before starting. |
| Rule engine always aborts | Check that imported candles have valid OHLC values. Inspect the `RULE_ENGINE_ABORT` timeline events for the abort reason. |
| Very few setup candidates | This is expected — the rule engine is selective. Typical rate is 5–15% of candles produce a setup candidate. |
