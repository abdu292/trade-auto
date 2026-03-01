using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Sessions.DTOs;
using Brain.Application.Features.Sessions.Queries;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Features.Sessions.Handlers;

public sealed class GetSessionStatesQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetSessionStatesQuery, IReadOnlyCollection<SessionStateDto>>
{
    public async Task<IReadOnlyCollection<SessionStateDto>> Handle(GetSessionStatesQuery request, CancellationToken cancellationToken)
    {
        if (!await dbContext.SessionStates.AnyAsync(cancellationToken))
        {
            dbContext.SessionStates.AddRange([
                SessionState.Create(SessionType.Japan, true),
                SessionState.Create(SessionType.India, true),
                SessionState.Create(SessionType.London, true),
                SessionState.Create(SessionType.NewYork, true),
            ]);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var rows = await dbContext.SessionStates
            .AsNoTracking()
            .Select(x => new SessionStateDto(x.Id.Value, x.Session.Value, x.IsEnabled, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(x => x.Session)
            .ToList();
    }
}
