using Brain.Application.Features.Signals.DTOs;
using MediatR;

namespace Brain.Application.Features.Signals.Queries;

public sealed record GetSignalsQuery : IRequest<IReadOnlyCollection<TradeSignalDto>>;
