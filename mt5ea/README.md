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

## Build and Deploy (PowerShell)

After installing MetaTrader 5 / MetaEditor, run a single command to compile **and** deploy in one step:

```powershell
./compile-and-deploy.ps1
```

This script:
1. Copies all EA source files to `MQL5\Experts\TradeAuto` inside the MT5 terminal data folder.
2. Compiles the EA with MetaEditor and validates the result (fails fast on errors).
3. Saves the compiled `ExpertAdvisor.ex5` binary to the local `build/` folder.

Optional parameters:

```powershell
# Target a specific terminal instance by its ID (the folder name under AppData\Roaming\MetaQuotes\Terminal)
./compile-and-deploy.ps1 -TerminalId <id>

# Override the compile-log output path
./compile-and-deploy.ps1 -LogPath ./build/metaeditor-compile.log
```

After running, refresh the EA list inside MT5: right-click **Expert Advisors** in the Navigator and choose **Refresh**.
