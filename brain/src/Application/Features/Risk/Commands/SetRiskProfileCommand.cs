using Brain.Application.Features.Risk.DTOs;
using MediatR;

namespace Brain.Application.Features.Risk.Commands;

public sealed record SetRiskProfileCommand(Guid RiskProfileId) : IRequest<RiskProfileDto>;
