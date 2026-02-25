using Brain.Application.Features.Trades.Commands;
using Brain.Application.Features.Trades.DTOs;
using Brain.Application.Features.Trades.Queries;
using Brain.Web.Filters;
using MediatR;

namespace Brain.Web.Endpoints;

public static class TradesEndpoints
{
    public static RouteGroupBuilder MapTradesEndpoints(this RouteGroupBuilder group)
    {
        var trades = group.MapGroup("/trades").WithTags("Trades");

        trades.MapGet(
            "/active",
            async Task<IResult> (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetActiveTradesQuery(), cancellationToken);
                return TypedResults.Ok(result);
            });

        trades.MapPost(
                "/",
                async Task<IResult> (CreateTradeCommand command, IMediator mediator, CancellationToken cancellationToken) =>
                {
                    var result = await mediator.Send(command, cancellationToken);
                    return TypedResults.Created($"/api/trades/{result.Id}", result);
                })
            .AddEndpointFilter<RequestLoggingFilter>();

        return group;
    }
}
