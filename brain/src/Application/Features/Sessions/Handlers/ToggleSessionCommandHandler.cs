using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Sessions.Commands;
using Brain.Application.Features.Sessions.DTOs;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Features.Sessions.Handlers;

public sealed class ToggleSessionCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<ToggleSessionCommand, SessionStateDto>
{
    public async Task<SessionStateDto> Handle(ToggleSessionCommand request, CancellationToken cancellationToken)
    {
        var type = new SessionType(request.Session);

        var state = await dbContext.SessionStates
            .SingleOrDefaultAsync(x => x.Session == type, cancellationToken);

        if (state is null)
        {
            state = SessionState.Create(type, request.IsEnabled);
            dbContext.SessionStates.Add(state);
        }
        else
        {
            state.SetEnabled(request.IsEnabled);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new SessionStateDto(state.Id.Value, state.Session.Value, state.IsEnabled, state.UpdatedAtUtc);
    }
}
