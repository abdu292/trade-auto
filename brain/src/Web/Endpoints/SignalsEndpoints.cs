using Brain.Application.Features.Signals.Commands;
using Brain.Application.Features.Signals.DTOs;
using Brain.Application.Features.Signals.Queries;
using MediatR;

namespace Brain.Web.Endpoints;

public static class SignalsEndpoints
{
    public static RouteGroupBuilder MapSignalsEndpoints(this RouteGroupBuilder group)
    {
        var signals = group.MapGroup("/signals").WithTags("Signals");

        signals.MapGet(
            "/",
            async Task<IResult> (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetSignalsQuery(), cancellationToken);
                return TypedResults.Ok(result);
            });

        signals.MapPost(
            "/analyze/{symbol}",
            async Task<IResult> (string symbol, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new AnalyzeSnapshotCommand(symbol), cancellationToken);
                return TypedResults.Ok(result);
            });

        return group;
    }
}
