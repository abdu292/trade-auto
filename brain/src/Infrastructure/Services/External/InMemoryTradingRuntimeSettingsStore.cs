using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryTradingRuntimeSettingsStore : ITradingRuntimeSettingsStore
{
    private const string DefaultSymbol = "XAUUSD.gram";
    // §C (refinement spec): Remove 100g minimum — default is now 0.1g so that
    // MICRO_ROTATION_MODE and small live-experience trades are not artificially floored.
    private const decimal DefaultMinTradeGrams = 0.1m;

    private readonly Lock _gate = new();
    private string _symbol;
    private bool _autoTradeEnabled;
    private decimal _minTradeGrams;
    private bool _microRotationEnabled;

    public InMemoryTradingRuntimeSettingsStore(string initialSymbol, bool initialAutoTradeEnabled = false, decimal initialMinTradeGrams = DefaultMinTradeGrams)
    {
        _symbol = Normalize(initialSymbol);
        _autoTradeEnabled = initialAutoTradeEnabled;
        _minTradeGrams = initialMinTradeGrams > 0m ? initialMinTradeGrams : DefaultMinTradeGrams;
        _microRotationEnabled = false;
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

    /// <inheritdoc />
    public bool GetMicroRotationEnabled()
    {
        lock (_gate)
        {
            return _microRotationEnabled;
        }
    }

    /// <inheritdoc />
    public void SetMicroRotationEnabled(bool enabled)
    {
        lock (_gate)
        {
            _microRotationEnabled = enabled;
        }
    }

    private static string Normalize(string? symbol)
    {
        var normalized = (symbol ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? DefaultSymbol : normalized;
    }
}