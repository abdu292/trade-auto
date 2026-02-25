/**
 * MINIMAL API EXTENSION METHODS PATTERN
 * 
 * Shows how to keep Program.cs clean by grouping endpoint mappings
 * into extension methods, organized by feature/domain.
 * 
 * This is what's already implemented in your project!
 */

// ============================================================================
// CURRENT Program.cs (CLEAN & MAINTAINABLE)
// ============================================================================

/*
using Brain.Application;
using Brain.Infrastructure.Data;
using Brain.Infrastructure.DependencyInjection;
using Brain.Web.Endpoints;
using Brain.Web.Filters;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- LOGGING ---
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// --- SERVICES ---
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<TradeApiSecurityOptions>(builder.Configuration.GetSection("Security"));

builder.Services
    .AddApplication()                          // MediatR + Validators
    .AddInfrastructure(builder.Configuration); // DB, HttpClient, Background Services

// --- BUILD ---
var app = builder.Build();

// --- MIDDLEWARE ---
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

// --- API ENDPOINTS (all grouped via extension methods) ---
app.MapGroup("/api")
    .MapTradesEndpoints()
    .MapStrategyEndpoints()
    .MapRiskEndpoints()
    .MapSessionEndpoints()
    .MapSignalsEndpoints();

app.MapMt5Endpoints();

await app.RunAsync();
*/

// Program.cs is only 58 lines! All endpoint logic lives in separate files.

// ============================================================================
// PATTERN 1: Signals Endpoints (Feature-based)
// File: src/Web/Endpoints/SignalsEndpoints.cs
// ============================================================================

/*
using Brain.Application.Features.Signals.Commands;
using Brain.Application.Features.Signals.DTOs;
using Brain.Application.Features.Signals.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Brain.Web.Endpoints;

public static class SignalsEndpoints
{
    // Extension method on RouteGroupBuilder
    public static RouteGroupBuilder MapSignalsEndpoints(this RouteGroupBuilder group)
    {
        var signals = group.MapGroup("/signals")
            .WithTags("Signals")
            .WithOpenApi();

        // GET /api/signals
        signals.MapGet(
            "/",
            async Task<IResult> (IMediator mediator, ILogger<object> logger, CancellationToken ct) =>
            {
                logger.LogInformation("→ GET /api/signals");
                var result = await mediator.Send(new GetSignalsQuery(), ct);
                logger.LogInformation("← GET /api/signals returned {Count} signals", 
                    result?.Count() ?? 0);
                return TypedResults.Ok(result);
            })
            .WithName("GetSignals")
            .WithDescription("Retrieve all trading signals");

        // POST /api/signals/analyze/{symbol}
        signals.MapPost(
            "/analyze/{symbol}",
            async Task<IResult> (
                string symbol,
                IMediator mediator,
                ILogger<object> logger,
                CancellationToken ct) =>
            {
                logger.LogInformation("→ POST /api/signals/analyze/{Symbol}", symbol);
                var result = await mediator.Send(new AnalyzeSnapshotCommand(symbol), ct);
                logger.LogInformation("← POST /api/signals/analyze/{Symbol} complete", symbol);
                return TypedResults.Ok(result);
            })
            .WithName("AnalyzeSnapshot")
            .WithDescription("Analyze market snapshot for given symbol");

        return group;
    }
}
*/

// ============================================================================
// PATTERN 2: MT5 Endpoints (Cross-cutting, separate group)
// File: src/Web/Endpoints/Mt5Endpoints.cs
// ============================================================================

/*
using Brain.Web.Filters;
using Microsoft.Extensions.Logging;

namespace Brain.Web.Endpoints;

public static class Mt5Endpoints
{
    // Extension method on IEndpointRouteBuilder (not RouteGroupBuilder)
    // because MT5 endpoints use different base path and security
    public static IEndpointRouteBuilder MapMt5Endpoints(this IEndpointRouteBuilder app)
    {
        var mt5Group = app.MapGroup("/mt5")
            .AddEndpointFilter<TradeApiSecurityFilter>()  // Only MT5 endpoints are secured
            .WithTags("MT5 Expert Advisor")
            .WithOpenApi();

        // GET /mt5/pending-trades
        mt5Group.MapGet(
            "/pending-trades",
            (ILogger<object> logger) =>
            {
                logger.LogInformation("→ GET /mt5/pending-trades");
                var response = new { ... };
                logger.LogInformation("← GET /mt5/pending-trades returns trade {Type}", response.type);
                return TypedResults.Ok(response);
            })
            .WithName("GetPendingTrades")
            .WithDescription("Fetch pending trade orders for MT5 Expert Advisor");

        // POST /mt5/trade-status
        mt5Group.MapPost(
            "/trade-status",
            (Mt5TradeStatusRequest request, ILogger<object> logger) =>
            {
                logger.LogInformation("→ POST /mt5/trade-status: {TradeId}", request.TradeId);
                logger.LogInformation("← /mt5/trade-status: Status callback processed");
                return TypedResults.Ok(new { received = true });
            })
            .WithName("UpdateTradeStatus")
            .WithDescription("Expert Advisor sends trade execution status callback");

        return app;
    }
}

public sealed record Mt5TradeStatusRequest(Guid TradeId, string Status);
*/

// ============================================================================
// KEY BENEFITS OF THIS PATTERN
// ============================================================================

