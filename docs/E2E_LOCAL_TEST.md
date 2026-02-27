# End-to-End Real-Time Test (MT5 Demo + TradingView + OpenRouter)

This runbook validates real flow (no synthetic execution path):
MT5 ticks -> snapshot posts -> Brain + aiworker decision -> approval -> MT5 pending order -> real BUY trigger/TP callbacks -> slip + ledger updates.

## 0) Prerequisites
- MT5 desktop installed and logged in to a demo account.
- XAUUSD chart open in MT5.
- Expert Advisor compiled from `mt5ea/ExpertAdvisor.mq5` and attachable to chart.
- TradingView account (free plan is fine).
- `aiworker/.env` configured with OpenRouter key and channels.

## 1) Start backend services

```powershell
Set-Location c:\Users\Abdulla\source\repos\trade-auto
.\start-local.ps1
```

## 2) Verify backend and aiworker health

```powershell
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod http://localhost:8001/health | ConvertTo-Json -Depth 6
```

Expected in aiworker health:
- `ai.providerMode = universal`
- `ai.analyzerCount > 0`
- analyzer names start with `openrouter:`

## 3) MT5 EA setup (real-time)

Attach EA to `XAUUSD` chart and set inputs:
- `BrainBaseUrl = http://127.0.0.1:5000`
- `BrainApiKey = dev-local-change-me`
- `SnapshotPushSeconds = 5`
- `PollTradeSeconds = 2`

Enable in MT5:
- AutoTrading ON
- Allow WebRequest URL: `http://127.0.0.1:5000`

Expected EA behavior:
- Every ~5s posts `/mt5/market-snapshot`
- Every ~2s polls `/mt5/pending-trades`
- Sends `/mt5/trade-status` for order placed, buy trigger, TP hit

## 4) TradingView integration (2 free indicators as confirmation)

Create 2 TradingView indicator alerts (free-plan compatible), each calling webhook:
- URL: `http://127.0.0.1:5000/api/tradingview/webhook`
- Method: POST
- Content-Type: JSON

Webhook payload template:
```json
{
  "symbol": "XAUUSD",
  "timeframe": "M15",
  "signal": "BUY",
  "confirmationTag": "CONFIRM",
  "bias": "BULLISH",
  "riskTag": "SAFE",
  "score": 0.7,
  "volatility": 0.8,
  "source": "TRADINGVIEW",
  "notes": "indicator_1_or_2"
}
```

Check latest TradingView signal:
```powershell
Invoke-RestMethod http://localhost:5000/api/tradingview/latest | ConvertTo-Json -Depth 6
```

Yes: TradingView is integrated as confirmation-only layer for the free indicators (normalized to `CONFIRM/NEUTRAL/CONTRADICT`).

## 5) Confirm live approval candidates

Wait ~30-60 seconds after EA is running and market is ticking:
```powershell
Invoke-RestMethod http://localhost:5000/api/monitoring/approvals | ConvertTo-Json -Depth 8
```

Expected:
- `[]` when regime is BLOCK / no valid setup
- one or more candidate entries when SAFE/CAUTION setup appears

## 6) Approve candidate and let MT5 execute for real

```powershell
$approvals = Invoke-RestMethod http://localhost:5000/api/monitoring/approvals
$tradeId = $approvals[0].id
Invoke-RestMethod -Uri "http://localhost:5000/api/monitoring/approvals/$tradeId/approve" -Method POST
```

After approval:
- EA polls and receives pending trade
- EA places pending order in demo account
- On fill, EA posts `BUY_TRIGGERED`
- On TP, EA posts `TP_HIT`

## 7) Verify slips + ledger (real callback results)

```powershell
Invoke-RestMethod http://localhost:5000/api/monitoring/ledger | ConvertTo-Json -Depth 6
Invoke-RestMethod http://localhost:5000/api/monitoring/notifications | ConvertTo-Json -Depth 8
```

Expected:
- BUY and SELL slip notifications appear as callbacks happen
- ledger updates cash/grams/exposure/deployable cash

## 8) Telegram ingestion verification

Your channels are read by aiworker; check health and logs:
```powershell
Invoke-RestMethod http://localhost:8001/health | ConvertTo-Json -Depth 6
```

If Telegram bot is configured, health shows channels; news impact contributes to `HIGH/MODERATE/LOW` regime gating.

## 9) Optional diagnostic fallback (only if EA not posting)

If MT5 WebRequest setup is blocked, use API-post simulation temporarily to debug connectivity, then return to live EA flow.

## 10) Troubleshooting (real MT5)

If EA log shows `HTTP=1001/1003` with `LastError=5203/4006`:

1. In MT5 open `Tools -> Options -> Expert Advisors`:
  - Enable `Allow algorithmic trading`
  - Enable `Allow WebRequest for listed URL`
  - Add exact URLs:
    - `http://127.0.0.1:5000`
    - `http://localhost:5000`

2. In EA input parameters, ensure:
  - `BrainBaseUrl` matches one allowed URL exactly
  - `BrainApiKey=dev-local-change-me` (or whatever Brain config uses)

3. Confirm backend reachability from Windows:
  ```powershell
  $h=@{"X-API-Key"="dev-local-change-me"}
  Invoke-WebRequest -Uri "http://127.0.0.1:5000/mt5/pending-trades" -Headers $h -Method GET -UseBasicParsing
  ```
  Status `204` is healthy (means no pending trades yet).

4. Attach EA only on `XAUUSD` chart. Remove from other symbols.

5. If still failing, restart MT5 terminal after changing WebRequest settings.
