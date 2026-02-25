#!/usr/bin/env markdown
# 🚀 QUICK REFERENCE CARD

## Starting the System

### Option 1: Three separate terminals (RECOMMENDED for development)

```powershell
# Terminal 1: AI Worker
cd .\aiworker\
.\venv\Scripts\Activate.ps1
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001

# Terminal 2: Brain Backend
cd .\brain\src\Web\
dotnet run

# Terminal 3: Monitor logs
# Watch output from Terminal 1 & 2
```

### Option 2: Background processes

```powershell
# Start aiworker
Start-Process pwsh -ArgumentList "-NoExit", "cd .\aiworker\; .\venv\Scripts\Activate.ps1; python -m uvicorn app.main:app --host 127.0.0.1 --port 8001"

# Start brain
Start-Process pwsh -ArgumentList "-NoExit", "cd .\brain\src\Web\; dotnet run"
```

---

## Service URLs

| Service | URL | Port | Purpose |
|---------|-----|------|---------|
| brain | http://localhost:5000 | 5000 | Main API gateway |
| brain (HTTPS) | https://localhost:7096 | 7096 | Secure (dev only) |
| aiworker | http://localhost:8001 | 8001 | AI analysis engine |
| aiworker docs | http://localhost:8001/docs | 8001 | Swagger UI |
| brain swagger | http://localhost:5000/swagger | 5000 | OpenAPI UI |

---

## Key Endpoints

### Brain (ASP.NET Core)

```
GET    /health
→ Health check

GET    /api/signals
→ List all signals

POST   /api/signals/analyze/{symbol}
→ Analyze single symbol (e.g., EURUSD)

GET    /mt5/pending-trades
→ Fetch pending trades (requires X-API-Key header)

POST   /mt5/trade-status
→ MT5 sends execution callback (requires X-API-Key header)
```

### AI Worker (FastAPI)

```
GET    /health
→ Health check

POST   /analyze
→ Request: { symbol, timeframeData[], atr, adr, ma20, session, timestamp }
→ Response: { rail, entry, tp, pe, ml, confidence }

GET    /docs
→ Interactive Swagger UI
```

---

## Required Headers

### For MT5 endpoints (brain)

```powershell
X-API-Key: dev-local-change-me
```

### For all requests

```
Content-Type: application/json
```

---

## Testing Commands

### Health Checks

```powershell
# Brain health
curl http://localhost:5000/health

# AI Worker health (FastAPI auto-generates /health)
curl http://localhost:8001/health
```

### Get Signals

```powershell
curl http://localhost:5000/api/signals
```

### Analyze Symbol

```powershell
$body = @{
    symbol = "EURUSD"
    timeframeData = @(
        @{ timeframe = "H1"; open = 1.1020; high = 1.1030; low = 1.1015; close = 1.1025 }
    )
    atr = 0.00095
    adr = 0.0012
    ma20 = 1.1025
    session = "EUROPE"
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

curl -X POST http://localhost:5000/api/signals/analyze/EURUSD `
  -Header @{"Content-Type"="application/json"} `
  -Body $body
```

### Get Pending Trades (MT5)

```powershell
curl -X GET http://localhost:5000/mt5/pending-trades `
  -Header @{"X-API-Key"="dev-local-change-me"}
```

### Send Trade Status (MT5 Callback)

```powershell
$body = @{
    tradeId = "550e8400-e29b-41d4-a716-446655440000"
    status = "EXECUTED"
} | ConvertTo-Json

curl -X POST http://localhost:5000/mt5/trade-status `
  -Header @{"X-API-Key"="dev-local-change-me"} `
  -Header @{"Content-Type"="application/json"} `
  -Body $body