/*
✓ CLEAN SEPARATION OF CONCERNS
  - Each endpoint file handles one feature area
  - Changes in Signals don't touch MT5 code
  
✓ DISCOVERABLE
  - Open Endpoints folder → see all API surface
  - File name matches feature name
  
✓ SCALABLE
  - Add new endpoint group in 5 min (copy/rename file)
  - Program.cs stays ~60 lines forever

✓ TESTABLE
  - Endpoints are pure functions (no side effects except logging)
  - Easy to unit test request/response
  
✓ DOCUMENTED VIA CODE
  - .WithName("GetSignals")
  - .WithDescription("...")
  - .WithOpenApi() → auto-generates OpenAPI/Swagger

✓ DISCOVERABLE DEPENDENCIES
  - Logger, IMediator injected explicitly
  - Not hidden in class constructors
  - Easy to add/remove dependencies
*/

// ============================================================================
// HOW TO ADD A NEW ENDPOINT GROUP
// ============================================================================

/*
1. Create new file: src/Web/Endpoints/MyFeatureEndpoints.cs

2. Template:

using MediatR;
using Microsoft.Extensions.Logging;

namespace Brain.Web.Endpoints;

public static class MyFeatureEndpoints
{
    public static RouteGroupBuilder MapMyFeatureEndpoints(this RouteGroupBuilder group)
    {
        var feature = group.MapGroup("/my-feature")
            .WithTags("My Feature")
            .WithOpenApi();

        feature.MapGet(
            "/",
            async Task<IResult> (IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetMyFeatureQuery(), ct);
                return TypedResults.Ok(result);
            })
            .WithName("GetMyFeature");

        feature.MapPost(
            "/",
            async Task<IResult> (CreateMyFeatureCommand cmd, IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(cmd, ct);
                return TypedResults.CreatedAtRoute("GetMyFeature", new { id = result.Id }, result);
            })
            .WithName("CreateMyFeature")
            .Produces(StatusCodes.Status201Created)
            .WithOpenApi();

        return feature;
    }
}

3. Wire in Program.cs:

app.MapGroup("/api")
    .MapMyFeatureEndpoints()  // Add this line
    .MapTradesEndpoints()
    .MapSignalsEndpoints()
    ...
*/

// ============================================================================
// DEPENDENCY INJECTION IN ENDPOINTS
// ============================================================================

/*
// All dependencies are injected inline via Minimal API parameters:

signals.MapPost(
    "/analyze/{symbol}",
    async Task<IResult> (
        string symbol,                      // [FromRoute]
        AnalyzeRequest body,                // [FromBody] (automatic for complex types)
        IMediator mediator,                 // Injected from DI
        ILogger<object> logger,             // Injected from DI
        CancellationToken ct)               // Injected by ASP.NET
        =>
    {
        logger.LogInformation("Analyzing {Symbol}", symbol);
        var result = await mediator.Send(new AnalyzeSnapshotCommand(symbol), ct);
        return TypedResults.Ok(result);
    });

// NO constructor! No ServiceLocator pattern!
// Clean, dependency inversion, easy to test.
*/

// ============================================================================
// LOGGING BEST PRACTICES IN ENDPOINTS
// ============================================================================

/*
// ✓ Good: Structured logging with context
logger.LogInformation(
    "→ POST /api/signals/analyze/{Symbol}",  // Message (→ = outbound request)
    symbol);                                 // Structured property

// Response with details
logger.LogInformation(
    "← POST /api/signals/analyze/{Symbol} complete: {Signal}",
    symbol,
    signal.Rail);  // Structured property

// Errors with context
catch (Exception ex)
{
    logger.LogError(
        ex,
        "✗ [AIWorker] HTTP request failed for {Symbol}",
        symbol);
}

// ✗ Bad: String concatenation
// logger.LogInformation($"Analyzing {symbol}");  // No structured search
// logger.LogInformation("Analyzing " + symbol);  // No structured search
*/

// ============================================================================
// ENDPOINT FILTERS (Middleware-like)
// ============================================================================

/*
// Applied at endpoint group level:

var mt5Group = app.MapGroup("/mt5")
    .AddEndpointFilter<TradeApiSecurityFilter>()
    .WithTags("MT5")
    .WithOpenApi();

// File: src/Web/Filters/TradeApiSecurityFilter.cs

public sealed class TradeApiSecurityFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var securityOptions = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<TradeApiSecurityOptions>>();

        // Validate API key
        var apiKey = httpContext.Request.Headers["X-API-Key"].ToString();
        if (securityOptions.Value.ApiKey != apiKey)
        {
            return TypedResults.Unauthorized();
        }

        // Validate IP if configured
        if (securityOptions.Value.AllowedIps.Any())
        {
            var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
            if (!securityOptions.Value.AllowedIps.Contains(remoteIp))
            {
                return TypedResults.Forbid();
            }
        }

        return await next(context);
    }
}
*/

// ============================================================================
// SUMMARY
// ============================================================================

/*
This pattern gives you:

✓ Clean Program.cs (stays <100 lines)
✓ Clear file organization (feature = domain)
✓ Easy to add endpoints (copy/paste template)
✓ Dependency injection clarity (no hidden dependencies)
✓ OpenAPI/Swagger automatic documentation
✓ Structured logging for observability
✓ Security via filters (zero boilerplate)

All without:
✗ Controllers with constructors
✗ ServiceLocator patterns
✗ Magic configuration
✗ Inheritance hierarchies

Just pure, composable functions with dependency injection.
*/
