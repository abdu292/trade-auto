using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Risk.Commands;
using Brain.Application.Features.Risk.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Features.Risk.Handlers;

public sealed class SetRiskProfileCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<SetRiskProfileCommand, RiskProfileDto>
{
    public async Task<RiskProfileDto> Handle(SetRiskProfileCommand request, CancellationToken cancellationToken)
    {
        var all = await dbContext.RiskProfiles.ToListAsync(cancellationToken);
        foreach (var profile in all)
        {
            profile.Deactivate();
        }

        var selected = all.Single(x => x.Id.Value == request.RiskProfileId);
        selected.Activate();

        await dbContext.SaveChangesAsync(cancellationToken);
        return new RiskProfileDto(selected.Id.Value, selected.Name, selected.Level.Value, selected.MaxDrawdownPercent, selected.IsActive);
    }
}
