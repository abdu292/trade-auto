using Brain.Application.Features.Risk.DTOs;
using MediatR;

namespace Brain.Application.Features.Risk.Queries;

public sealed record GetRiskProfilesQuery : IRequest<IReadOnlyCollection<RiskProfileDto>>;
