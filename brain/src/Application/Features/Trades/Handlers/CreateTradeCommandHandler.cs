using Brain.Application.Common.Interfaces;
using Brain.Application.Features.Trades.Commands;
using Brain.Application.Features.Trades.DTOs;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using MediatR;

namespace Brain.Application.Features.Trades.Handlers;

public sealed class CreateTradeCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<CreateTradeCommand, TradeDto>
{
    public async Task<TradeDto> Handle(CreateTradeCommand request, CancellationToken cancellationToken)
    {
        var trade = Trade.Create(
            request.Symbol,
            new RailType(request.Rail),
            new Price(request.Entry),
            new Price(request.Tp),
            request.ExpiryUtc,
            request.MaxLifeSeconds);

        dbContext.Trades.Add(trade);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TradeDto(
            trade.Id.Value,
            trade.Symbol,
            trade.Rail.Value,
            trade.Entry.Value,
            trade.TakeProfit.Value,
            trade.ExpiryUtc,
            trade.MaxLifeSeconds,
            trade.Status,
            trade.CreatedAtUtc);
    }
}
