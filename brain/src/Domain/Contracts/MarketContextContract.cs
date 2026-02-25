namespace Brain.Domain.Contracts;

/// <summary>
/// Structured market data sent to AI providers for decision-making
/// </summary>
public record MarketContextContract(
    string Symbol,
    decimal CurrentPrice,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    DateTime CandleTime,
    
    // Technical indicators
    decimal? RSI14,
    decimal? MACD,
    decimal? MACDSignal,
    decimal? MA20,
    decimal? MA50,
    decimal? ATR14,
    
    // Session context
    string SessionName,           // London, NY, Sydney, Asian
    bool IsSessionOpen,
    int MinutesUntilSessionEnd,
    
    // Economic calendar
    string? UpcomingEvent,
    string? EventImpact,          // High, Medium, Low
    
    // Previous signal (avoid duplicate trades)
    string? LastSignalId,
    DateTime? LastSignalTime
)
{
    /// <summary>
    /// Create a prompt-friendly summary for AI
    /// </summary>
    public string ToPromptContext()
    {
        return $@"
Market Data: {Symbol}
Current Price: {CurrentPrice}
OHLC: O={Open} H={High} L={Low} C={Close}
Volume: {Volume}

Technical Indicators:
- RSI(14): {RSI14:F2}
- MACD: {MACD:F5}
- MA20: {MA20:F5}
- MA50: {MA50:F5}
- ATR(14): {ATR14:F5}

Session: {SessionName} ({(IsSessionOpen ? "OPEN" : "CLOSED")})
Minutes until session end: {MinutesUntilSessionEnd}
Economic Event: {UpcomingEvent ?? "None"}
Event Impact: {EventImpact ?? "N/A"}

Last Trade: {(LastSignalId != null ? $"{LastSignalId} at {LastSignalTime}" : "None")}
";
    }
}
