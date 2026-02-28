using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class TradingViewAlertLog : BaseEntity<Guid>
{
    private TradingViewAlertLog()
    {
    }

    public string Symbol { get; private set; } = "XAUUSD";
    public string Timeframe { get; private set; } = "M15";
    public string Signal { get; private set; } = "NEUTRAL";
    public string ConfirmationTag { get; private set; } = "NEUTRAL";
    public string Bias { get; private set; } = "NEUTRAL";
    public string RiskTag { get; private set; } = "CAUTION";
    public decimal Score { get; private set; }
    public decimal Volatility { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public string Source { get; private set; } = "TRADINGVIEW";
    public string Notes { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static TradingViewAlertLog Create(
        string symbol,
        string timeframe,
        string signal,
        string confirmationTag,
        string bias,
        string riskTag,
        decimal score,
        decimal volatility,
        DateTimeOffset timestamp,
        string source,
        string notes)
    {
        return new TradingViewAlertLog
        {
            Id = Guid.NewGuid(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            Timeframe = timeframe.Trim().ToUpperInvariant(),
            Signal = signal.Trim().ToUpperInvariant(),
            ConfirmationTag = confirmationTag.Trim().ToUpperInvariant(),
            Bias = bias.Trim().ToUpperInvariant(),
            RiskTag = riskTag.Trim().ToUpperInvariant(),
            Score = Math.Clamp(score, 0m, 1m),
            Volatility = Math.Max(0m, volatility),
            Timestamp = timestamp,
            Source = string.IsNullOrWhiteSpace(source) ? "TRADINGVIEW" : source.Trim(),
            Notes = notes ?? string.Empty,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
