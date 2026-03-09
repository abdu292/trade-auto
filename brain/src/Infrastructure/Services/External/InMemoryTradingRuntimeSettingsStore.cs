using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryTradingRuntimeSettingsStore : ITradingRuntimeSettingsStore
{
    private const string DefaultSymbol = "XAUUSD.gram";
    private const decimal DefaultMinTradeGrams = 100m;

    private readonly Lock _gate = new();
    private string _symbol;
    private bool _autoTradeEnabled;
    private decimal _minTradeGrams;

    public InMemoryTradingRuntimeSettingsStore(string initialSymbol, bool initialAutoTradeEnabled = false, decimal initialMinTradeGrams = DefaultMinTradeGrams)
    {
        _symbol = Normalize(initialSymbol);
        _autoTradeEnabled = initialAutoTradeEnabled;
        _minTradeGrams = initialMinTradeGrams > 0m ? initialMinTradeGrams : DefaultMinTradeGrams;
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

    /// <inheritdoc />
    public decimal GetMinTradeGrams()
    {
        lock (_gate)
        {
            return _minTradeGrams;
        }
    }

    /// <inheritdoc />
    public void SetMinTradeGrams(decimal grams)
    {
        lock (_gate)
        {
            _minTradeGrams = grams > 0m ? grams : DefaultMinTradeGrams;
        }
    }

    private static string Normalize(string? symbol)
    {
        var normalized = (symbol ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? DefaultSymbol : normalized;
    }
}