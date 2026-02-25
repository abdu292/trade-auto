using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Signals.DTOs;
using Brain.Application.Features.Signals.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Features.Signals.Handlers;

public sealed class GetSignalsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetSignalsQuery, IReadOnlyCollection<TradeSignalDto>>
{
    public async Task<IReadOnlyCollection<TradeSignalDto>> Handle(GetSignalsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.TradeSignals
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new TradeSignalDto(
                x.Id.Value,
                x.Symbol,
                x.Rail.Value,
                x.Entry.Value,
                x.TakeProfit.Value,
                x.PendingExpirationUtc,
                x.MaxLifeSeconds,
                x.Confidence,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