```

---

## Log Patterns (What to Look For)

### Normal Operation

```
[INF] → [AIWorker] POST /analyze for EURUSD, session=EUROPE
[INF] ← [AIWorker] Analysis complete: BUY_LIMIT (confidence=0.85)
```

### Request/Response Flow

```
→ = Request out
← = Response in
✅ = Success
✗ = Error
```

### Market Snapshot Polling (every 30s)

```
[INF] 📊 [Poll] Sending EURUSD snapshot to AI Worker
[INF] ✅ [Poll] Analysis received: BUY_LIMIT
```

---

## Common Issues & Quick Fixes

| Problem | Solution |
|---------|----------|
| **Connection refused on aiworker** | Check Terminal 1. Run: `python -m uvicorn app.main:app --host 127.0.0.1 --port 8001` |
| **401 Unauthorized on MT5 endpoints** | Add header: `-Header @{"X-API-Key"="dev-local-change-me"}` |
| **"No module named app"** | Activate venv: `.\aiworker\venv\Scripts\Activate.ps1` |
| **Database error in brain** | Run: `cd brain\src\Web\ && dotnet ef database update` |
| **Port 5000 already in use** | Change brain port in `Program.cs` or kill existing process |
| **No logs visible** | Check `appsettings.Development.json` LogLevel is "Information" |

---

## File Structure (Key Files)

```
brain/
├── src/
│   ├── Web/
│   │   ├── Program.cs                          ← Main entry point
│   │   ├── Endpoints/
│   │   │   ├── SignalsEndpoints.cs             ← /api/signals
│   │   │   └── Mt5Endpoints.cs                 ← /mt5/*
│   │   └── Filters/
│   │       └── TradeApiSecurityFilter.cs       ← API key validation
│   ├── Infrastructure/
│   │   ├── Services/
│   │   │   ├── External/
│   │   │   │   └── HttpAIWorkerClient.cs       ← calls aiworker
│   │   │   └── Background/
│   │   │       └── MarketSnapshotPollingService.cs  ← 30s polling
│   │   └── DependencyInjection/
│   │       └── DependencyInjection.cs          ← DI config
│   ├── Application/
│   │   └── Common/
│   │       ├── Interfaces/
│   │       │   └── IAIWorkerClient.cs
│   │       └── Models/
│   │           └── Contracts.cs                ← Shared DTOs
│   └── Domain/
│       └── Entities/                            ← Business logic
│
aiworker/
├── app/
│   ├── main.py                                 ← FastAPI app
│   ├── routers/
│   │   └── analyze.py                          ← /analyze endpoint
│   ├── services/
│   │   └── analyzer.py
│   └── models/
│       └── contracts.py                        ← Pydantic models
```

---

## Minimal API Pattern Cheat Sheet

### Adding a New Feature Endpoint

1. Create file: `src/Web/Endpoints/MyFeatureEndpoints.cs`

```csharp
namespace Brain.Web.Endpoints;

public static class MyFeatureEndpoints
{
    public static RouteGroupBuilder MapMyFeatureEndpoints(
        this RouteGroupBuilder group)
    {
        var feature = group.MapGroup("/my-feature")
            .WithTags("My Feature");

        feature.MapGet("/", (ILogger<object> logger) => {
            logger.LogInformation("→ GET /api/my-feature");
            return TypedResults.Ok(new { message = "Hello" });
        }).WithName("GetMyFeature");

        return feature;
    }
}
```

2. Register in `Program.cs`:

```csharp
app.MapGroup("/api")
    .MapMyFeatureEndpoints()  ← Add this
    .MapSignalsEndpoints()
    ...
```

3. Done! Endpoint is live.

---

## Dependency Injection Cheat Sheet

### Adding a Service

1. Create interface in `Application/Common/Interfaces/`
2. Create implementation in `Infrastructure/Services/`
3. Register in `Infrastructure/DependencyInjection/DependencyInjection.cs`

```csharp
public static IServiceCollection AddInfrastructure(...)
{
    // ...
    
    // Real implementation
    services.AddScoped<IMyService, MyRealService>();
    
    // OR with HttpClient
    services.AddHttpClient<MyHttpClient>()
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));
    services.AddScoped<IMyService>(provider => 
        provider.GetRequiredService<MyHttpClient>());
    
    return services;
}
```

4. Inject in endpoints:

```csharp
feature.MapGet("/", (IMyService service) => {
    var result = service.DoSomething();
    return TypedResults.Ok(result);
});
```

---

## Environment Configuration

### brain (appsettings.Development.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "Security": {
    "Enabled": true,
    "ApiKeyHeaderName": "X-API-Key",
    "ApiKey": "dev-local-change-me",
    "AllowedIps": ["127.0.0.1", "::1"]
  }
}
```

### aiworker (no config needed)

Runs on port 8001 by default. To change:

```powershell
python -m uvicorn app.main:app --host 127.0.0.1 --port 8002
```

---

## Debugging Tips

### View All Logs (verbose)

```powershell
# Update appsettings.Development.json
"LogLevel": {
  "Default": "Debug",  ← Change to Debug
  "Microsoft": "Debug"
}
```

### Attach Debugger (Visual Studio)

1. Open solution in Visual Studio
2. Set breakpoint in `HttpAIWorkerClient.cs` or endpoint
3. Run with F5
4. Send request via PowerShell or Swagger UI

### Run Tests

```powershell
cd brain
dotnet test
```

### View HTTP Request/Response

Install & use [Fiddler](https://www.telerik.com/fiddler) or [Charles Proxy]:
- Intercept requests between brain & aiworker
- See full JSON payloads
- Helpful for debugging serialization issues

---

## Integration Checklist

- [ ] aiworker running on http://localhost:8001
- [ ] brain running on http://localhost:5000
- [ ] Logs showing → ← flow every 30 seconds
- [ ] Can GET /api/signals without errors
- [ ] Can POST /api/signals/analyze/{symbol} and get response
- [ ] Can GET /mt5/pending-trades (with API key header)
- [ ] Can POST /mt5/trade-status (with API key header)
- [ ] No connection errors in logs

---

## Documentation Files

| File | Purpose |
|------|---------|
| [IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md) | Overview of all changes |
| [LOCAL_INTEGRATION_SETUP.md](LOCAL_INTEGRATION_SETUP.md) | Step-by-step execution guide |
| [SHARED_MODELS.md](SHARED_MODELS.md) | JSON models & payloads |
| [MINIMAL_API_PATTERN.md](MINIMAL_API_PATTERN.md) | Endpoint design patterns |
| [QUICK_REFERENCE.md](QUICK_REFERENCE.md) | This file |

---

## Quick Start

```powershell
# Terminal 1
cd .\aiworker\
.\venv\Scripts\Activate.ps1
python -m uvicorn app.main:app --host 127.0.0.1 --port 8001

# Terminal 2
cd .\brain\src\Web\
dotnet run

# Terminal 3
# Watch logs for: → [AIWorker] POST /analyze ...
# Then:           ← [AIWorker] Analysis complete: BUY_LIMIT
```

🎉 You're running a complete local integration!

---

## Need Help?

- **Check logs first** - They show exactly what's happening
- **Read IMPLEMENTATION_COMPLETE.md** - Full overview
- **Read LOCAL_INTEGRATION_SETUP.md** - Detailed troubleshooting
- **Review code comments** - All key code has inline documentation
