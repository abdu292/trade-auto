using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Trades.DTOs;
using Brain.Application.Features.Trades.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Features.Trades.Handlers;

public sealed class GetActiveTradesQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetActiveTradesQuery, IReadOnlyCollection<TradeDto>>
{
    public async Task<IReadOnlyCollection<TradeDto>> Handle(GetActiveTradesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Trades
            .AsNoTracking()
            .Where(x => x.Status == "Pending" || x.Status == "Executed")
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new TradeDto(
                x.Id.Value,
                x.Symbol,
                x.Rail.Value,
                x.Entry.Value,
                x.TakeProfit.Value,
                x.ExpiryUtc,
                x.MaxLifeSeconds,
                x.Status,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
