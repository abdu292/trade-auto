using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Brain.Web.Endpoints;

public static class TradingViewEndpoints
{
    private const string DefaultSymbol = "XAUUSD.gram";

    public static RouteGroupBuilder MapTradingViewEndpoints(this RouteGroupBuilder group)
    {
        var tradingView = group.MapGroup("/tradingview").WithTags("TradingView");

        tradingView.MapPost(
            "/webhook",
            IResult (TradingViewWebhookRequest request,
                     HttpRequest httpRequest,
                     IConfiguration configuration,
                     ITradingViewSignalStore store,
                     IApplicationDbContext db,
                     ILogger<object> logger) =>
            {
                var configuredSecret = configuration["TradingView:WebhookSecret"]?.Trim();
                if (!string.IsNullOrWhiteSpace(configuredSecret))
                {
                    var providedSecret = request.Secret?.Trim();
                    if (string.IsNullOrWhiteSpace(providedSecret) &&
                        httpRequest.Headers.TryGetValue("X-TradingView-Secret", out var headerSecret))
                    {
                        providedSecret = headerSecret.ToString().Trim();
                    }

                    if (!string.Equals(configuredSecret, providedSecret, StringComparison.Ordinal))
                    {
                        return TypedResults.Unauthorized();
                    }
                }

                var signal = new TradingViewSignalContract(
                    Symbol: string.IsNullOrWhiteSpace(request.Symbol) ? DefaultSymbol : request.Symbol.Trim(),
                    Timeframe: (request.Timeframe ?? "M15").Trim().ToUpperInvariant(),
                    Signal: (request.Signal ?? "NEUTRAL").Trim().ToUpperInvariant(),
                    ConfirmationTag: NormalizeConfirmationTag(request.ConfirmationTag),
                    Bias: NormalizeBias(request.Bias),
                    RiskTag: NormalizeRiskTag(request.RiskTag),
                    Score: Math.Clamp(request.Score ?? 0.5m, 0m, 1m),
                    Volatility: Math.Max(0m, request.Volatility ?? 0m),
                    Timestamp: request.Timestamp ?? DateTimeOffset.UtcNow,
                    Source: string.IsNullOrWhiteSpace(request.Source) ? "TRADINGVIEW" : request.Source.Trim(),
                    Notes: request.Notes?.Trim() ?? string.Empty);

                store.Upsert(signal);

                db.TradingViewAlertLogs.Add(TradingViewAlertLog.Create(
                    symbol: signal.Symbol,
                    timeframe: signal.Timeframe,
                    signal: signal.Signal,
                    confirmationTag: signal.ConfirmationTag,
                    bias: signal.Bias,
                    riskTag: signal.RiskTag,
                    score: signal.Score,
                    volatility: signal.Volatility,
                    timestamp: signal.Timestamp,
                    source: signal.Source,
                    notes: signal.Notes));
                db.SaveChangesAsync(httpRequest.HttpContext.RequestAborted).GetAwaiter().GetResult();

                logger.LogInformation(
                    "TradingView webhook stored signal: {Symbol} {Timeframe} {Signal} bias={Bias} risk={Risk} score={Score:0.00}",
                    signal.Symbol,
                    signal.Timeframe,
                    signal.Signal,
                    signal.Bias,
                    signal.RiskTag,
                    signal.Score);

                return TypedResults.Ok(new { received = true, signal });
            })
            .WithName("TradingViewWebhook")
            .WithDescription("Receives TradingView webhook payloads for decision engine enrichment.");

        tradingView.MapGet(
            "/latest",
            IResult (ITradingViewSignalStore store) =>
            {
                if (!store.TryGetLatest(out var latest) || latest is null)
                {
                    return TypedResults.NotFound(new { message = "No TradingView signal available." });
                }

                return TypedResults.Ok(latest);
            })
            .WithName("GetLatestTradingViewSignal")
            .WithDescription("Returns latest TradingView signal currently held in memory.");

        return group;
    }

    private static string NormalizeRiskTag(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "SAFE" => "SAFE",
            "CAUTION" => "CAUTION",
            "BLOCK" => "BLOCK",
            _ => "CAUTION",
        };
    }

    private static string NormalizeBias(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "BULLISH" => "BULLISH",
            "BEARISH" => "BEARISH",
            _ => "NEUTRAL",
        };
    }

    private static string NormalizeConfirmationTag(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "CONFIRM" => "CONFIRM",
            "CONTRADICT" => "CONTRADICT",
            _ => "NEUTRAL",
        };
    }
}

public sealed record TradingViewWebhookRequest(
    string? Secret,
    string? Symbol,
    string? Timeframe,
    string? Signal,
    string? ConfirmationTag,
    string? Bias,
    string? RiskTag,
    decimal? Score,
    decimal? Volatility,
    DateTimeOffset? Timestamp,
    string? Source,
    string? Notes);
