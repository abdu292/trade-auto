using Brain.Web.Filters;

namespace Brain.Web.Endpoints;

public static class Mt5Endpoints
{
    public static IEndpointRouteBuilder MapMt5Endpoints(this IEndpointRouteBuilder app)
    {
        var mt5Group = app.MapGroup("/mt5")
            .AddEndpointFilter<TradeApiSecurityFilter>();

        mt5Group.MapGet(
            "/pending-trades",
            () =>
                TypedResults.Ok(new
                {
                    id = Guid.NewGuid(),
                    type = "BUY_LIMIT",
                    price = 1.10250m,
                    tp = 1.10400m,
                    expiry = DateTimeOffset.UtcNow.AddMinutes(20),
                    ml = 3600
                }));

        mt5Group.MapPost(
            "/trade-status",
            (Mt5TradeStatusRequest request, ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("Mt5Status");
                logger.LogInformation("MT5 status callback: {TradeId} => {Status}", request.TradeId, request.Status);
                return TypedResults.Ok();
            });

        return app;
    }
}

public sealed record Mt5TradeStatusRequest(Guid TradeId, string Status);
