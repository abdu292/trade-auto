using Brain.Web.Filters;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Brain.Web.Endpoints;

public static class Mt5Endpoints
{
    public static IEndpointRouteBuilder MapMt5Endpoints(this IEndpointRouteBuilder app)
    {
        var mt5Group = app.MapGroup("/mt5")
            .AddEndpointFilter<TradeApiSecurityFilter>()
            .WithTags("MT5 Expert Advisor");

        mt5Group.MapGet(
            "/pending-trades",
            (ILogger<object> logger) =>
            {
                logger.LogInformation("→ GET /mt5/pending-trades");
                var response = new
                {
                    id = Guid.NewGuid(),
                    type = "BUY_LIMIT",
                    symbol = "EURUSD",
                    price = 1.10250m,
                    tp = 1.10400m,
                    expiry = DateTimeOffset.UtcNow.AddMinutes(20),
                    ml = 3600
                };
                logger.LogInformation("← GET /mt5/pending-trades returns trade {Type} @ {Price}", 
                    response.type, response.price);
                return TypedResults.Ok(response);
            })
            .WithName("GetPendingTrades")
            .WithDescription("Fetch pending trade orders for MT5 Expert Advisor");

        mt5Group.MapPost(
            "/trade-status",
            async Task<IResult> (HttpRequest httpRequest, ILogger<object> logger, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(httpRequest.Body);
                var rawBody = await reader.ReadToEndAsync(cancellationToken);
                var sanitizedBody = rawBody.TrimEnd('\0', ' ', '\r', '\n', '\t');

                Mt5TradeStatusRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<Mt5TradeStatusRequest>(
                        sanitizedBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Invalid JSON payload on /mt5/trade-status. RawBody={RawBody}", rawBody);
                    return TypedResults.BadRequest(new { error = "Invalid JSON payload" });
                }

                if (request is null || string.IsNullOrWhiteSpace(request.TradeId) || string.IsNullOrWhiteSpace(request.Status))
                {
                    return TypedResults.BadRequest(new { error = "tradeId and status are required" });
                }

                logger.LogInformation(
                    "→ POST /mt5/trade-status: TradeId={TradeId}, Status={Status}",
                    request.TradeId, request.Status);
                logger.LogInformation(
                    "← /mt5/trade-status: Status callback processed");
                return TypedResults.Ok(new { received = true });
            })
            .WithName("UpdateTradeStatus")
            .WithDescription("Expert Advisor sends trade execution status callback");

        return app;
    }
}

public sealed record Mt5TradeStatusRequest(string TradeId, string Status);
