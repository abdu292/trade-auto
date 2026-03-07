using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryTradingRuntimeSettingsStore : ITradingRuntimeSettingsStore
{
    private const string DefaultSymbol = "XAUUSD.gram";

    private readonly Lock _gate = new();
    private string _symbol;

    public InMemoryTradingRuntimeSettingsStore(string initialSymbol)
    {
        _symbol = Normalize(initialSymbol);
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

    private static string Normalize(string? symbol)
    {
        var normalized = (symbol ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? DefaultSymbol : normalized;
    }
}