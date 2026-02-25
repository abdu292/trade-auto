using Brain.Web.Filters;
using Microsoft.Extensions.Logging;

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
            (Mt5TradeStatusRequest request, ILogger<object> logger) =>
            {
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

public sealed record Mt5TradeStatusRequest(Guid TradeId, string Status);
