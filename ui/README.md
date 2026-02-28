# UI (Flutter)

Feature-first Flutter app with Riverpod and Dio.

The UI is now oriented to real MT5 runtime monitoring (demo/live), not synthetic snapshot testing.

## Run

```bash
flutter pub get
flutter run --dart-define=BRAIN_API_BASE_URL=http://localhost:5000
```

On Android emulator, you can also use:

```bash
flutter run --dart-define=BRAIN_API_BASE_URL=http://10.0.2.2:5000
```

## Required Screens

- DashboardScreen
- StrategyControlScreen
- RiskControlScreen
- TradesScreen
- SessionOverviewScreen

## Demo-account realtime test

1. Start backend + aiworker from repo root:

```powershell
.\start-local.ps1
```

2. Run Flutter app with backend URL:

```bash
flutter run --dart-define=BRAIN_API_BASE_URL=http://localhost:5000
```

3. Attach EA to MT5 `XAUUSD` demo chart and enable AutoTrading/WebRequest.

4. In UI verify:
- Dashboard: health + runtime + ledger update
- Trades: active trades + recent signals + runtime telemetry
