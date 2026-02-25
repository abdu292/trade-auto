using Brain.Application.Features.Sessions.DTOs;
using MediatR;

namespace Brain.Application.Features.Sessions.Commands;

public sealed record ToggleSessionCommand(string Session, bool IsEnabled) : IRequest<SessionStateDto>;
