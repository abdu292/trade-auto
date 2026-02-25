namespace Brain.Application.Features.Signals.DTOs;

public sealed record TradeSignalDto(
    Guid Id,
    string Symbol,
    string Rail,
    decimal Entry,
    decimal Tp,
    DateTimeOffset Pe,
    int Ml,
    decimal Confidence,
    DateTimeOffset CreatedAtUtc);
