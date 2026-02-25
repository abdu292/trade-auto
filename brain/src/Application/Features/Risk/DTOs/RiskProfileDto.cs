namespace Brain.Application.Features.Risk.DTOs;

public sealed record RiskProfileDto(Guid Id, string Name, string Level, decimal MaxDrawdownPercent, bool IsActive);
