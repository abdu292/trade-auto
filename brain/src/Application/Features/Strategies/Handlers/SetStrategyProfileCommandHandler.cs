using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Strategies.Commands;
using Brain.Application.Features.Strategies.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Features.Strategies.Handlers;

public sealed class SetStrategyProfileCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<SetStrategyProfileCommand, StrategyProfileDto>
{
    public async Task<StrategyProfileDto> Handle(SetStrategyProfileCommand request, CancellationToken cancellationToken)
    {
        var all = await dbContext.StrategyProfiles.ToListAsync(cancellationToken);
        foreach (var profile in all)
        {
            profile.Deactivate();
        }

        var selected = all.Single(x => x.Id.Value == request.StrategyProfileId);
        selected.Activate();

        await dbContext.SaveChangesAsync(cancellationToken);
        return new StrategyProfileDto(selected.Id.Value, selected.Name, selected.Description, selected.IsActive);
    }
}
