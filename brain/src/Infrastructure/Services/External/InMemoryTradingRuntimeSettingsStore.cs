using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryTradingRuntimeSettingsStore : ITradingRuntimeSettingsStore
{
    private const string DefaultSymbol = "XAUUSD.gram";

    private readonly Lock _gate = new();
    private string _symbol;
    private bool _autoTradeEnabled;

    public InMemoryTradingRuntimeSettingsStore(string initialSymbol, bool initialAutoTradeEnabled = false)
    {
        _symbol = Normalize(initialSymbol);
        _autoTradeEnabled = initialAutoTradeEnabled;
    }

    public string GetSymbol()
    {
        lock (_gate)
        {
            return _symbol;
        }
    }

    public void SetSymbol(string symbol)
    {
        lock (_gate)
        {
            _symbol = Normalize(symbol);
        }
    }

    /// <inheritdoc />
    public bool GetAutoTradeEnabled()
    {
        lock (_gate)
        {
            return _autoTradeEnabled;
        }
    }

    /// <inheritdoc />
    public void SetAutoTradeEnabled(bool enabled)
    {
        lock (_gate)
        {
            _autoTradeEnabled = enabled;
        }
    }

    private static string Normalize(string? symbol)
    {
        var normalized = (symbol ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? DefaultSymbol : normalized;
    }
}