using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Strategies.DTOs;
using Brain.Application.Features.Strategies.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Features.Strategies.Handlers;

public sealed class GetStrategyProfilesQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetStrategyProfilesQuery, IReadOnlyCollection<StrategyProfileDto>>
{
    public async Task<IReadOnlyCollection<StrategyProfileDto>> Handle(GetStrategyProfilesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.StrategyProfiles
            .AsNoTracking()
            .Select(x => new StrategyProfileDto(x.Id.Value, x.Name, x.Description, x.IsActive))
            .ToListAsync(cancellationToken);
    }
}
