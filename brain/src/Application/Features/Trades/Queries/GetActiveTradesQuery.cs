using Brain.Application.Features.Trades.DTOs;
using MediatR;

namespace Brain.Application.Features.Trades.Queries;

public sealed record GetActiveTradesQuery : IRequest<IReadOnlyCollection<TradeDto>>;
