using Brain.Application.Common.Models;
using Brain.Application.Features.Signals.Commands;
using Brain.Application.Features.Signals.DTOs;
using Brain.Application.Features.Signals.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Brain.Web.Endpoints;

public static class SignalsEndpoints
{
    public static RouteGroupBuilder MapSignalsEndpoints(this RouteGroupBuilder group)
    {
        var signals = group.MapGroup("/signals")
            .WithTags("Signals");

        signals.MapGet(
            "/",
            async Task<IResult> (IMediator mediator, ILogger<object> logger, CancellationToken cancellationToken) =>
            {
                logger.LogInformation("→ GET /api/signals");
                var result = await mediator.Send(new GetSignalsQuery(), cancellationToken);
                logger.LogInformation("← GET /api/signals returned {Count} signals", 
                    result?.Count() ?? 0);
                return TypedResults.Ok(result);
            })
            .WithName("GetSignals")
            .WithDescription("Retrieve all trading signals");

        signals.MapPost(
            "/analyze",
            async Task<IResult> (MarketSnapshotContract snapshot, IMediator mediator, ILogger<object> logger, CancellationToken cancellationToken) =>
            {
                logger.LogInformation("→ POST /api/signals/analyze for {Symbol}", snapshot.Symbol);
                var result = await mediator.Send(new AnalyzeSnapshotCommand(snapshot), cancellationToken);
                logger.LogInformation("← POST /api/signals/analyze for {Symbol} complete", snapshot.Symbol);
                return TypedResults.Ok(result);
            })
            .WithName("AnalyzeSnapshot")
            .WithDescription("Analyze market snapshot and generate trade signal");

        return group;
    }
}
