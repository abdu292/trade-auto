using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Risk.DTOs;
using Brain.Application.Features.Risk.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Features.Risk.Handlers;

public sealed class GetRiskProfilesQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetRiskProfilesQuery, IReadOnlyCollection<RiskProfileDto>>
{
    public async Task<IReadOnlyCollection<RiskProfileDto>> Handle(GetRiskProfilesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.RiskProfiles
            .AsNoTracking()
            .Select(x => new RiskProfileDto(x.Id.Value, x.Name, x.Level.Value, x.MaxDrawdownPercent, x.IsActive))
            .ToListAsync(cancellationToken);
    }
}
