# ARCHITECTURE DIAGRAMS

## 1. System Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         LOCAL INTEGRATION SYSTEM                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────────┐         ┌──────────────────┐      ┌──────────────┐  │
│  │  brain           │◄────────┤  aiworker        │      │   MT5 EA     │  │
│  │ (ASP.NET Core)   │ HTTP    │   (FastAPI)      │      │  (MQL5)      │  │
│  │                  │ POST    │                  │      │              │  │
│  │ localhost:5000   │ /analyze│ localhost:8001   │      │  Reads       │  │
│  │                  │         │                  │      │  from brain  │  │
│  └──────────────────┘         └──────────────────┘      │  Writes to   │  │
│         ▲                                                │  brain       │  │
│         │                                                └──────────────┘  │
│         │ HTTP                                                    ▲        │
│         │ GET /mt5/pending-trades                               │        │
│         │ POST /mt5/trade-status (w/ API key)                   │        │
│         │                                                        │        │
│         └────────────────────────────────────────────────────────┘        │
│                                                                             │
│  Every 30 seconds:                                                          │
│  brain → generates MarketSnapshot → sends to aiworker → gets TradeSignal   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Request/Response Flow

```
Time    Actor               Action                              Log Line
────────────────────────────────────────────────────────────────────────────────

T+0     brain app           Every 30 seconds:
                            1. MarketSnapshotPollingService
                               wakes up

T+0.1   brain service       Generates mock EURUSD snapshot
                            timeframeData, ATR, MA20, etc.      📊 [Poll] Sending...

T+0.2   brain httpclient    Opens connection to aiworker
                            POST http://localhost:8001/analyze

T+0.5   aiworker            Receives MarketSnapshot JSON        INFO POST /analyze 200

T+0.6   aiworker analyzer   Analyzes market data
                            Returns: TradeSignal
                            (rail=BUY_LIMIT, confidence=0.85)

T+0.7   brain httpclient    Receives TradeSignal JSON
                            Deserializes to C# record

T+0.8   brain service       Logs result                         ✅ [Poll] Analysis...

T+0.9   brain service       Sleeps 29 seconds

────────────────────────────────────────────────────────────────────────────────

Manual request from user:

U+0     user/curl           GET /mt5/pending-trades             
                            header: X-API-Key=dev-local-...

U+0.1   brain endpoint      TradeApiSecurityFilter validates API key
                            Returns pending trade object        → GET /mt5/...

U+0.2   browser/curl        Receives trade details (200 OK)     ← GET /mt5/...

U+0.3   user/mql5           Executes trade in MT5 terminal
        (mt5ea)

U+0.4   mt5ea               Trade executed or partially filled
                            POST /mt5/trade-status
                            { tradeId, status="EXECUTED" }

U+0.5   brain endpoint      Mt5TradeStatusRequest received      → POST /mt5/...
                            Logs callback
                            Returns { received: true }          ← POST /mt5/...

U+0.6   mt5ea               Confirmed, trade status recorded
```

---

## 3. Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────────────┐
│                          WEB LAYER                              │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐            │
│  │ Signals  │ │ Mt5      │ │ Trades   │ │ Risk     │  Endpoints │
│  │Endpoints │ │Endpoints │ │Endpoints │ │Endpoints │            │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘            │
│                                                                   │
│  Responsibilities:                                               │
│  • Map HTTP routes                                              │
│  • Inject dependencies                                          │
│  • Log request/response                                         │
│  • Return IResult                                               │
└─────────────────────────────────────────────────────────────────┘
                             ▲
                             │ (uses)
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     APPLICATION LAYER                           │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐            │
│  │ Commands │ │ Queries  │ │Validators│ │ Handlers │ (MediatR)  │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘            │
│                                                                   │
│  Responsibilities:                                               │
│  • Business logic (what to do)                                  │
│  • Command/Query definitions                                    │
│  • Validation rules                                             │
│  • Orchestration (call multiple services)                       │
└─────────────────────────────────────────────────────────────────┘
                             ▲
                             │ (implements)
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                   INFRASTRUCTURE LAYER                          │
│  ┌──────────────┐ ┌────────────────┐ ┌──────────────┐           │
│  │  Services    │ │  Database      │ │   External   │           │
│  │              │ │  (EF Core)     │ │   Adapters   │           │
│  │ • Background │ │                │ │              │ (Impls)   │
│  │   Services   │ │ • Migrations   │ │ • HttpClient │           │
│  │ • HttpClient │ │ • DbContext    │ │ • Mock*      │           │
│  │ • Logging    │ │ • Entities     │ │   services   │           │
│  └──────────────┘ └────────────────┘ └──────────────┘           │
│                                                                   │
│  Responsibilities:                                               │
│  • Implement interfaces from Domain                             │
│  • External I/O (HTTP, database, file system)                   │
│  • Infrastructure concerns (DI, configuration)                  │
└─────────────────────────────────────────────────────────────────┘
                             ▲
                             │ (implements)
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                       DOMAIN LAYER                              │
│  ┌──────────────┐ ┌──────────┐ ┌──────────┐                     │
│  │  Interfaces  │ │ Entities │ │   Value  │  (Pure business)    │
│  │              │ │          │ │ Objects  │                     │
│  │ • IaiWorker  │ │ • Trade  │ │ • Price  │                     │
│  │   Client     │ │ • Signal │ │ • Risk   │                     │
│  │ • IMarket    │ │ • Session│ │ • Money  │                     │
│  │   Data...    │ │          │ │          │                     │
│  └──────────────┘ └──────────┘ └──────────┘                     │
│                                                                   │
│  Responsibilities:                                               │
│  • Interface contracts (what external world must provide)       │
│  • Business entities & rules                                    │
│  • Value objects (encapsulate business concepts)               │
│  • NO dependencies on other layers (top-level)                 │
└─────────────────────────────────────────────────────────────────┘

