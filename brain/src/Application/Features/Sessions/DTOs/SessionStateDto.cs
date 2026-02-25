namespace Brain.Application.Features.Sessions.DTOs;

public sealed record SessionStateDto(Guid Id, string Session, bool IsEnabled, DateTimeOffset UpdatedAtUtc);
