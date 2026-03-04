# PRD3 End-to-End Test Guide (Straightforward)

## Start here (use this first)

- If you want to start testing right now, follow this file from top to bottom.
- Keep `useMockAI=false` only for short paid checks.
- For cheap dry-runs, set `useMockAI=true`.
- Use `docs/HISTORICAL_REPLAY_GUIDE.md` only when you need detailed CSV format notes or extended troubleshooting.

## 1) Where timeframe data comes from

- **Live mode**: timeframe candles come from **real MT5 terminal** via EA (`OnTick` + candle-close detection), then posted to Brain at `/mt5/market-snapshot`.
  - Source file: `mt5ea/ExpertAdvisor.mq5`
  - Payload builder: `mt5ea/Http/ApiClient.mqh`
  - Active stack now: **H1, M15, M5** only.
- **Replay mode**: timeframe candles come from **CSV files you import** into `/api/replay/import`.
  - Source file: `brain/src/Infrastructure/Services/External/HistoricalReplayService.cs`
- **Mock data** is used only when explicitly requested in replay (`useMockAI=true`) or when AI request fails.

---

## 2) Prerequisites

- Preferred startup (Windows):
  - `./start-local.ps1`
  - This starts both Brain (`:5000`) and AI worker (`:8001`) and runs health checks.
- Manual fallback:
  - AI worker: `cd aiworker` then `python -m uvicorn app.main:app --host 127.0.0.1 --port 8001`
  - Brain backend: `dotnet run --project brain/src/Web/Web.csproj`
- Verify health:
  - `GET http://127.0.0.1:8001/health`
  - `GET http://127.0.0.1:5000/health`

---

## 3) Live mode test (real MT5)

### A. Run EA in MT5

- Attach `mt5ea/ExpertAdvisor.ex5` to XAUUSD chart.
- Ensure inputs:
  - `BrainBaseUrl = http://127.0.0.1:5000`
  - `BrainApiKey = dev-local-change-me`
- EA now sends snapshots at candle close transitions (M5/M15/H1 aligned), not timer-only.

### B. Confirm ingest

- Check latest runtime:
  - `GET /api/monitoring/runtime`
- You should see symbol/session/bid/ask/timeframe updates.

### C. Verify timeline order (critical)

Call:
- `GET /api/monitoring/timeline?take=300`

For each live cycle (`cycleId` starting with `cyc_...`), verify this order:
1. `CYCLE_STARTED`
2. `RULE_ENGINE_SETUP_CANDIDATE` **or** `RULE_ENGINE_ABORT`
3. If setup valid: `NEWS_CHECK`
4. If news not blocked: `AI_ANALYZE_REQUEST` -> `AI_ANALYZE_RESPONSE`
5. Then: `DECISION_EVALUATED`
6. If tradable: `TRADE_ROUTED`

If blocked by news, cycle should stop after `NEWS_CHECK` with abort/no-trade behavior.

---

## 4) Replay mode test (historical)

### A. Export from MT5

Export 3 CSV files for XAUUSD:
- H1
- M15
- M5

Minimum columns:
- `timestamp,open,high,low,close,volume`

### B. Import CSV into replay

- `POST /api/replay/import?symbol=XAUUSD&timeframe=H1`
- `POST /api/replay/import?symbol=XAUUSD&timeframe=M15`
- `POST /api/replay/import?symbol=XAUUSD&timeframe=M5`

### C. Start replay (real AI default)

Use:
```json
{
  "symbol": "XAUUSD",
  "speedMultiplier": 200,
  "useAI": true,
  "useMockAI": false
}
```
POST to `/api/replay/start`.

### D. Start replay with explicit mock (optional)

Use:
```json
{
  "symbol": "XAUUSD",
  "speedMultiplier": 200,
  "useMockAI": true
}
```

### E. Check replay status

- `GET /api/replay/status?symbol=XAUUSD`

Validate counters:
- `processedCandles`
- `cyclesTriggered`
- `setupCandidatesFound`
- `tradesArmed`

### F. Verify replay timeline stages

- `GET /api/monitoring/timeline?take=500`
- or by cycle: `GET /api/monitoring/timeline?cycleId=replay_...`

Expected sequence per replay cycle:
1. `REPLAY_CYCLE_STARTED`
2. `RULE_ENGINE_SETUP_CANDIDATE` **or** `RULE_ENGINE_ABORT`
3. If setup valid: `REPLAY_NEWS_CHECK`
4. If not blocked: `REPLAY_AI_RESPONSE`
5. `REPLAY_TRADE_ARMED` **or** `REPLAY_CYCLE_NO_TRADE`

Replay safety requirement:
- no real execution; timeline payload includes `replayMode=true` and `executionBlocked=true`.

---

## 5) Quick failure checks

- If timeline stops at `NEWS_CHECK`: economic news gate blocked cycle.
- If timeline stops before AI in live cycle with `RULE_ENGINE_ABORT`: setup not valid.
- If real AI is slow/unavailable: replay/live may fallback or stall depending on timeout/failure; check backend logs.
- If no MT5 live cycles: verify EA API key, URL, and chart symbol is XAUUSD.

---

## 6) One-line acceptance checklist

System is working end-to-end when all are true:
- Live MT5 snapshots are arriving.
- Rule engine runs before AI.
- News gate appears before AI in timeline.
- Replay imports H1/M15/M5 and runs through same stage order.
- Real AI is default in replay; mock only when explicitly enabled.
- Replay never executes real trades.