Direction of dependencies: Web → App → Infra → Domain (downward only)
```

---

## 4. Data Flow: Signal Analysis (Detailed)

```
┌─────────────────────────────────────────────────────────────────────┐
│  BRAIN APPLICATION (Orchestration)                                 │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  MarketSnapshotPollingService (BackgroundService)                 │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ Runs every 30 seconds:                                        │ │
│  │                                                               │ │
│  │ 1. Call GenerateMockSnapshot()                               │ │
│  │    └─> Creates: MarketSnapshotContract {                     │ │
│  │        • symbol: "EURUSD"                                    │ │
│  │        • timeframeData: [H1, H4]                             │ │
│  │        • atr: 0.00095                                        │ │
│  │        • ma20: 1.10250                                       │ │
│  │        • session: "EUROPE"                                   │ │
│  │        • timestamp: 2025-02-25T14:30:00Z }                  │ │
│  │                                                               │ │
│  │ 2. Call HttpAIWorkerClient.AnalyzeAsync(snapshot)            │ │
│  │    └─> Serializes to JSON:                                  │ │
│  │        {                                                      │ │
│  │          "symbol": "EURUSD",         // PascalCase -> camel  │ │
│  │          "timeframeData": [...],                             │ │
│  │          "atr": 0.00095,                                     │ │
│  │          "adr": 0.0012,                                      │ │
│  │          "ma20": 1.10250,                                    │ │
│  │          "session": "EUROPE",                                │ │
│  │          "timestamp": "2025-02-25T14:30:00Z"                │ │
│  │        }                                                      │ │
│  │                                                               │ │
│  │ 3. HTTP POST to http://localhost:8001/analyze               │ │
│  │    ┌──────────────────────────────────────────────────────┐ │ │
│  │    │ Logs: → [AIWorker] POST /analyze for EURUSD           │ │ │
│  │    └──────────────────────────────────────────────────────┘ │ │
│  │                                                               │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│                            (network)                                │
│                                  ↓↑                                 │
│  HttpAIWorkerClient                                                 │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ 4. Receives HTTP 200 response with JSON:                     │ │
│  │    {                                                           │ │
│  │      "rail": "BUY_LIMIT",     // snake_case -> camelCase     │ │
│  │      "entry": 1.10250,                                        │ │
│  │      "tp": 1.10401,                                           │ │
│  │      "pe": "2025-02-25T15:00:00Z",                           │ │
│  │      "ml": 3600,                                              │ │
│  │      "confidence": 0.85                                       │ │
│  │    }                                                           │ │
│  │                                                               │ │
│  │ 5. Deserialize to TradeSignalContract {                      │ │
│  │    • Rail: "BUY_LIMIT"                                      │ │
│  │    • Entry: 1.10250 (decimal)                               │ │
│  │    • Tp: 1.10401 (decimal)                                  │ │
│  │    • Pe: 2025-02-25T15:00:00Z (DateTimeOffset)              │ │
│  │    • Ml: 3600 (int)                                         │ │
│  │    • Confidence: 0.85 (decimal) }                           │ │
│  │                                                               │ │
│  │ 6. Return to caller                                          │ │
│  │    ┌──────────────────────────────────────────────────────┐ │ │
│  │    │ Logs: ← [AIWorker] Analysis complete:                 │ │ │
│  │    │       BUY_LIMIT (confidence=0.85)                     │ │ │
│  │    └──────────────────────────────────────────────────────┘ │ │
│  │                                                               │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
│  MarketSnapshotPollingService (continued)                         │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ 7. Log result:                                                │ │
│  │    ✅ [Poll] Analysis received: BUY_LIMIT @ 1.10250         │ │
│  │    (TP=1.10401, Confidence=0.85)                            │ │
│  │                                                               │ │
│  │ 8. Sleep 30 seconds                                          │ │
│  │                                                               │ │
│  │ 9. Repeat                                                    │ │
│  │                                                               │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 5. Dependency Injection Container

