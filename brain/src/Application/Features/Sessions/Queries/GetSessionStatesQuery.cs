using Brain.Application.Features.Sessions.DTOs;
using MediatR;

namespace Brain.Application.Features.Sessions.Queries;

public sealed record GetSessionStatesQuery : IRequest<IReadOnlyCollection<SessionStateDto>>;
