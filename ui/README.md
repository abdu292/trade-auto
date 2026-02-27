# UI (Flutter)

Feature-first Flutter app with Riverpod and Dio.

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
