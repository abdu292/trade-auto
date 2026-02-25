using Brain.Application.Features.Strategies.Commands;
using Brain.Application.Features.Strategies.DTOs;
using Brain.Application.Features.Strategies.Queries;
using MediatR;

namespace Brain.Web.Endpoints;

public static class StrategyEndpoints
{
    public static RouteGroupBuilder MapStrategyEndpoints(this RouteGroupBuilder group)
    {
        var strategies = group.MapGroup("/strategies").WithTags("Strategies");

        strategies.MapGet(
            "/",
            async Task<IResult> (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetStrategyProfilesQuery(), cancellationToken);
                return TypedResults.Ok(result);
            });

        strategies.MapPut(
            "/{id:guid}/activate",
            async Task<IResult> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new SetStrategyProfileCommand(id), cancellationToken);
                return TypedResults.Ok(result);
            });

        return group;
    }
}