```
IServiceCollection (Program.cs builder.Services)
│
├─ Logging
│  ├─ ILogger<T> (Serilog)
│  └─ ILoggerFactory
│
├─ ASP.NET Core
│  ├─ IHostEnvironment
│  ├─ IConfiguration
│  └─ IHostApplicationBuilder
│
├─ Application Layer (MediatR)
│  ├─ IMediator
│  ├─ IPipelineBehavior<TRequest, TResponse>
│  └─ IValidator<T> (FluentValidation)
│
├─ Infrastructure Layer
│  │
│  ├─ Data
│  │  └─ IApplicationDbContext
│  │     └─ ApplicationDbContext (EF Core SQLServer)
│  │
│  ├─ External Services (Interfaces from Domain)
│  │  ├─ IAIWorkerClient
│  │  │  └─ HttpAIWorkerClient (NEW - calls http://localhost:8001)
│  │  │
│  │  ├─ IMt5BridgeClient
│  │  │  └─ MockMt5BridgeClient
│  │  │
│  │  ├─ IMarketDataProvider
│  │  │  └─ MockMarketDataProvider
│  │  │
│  │  ├─ INotificationService
│  │  │  └─ MockNotificationService
│  │  │
│  │  ├─ IWhatsAppService
│  │  │  └─ MockWhatsAppService
│  │  │
│  │  └─ ICalendarService
│  │     └─ MockCalendarService
│  │
│  ├─ Background Services
│  │  ├─ SessionSchedulerBackgroundService
│  │  ├─ SignalPollingBackgroundService
│  │  └─ MarketSnapshotPollingService (NEW)
│  │
│  └─ HTTP Client Factory
│     └─ HttpClientFactory (for HttpAIWorkerClient)
│
└─ Domain Layer
   ├─ IAIWorkerClient (interface only)
   ├─ IMt5BridgeClient (interface only)
   └─ ...

Resolution Path Example:
  endpoint needs IMediator
    IMediator creates AnalyzeSnapshotCommand handler
      handler needs IAIWorkerClient
        DI returns HttpAIWorkerClient (registered impl)
          HttpAIWorkerClient needs HttpClient
            DI returns HttpClient from factory
              HttpClient needs ILogger<HttpAIWorkerClient>
                DI creates Serilog logger
```

---

## 6. Request Pipeline (ASP.NET Core)

```
HTTP Request (GET /api/signals)
  │
  ▼
┌──────────────────────────────────────────┐
│ Middleware Pipeline                      │
├──────────────────────────────────────────┤
│                                          │
│ 1. Routing (MapGroup + MapGet)           │
│    └─> Route matches */signals/*         │
│                                          │
│ 2. Authorization (if needed)             │
│    └─> For MT5 endpoints: API key check  │
│                                          │
│ 3. EndpointFilter<TradeApiSecurityFilter>
│    └─> Validates X-API-Key header       │
│        Checks AllowedIps                 │
│                                          │
│ 4. Dependency Injection                  │
│    └─> Resolves: ILogger<object>         │
│        Resolves: IMediator               │
│        Resolves: CancellationToken       │
│                                          │
│ 5. Endpoint Handler (Lambda)             │
│    ├─> logger.LogInformation("→ GET...")│
│    ├─> mediator.Send(GetSignalsQuery)   │
│    ├─> logger.LogInformation("← ...")   │
│    └─> TypedResults.Ok(result)           │
│                                          │
│ 6. Response Serialization                │
│    └─> JSON (System.Text.Json)           │
│                                          │
│ 7. Middleware Pipeline (response)        │
│    └─> Exception handler (if error)     │
│                                          │
└──────────────────────────────────────────┘
  │
  ▼
HTTP Response (200 OK with JSON body)
```

---

## 7. Configuration Flow

