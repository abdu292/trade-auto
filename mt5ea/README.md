# MT5 EA

Skeleton Expert Advisor with REST integration.

## REST Endpoints

- `GET /mt5/pending-trades`
- `POST /mt5/trade-status`

## Notes

- Uses `WebRequest()` with JSON payloads.
- Includes modular files:
  - `Http/ApiClient.mqh`
  - `Services/TradeExecutor.mqh`
  - `Services/RiskGuards.mqh`
- `ExpertAdvisor.mq5` now exposes `BrainApiKey` input and sends it as `X-API-Key` header for `/mt5/*` calls.

Set `BrainApiKey` to match Brain `Security:ApiKey` value.

## Compile (PowerShell)

After installing MetaTrader 5 / MetaEditor, run:

```powershell
./compile-ea.ps1
```

Optional custom inputs:

```powershell
./compile-ea.ps1 -EaPath ./ExpertAdvisor.mq5 -LogPath ./build/metaeditor-compile.log
```
