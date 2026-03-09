namespace Brain.Application.Common.Interfaces;

public interface ITradingRuntimeSettingsStore
{
    string GetSymbol();

    void SetSymbol(string symbol);

    /// <summary>
    /// Returns whether Auto Trade mode is enabled (trades routed directly to MT5).
    /// Defaults to false — all ARMED trades go to the approval queue until the client enables this toggle.
    /// </summary>
    bool GetAutoTradeEnabled();

    void SetAutoTradeEnabled(bool enabled);

    /// <summary>
    /// Returns the minimum trade size in grams. Defaults to 100 g.
    /// Orders computed below this threshold are rejected by the decision engine.
    /// Configurable via the UI so different gram weights can be tested during analysis.
    /// </summary>
    decimal GetMinTradeGrams();

    void SetMinTradeGrams(decimal grams);
}