using Brain.Application.Features.Sessions.Commands;
using Brain.Application.Features.Sessions.DTOs;
using Brain.Application.Features.Sessions.Queries;
using MediatR;

namespace Brain.Web.Endpoints;

public static class SessionEndpoints
{
    public static RouteGroupBuilder MapSessionEndpoints(this RouteGroupBuilder group)
    {
        var sessions = group.MapGroup("/sessions").WithTags("Sessions");

        sessions.MapGet(
            "/",
            async Task<IResult> (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetSessionStatesQuery(), cancellationToken);
                return TypedResults.Ok(result);
            });

        sessions.MapPut(
            "/toggle",
            async Task<IResult> (ToggleSessionCommand command, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(command, cancellationToken);
                return TypedResults.Ok(result);
            });

        return group;
    }
}
