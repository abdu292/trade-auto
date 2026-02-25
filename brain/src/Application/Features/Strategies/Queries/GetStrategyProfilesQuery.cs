using Brain.Application.Features.Strategies.DTOs;
using MediatR;

namespace Brain.Application.Features.Strategies.Queries;

public sealed record GetStrategyProfilesQuery : IRequest<IReadOnlyCollection<StrategyProfileDto>>;
