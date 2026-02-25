using Brain.Application.Features.Trades.DTOs;
using MediatR;

namespace Brain.Application.Features.Trades.Commands;

public sealed record CreateTradeCommand(
    string Symbol,
    string Rail,
    decimal Entry,
    decimal Tp,
    DateTimeOffset ExpiryUtc,
    int MaxLifeSeconds) : IRequest<TradeDto>;
