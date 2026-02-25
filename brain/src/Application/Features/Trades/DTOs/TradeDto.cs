namespace Brain.Application.Features.Trades.DTOs;

public sealed record TradeDto(
    Guid Id,
    string Symbol,
    string Rail,
    decimal Entry,
    decimal Tp,
    DateTimeOffset ExpiryUtc,
    int MaxLifeSeconds,
    string Status,
    DateTimeOffset CreatedAtUtc);