```
Program.cs (Entry Point)
  │
  ├─> appsettings.json (all environments)
  ├─> appsettings.Development.json (dev only)
  └─> Environment variables (override all)
       │
       ▼
  ┌─────────────────────────────────────┐
  │ WebApplicationBuilder               │
  │                                     │
  │ LoadConfiguration() reads:          │
  │ ├─ ConnectionStrings               │
  │ ├─ Logging:LogLevel                │
  │ ├─ Security:ApiKey                 │
  │ ├─ Security:AllowedIps             │
  │ └─ Security:Enabled                │
  │                                     │
  └─────────────────────────────────────┘
       │
       ▼
  builder.Services.Configure<TradeApiSecurityOptions>(
      builder.Configuration.GetSection("Security"))
       │
       ▼
  Dependency Injection Container
       │
       ├─> IOptions<TradeApiSecurityOptions>
       │   └─> Available in TradeApiSecurityFilter
       │
       ├─> IConfiguration
       │   └─> Available in any service
       │
       └─> IHostEnvironment
           └─> Check if IsDevelopment(), IsProduction()
```

---

## 8. Error Handling Flow

```
HTTP Request
  │
  ▼
Endpoint Handler
  │
  ├─── NO ERROR ──────────────────┐
  │                               │
  │                         Return 200 OK
  │                         + JSON response
  │
  └─── ERROR ────────────────────┐
                                  │
                                  ▼
                         Exception thrown
                                  │
                                  ▼
                    app.UseExceptionHandler()
                         (middleware)
                                  │
                                  ▼
                    ExceptionHandlingMiddleware
                         (built-in)
                                  │
                                  ├─> Log error via ILogger
                                  │
                                  ├─> ProblemDetails response
                                  │   (RFC 7807)
                                  │
                                  └─> Return 500 Internal Server Error
                                      {
                                        "type": "https://...",
                                        "title": "Exception",
                                        "status": 500,
                                        "detail": "..."
                                      }

Validation Error (HttpAIWorkerClient)
  │
  ├─> HttpRequestException
  │   └─> Log error in AnalyzeAsync catch block
  │       → Rethrow (caller handles)
  │
  ├─> TimeoutException
  │   └─> Log warning (aiworker slow)
  │       → Rethrow (caller handles)
  │
  └─> InvalidOperationException
      └─> Log error (null response)
          → Rethrow (caller handles)
```

---

## 9. Extension Methods Chain (Fluent API)

```
app.MapGroup("/api")                        ← IEndpointRouteBuilder
   .MapTradesEndpoints()                   ← extension method
   .MapStrategyEndpoints()                 ← chained call
   .MapSignalsEndpoints()                  ← (each returns RouteGroupBuilder)
   .MapRiskEndpoints()
   .MapSessionEndpoints();

Inside MapSignalsEndpoints():
  ├─ var signals = group.MapGroup("/signals")    ← nested group
  │                   .WithTags("Signals")        ← fluent API
  │                   .WithOpenApi();
  │
  ├─ signals.MapGet("/", handler)
  │          .WithName("GetSignals")
  │          .WithDescription("...");
  │
  ├─ signals.MapPost("/analyze/{symbol}", handler)
  │          .WithName("AnalyzeSnapshot")
  │          .WithDescription("...");
  │
  └─ return group;                    ← return for chaining
```

---

## 10. MT5 Security Filter Flow

```
HTTP Request to /mt5/pending-trades
  │
  │ Header: X-API-Key: dev-local-change-me
  │
  ▼
TradeApiSecurityFilter.InvokeAsync()
  │
  ├─ Get IOptions<TradeApiSecurityOptions> from DI
  │  ├─ ApiKey: "dev-local-change-me"
  │  ├─ AllowedIps: ["127.0.0.1", "::1"]
  │  └─ Enabled: true
  │
  ├─ Get X-API-Key header from request
  │  │
  │  ├─ If MISSING or WRONG
  │  │  └─> Return TypedResults.Unauthorized()
  │  │      Response: 401 Unauthorized
  │  │
  │  └─ If CORRECT
  │     └─> Proceed to next filter/endpoint
  │
  │
  ├─ Check AllowedIps
  │  │
  │  ├─ Get RemoteIpAddress from HttpContext
  │  │
  │  ├─ If IP NOT in AllowedIps
  │  │  └─> Return TypedResults.Forbid()
  │  │      Response: 403 Forbidden
  │  │
  │  └─ If IP IN AllowedIps (or list empty)
  │     └─> Proceed to endpoint
  │
  │
  └─ Await next(context)  ← Invoke next filter/endpoint
      │
      ▼
    Endpoint Handler
      │
      ├─ logger.LogInformation("→ GET /mt5/pending-trades")
      ├─ Generate mock trade response
      ├─ logger.LogInformation("← ...")
      └─ Return TypedResults.Ok(response)
          │
          ▼
      HTTP 200 OK
      {
        id: "abc123",
        type: "BUY_LIMIT",
        ...
      }
```

---

This covers the complete architecture and request flows. Visual references for:
- System topology
- Data transformations (C# ↔ Python ↔ JSON)
- Clean architecture layers
- Dependency resolution
- Request/response pipelines
