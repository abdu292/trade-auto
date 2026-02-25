using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Sessions.DTOs;
using Brain.Application.Features.Sessions.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Features.Sessions.Handlers;

public sealed class GetSessionStatesQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetSessionStatesQuery, IReadOnlyCollection<SessionStateDto>>
{
    public async Task<IReadOnlyCollection<SessionStateDto>> Handle(GetSessionStatesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.SessionStates
            .AsNoTracking()
            .Select(x => new SessionStateDto(x.Id.Value, x.Session.Value, x.IsEnabled, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
