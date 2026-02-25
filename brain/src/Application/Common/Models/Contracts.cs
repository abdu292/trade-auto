namespace Brain.Application.Common.Models;

public sealed record TimeframeDataContract(string Timeframe, decimal Open, decimal High, decimal Low, decimal Close);

public sealed record MarketSnapshotContract(
    string Symbol,
    IReadOnlyCollection<TimeframeDataContract> TimeframeData,
    decimal Atr,
    decimal Adr,
    decimal Ma20,
    string Session,
    DateTimeOffset Timestamp);

public sealed record TradeSignalContract(
    string Rail,
    decimal Entry,
    decimal Tp,
    DateTimeOffset Pe,
    int Ml,
    decimal Confidence);

public sealed record TradeCommandContract(
    string Type,
    decimal Price,
    decimal Tp,
    DateTimeOffset Expiry,
    int Ml);
