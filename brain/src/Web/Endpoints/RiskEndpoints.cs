using Brain.Application.Features.Risk.Commands;
using Brain.Application.Features.Risk.DTOs;
using Brain.Application.Features.Risk.Queries;
using MediatR;

namespace Brain.Web.Endpoints;

public static class RiskEndpoints
{
    public static RouteGroupBuilder MapRiskEndpoints(this RouteGroupBuilder group)
    {
        var risk = group.MapGroup("/risk").WithTags("Risk");

        risk.MapGet(
            "/profiles",
            async Task<IResult> (IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetRiskProfilesQuery(), cancellationToken);
                return TypedResults.Ok(result);
            });

        risk.MapPut(
            "/profiles/{id:guid}/activate",
            async Task<IResult> (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new SetRiskProfileCommand(id), cancellationToken);
                return TypedResults.Ok(result);
            });

        return group;
    }
}
