using Brain.Application.Features.Strategies.DTOs;
using MediatR;

namespace Brain.Application.Features.Strategies.Commands;

public sealed record SetStrategyProfileCommand(Guid StrategyProfileId) : IRequest<StrategyProfileDto>;
